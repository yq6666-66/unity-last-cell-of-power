using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

// Coordinate/energy fixtures and simulated focus exercise the actual session and
// Restart paths. This is background lifecycle logic, not desktop play evidence.
public static class ExpandedLifecycleChecks
{
    public static string LastResult { get; private set; } = "NOT_RUN";
    static StringBuilder report;
    static int baselineClones;

    public static IEnumerator Run()
    {
        Require(Application.isPlaying, "PlayMode required");
        report = new StringBuilder("SCRIPTED LIFECYCLE LOGIC: public battery drain, guarded enemy Warp, exit coordinate fixture, virtual devices and simulated focus. Not desktop input acceptance.\n");
        LastResult = "RUNNING"; baselineClones = -1;
        var routine = Checks();
        try
        {
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e)
                {
                    LastResult = "FAIL"; report.AppendLine("FAIL " + e); Save(); throw;
                }
                if (!more) break;
                yield return routine.Current;
            }
            LastResult = "PASS"; report.AppendLine("PASS all three expanded outcomes restart to a clean configured state"); Save();
            Debug.Log("EXPANDED_LIFECYCLE_CHECKS_PASS");
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    static IEnumerator Checks()
    {
        var initialWait = WaitReady(0); while (initialWait.MoveNext()) yield return null;
        foreach (RunOutcome outcome in new[] { RunOutcome.Depleted, RunOutcome.Caught, RunOutcome.Escaped })
        {
            var session = Session(); int previousId = session.GetInstanceID();
            using (var input = new ControlledSession(session))
            {
                if (outcome == RunOutcome.Depleted)
                {
                    session.battery.Tick(1000, false, false);
                    Require(session.battery.Depleted, "Depletion fixture reached zero through public drain");
                    input.Resume(); session.Tick(.02f);
                }
                else if (outcome == RunOutcome.Caught)
                {
                    Vector3 desired = session.player.transform.position + Vector3.right * .5f;
                    Require(NavMesh.SamplePosition(desired, out var hit, .3f, session.enemy.agent.areaMask), "Close-contact fixture has navigation");
                    Require(session.enemy.agent.Warp(hit.position), "Enemy fixture placement succeeds");
                    var facing = session.player.transform.position - session.enemy.agent.transform.position; facing.y = 0;
                    session.enemy.agent.transform.rotation = Quaternion.LookRotation(facing);
                    Physics.SyncTransforms();
                    Require(facing.magnitude <= session.profile.captureDistance, "Fixture inside actual capture radius");
                    input.Resume();
                    session.Tick(session.profile.alertSeconds * .5f);
                    Require(session.Outcome == RunOutcome.Running && session.enemy.State == ExpandedEnemyState.Alert, "Close contact still gives an Alert window");
                    session.Tick(session.profile.alertSeconds * .5f + .001f);
                    Require(session.Outcome == RunOutcome.Running && session.enemy.State == ExpandedEnemyState.Chase, "Full Alert enters Chase before capture confirmation");
                    session.Tick(session.profile.captureSeconds * .9f);
                    Require(session.Outcome == RunOutcome.Running, "Short capture contact has not ended session");
                    session.Tick(session.profile.captureSeconds * .2f);
                }
                else
                {
                    session.door.BeginOpening(); session.door.Tick(session.rules.openingSeconds);
                    var wait = Frames(4); while (wait.MoveNext()) yield return null;
                    session.player.controller.enabled = false;
                    session.player.transform.position = session.door.exitAnchor.TransformPoint(new Vector3(0, .03f, .5f));
                    session.player.controller.enabled = true; Physics.SyncTransforms();
                    Require(session.door.HasEscaped(session.player.transform.position), "Exit coordinate fixture is inside open exit");
                    input.Resume(); session.Tick(.02f);
                }
                Require(session.Outcome == outcome, "Actual Session.Tick yielded " + outcome + ", got " + session.Outcome);
                Require(session.Paused && !session.player.MoveAction.enabled && !session.interaction.InteractAction.enabled && session.enemy.agent.isStopped,
                    "Terminal state freezes owned inputs and enemy");
                float energy = session.battery.Current, doorTime = session.door.OpeningElapsed;
                session.Tick(5);
                Require(session.Outcome == outcome && session.battery.Current == energy && session.door.OpeningElapsed == doorTime,
                    "Ended session cannot advance resource or door time");
                Record("PASS actual session " + outcome + " and terminal freeze", session);
            }
            session.Restart();
            var reset = WaitReady(previousId); while (reset.MoveNext()) yield return null;
            Record("PASS " + outcome + " Restart reset", Session());
        }
    }

