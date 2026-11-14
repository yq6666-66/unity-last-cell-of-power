using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.StealthSample;

public static class EscapeSampleDelivery
{
    public const string ScenePath = "Assets/EscapeSample/Scenes/EscapeSample.unity";
    public static string LastOutput { get; private set; }
    static void Require(bool value, string message) { if (!value) throw new Exception("ESCAPE_CONTRACT: " + message); }
    public static void Contract()
    {
        Require(!EditorApplication.isPlaying, "Edit mode required");
        Require(EditorSceneManager.GetActiveScene().path == ScenePath, "Open escape scene");
        var sessions = UnityEngine.Object.FindObjectsByType<EscapeSession>(FindObjectsSortMode.None);
        Require(sessions.Length == 1, "Exactly one escape session");
        var s = sessions[0];
        Require(s.player && s.enemy && s.battery && s.interaction && s.door && s.rules, "Runtime references");
        Require(UnityEngine.Object.FindObjectsByType<StealthSession>(FindObjectsSortMode.None).Length == 0, "No competing old session");
        Require(s.interaction.targets.Length == 3, "Three interactables");
        Require(s.interaction.targets.Count(t => t && t.kind == InteractionKind.Charger) == 2, "Two charging stations");
        Require(s.interaction.targets.Count(t => t && t.kind == InteractionKind.Terminal) == 1, "One terminal");
        Require(s.door.leftLeaf && s.door.rightLeaf && s.door.doorCollider && s.door.doorObstacle, "Door references");
        Require(s.door.leftLeaf.position.x < s.door.rightLeaf.position.x, "World-sorted door leaves");
        Require(Mathf.Abs((s.door.leftLeaf.position.x + s.door.rightLeaf.position.x) * .5f - 1) < .05f, "Door centered at x=1");
        Require(GameObject.Find("NorthWallWithClosedDoor") == null, "Old solid north-wall collider removed");
        var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        Require(surfaces.Length == 1 && surfaces[0].navMeshData, "One baked surface");
        Require(AssetDatabase.GetAssetPath(surfaces[0].navMeshData).StartsWith("Assets/EscapeSample/"), "Navigation must not overwrite old sample");
        foreach (var point in s.enemy.patrolPoints)
            Require(NavMesh.SamplePosition(point, out _, .3f, s.enemy.agent.areaMask), "Patrol point reachable: " + point);
        Require(NavMesh.SamplePosition(new Vector3(1, 0, 7), out _, .3f, s.enemy.agent.areaMask), "Exit platform has navigation");
        s.rules.Validate();
        Debug.Log("ESCAPE_SCENE_CONTRACT_PASS");
    }
    public static void Build()
    {
        Contract();
        var directory = Path.GetFullPath("../Builds/EscapeSample-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Require(!Directory.Exists(directory), "Build output must be new");
        Directory.CreateDirectory(directory);
        LastOutput = Path.Combine(directory, "LastCellOfPower.exe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = LastOutput,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(directory, "build-result.txt"),
            "Unity=" + Application.unityVersion + "\nScene=" + ScenePath + "\nResult=" + report.summary.result +
            "\nErrors=" + report.summary.totalErrors + "\nWarnings=" + report.summary.totalWarnings + "\nBytes=" + report.summary.totalSize + "\n");
        Require(report.summary.result == BuildResult.Succeeded, "Build result: " + report.summary.result);
        Debug.Log("ESCAPE_WINDOWS_BUILD_PASS " + LastOutput);
    }
}
