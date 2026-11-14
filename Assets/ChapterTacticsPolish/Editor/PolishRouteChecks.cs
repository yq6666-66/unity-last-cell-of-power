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
using LastCellOfPower.ChapterTactics;

// Background integration smoke: every player displacement comes from MoveAction
// -> StealthPlayerController -> CharacterController.Move. This is synthetic input,
// simulated focus and explicit session timing, NOT Windows desktop acceptance.

public static class PolishRouteChecks
{
    public static string LastResult { get; private set; } = "NOT_RUN";

    public static IEnumerator Run()
    {
        const TacticsRouteMode mode=TacticsRouteMode.Noise;
        var session = UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        if (!Application.isPlaying || !session || !session.Ready || !session.Paused)
            throw new InvalidOperationException("Route smoke requires initialized, paused ExpandedSession");
        if (!session.pulse || !session.statistics || !session.externalPlayerUI) throw new InvalidOperationException("Chapter core required");
        if (Vector3.Distance(session.player.transform.position, session.level.playerSpawn) > .1f || Mathf.Abs(session.battery.Current - 40) > .001f)
            throw new InvalidOperationException("Reload a fresh scene before route smoke; no repositioning is permitted");
        LastResult = "RUNNING";
        using (var driver = new RouteDriver(session,mode))
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
        PolishChecks.Pass("Continuous muted controller route: move cancels charge, pause/resume release gate, E noise feedback, supply and escape; no warp");
        Debug.Log("POLISH_ROUTE_SMOKE_PASS mode="+mode);
    }

    sealed class RouteDriver : IDisposable
    {
        readonly ExpandedSession session;
        readonly ChapterPlayerUI ui;
        readonly TacticsRouteMode mode;
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

        public RouteDriver(ExpandedSession session,TacticsRouteMode mode)
        {
            this.session = session;this.mode=mode;
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
            started = EditorApplication.timeSinceStartup; deadline = started + 120;
            report.AppendLine("POLISH MUTED "+mode+" SYNTHETIC BACKGROUND ROUTE SMOKE. Simulated focus, virtual devices, explicit Session.Tick; not desktop keyboard acceptance.");
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
            ui.SetMasterVolume(0);
            if(session.Paused || ui.Mode!=ChapterViewMode.Playing)throw new Exception("UI begin command did not start gameplay");
            Record("UI_BEGIN");
            var routine = WaitReleased(3); while (routine.MoveNext()) yield return null;
            var charger = session.interaction.targets.Where(t => t.kind == InteractionKind.Charger)
                .OrderBy(t => Vector3.Distance(t.Position, session.level.playerSpawn)).First();
            Vector3 chargerApproach = charger.Position + Vector3.forward; chargerApproach.y = 0;
            routine = Walk(chargerApproach, "starter charger"); while (routine.MoveNext()) yield return null;
            routine = Aim(charger.Position); while (routine.MoveNext()) yield return null;
            routine=CancelAndPauseAtCharger(charger);while(routine.MoveNext())yield return null;
            float beforeCharge = session.battery.Current;
            float chargeSeconds=5f;
            routine = Hold(charger, chargeSeconds); while (routine.MoveNext()) yield return null;
            if (session.battery.Current < Mathf.Min(session.rules.maximumEnergy,beforeCharge+chargeSeconds*(session.rules.chargingRate-session.rules.idleDrain))-1f)
                throw new Exception("Starter charge did not replenish expected energy");
            Record("CHARGED");
            routine=UseNoise();while(routine.MoveNext())yield return null;

            var terminal = session.interaction.targets.Single(t => t.kind == InteractionKind.Terminal);
            Vector3 terminalApproach = session.level.terminal + Vector3.back * 1.5f;
            routine = Walk(terminalApproach, "terminal"); while (routine.MoveNext()) yield return null;
            routine = Aim(terminal.Position); while (routine.MoveNext()) yield return null;
            routine = SupplyWithThreatResponse(terminal); while (routine.MoveNext()) yield return null;
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
            if(session.Summary==null || session.Summary.SuccessfulPulseCount!=session.pulse.SuccessfulUses)throw new Exception("Actual Q use statistics differ from summary");
            if(mode==TacticsRouteMode.Noise && session.tactics.UsedNoiseCount!=1)throw new Exception("Noise route use count");
            Record("ESCAPED");
        }

