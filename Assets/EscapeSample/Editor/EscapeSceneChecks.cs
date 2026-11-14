using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.StealthSample;

// Scene geometry/lifecycle logic fixtures. Coordinates, battery and simulated focus
// are controlled here; results are never evidence of ordinary keyboard gameplay.
[InitializeOnLoad]
public static class EscapeSceneChecks
{
    const string RunningKey = "EscapeSceneChecks.Running";
    static IEnumerator routine;
    static double deadline;
    static string results;
    static int baselineClones = -1;
    public static string LastResult { get; private set; } = "NOT_RUN";
    static EscapeSceneChecks() { EditorApplication.playModeStateChanged += ModeChanged; }

    public static void RunPlayMode()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in edit mode");
        Require(SceneManager.GetActiveScene().path == EscapeSampleBuilder.ScenePath, "Open EscapeSample scene");
        Require(!SessionState.GetBool(RunningKey, false), "Runner already active");
        SessionState.SetBool(RunningKey, true); LastResult = "RUNNING";
        EditorApplication.EnterPlaymode();
    }
    static void ModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results = "SCRIPTED SCENE LOGIC ONLY: controller Move, coordinate/battery fixtures, simulated focus, public session Tick and Restart. Not physical input acceptance.\n";
            baselineClones = -1; deadline = EditorApplication.timeSinceStartup + 120;
            routine = Checks(); EditorApplication.update += Advance;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && routine != null) Finish(false, "Unexpected PlayMode exit");
    }
    static void Advance()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Scene checks timed out");
            if (!routine.MoveNext()) Finish(true, "All scene checks finished");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool ok, string message)
    {
        EditorApplication.update -= Advance; routine = null;
        SessionState.SetBool(RunningKey, false); LastResult = ok ? "PASS" : "FAIL";
        results += LastResult + " " + message + "\n";
        var dir = Path.GetFullPath("Evidence/EscapeSample"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt"), results);
        if (ok) Debug.Log("ESCAPE_SCENE_CHECKS_PASS"); else Debug.LogError("ESCAPE_SCENE_CHECKS_FAIL " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Pass(string message) { results += "PASS " + message + "\n"; Debug.Log("ESCAPE_SCENE_PASS " + message); }
    static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target) yield return null;
    }
    static EscapeSession Session() => UnityEngine.Object.FindFirstObjectByType<EscapeSession>();
    static void ResetCheck(EscapeSession s, int previousId)
    {
        Require(s && s.Ready && s.Failure.Length == 0, "Session must initialize without error");
        Require(previousId == 0 || s.GetInstanceID() != previousId, "Reload creates a fresh session");
        Require(UnityEngine.Object.FindObjectsByType<EscapeSession>(FindObjectsSortMode.None).Length == 1, "One EscapeSession");
        Require(s.Outcome == RunOutcome.Running && s.Paused && !s.DebugVisible, "Reset running/paused/debug state");
        Require(Mathf.Abs(s.battery.Current - 35) < .001f, "Reset energy 35");
        Require(s.door.State == EscapeDoorState.Closed && s.door.doorCollider.enabled && s.door.doorObstacle.enabled, "Reset closed door and both barriers");
        Require(!s.interaction.Powered && s.interaction.SupplyElapsed == 0 && s.interaction.Current == null, "Reset terminal interaction");
        Require(s.enemy.State == EnemyState.Patrol && s.enemy.agent.isStopped, "Reset stopped Patrol");
        Require(Vector3.Distance(s.player.controller.transform.position, new Vector3(0,0,-4)) < .06f, "Reset player spawn");
        Require(!s.player.MoveAction.enabled && !s.interaction.InteractAction.enabled, "Reset disabled owned inputs");
        Require(!InputSystem.ListEnabledActions().Any(a => a.name == "EscapeInteract" ||
            (a.name == "Move" && a.actionMap != null && a.actionMap.asset != null && a.actionMap.asset.name.Contains("(Clone)"))), "No accumulated enabled owned actions");
        int clones = Resources.FindObjectsOfTypeAll<InputActionAsset>().Count(a => a.name.Contains("(Clone)") && a.FindAction("Player/Move", false) != null);
        if (baselineClones < 0) baselineClones = clones;
        Require(clones == baselineClones, "No accumulating cloned input assets");
    }
    static void ControlFixture(EscapeSession s)
    {
        s.enabled = false; // OnDisable pauses; nav/player loop still runs.
        s.SendMessage("OnApplicationFocus", true); // Explicitly simulated, not focus acceptance.
    }
    static void Place(EscapeSession s, Vector3 position)
    {
        s.player.controller.enabled = false;
        s.player.controller.transform.SetPositionAndRotation(position, Quaternion.identity);
        s.player.controller.enabled = true; Physics.SyncTransforms();
    }
    static void Open(EscapeSession s)
    {
        s.door.BeginOpening(); s.door.Tick(s.rules.openingSeconds); Physics.SyncTransforms();
        Require(s.door.State == EscapeDoorState.Open && !s.door.doorCollider.enabled && !s.door.doorObstacle.enabled, "Fully open releases physical/nav barriers");
    }
    static void Frozen(EscapeSession s, RunOutcome result)
    {
        Require(s.Outcome == result && s.Paused, "Expected terminal result " + result + ", got " + s.Outcome);
        Require(!s.player.MoveAction.enabled && !s.interaction.InteractAction.enabled && s.enemy.agent.isStopped, "Terminal freezes player/input/navigation");
        float energy = s.battery.Current, doorTime = s.door.OpeningElapsed;
        s.Tick(5);
        Require(s.battery.Current == energy && s.door.OpeningElapsed == doorTime && s.Outcome == result, "Terminal Tick cannot consume or change result");
    }
    static IEnumerator ReadyAfterReload(int previousId)
    {
        var frames = Frames(4); while (frames.MoveNext()) yield return null;
        while (!Session() || !Session().Ready)
        {
            if (Session() && Session().Failure.Length > 0) throw new Exception(Session().Failure);
            yield return null;
        }
        ResetCheck(Session(), previousId);
    }
    static IEnumerator Checks()
    {
        var wait = ReadyAfterReload(0); while (wait.MoveNext()) yield return null;
        var s = Session(); Pass("Initial scene state and input ownership"); ControlFixture(s);

        Place(s, new Vector3(1,.03f,5.2f));
        s.player.controller.Move(Vector3.forward * 2);
        Require(s.player.controller.transform.position.z < 6, "Closed door blocks controller before doorway");
        s.door.BeginOpening(); s.door.Tick(s.rules.openingSeconds * .5f);
        Place(s, new Vector3(1,.03f,5.2f)); s.player.controller.Move(Vector3.forward * 2);
        Require(s.door.State == EscapeDoorState.Opening && s.door.doorCollider.enabled && s.door.doorObstacle.enabled && s.player.controller.transform.position.z < 6,
            "Half-open door continues blocking controller and navigation");
        s.door.Tick(s.rules.openingSeconds * .5f); Physics.SyncTransforms();
        wait = Frames(5); while (wait.MoveNext()) yield return null;
        var path = new NavMeshPath();
        Require(NavMesh.SamplePosition(new Vector3(1,0,5.3f), out var inside, .3f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(new Vector3(1,0,7.2f), out var outside, .3f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(inside.position, outside.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
            "Open doorway has complete navigation into platform");
        Place(s, new Vector3(1,.03f,5.2f)); s.player.controller.Move(Vector3.forward * 2);
        Require(s.player.controller.transform.position.z >= 6.8f && s.door.HasEscaped(s.player.controller.transform.position), "Open door permits actual controller Move into exit");
        Pass("Closed/half-open block; fully open controller passage and complete NavMesh");

        // Escaped via production Tick, then production Restart (not a raw scene load).
        Require(s.enemy.agent.Warp(new Vector3(3,0,-4)), "Place guard away from exit fixture");
        Require(s.enemy.agent.SetDestination(s.enemy.Destination), "Restore patrol path after fixture Warp");
        s.Resume(); s.Tick(.02f); Frozen(s, RunOutcome.Escaped); Pass("Escaped outcome through session Tick");
        int oldId = s.GetInstanceID(); s.Restart();
        wait = ReadyAfterReload(oldId); while (wait.MoveNext()) yield return null;
        Pass("Escaped Restart fully resets scene/input/energy/door");

        // Depleted takes precedence over an otherwise valid open exit.
        s = Session(); ControlFixture(s); Open(s);
        wait = Frames(5); while (wait.MoveNext()) yield return null;
        Place(s, new Vector3(1,.03f,7.1f));
        Require(s.enemy.agent.Warp(new Vector3(3,0,-4)), "Guard away for depletion fixture");
        Require(s.enemy.agent.SetDestination(s.enemy.Destination), "Restore patrol path after depletion fixture Warp");
        s.battery.Tick(100, false, false); s.Resume(); s.Tick(.02f);
        Frozen(s, RunOutcome.Depleted); Pass("Depleted beats Escaped through session Tick");
        oldId = s.GetInstanceID(); s.Restart();
        wait = ReadyAfterReload(oldId); while (wait.MoveNext()) yield return null;
        Pass("Depleted Restart fully resets scene/input/energy/door");

        // Caught takes precedence over simultaneous zero battery and open exit.
        s = Session(); ControlFixture(s); Open(s);
        wait = Frames(5); while (wait.MoveNext()) yield return null;
        Place(s, new Vector3(1,.03f,7.1f));
        Require(s.enemy.agent.Warp(new Vector3(1,0,7.6f)), "Place guard within capture distance on platform");
        s.battery.Tick(100, false, false); s.Resume(); s.Tick(.02f);
        Frozen(s, RunOutcome.Caught); Pass("Caught beats Depleted and Escaped through session Tick");
        oldId = s.GetInstanceID(); s.Restart();
        wait = ReadyAfterReload(oldId); while (wait.MoveNext()) yield return null;
        Pass("Caught Restart fully resets scene/input/energy/door");

        // Error is tested by real Update's validation, not by calling Fail().
        s = Session(); ControlFixture(s); Open(s);
        wait = Frames(5); while (wait.MoveNext()) yield return null;
        Place(s, new Vector3(1,.03f,7.1f));
        Require(s.enemy.agent.Warp(new Vector3(1,0,7.6f)), "Error fixture capture placement");
        s.battery.Tick(100, false, false); s.Resume();
        // Make Caught true without ending the session, so all three normal
        // terminal candidates are present when Update validates the missing ref.
        s.enemy.Tick(.02f);
        Require(s.enemy.State == EnemyState.Caught && s.battery.Depleted && s.door.HasEscaped(s.player.controller.transform.position), "All lower-priority candidates prepared");
        Debug.Log("ESCAPE_SCENE_EXPECTED_ERROR_BEGIN: destroyed camera");
        UnityEngine.Object.DestroyImmediate(s.player.viewCamera);
        s.enabled = true;
        while (s.Outcome != RunOutcome.Error) yield return null;
        Require(!s.Ready && s.Failure.Contains("reference missing"), "Real Update reports missing-reference Error");
        Frozen(s, RunOutcome.Error);
        Debug.Log("ESCAPE_SCENE_EXPECTED_ERROR_END");
        Pass("Error beats all normal outcomes through actual Session.Update validation");
    }
}
