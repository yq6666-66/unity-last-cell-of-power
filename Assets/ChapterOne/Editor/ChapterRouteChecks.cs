using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.ChapterOne;

// Background integration smoke: every player displacement comes from MoveAction
// -> StealthPlayerController -> CharacterController.Move. This is synthetic input,
// simulated focus and explicit session timing, NOT Windows desktop acceptance.
public static class ChapterRouteChecks
{
    public static string LastResult { get; private set; } = "NOT_RUN";
    // Optional evidence destination for independent scene variants; legacy default retained.
    public static string EvidenceDirectory { get; set; }

    public static IEnumerator RunWithoutPulse() => Run(false);
    public static IEnumerator RunWithPulse() => Run(true);
    static IEnumerator Run(bool withPulse)
    {
        var session = UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        if (!Application.isPlaying || !session || !session.Ready || !session.Paused)
            throw new InvalidOperationException("Route smoke requires initialized, paused ExpandedSession");
        if (!session.pulse || !session.statistics || !session.externalPlayerUI) throw new InvalidOperationException("Chapter core required");
        if (Vector3.Distance(session.player.transform.position, session.level.playerSpawn) > .1f || Mathf.Abs(session.battery.Current - 40) > .001f)
            throw new InvalidOperationException("Reload a fresh scene before route smoke; no repositioning is permitted");
        LastResult = "RUNNING";
        using (var driver = new RouteDriver(session,withPulse))
        {
            IEnumerator routine = driver.Execute();
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e)
                {
                    LastResult = "FAIL"; driver.Record("FAIL " + e); driver.Save();
                    (routine as IDisposable)?.Dispose();
                    throw;
                }
                if (!more) break;
                yield return routine.Current;
            }
            LastResult = "PASS"; driver.Record("PASS route completed without any player/guard warp or position setter"); driver.Save();
        }
        Debug.Log("CHAPTER_ROUTE_SMOKE_PASS mode="+(withPulse?"with-pulse":"no-pulse"));
    }

    sealed class RouteDriver : IDisposable
    {
        readonly ExpandedSession session;
        readonly ChapterPlayerUI ui;
        readonly bool withPulse;
        readonly Keyboard keyboard, previousKeyboard;
        readonly Mouse mouse, previousMouse;
        readonly ReadOnlyArray<InputDevice>? previousMoveDevices;
        readonly string previousInteractOverride, previousPulseOverride;
        readonly bool previousEnabled;
        readonly StringBuilder report = new StringBuilder();
        readonly double started, deadline;
        int tickedFrame = -1;
        float travelled;
        Vector3 previousPosition;

        public RouteDriver(ExpandedSession session,bool withPulse)
        {
            this.session = session;this.withPulse=withPulse;
            ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
            if(!ui || !ui.enabled)throw new InvalidOperationException("Enabled ChapterPlayerUI required");
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            previousEnabled = session.enabled;
            previousMoveDevices = session.player.MoveAction.actionMap.devices;
            previousInteractOverride = session.interaction.InteractAction.bindings[0].overridePath;
            previousPulseOverride = session.pulse.PulseAction.bindings[0].overridePath;
            keyboard = InputSystem.AddDevice<Keyboard>("ChapterRouteKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("ChapterRouteMouse");
            session.player.MoveAction.actionMap.devices = new InputDevice[] { keyboard };
            session.interaction.InteractAction.ApplyBindingOverride(0, keyboard.eKey.path);
            session.pulse.PulseAction.ApplyBindingOverride(0,keyboard.qKey.path);
            session.enabled = false; // OnDisable pauses before explicit ownership is taken.
            session.SendMessage("OnApplicationFocus", true); // Declared simulated focus.
            // Start is requested through the real Chapter UI button command in Execute.

            previousPosition = session.player.transform.position;
            started = EditorApplication.timeSinceStartup; deadline = started + 90;
            report.AppendLine("CHAPTER "+(withPulse?"WITH-PULSE":"NO-PULSE")+" SYNTHETIC BACKGROUND ROUTE SMOKE. Simulated focus, virtual devices, explicit Session.Tick; not desktop keyboard acceptance.");
            report.AppendLine("warp=false; positionSetter=false; AI/energy/profile unchanged; navigation only supplies walking directions.");
            Record("START");
        }

        public IEnumerator Execute()
        {
            while(!ui.LayoutBuilt || ui.Mode==ChapterViewMode.Loading)
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("UI initialization timeout");
                yield return null;
            }
            session.SendMessage("OnApplicationFocus",true);
            ui.BeginOrContinue();
            if(session.Paused || ui.Mode!=ChapterViewMode.Playing)throw new Exception("UI begin command did not start gameplay");
            Record("UI_BEGIN");
            var routine = WaitReleased(3); while (routine.MoveNext()) yield return null;
            var charger = session.interaction.targets.Where(t => t.kind == InteractionKind.Charger)
                .OrderBy(t => Vector3.Distance(t.Position, session.level.playerSpawn)).First();
            Vector3 chargerApproach = charger.Position + Vector3.forward; chargerApproach.y = 0;
            routine = Walk(chargerApproach, "starter charger"); while (routine.MoveNext()) yield return null;
            routine = Aim(charger.Position); while (routine.MoveNext()) yield return null;
            float beforeCharge = session.battery.Current;
            routine = Hold(charger, withPulse ? 4f : 2.1f); while (routine.MoveNext()) yield return null;
            if (session.battery.Current < beforeCharge + (withPulse ? 29f : 14f))
                throw new Exception("Starter charge did not replenish expected energy");
            Record("CHARGED");
            if(withPulse)
            {
                routine=ApproachAndPulse();while(routine.MoveNext())yield return null;
            }

            var terminal = session.interaction.targets.Single(t => t.kind == InteractionKind.Terminal);
            Vector3 terminalApproach = session.level.terminal + Vector3.back * 1.5f;
            routine = Walk(terminalApproach, "terminal"); while (routine.MoveNext()) yield return null;
            routine = Aim(terminal.Position); while (routine.MoveNext()) yield return null;
            routine = Hold(terminal, session.rules.supplySeconds + .15f); while (routine.MoveNext()) yield return null;
            if (!session.interaction.Powered) throw new Exception("Terminal did not complete from held E input");
            Record("SUPPLIED");
            while (session.door.State != EscapeDoorState.Open)
            {
                var step = Step(Vector2.zero); while (step.MoveNext()) yield return null;
            }
            Record("DOOR_OPEN");
            // Allow the actual NavMesh carving update to become visible.
            routine = WaitReleased(5); while (routine.MoveNext()) yield return null;
            Vector3 exit = session.door.exitAnchor.TransformPoint(new Vector3(0, 0, .5f));
            routine = Walk(exit, "exit platform"); while (routine.MoveNext()) yield return null;
            if (session.Outcome != RunOutcome.Escaped) throw new Exception("Walking across the exit did not yield Escaped");
            if(session.pulse.SuccessfulUses!=(withPulse?1:0) || session.Summary==null || session.Summary.SuccessfulPulseCount!=(withPulse?1:0))
                throw new Exception("Pulse use statistics do not match route mode");
            Record("ESCAPED");
        }

        IEnumerator ApproachAndPulse()
        {
            double approachDeadline=EditorApplication.timeSinceStartup+20;
            double repathAt=0;
            Vector3[] corners=null;int corner=1;
            Record("APPROACH_GUARD");
            while(true)
            {
                EnsureRunning();
                var flat=session.enemy.transform.position-session.player.transform.position;flat.y=0;
                if(session.pulse.HasValidTarget && session.pulse.ReadyToUse && flat.magnitude<session.pulse.profile.range-.02f)
                    break;
                if(EditorApplication.timeSinceStartup>approachDeadline)throw new TimeoutException("Could not naturally approach a valid threat within 20 seconds");
                if(corners==null || EditorApplication.timeSinceStartup>=repathAt)
                {
                    corners=GuardApproachPath();corner=1;repathAt=EditorApplication.timeSinceStartup+.35;
                }
                Vector2 mouseDelta=Vector2.zero;bool walk=false;
                if(corner<corners.Length)
                {
                    Vector3 delta=corners[corner]-session.player.transform.position;delta.y=0;
                    if(delta.magnitude<.17f)corner++;
                    else
                    {
                        float yaw=Mathf.DeltaAngle(session.player.transform.eulerAngles.y,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
                        mouseDelta=new Vector2(Mathf.Clamp(yaw,-18,18)/session.player.sensitivity,session.player.Pitch/session.player.sensitivity);
                        walk=Mathf.Abs(yaw)<15;
                    }
                }
                var step=walk?Step(mouseDelta,Key.W):Step(mouseDelta);
                while(step.MoveNext())yield return null;
            }
            float before=session.battery.Current;
            int uses=session.pulse.SuccessfulUses;
            Record("PULSE_REQUEST");
            var pulseStep=Step(Vector2.zero,Key.Q);while(pulseStep.MoveNext())yield return null;
            if(session.pulse.LastResult!=PulseUseResult.Success || session.pulse.SuccessfulUses!=uses+1 || session.enemy.State!=ExpandedEnemyState.Disrupted)
                throw new Exception("Q did not produce the expected real pulse result: "+session.pulse.LastResult);
            float spent=before-session.battery.Current;
            if(Mathf.Abs(spent-session.pulse.profile.cost)>LastStepDuration*(session.rules.idleDrain+session.rules.movementDrain)+.03f)
                throw new Exception("Pulse fee differs from configured cost: "+spent);
            Record("PULSE_SUCCESS spent="+spent.ToString("F3"));
            var release=WaitReleased(2);while(release.MoveNext())yield return null;
        }

        Vector3[] GuardApproachPath()
        {
            if(!NavMesh.SamplePosition(session.player.transform.position,out var start,.7f,NavMesh.AllAreas))
                throw new Exception("Player not on reachable walking surface while approaching guard");
            // Only this test driver reads the moving guard pose to select a front approach.
            // It does not modify the guard, route, sensing or speed.
            foreach(float angle in new[]{0f,-25f,25f})
            {
                var direction=Quaternion.Euler(0,angle,0)*session.enemy.transform.forward;direction.y=0;direction.Normalize();
                Vector3 desired=session.enemy.transform.position+direction*2.8f;
                if(!NavMesh.SamplePosition(desired,out var end,.8f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();
                if(NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete)
                    return path.corners.Length>1 ? path.corners : new[]{start.position,end.position};
            }
            // Wait for the moving guard to expose another front approach, rather
            // than treating one temporarily blocked sample as a broken level.
            return Array.Empty<Vector3>();
        }

        IEnumerator Walk(Vector3 goal, string label)
        {
            if (!NavMesh.SamplePosition(session.player.transform.position, out var start, .7f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(goal, out var end, .7f, NavMesh.AllAreas))
                throw new Exception("No walking navigation near " + label);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new Exception("No complete route to " + label);
            float pathLength = 0; for (int i = 1; i < path.corners.Length; i++) pathLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Record("WALK " + label + " navLength=" + pathLength.ToString("F2") + " corners=" + path.corners.Length);
            for (int corner = 1; corner < path.corners.Length; corner++)
            {
                double lastProgress = EditorApplication.timeSinceStartup;
                float bestDistance = float.MaxValue;
                while (true)
                {
                    if (session.Outcome == RunOutcome.Escaped) yield break;
                    EnsureRunning();
                    Vector3 delta = path.corners[corner] - session.player.transform.position; delta.y = 0;
                    if (delta.magnitude < .17f) break;
                    if (delta.magnitude < bestDistance - .02f) { bestDistance = delta.magnitude; lastProgress = EditorApplication.timeSinceStartup; }
                    if (EditorApplication.timeSinceStartup - lastProgress > 4)
                        throw new Exception("Walking blocked near " + label + " corner=" + corner + " player=" + session.player.transform.position + " goal=" + path.corners[corner]);
                    float desiredYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.DeltaAngle(session.player.transform.eulerAngles.y, desiredYaw);
                    Vector2 mouseDelta = new Vector2(Mathf.Clamp(yaw, -18, 18) / session.player.sensitivity, session.player.Pitch / session.player.sensitivity);
                    var step = Mathf.Abs(yaw) < 15 ? Step(mouseDelta, Key.W) : Step(mouseDelta);
                    while (step.MoveNext()) yield return null;
                }
            }
            var release = WaitReleased(2); while (release.MoveNext()) yield return null;
            Record("ARRIVED " + label);
        }

        IEnumerator Aim(Vector3 target)
        {
            for (int i = 0; i < 60; i++)
            {
                Vector3 delta = target - session.player.viewCamera.transform.position;
                float yaw = Mathf.DeltaAngle(session.player.transform.eulerAngles.y, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
                float pitch = -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
                float pitchDelta = pitch - session.player.Pitch;
                var step = Step(new Vector2(Mathf.Clamp(yaw, -18, 18) / session.player.sensitivity, -Mathf.Clamp(pitchDelta, -18, 18) / session.player.sensitivity));
                while (step.MoveNext()) yield return null;
                if (Mathf.Abs(yaw) < .5f && Mathf.Abs(pitchDelta) < .5f) yield break;
            }
            throw new Exception("Could not aim at interactable through mouse input");
        }

        IEnumerator Hold(EscapeInteractable target, float duration)
        {
            var release = WaitReleased(2); while (release.MoveNext()) yield return null;
            float elapsed = 0;
            while (elapsed < duration)
            {
                var step = Step(Vector2.zero, Key.E); while (step.MoveNext()) yield return null;
                elapsed += LastStepDuration;
                if (elapsed > .1f && session.interaction.Current != target && !session.interaction.Powered)
                    throw new Exception("Held E did not latch " + target.label + "; hint=" + session.interaction.Hint);
            }
            release = WaitReleased(2); while (release.MoveNext()) yield return null;
        }
        IEnumerator WaitReleased(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                var step = Step(Vector2.zero); while (step.MoveNext()) yield return null;
            }
        }
        float LastStepDuration { get; set; }
        IEnumerator Step(Vector2 mouseDelta, params Key[] keys)
        {
            while (Time.frameCount == tickedFrame) { EnsureRunning(); yield return null; }
            EnsureRunning(); tickedFrame = Time.frameCount;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = mouseDelta });
            InputSystem.Update(); keyboard.MakeCurrent(); mouse.MakeCurrent();
            if (session.Paused)
            {
                session.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
                if(session.Paused)throw new Exception("UI resume refused during route");
            }
            // Use the same frame duration as the live NavMeshAgent/player loop;
            // do not slow resource time or player time relative to the guard.
            LastStepDuration = Mathf.Max(Time.deltaTime, .00001f);
            session.Tick(LastStepDuration);
            Vector3 current = session.player.transform.position;
            var flat = current - previousPosition; flat.y = 0; travelled += flat.magnitude; previousPosition = current;
            if (session.Outcome != RunOutcome.Escaped) EnsureRunning();
            yield return null;
        }
        void EnsureRunning()
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("90-second route deadline exceeded");
            if (!session || !session.Ready || session.Outcome == RunOutcome.Error) throw new Exception("Route session error: " + (session ? session.Failure : "missing"));
            if (session.Outcome == RunOutcome.Caught || session.Outcome == RunOutcome.Depleted)
                throw new Exception("Normal route failed: " + session.Outcome + " at " + session.player.transform.position);
        }
        public void Record(string stage)
        {
            report.AppendLine($"{stage} realtime={EditorApplication.timeSinceStartup - started:F2} frame={Time.frameCount} position={session.player.transform.position:F3} energy={session.battery.Current:F3} outcome={session.Outcome} enemy={session.enemy.State} pulseUses={session.pulse.SuccessfulUses} ui={ui.Mode} travelled={travelled:F3} warp=false");
        }
        public void Save()
        {
            string dir = string.IsNullOrEmpty(EvidenceDirectory) ? Path.Combine(Application.dataPath, "../Evidence/ChapterOne") : EvidenceDirectory; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, withPulse ? "route-with-pulse.txt" : "route-no-pulse.txt"), report.ToString());
        }
        public void Dispose()
        {
            if (session)
            {
                session.Pause(); session.player.MoveAction.actionMap.devices = previousMoveDevices;
                if (previousInteractOverride == null) session.interaction.InteractAction.RemoveBindingOverride(0);
                else session.interaction.InteractAction.ApplyBindingOverride(0, previousInteractOverride);
                if(previousPulseOverride==null)session.pulse.PulseAction.RemoveBindingOverride(0);
                else session.pulse.PulseAction.ApplyBindingOverride(0,previousPulseOverride);
                session.enabled = previousEnabled;
            }
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }
}