        IEnumerator CancelAndPauseAtCharger(EscapeInteractable charger)
        {
            var released=WaitReleased(3);while(released.MoveNext())yield return null;
            var press=Step(Vector2.zero,Key.E);while(press.MoveNext())yield return null;
            if(session.interaction.Current!=charger || !session.interaction.Charging)throw new Exception("Normal E did not latch starting charger");
            var leave=Step(Vector2.zero,Key.E,Key.W);while(leave.MoveNext())yield return null;
            if(session.interaction.Current || !session.interaction.WaitingForRelease || session.player.HorizontalDistanceLastTick<=.000001f)
                throw new Exception("Movement did not cancel charge and free the real controller in the same input loop");
            Record("MOVE_CANCELLED_CHARGE");
            released=WaitReleased(3);while(released.MoveNext())yield return null;
            var aim=Aim(charger.Position);while(aim.MoveNext())yield return null;
            press=Step(Vector2.zero,Key.E);while(press.MoveNext())yield return null;
            if(!session.interaction.Charging)throw new Exception("Fresh E after release could not recharge");
            ui.ShowSettings();
            float energy=session.battery.Current;Vector3 pausedPosition=session.player.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.E));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
            int start=Time.frameCount;
            while(Time.frameCount<start+3){session.Tick(Time.deltaTime);yield return null;}
            if(!session.Paused || session.interaction.Current || Mathf.Abs(session.battery.Current-energy)>.001f || Vector3.Distance(pausedPosition,session.player.transform.position)>.001f)
                throw new Exception("Settings pause retained interaction or advanced resources/movement");
            ui.ReturnToPause();session.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
            var held=Step(Vector2.zero,Key.W,Key.E);while(held.MoveNext())yield return null;
            if(session.interaction.Current || session.player.HorizontalDistanceLastTick>.000001f)
                throw new Exception("Held movement/E leaked across pause resume release gate");
            Record("PAUSE_RESUME_NEUTRAL_GATE");
            released=WaitReleased(3);while(released.MoveNext())yield return null;
            aim=Aim(charger.Position);while(aim.MoveNext())yield return null;
        }

        IEnumerator SupplyWithThreatResponse(EscapeInteractable terminal)
        {
            var released=WaitReleased(3);while(released.MoveNext())yield return null;
            float elapsed=0;int interruptions=0;
            double until=EditorApplication.timeSinceStartup+20;
            while(!session.interaction.Powered)
            {
                EnsureRunning();if(EditorApplication.timeSinceStartup>until || interruptions>2)throw new Exception("Could not safely finish terminal supply within bounded attempts");
                var threat=session.pulse.SelectedTarget;
                if(session.pulse.ReadyToUse && threat && Vector3.Distance(threat.transform.position,session.player.transform.position)<3.8f)
                {
                    Record("DEFENSIVE_Q_REQUEST guard="+threat.guardId+" state="+threat.State);
                    int uses=session.pulse.SuccessfulUses;
                    var pulseStep=Step(Vector2.zero,Key.Q);while(pulseStep.MoveNext())yield return null;
                    if(session.pulse.LastResult!=PulseUseResult.Success || session.pulse.SuccessfulUses!=uses+1)throw new Exception("Defensive Q failed: "+session.pulse.LastResult);
                    Record("DEFENSIVE_Q_SUCCESS guard="+session.pulse.LastResolvedTarget.guardId);
                    elapsed=0;interruptions++;
                    released=WaitReleased(3);while(released.MoveNext())yield return null;
                    var aim=Aim(terminal.Position);while(aim.MoveNext())yield return null;
                    continue;
                }
                var step=Step(Vector2.zero,Key.E);while(step.MoveNext())yield return null;
                elapsed+=LastStepDuration;
                if(elapsed>.15f && session.interaction.Current!=terminal && !session.interaction.Powered)throw new Exception("Supply did not latch after threat handling: "+session.interaction.Hint);
            }
            released=WaitReleased(2);while(released.MoveNext())yield return null;
        }

        IEnumerator UseNoise()
        {
            var noise=session.tactics.noiseDevices[0];
            var approach=noise.interactable.Position+Vector3.forward*1.05f;approach.y=0;
            var routine=Walk(approach,"NW noise control");while(routine.MoveNext())yield return null;
            routine=Aim(noise.interactable.Position);while(routine.MoveNext())yield return null;
            routine=WaitReleased(3);while(routine.MoveNext())yield return null;
            var press=Step(Vector2.zero,Key.E);while(press.MoveNext())yield return null;
            routine=WaitReleased(2);while(routine.MoveNext())yield return null;
            if(!noise.Used)throw new Exception("E did not activate NW noise device");
            if(!ui.polishFeedback || string.IsNullOrEmpty(ui.polishFeedback.CurrentFeedbackText))throw new Exception("Muted E activation lacks visible feedback");
            int sequence=session.tactics.LastFeedback.Sequence;
            if(ui.polishFeedback.LastFeedbackSequence!=sequence)throw new Exception("Visible feedback did not consume the actual committed observation");
            Record("MUTED_NOISE_ACTIVATED event="+noise.EventId+" responses="+noise.ResponseCount+" text="+ui.polishFeedback.CurrentFeedbackText);
            // Zero response is a legitimate result. A route must not manufacture proximity or require
            // a guard to accept merely because the player consumed a one-use source.
            var observe=WaitReleased(4);while(observe.MoveNext())yield return null;
            Record("NOISE_OBSERVED responses="+noise.ResponseCount);
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
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("120-second route deadline exceeded");
            if (!session || !session.Ready || session.Outcome == RunOutcome.Error) throw new Exception("Route session error: " + (session ? session.Failure : "missing"));
            if (session.Outcome == RunOutcome.Caught || session.Outcome == RunOutcome.Depleted)
                throw new Exception("Normal route failed: " + session.Outcome + " at " + session.player.transform.position);
        }
        public void Record(string stage)
        {
            report.AppendLine($"{stage} realtime={EditorApplication.timeSinceStartup - started:F2} frame={Time.frameCount} position={session.player.transform.position:F3} energy={session.battery.Current:F3} outcome={session.Outcome} guards={string.Join("/",session.Guards.Select(g=>g.guardId+":"+g.State+"@"+g.transform.position.ToString("F2")))} noise={session.tactics.UsedNoiseCount} shutter={session.tactics.shutter.State} pulseUses={session.pulse.SuccessfulUses} ui={ui.Mode} travelled={travelled:F3} warp=false");
        }
        public void Save()
        {
            string dir = PolishChecks.Evidence; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "route-normal-input.txt"), report.ToString());
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

