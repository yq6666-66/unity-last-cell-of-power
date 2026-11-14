using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using Unity.Pipeline.Commands;
using LastCellOfPower.WarehouseExpanded;

public static class ExpandedDelivery
{
    public const string ScenePath = "Assets/WarehouseExpanded/Scenes/WarehouseExpanded.unity";
    public static string Evidence => Path.GetFullPath("Evidence/WarehouseExpanded");
    static void Require(bool value,string message) { if(!value) throw new Exception("EXPANDED_CONTRACT: "+message); }
    static float PathLength(Vector3 from,Vector3 to)
    {
        Require(NavMesh.SamplePosition(from,out var a,.5f,NavMesh.AllAreas),"Off navigation: "+from);
        Require(NavMesh.SamplePosition(to,out var b,.5f,NavMesh.AllAreas),"Off navigation: "+to);
        var path=new NavMeshPath();
        Require(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Unreachable: "+from+" -> "+to);
        float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);return length;
    }
    [CliCommand("warehouse","generate","Generate the independent expanded warehouse")]
    public static string Generate()
    {
        ExpandedWarehouseBuilder.Create(); return Inspect();
    }
    [CliCommand("warehouse","inspect","Validate expanded warehouse topology and resource budget")]
    public static string Inspect()
    {
        Require(!EditorApplication.isPlaying,"Edit mode required");
        Require(EditorSceneManager.GetActiveScene().path==ScenePath,"Open expanded scene");
        var sessions=UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None);
        Require(sessions.Length==1,"Exactly one session");var s=sessions[0];
        Require(s.level && s.profile && s.player && s.enemy && s.battery && s.interaction && s.door,"References missing");
        Require(s.interaction.targets.Length==4,"Three chargers and one terminal");
        Require(s.enemy.level==s.level && s.enemy.profile==s.profile,"Guard configuration is shared");
        PathLength(s.level.playerSpawn,new Vector3(-7,0,-1.8f));
        // Test all four direct links, not merely global reachability around another route.
        var starts=new[]{new Vector3(-1.8f,0,-7),new Vector3(-1.8f,0,7),new Vector3(-7,0,-1.8f),new Vector3(7,0,-1.8f)};
        var ends=new[]{new Vector3(1.8f,0,-7),new Vector3(1.8f,0,7),new Vector3(-7,0,1.8f),new Vector3(7,0,1.8f)};
        for(int i=0;i<starts.Length;i++)Require(PathLength(starts[i],ends[i])<5,"Portal is blocked; path detours around the ring: "+i);
        foreach(var stop in s.level.patrolStops)
        {
            Require(!(stop.position.x<0 && stop.position.z<0),"Patrol stop in starting quadrant");
            Require(Vector3.Distance(stop.position,s.level.terminal)>=4,"Patrol stop camps terminal");
            PathLength(s.level.enemySpawn,stop.position);
        }
        foreach(var target in s.interaction.targets)
        {
            Require(target && target.anchor,"Interaction anchor missing");
            Require(NavMesh.SamplePosition(target.Position,out _,1.8f,NavMesh.AllAreas),"No approach near interactable "+target.label);
        }
        // Separate prescribed portal sequences demonstrate the intended short and long routes.
        Vector3 terminalApproach=s.level.terminal+Vector3.back*1.5f;
        float shortRoute=PathLength(s.level.playerSpawn,new Vector3(-7,0,1.8f))+PathLength(new Vector3(-7,0,1.8f),terminalApproach);
        var longNodes=new[]{s.level.playerSpawn,new Vector3(1.8f,0,-7),new Vector3(7,0,1.8f),new Vector3(-1.8f,0,7),terminalApproach};
        float longRoute=0;for(int i=1;i<longNodes.Length;i++)longRoute+=PathLength(longNodes[i-1],longNodes[i]);
        float fullRouteBudget=(s.profile.idleDrain+s.profile.movementDrain)*longRoute/s.profile.playerSpeed+s.profile.supplyCost+s.profile.idleDrain*s.profile.supplySeconds;
        Require(fullRouteBudget<80,"Long route plus supply consumes excessive full-charge budget");
        Directory.CreateDirectory(Evidence);
        string report=$"PASS four direct inter-zone links, patrol reachability/exclusion, interaction approaches.\nShort route={shortRoute:F2} m; long route={longRoute:F2} m; long-route travel+terminal budget={fullRouteBudget:F2}.\nThis static navigation check is not a normal-input playthrough.\n";
        File.WriteAllText(Path.Combine(Evidence,"topology.txt"),report);Debug.Log("EXPANDED_TOPOLOGY_PASS");return report;
    }
    [CliCommand("warehouse","build","Build an independent Windows development package")]
    public static string Build()
    {
        if(EditorSceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        Inspect();
        string output=Path.GetFullPath("../Builds/WarehouseExpanded-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Require(!Directory.Exists(output),"Refusing to overwrite build");Directory.CreateDirectory(output);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(output,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Path.Combine(output,"build-result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nUnity={Application.unityVersion}\n");
        Require(report.summary.result==BuildResult.Succeeded,"Build failed");return output;
    }
}
