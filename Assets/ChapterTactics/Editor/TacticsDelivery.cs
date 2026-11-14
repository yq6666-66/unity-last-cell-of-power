using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

public static class TacticsDelivery
{
    public const string SourceScene="Assets/ChapterOneArt/Scenes/WarehouseChapterOneArtRealism.unity";
    public const string ScenePath="Assets/ChapterTactics/Scenes/WarehouseTactics.unity";
    public static string Evidence=>Path.GetFullPath("Evidence/ChapterTactics");
    const string Config="Assets/ChapterTactics/Config";
    const string NavPath="Assets/ChapterTactics/Navigation/WarehouseTactics.asset";
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("TACTICS_CONTRACT: "+message);}

    [CliCommand("tactics","generate","Create independent two-guard environmental-tactics scene")]
    public static string Generate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene before generation");
        Require(!File.Exists(ScenePath)&&!Directory.Exists(Config),"Tactics candidate already exists; preserve and repair explicitly");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));Directory.CreateDirectory(Config);Directory.CreateDirectory(Path.GetDirectoryName(NavPath));Directory.CreateDirectory(Evidence);
        var scene=EditorSceneManager.OpenScene(SourceScene);
        Require(EditorSceneManager.SaveScene(scene,ScenePath,true),"Could not copy source scene");
        scene=EditorSceneManager.OpenScene(ScenePath);
        var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();Require(session && session.enemy,"Source chapter session");
        var original=session.enemy;
        var profile=UnityEngine.Object.Instantiate(session.profile);profile.name="TacticsGameplay";AssetDatabase.CreateAsset(profile,Config+"/Gameplay.asset");
        var level=UnityEngine.Object.Instantiate(session.level);level.name="TacticsLevel";AssetDatabase.CreateAsset(level,Config+"/Level.asset");
        var pulse=UnityEngine.Object.Instantiate(session.pulse.profile);pulse.name="TacticsPulse";AssetDatabase.CreateAsset(pulse,Config+"/Pulse.asset");
        session.profile=profile;session.level=level;session.pulse.profile=pulse;
        var font=CopyFont(UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font);
        UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font=font;
        foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))text.font=font;
        original.agent.enabled=false;
        var secondObject=UnityEngine.Object.Instantiate(original.gameObject);secondObject.name="Guard_02";
        var second=secondObject.GetComponent<ExpandedEnemy>();Require(second,"Cloned guard runtime");
        original.name="Guard_01";
        original.guardId="01";second.guardId="02";
        original.routeOverride=Route(new[]{new Vector3(7,0,7),new Vector3(3,0,7),new Vector3(-3,0,7),new Vector3(-7,0,3),new Vector3(-3,0,7),new Vector3(3,0,7),new Vector3(7,0,3),new Vector3(7,0,1.5f)});
        second.routeOverride=Route(new[]{new Vector3(10,0,-4),new Vector3(10,0,-7),new Vector3(6,0,-10),new Vector3(3,0,-7),new Vector3(7,0,-1.5f)});
        original.transform.SetPositionAndRotation(new Vector3(7,0,7),Quaternion.identity);
        second.transform.SetPositionAndRotation(new Vector3(10,0,-4),Quaternion.identity);
        level.enemySpawn=original.transform.position;level.patrolStops=original.routeOverride;
        session.enemies=new[]{original,second};session.enemy=original;
        foreach(var guard in session.enemies)
        {
            guard.profile=profile;guard.level=level;
            guard.perception.observer=guard.transform;guard.perception.target=session.player.transform;
            foreach(var visual in guard.GetComponentsInChildren<GuardVisualState>(true))visual.enemy=guard;
            foreach(var light in guard.GetComponentsInChildren<Light>(true))light.shadows=LightShadows.None;
            NumberPlate(guard,font);
        }
        var tacticsObject=new GameObject("TacticsDevices");
        var tactics=tacticsObject.AddComponent<TacticsDevices>();tactics.session=session;
        tactics.profile=ScriptableObject.CreateInstance<TacticsProfile>();tactics.profile.name="TacticsDevicesProfile";AssetDatabase.CreateAsset(tactics.profile,Config+"/Devices.asset");
        var allTargets=session.interaction.targets.ToList();
        var n1=Noise(tacticsObject.transform,"Noise_NW",new Vector3(-5.2f,.9f,.65f),new Vector3(-10,.7f,2.5f),new Vector3(-10,0,2),1,font);
        var n2=Noise(tacticsObject.transform,"Noise_SE",new Vector3(10.6f,.9f,-.65f),new Vector3(6,.7f,-10.5f),new Vector3(6,0,-10),-1,font);
        tactics.noiseDevices=new[]{n1,n2};allTargets.Add(n1.interactable);allTargets.Add(n2.interactable);
        var shutterObject=new GameObject("TacticalShutter");shutterObject.transform.SetParent(tacticsObject.transform,false);shutterObject.transform.position=new Vector3(7,0,0);
        var shutter=shutterObject.AddComponent<TacticalShutter>();shutter.deviceId="SHUTTER_EAST";
        shutter.leaf=Visual(shutterObject.transform,"ShutterLeaf",new Vector3(7,3.8f,0),new Vector3(3.2f,2.4f,.14f),"MAT_Steel").transform;
        shutter.openOffset=new Vector3(0,2.6f,0);
        var barrierObject=new GameObject("TacticsShutterBarrier");barrierObject.transform.SetParent(shutterObject.transform,false);barrierObject.transform.localPosition=new Vector3(0,1.2f,0);
        var barrier=barrierObject.AddComponent<BoxCollider>();barrier.size=new Vector3(3.2f,2.4f,.18f);barrier.enabled=false;shutter.barrier=barrier;
        var obstacle=barrierObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(3.2f,2.4f,.24f);obstacle.carving=true;obstacle.carveOnlyStationary=false;obstacle.enabled=false;shutter.obstacle=obstacle;
        var safety=new GameObject("ShutterSafetyVolume").transform;safety.SetParent(shutterObject.transform,false);safety.localPosition=new Vector3(0,1.2f,0);shutter.safetyVolume=safety;shutter.safetySize=new Vector3(3.2f,2.4f,.8f);
        Visual(shutterObject.transform,"ShutterHeader",new Vector3(7,2.65f,0),new Vector3(3.6f,.25f,.32f),"MAT_ServiceYellow");
        foreach(float x in new[]{5.3f,8.7f})Visual(shutterObject.transform,"ShutterRail",new Vector3(x,1.2f,0),new Vector3(.2f,2.4f,.3f),"MAT_Graphite");
        var south=Control(shutterObject.transform,"ShutterSwitchSouth",InteractionKind.Shutter,new Vector3(5.05f,.9f,-.65f),-1,"临时封门",font);
        var north=Control(shutterObject.transform,"ShutterSwitchNorth",InteractionKind.Shutter,new Vector3(5.05f,.9f,.65f),1,"临时封门",font);
        shutter.switches=new[]{south,north};allTargets.AddRange(shutter.switches);tactics.shutter=shutter;
        session.tactics=tactics;session.interaction.tactics=tactics;session.interaction.targets=allTargets.ToArray();
        var feedback=UnityEngine.Object.FindFirstObjectByType<ChapterVisualFeedback>();
        if(feedback)
        {
            var bindings=feedback.highlightBindings.ToList();
            foreach(var target in new[]{n1.interactable,n2.interactable,south,north})bindings.Add(new ChapterVisualFeedback.HighlightBinding{target=target,renderers=target.GetComponentsInChildren<Renderer>(true)});
            feedback.highlightBindings=bindings.ToArray();
        }
        var surface=UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();Require(surface && surface.navMeshData,"Source navigation");
        var nav=UnityEngine.Object.Instantiate(surface.navMeshData);nav.name="WarehouseTactics";surface.RemoveData();surface.navMeshData=nav;AssetDatabase.CreateAsset(nav,NavPath);surface.AddData();
        Physics.SyncTransforms();
        foreach(var guard in session.enemies)
        {
            foreach(var stop in guard.PatrolRoute)Require(NavMesh.SamplePosition(stop.position,out _, .6f,guard.agent.areaMask),"Patrol point not on navigation: "+guard.guardId+" "+stop.position);
            guard.agent.enabled=true;
        }
        EditorUtility.SetDirty(level);EditorUtility.SetDirty(surface);
        WallSurfaceRepair.Apply(scene,"Assets/ChapterTactics/WallSurfaceMeshes");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return Inspect();
    }

    static TMP_FontAsset CopyFont(TMP_FontAsset source)
    {
        Require(source,"Source chapter font");string path=Config+"/TacticsChinese.asset";
        Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path),"Independent font copy");
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);Require(font,"Copied font");return font;
    }
    static ExpandedLevelDefinition.PatrolStop[] Route(Vector3[] positions)
    {
        var result=new ExpandedLevelDefinition.PatrolStop[positions.Length];
        for(int i=0;i<positions.Length;i++){Vector3 delta=positions[(i+1)%positions.Length]-positions[i];result[i]=new ExpandedLevelDefinition.PatrolStop{position=positions[i],waitSeconds=.5f,lookYaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg};}
        return result;
    }
    static Material Mat(string name){var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage4/Materials/"+name+".mat");Require(m,"Material "+name);return m;}
    static GameObject Visual(Transform parent,string name,Vector3 position,Vector3 size,string material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=Mat(material);return go;
    }
    static void NumberPlate(ExpandedEnemy guard,TMP_FontAsset font)
    {
        var plate=Visual(guard.transform,"GuardNumberPlate",guard.transform.position+new Vector3(0,.98f,.55f),new Vector3(.34f,.18f,.018f),"MAT_Graphite");
        var label=new GameObject("GuardNumber_"+guard.guardId);label.SetActive(false);label.transform.SetParent(guard.transform,false);label.transform.localPosition=new Vector3(0,.98f,.565f);label.transform.localRotation=Quaternion.Euler(0,180,0);label.layer=guard.gameObject.layer;
        var text=label.AddComponent<TextMeshPro>();text.font=font;text.text=guard.guardId;text.fontSize=1.6f;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.9f,.82f,.55f);text.rectTransform.sizeDelta=new Vector2(.32f,.16f);text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        label.SetActive(true);plate.layer=guard.gameObject.layer;
    }
    static EscapeInteractable Control(Transform parent,string name,InteractionKind kind,Vector3 anchor,int face,string label,TMP_FontAsset font)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=anchor;
        var target=go.AddComponent<EscapeInteractable>();target.kind=kind;target.anchor=go.transform;target.label=label;target.ignoredColliders=Array.Empty<Collider>();
        Visual(go.transform,name+"Body",anchor-Vector3.forward*(face*.08f),new Vector3(.38f,.55f,.13f),"MAT_Graphite");
        Visual(go.transform,name+"Button",anchor,new Vector3(.16f,.16f,.035f),"MAT_PowerMint");
        return target;
    }
    static NoiseDevice Noise(Transform parent,string id,Vector3 control,Vector3 source,Vector3 approach,int face,TMP_FontAsset font)
    {
        var go=new GameObject(id);go.transform.SetParent(parent,false);var noise=go.AddComponent<NoiseDevice>();noise.deviceId=id;
        noise.interactable=Control(go.transform,id+"Control",InteractionKind.Noise,control,face,"启动诱导扬声器",font);
        noise.soundAnchor=new GameObject(id+"SpeakerAnchor").transform;noise.soundAnchor.SetParent(go.transform,false);noise.soundAnchor.position=source;
        noise.investigationAnchor=new GameObject(id+"Approach").transform;noise.investigationAnchor.SetParent(go.transform,false);noise.investigationAnchor.position=approach;
        Visual(go.transform,id+"SpeakerBody",source,new Vector3(.5f,.6f,.25f),"MAT_Steel");
        Visual(go.transform,id+"SpeakerGrille",source+Vector3.forward*.14f,new Vector3(.38f,.4f,.035f),"MAT_Graphite");return noise;
    }

    [CliCommand("tactics","inspect","Check tactics references, routes, device geometry and independent assets")]
    public static string Inspect()
    {
        Require(!EditorApplication.isPlaying,"Edit mode required");
        if(EditorSceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();Require(s && s.Guards.Count==2 && s.tactics,"Two guards and tactics manager");
        Require(s.Guards.Select(g=>g.guardId).OrderBy(x=>x).SequenceEqual(new[]{"01","02"}),"Guard ids");
        Require(s.interaction.targets.Count(t=>t.kind==InteractionKind.Noise)==2 && s.interaction.targets.Count(t=>t.kind==InteractionKind.Shutter)==2,"Device aliases integrated");
        Require(s.tactics.noiseDevices.Length==2 && s.tactics.shutter.switches.Length==2,"Two one-use noise devices and one shared shutter");
        foreach(var g in s.Guards)
        {
            Require(g.perception.observer==g.transform && g.perception.target==s.player.transform,"Per-guard perception ownership");
            Require(g.GetComponentsInChildren<GuardVisualState>(true).All(v=>v.enemy==g),"Per-guard visual ownership");
            Require(g.PatrolRoute.Length==(g.guardId=="01"?8:5),"Configured route count");
            foreach(var p in g.PatrolRoute)Require(NavMesh.SamplePosition(p.position,out _, .6f,g.agent.areaMask),"Patrol reachability");
        }
        Require(AssetDatabase.GetAssetPath(s.profile).StartsWith(Config) && AssetDatabase.GetAssetPath(s.level).StartsWith(Config),"Independent gameplay config");
        Require(AssetDatabase.GetAssetPath(UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>().navMeshData)==NavPath,"Independent navigation");
        Require(s.tactics.shutter.safetySize.x>=3.2f && !s.tactics.shutter.barrier.enabled && !s.tactics.shutter.obstacle.enabled,"Shutter starts open with full safety volume");
        Directory.CreateDirectory(Evidence);string result="PASS two guards, independent routes/perception/scanner, two noise sources, one shared two-switch shutter, independent configs and nav. Edit-mode contract only.\n";
        File.WriteAllText(Path.Combine(Evidence,"contract.txt"),result);return result;
    }
    [CliCommand("tactics","verify","Run consolidated tactics PlayMode fixtures")]
    public static Task<string> Verify()=>TacticsBatchChecks.Run();
    [CliCommand("tactics","routes","Run three bounded background input routes")]
    public static Task<string> Routes()=>TacticsBatchChecks.Routes();
    [CliCommand("tactics","route_plain","Run only the route without environmental devices")]
    public static Task<string> RoutePlain()=>TacticsBatchChecks.Route(TacticsRouteMode.Plain);
    [CliCommand("tactics","route_noise","Run only the noise-assisted terminal route")]
    public static Task<string> RouteNoise()=>TacticsBatchChecks.Route(TacticsRouteMode.Noise);
    [CliCommand("tactics","route_shutter","Run only the shutter escape route")]
    public static Task<string> RouteShutter()=>TacticsBatchChecks.Route(TacticsRouteMode.Shutter);
    [CliCommand("tactics","build","Build the independent Windows tactics candidate")]
    public static string Build()
    {
        Inspect();string directory=Path.GetFullPath("../Builds/ChapterTactics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Require(!Directory.Exists(directory),"New output directory required");Directory.CreateDirectory(directory);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(directory,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Path.Combine(directory,"build-result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nUnity={Application.unityVersion}\n");Require(report.summary.result==BuildResult.Succeeded,"Windows build");return directory;
    }
}
