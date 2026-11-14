using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using LastCellOfPower.StealthSample;

// These are coordinate-controlled logic checks, NOT physical keyboard/focus acceptance.
[InitializeOnLoad]
public static class StealthSampleChecks
{
    const string ScenePath = "Assets/StealthSample/Scenes/StealthSample.unity";
    const string RunningKey = "StealthSampleChecks.Running";
    static IEnumerator routine;
    static double deadline;
    static string results;
    public static string LastResult { get; private set; } = "NOT_RUN";

    static StealthSampleChecks()
    {
        EditorApplication.playModeStateChanged += OnMode;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("STEALTH_CHECK_FAIL: " + message);
    }

    public static void RunContract()
    {
        Require(!EditorApplication.isPlaying, "Contract requires edit mode");
        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ScenePath, "Open the StealthSample scene before contract");
        var sessions = UnityEngine.Object.FindObjectsByType<StealthSession>(FindObjectsSortMode.None);
        Require(sessions.Length == 1, "Exactly one session");
        var s = sessions[0];
        Require(s.player && s.enemy && s.player.controller && s.player.viewCamera && s.player.inputAsset, "Player references");
        Require(s.enemy.agent && s.enemy.perception && s.enemy.perception.target == s.player.controller.transform, "Enemy references");
        Require(s.pathLine && s.sightLine && s.lastSeenLine, "Debug lines");
        Require(s.pathLine.useWorldSpace && s.sightLine.useWorldSpace && s.lastSeenLine.useWorldSpace, "Debug lines must use world coordinates");
        Require(!s.player.viewCamera.orthographic, "First-person perspective camera");
        Require((s.player.viewCamera.cullingMask & (1 << s.player.controller.gameObject.layer)) == 0, "Player hidden from main camera");
        Require(UnityEngine.Object.FindObjectsByType<Stage4Runtime>(FindObjectsSortMode.None).Length == 0, "Old runtime removed in NEW scene");
        s.enemy.perception.Initialize();
        Require(s.enemy.patrolPoints.Length == 4, "Four patrol points");
        foreach (var p in s.enemy.patrolPoints) Require(NavMesh.SamplePosition(p, out _, .3f, s.enemy.agent.areaMask), "Patrol NavMesh: " + p);
        Debug.Log("STEALTH_CONTRACT_PASS");
        LastResult = "CONTRACT_PASS";
    }

    public static void RunPlayMode()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start from edit mode");
        RunContract();
        SessionState.SetBool(RunningKey, true);
        LastResult = "RUNNING";
        EditorApplication.EnterPlaymode();
    }

    static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            results = "Coordinate-controlled PlayMode logic checks; not physical input acceptance.\n";
            routine = Checks();
            EditorApplication.update += Advance;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && routine != null)
            Finish(false, "PlayMode exited before checks finished");
    }

    static void Pass(string name) { results += "PASS " + name + "\n"; Debug.Log("STEALTH_CHECK_PASS " + name); }

    static void Advance()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup <= deadline, "90-second check timeout");
            if (!routine.MoveNext()) Finish(true, "All logic checks finished");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Advance;
        routine = null;
        SessionState.SetBool(RunningKey, false);
        LastResult = passed ? "PASS" : "FAIL";
        results += LastResult + " " + message + "\n";
        string dir = Path.GetFullPath("../StealthSampleEvidence");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "logic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), results);
        if (passed) Debug.Log("STEALTH_PLAYMODE_LOGIC_PASS"); else Debug.LogError("STEALTH_PLAYMODE_LOGIC_FAIL " + message);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void PlacePlayer(StealthSession s, Vector3 position)
    {
        s.player.controller.enabled = false;
        s.player.controller.transform.position = position;
        s.player.controller.enabled = true;
        Physics.SyncTransforms();
    }

    static IEnumerator Checks()
    {
        // Wait for Start without assuming a rendered frame implies initialization.
        StealthSession s = null;
        for (int i = 0; i < 200; i++)
        {
            s = UnityEngine.Object.FindFirstObjectByType<StealthSession>();
            if (s && s.Ready) break;
            if (s && s.Failure.Length > 0) throw new Exception(s.Failure);
            yield return null;
        }
        Require(s && s.Ready, "Session ready");
        s.enabled = false; // Stops UI/input ownership; test explicitly ticks enemy only.
        var e = s.enemy; var p = e.perception;
        Require(e.State == EnemyState.Patrol, "Initial Patrol");
        Require(e.agent.Warp(new Vector3(3,0,-4)), "Enemy placement");
        e.agent.transform.rotation = Quaternion.identity;
        PlacePlayer(s, new Vector3(3,0,-2));
        p.Evaluate(); Require(p.Visible && !p.Obstructed, "Clear vision within cone");
        e.agent.transform.rotation = Quaternion.Euler(0,180,0);
        p.Evaluate(); Require(!p.Visible, "Outside cone invisible");
        e.agent.transform.rotation = Quaternion.identity;
        PlacePlayer(s, new Vector3(-3,0,0));
        Require(e.agent.Warp(new Vector3(3,0,0)), "Shelf test placement");
        e.agent.transform.LookAt(new Vector3(-3,0,0));
        p.Evaluate(); Require(p.Obstructed && !p.Visible, "Existing central shelf blocks vision");
        Pass("clear/cone/shelf perception");

        e.agent.Warp(new Vector3(3,0,-4)); e.agent.transform.rotation = Quaternion.identity;
        PlacePlayer(s, new Vector3(3,0,-2));
        e.SetActive(true); e.Tick(.02f);
        Require(e.State == EnemyState.Chase && e.HasLastSeen, "Visible player enters Chase");
        var seen = e.LastSeen; var patrol = e.PatrolIndex;
        PlacePlayer(s, new Vector3(100,0,100));
        e.Tick(.02f);
        Require(e.State == EnemyState.Search && e.LastSeen == seen, "Lost player enters Search at frozen point");
        Pass("Patrol-Chase-Search and last seen");
        double until = EditorApplication.timeSinceStartup + 20;
        while (!e.SearchingAtPoint)
        {
            Require(EditorApplication.timeSinceStartup < until, "Navigation reaches last seen");
            e.Tick(.02f); Require(e.LastSeen == seen, "Hidden player not tracked"); yield return null;
        }
        float elapsed = e.SearchElapsed;
        e.SetActive(false);
        for (int i=0;i<10;i++) { e.Tick(.1f); yield return null; }
        Require(e.SearchElapsed == elapsed && e.agent.isStopped, "Paused search timer and agent");
        e.SetActive(true);
        while (e.State == EnemyState.Search) { e.Tick(.02f); yield return null; }
        Require(e.State == EnemyState.Return && e.PatrolIndex == patrol, "Return resumes interrupted patrol target");
        Pass("Search arrival/pause/Return");
        until = EditorApplication.timeSinceStartup + 25;
        while (e.State == EnemyState.Return)
        {
            Require(EditorApplication.timeSinceStartup < until, "Return reaches patrol target");
            e.Tick(.02f); yield return null;
        }
        Require(e.State == EnemyState.Patrol, "Return becomes Patrol");
        Pass("Return-Patrol navigation");

        Require(e.agent.Warp(new Vector3(3,0,-3)), "Capture placement");
        PlacePlayer(s, new Vector3(3,0,-2.4f));
        var wall = new GameObject("LogicCheckTemporaryWall");
        wall.layer = 0; wall.transform.position = new Vector3(3,.8f,-2.7f);
        wall.AddComponent<BoxCollider>().size = new Vector3(1,2,.08f);
        Physics.SyncTransforms(); e.Tick(.02f);
        Require(e.State != EnemyState.Caught && p.Obstructed, "No capture through thin wall");
        UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms();
        e.Tick(.02f); Require(e.State == EnemyState.Caught && e.agent.isStopped, "Close clear capture");
        PlacePlayer(s, new Vector3(100,0,100));
        e.Tick(10); Require(e.State == EnemyState.Caught && e.agent.isStopped, "Caught terminal");
        Pass("wall-safe capture and terminal state");
    }
}