    static IEnumerator WaitReady(int previousId)
    {
        var wait = Frames(4); while (wait.MoveNext()) yield return null;
        float deadline = Time.realtimeSinceStartup + 10;
        while (!Session() || !Session().Ready)
        {
            Require(Time.realtimeSinceStartup < deadline, "Session initialization timeout");
            if (Session() && Session().Failure.Length > 0) throw new Exception(Session().Failure);
            yield return null;
        }
        var s = Session();
        Require(previousId == 0 || previousId != s.GetInstanceID(), "Restart creates a fresh session");
        Require(UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None).Length == 1, "Exactly one session");
        Require(s.Ready && s.Failure.Length == 0 && s.Outcome == RunOutcome.Running && s.Paused && !s.DebugVisible, "Initial session state restored");
        Require(Mathf.Abs(s.battery.Current - 40) < .001f, "Initial battery 40 restored");
        Require(Vector3.Distance(s.player.transform.position, s.level.playerSpawn) < .06f, "Player spawn restored");
        Require(Vector3.Distance(s.enemy.agent.transform.position, s.level.enemySpawn) < .2f, "Enemy spawn restored");
        Require(s.enemy.State == ExpandedEnemyState.Patrol && s.enemy.Awareness01 == 0 && s.enemy.CaptureElapsed == 0 && !s.enemy.HasLastSeen,
            "Patrol/awareness/capture/memory reset");
        Require(s.enemy.agent.isStopped && !s.player.HorizontalMovementLocked, "Paused navigation and unlocked player");
        Require(s.door.State == EscapeDoorState.Closed && s.door.OpeningElapsed == 0 && s.door.doorCollider.enabled && s.door.doorObstacle.enabled,
            "Closed door and barriers reset");
        Require(!s.interaction.Powered && !s.interaction.Current && s.interaction.SupplyElapsed == 0, "Terminal reset");
        Require(!s.player.MoveAction.enabled && !s.interaction.InteractAction.enabled, "Owned actions disabled initially");
        Require(!InputSystem.ListEnabledActions().Any(a => a.name == "EscapeInteract" ||
            (a.name == "Move" && a.actionMap != null && a.actionMap.asset != null && a.actionMap.asset.name.Contains("(Clone)"))),
            "No enabled owned action accumulated");
        int clones = Resources.FindObjectsOfTypeAll<InputActionAsset>().Count(a => a.name.Contains("(Clone)") && a.FindAction("Player/Move", false) != null);
        if (baselineClones < 0) baselineClones = clones;
        Require(clones == baselineClones, "No accumulating input asset clones");
    }
    static IEnumerator Frames(int count)
    {
        int end = Time.frameCount + count;
        while (Time.frameCount < end) yield return null;
    }
    static ExpandedSession Session() => UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
    static void Require(bool value, string message) { if (!value) throw new Exception("EXPANDED_LIFECYCLE_CHECK: " + message); }
    static void Record(string message, ExpandedSession s)
    {
        report.AppendLine($"{message}; frame={Time.frameCount}; energy={s.battery.Current:F3}; result={s.Outcome}; state={s.enemy.State}; position={s.player.transform.position:F3}");
    }
    static void Save()
    {
        string dir = Path.Combine(Application.dataPath, "../Evidence/WarehouseExpanded"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "lifecycle.txt"), report.ToString());
    }

    sealed class ControlledSession : IDisposable
    {
        readonly ExpandedSession session;
        readonly bool previousEnabled;
        readonly Keyboard previousKeyboard, keyboard;
        readonly Mouse previousMouse, mouse;
        readonly ReadOnlyArray<InputDevice>? previousDevices;
        readonly string previousOverride;
        public ControlledSession(ExpandedSession session)
        {
            this.session = session; previousEnabled = session.enabled;
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            previousDevices = session.player.MoveAction.actionMap.devices;
            previousOverride = session.interaction.InteractAction.bindings[0].overridePath;
            session.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>("ExpandedLifecycleKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("ExpandedLifecycleMouse");
            session.player.MoveAction.actionMap.devices = new InputDevice[] { keyboard };
            session.interaction.InteractAction.ApplyBindingOverride(0, keyboard.eKey.path);
            Clear();
        }
        public void Resume()
        {
            session.SendMessage("OnApplicationFocus", true);
            session.Resume(); Clear();
        }
        void Clear()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update(); keyboard.MakeCurrent(); mouse.MakeCurrent();
        }
        public void Dispose()
        {
            if (session)
            {
                session.Pause(); session.player.MoveAction.actionMap.devices = previousDevices;
                if (previousOverride == null) session.interaction.InteractAction.RemoveBindingOverride(0);
                else session.interaction.InteractAction.ApplyBindingOverride(0, previousOverride);
                session.enabled = previousEnabled;
            }
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }
}
