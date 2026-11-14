using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastCellOfPower.StealthSample;

// Scripted scene reload / failure lifecycle checks. NOT physical R-key acceptance.
[InitializeOnLoad]
public static class StealthSessionChecks
{
    const string ScenePath = "Assets/StealthSample/Scenes/StealthSample.unity";
    const string RunningKey = "StealthSessionChecks.Running";
    static IEnumerator routine;
    static double deadline;
    static string results;
    public static string LastResult { get; private set; } = "NOT_RUN";

    static StealthSessionChecks() { EditorApplication.playModeStateChanged += OnMode; }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void RunPlayMode()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in edit mode");
        Require(SceneManager.GetActiveScene().path == ScenePath, "Open StealthSample first");
        Require(!SessionState.GetBool(RunningKey, false), "Check already running");
        SessionState.SetBool(RunningKey, true);
        LastResult = "RUNNING";
        EditorApplication.EnterPlaymode();
    }

    static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results = "Scripted reload and runtime failure checks; NOT physical R-key/input acceptance.\n";
            deadline = EditorApplication.timeSinceStartup + 60;
            routine = Checks();
            EditorApplication.update += Advance;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && routine != null)
            Finish(false, "PlayMode exited before checks completed");
    }

    static void Advance()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "60-second lifecycle timeout");
            if (!routine.MoveNext()) Finish(true, "All lifecycle checks finished");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Pass(string message)
    {
        results += "PASS " + message + "\n";
        Debug.Log("STEALTH_SESSION_CHECK_PASS " + message);
    }
    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Advance;
        routine = null;
        SessionState.SetBool(RunningKey, false);
        LastResult = passed ? "PASS" : "FAIL";
        results += LastResult + " " + message + "\n";
        string dir = Path.GetFullPath("Evidence/StealthSample");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt"), results);
        if (passed) Debug.Log("STEALTH_SESSION_LIFECYCLE_PASS");
        else Debug.LogError("STEALTH_SESSION_LIFECYCLE_FAIL " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static bool IsOwnedMove(InputAction a)
    {
        return a != null && a.name == "Move" && a.actionMap != null && a.actionMap.name == "Player"
            && a.actionMap.asset != null && a.actionMap.asset.name.Contains("(Clone)");
    }
    static int EnabledClonedMoves() { return InputSystem.ListEnabledActions().Count(IsOwnedMove); }
    static int LiveClonedAssets()
    {
        return Resources.FindObjectsOfTypeAll<InputActionAsset>().Count(a => a.name.Contains("(Clone)")
            && a.FindAction("Player/Move", false) != null);
    }

    static IEnumerator Checks()
    {
        StealthSession s = null;
        InputAction previousMove = null;
        int previousId = 0, baselineAssets = -1;
        for (int cycle = 0; cycle <= 3; cycle++)
        {
            // Advance real player-loop frames so Start and deferred Destroy can complete.
            int frame = Time.frameCount;
            while (Time.frameCount < frame + 3) yield return null;
            while (true)
            {
                s = UnityEngine.Object.FindFirstObjectByType<StealthSession>();
                if (s && s.Failure.Length > 0) throw new Exception(s.Failure);
                if (s && s.Ready) break;
                yield return null;
            }
            Require(UnityEngine.Object.FindObjectsByType<StealthSession>(FindObjectsSortMode.None).Length == 1, "One session after reload");
            Require(cycle == 0 || s.GetInstanceID() != previousId, "New session instance after reload");
            Require(s.Paused && !s.DebugVisible && s.enemy.State == EnemyState.Patrol, "Initial paused Patrol and debug off");
            Require(Vector3.Distance(s.player.controller.transform.position, new Vector3(0,0,-4)) < .05f, "Player reset to spawn");
            Require(Vector3.Distance(s.enemy.agent.transform.position, new Vector3(0,.05f,4)) < .1f, "Enemy reset to spawn");
            Require(!s.player.MoveAction.enabled && s.enemy.agent.isStopped, "Initial input disabled and navigation stopped");
            Require(EnabledClonedMoves() == 0, "No enabled Move clone remains after reload");
            if (previousMove != null) Require(!previousMove.enabled, "Previous owned Move disabled");
            int assets = LiveClonedAssets();
            if (cycle == 0) baselineAssets = assets;
            Require(assets == baselineAssets, "No accumulating cloned input assets");
            Pass("Initialization/reload cycle " + cycle + "; clonedAssets=" + assets);
            if (cycle < 3)
            {
                previousMove = s.player.MoveAction;
                previousId = s.GetInstanceID();
                // Leave owned input enabled before unload to exercise teardown cleanup.
                s.player.SetActive(true);
                Require(previousMove.enabled && EnabledClonedMoves() == 1, "Exactly one enabled owned Move before unload");
                EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            }
        }

        s.player.SetActive(true);
        s.enemy.SetActive(true);
        Require(s.player.MoveAction.enabled && !s.enemy.agent.isStopped, "Failure fixture starts active");
        Debug.Log("STEALTH_SESSION_EXPECTED_ERROR_BEGIN: destroyed camera");
        UnityEngine.Object.Destroy(s.player.viewCamera);
        int failureFrame = Time.frameCount;
        while (s.Failure.Length == 0 || Time.frameCount <= failureFrame + 1) yield return null;
        Require(s.enabled && !s.Ready && s.Paused, "Actual Session.Update enters stopped failure state");
        Require(s.Failure.Contains("required runtime reference"), "Explicit missing-reference failure reason");
        Require(!s.player.MoveAction.enabled && EnabledClonedMoves() == 0, "Failure disables owned input");
        Require(s.enemy.agent.isOnNavMesh && s.enemy.agent.isStopped, "Failure stops navigation");
        Debug.Log("STEALTH_SESSION_EXPECTED_ERROR_END");
        Pass("Destroyed camera handled by actual Update: Failure, Ready=false, input off, agent stopped");
    }
}
