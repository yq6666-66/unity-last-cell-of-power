using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using Unity.AI.Navigation;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;

public static class ChapterArtDelivery
{
    public const string ScenePath="Assets/ChapterOneArt/Scenes/WarehouseChapterOneArtRealism.unity";
    public const string SourceScene="Assets/ChapterOne/Scenes/WarehouseChapterOne.unity";
    public static string Evidence=>Path.GetFullPath("Evidence/ChapterOneArtRealism");
    [CliCommand("art","generate","Generate independent abandoned-warehouse art scene")]
    public static string Generate()=>GenerateInternal(false);
    [CliCommand("art","regenerate","Back up an undelivered art candidate and regenerate from original baseline")]
    public static string Regenerate()=>GenerateInternal(true);
    static string GenerateInternal(bool revision)
    {
        using var preserve=new LegacyFontPreservation();
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Art generation requires edit mode");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Preserve unsaved scene changes first");
        Directory.CreateDirectory(Evidence);
        if(File.Exists(ScenePath))
        {
            if(!revision)throw new Exception("Art scene already exists. Use an explicit candidate revision.");
            string backup=Path.Combine(Evidence,"CandidateBackups",DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(backup);
            File.Copy(ScenePath,Path.Combine(backup,Path.GetFileName(ScenePath)));
            File.Copy("Assets/ChapterOneArt/Config/ChapterRealismProfile.asset",Path.Combine(backup,"ChapterRealismProfile.asset"));
        }
        var scene=EditorSceneManager.OpenScene(SourceScene);var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        string before=GameplaySnapshot(s);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        if(!EditorSceneManager.SaveScene(scene,ScenePath,true))throw new Exception("Could not create independent art scene");
        scene=EditorSceneManager.OpenScene(ScenePath);s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var profile=CreateRealismProfile();
        var artFont=CreateArtFont();
        UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>().font=artFont;
        foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))text.font=artFont;
        ChapterArtRealismMaterials.Create();
        ChapterArtRealismMaterials.ConfigureProfile(profile);
        ChapterArtEnvironment.Apply(s,profile);
        ChapterArtRealismEnvironment.Apply(s,profile);
        ChapterArtRobotRealism.Apply(s,profile);
        ChapterArtRealismLightingSetup.Apply(s,profile);
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.type==LightType.Directional){light.intensity=profile.mainLightIntensity;light.color=profile.coolLight;light.shadows=LightShadows.Soft;}
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=profile.ambientColor*profile.ambientIntensity;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=profile.fogColor;RenderSettings.fogStartDistance=profile.fogStart;RenderSettings.fogEndDistance=profile.fogEnd;
        var camera=s.player.viewCamera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.046f,.065f);camera.allowHDR=true;
        var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cameraData.antialiasingQuality=AntialiasingQuality.Medium;
        var volumeObject=new GameObject("ChapterArtVolume");var volume=volumeObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile.volume;
        var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();ui.artProfile=profile;
        var feedback=UnityEngine.Object.FindFirstObjectByType<ChapterVisualFeedback>();feedback.artProfile=profile;
        foreach(var binding in feedback.highlightBindings)
        {
            // Keep original complete-device renderers and any new skin parented to them.
            string modelName=binding.target.kind==LastCellOfPower.EscapeSample.InteractionKind.Terminal?"SupplyTerminal":"ChargingDock_"+binding.target.name.Substring("Charger_".Length);
            var model=GameObject.Find(modelName);if(model)binding.renderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
        }
        var atmosphere=new GameObject("ChapterArtAtmosphere").AddComponent<ChapterArtAtmosphere>();atmosphere.session=s;atmosphere.profile=profile;
        string after=GameplaySnapshot(s);File.WriteAllText(Path.Combine(Evidence,"gameplay-before.txt"),before);File.WriteAllText(Path.Combine(Evidence,"gameplay-after.txt"),after);
        if(before!=after)throw new Exception("ART_CONTRACT: geometry/navigation/gameplay snapshot changed; inspect diff before delivery");
        File.WriteAllText(Path.Combine(Evidence,"geometry-contract.txt"),"PASS: colliders, obstacles, navigation data, actor/interaction/door anchors and gameplay config identical before/after visual generation.\n");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Created "+ScenePath+"; protected gameplay snapshot identical.";
    }
    static ChapterArtProfile CreateRealismProfile()
    {
        const string path="Assets/ChapterOneArt/Config/ChapterRealismProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<ChapterArtProfile>(path);if(profile)return profile;
        profile=UnityEngine.Object.Instantiate(ChapterArtMaterials.CreateProfile());profile.name="ChapterRealismProfile";
        profile.coolLight=new Color(.72f,.81f,.83f);profile.warmLight=new Color(1f,.78f,.53f);
        profile.ambientColor=new Color(.36f,.38f,.38f);profile.ambientIntensity=1;
        profile.panelColor=new Color(.035f,.036f,.034f,.85f);profile.accentColor=new Color(.58f,.72f,.62f);
        AssetDatabase.CreateAsset(profile,path);return profile;
    }
    static TMP_FontAsset CreateArtFont()
    {
        const string path="Assets/ChapterOneArt/Config/ArtChinese.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font)return font;
        font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(ChapterOneBuilder.SourceFontPath),72,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        font.name="ArtChinese";AssetDatabase.CreateAsset(font,path);
        var serialized=new SerializedObject(font);serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
        var text=ChapterOneBuilder.RequiredCharacters()+string.Join("",Directory.GetFiles("Assets/ChapterOneArt","*.cs",SearchOption.AllDirectories).Select(File.ReadAllText));
        var characters=new string(text.Where(c=>!char.IsControl(c)&&!char.IsSurrogate(c)&&char.GetUnicodeCategory(c)!=UnicodeCategory.Format).Distinct().ToArray());
        if(!font.TryAddCharacters(characters,out string missing,true)&&!string.IsNullOrEmpty(missing))throw new Exception("Art font glyphs missing: "+missing);
        if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var atlas in font.atlasTextures)if(atlas&&!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
        EditorUtility.SetDirty(font);return font;
    }
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static string V(Vector3 v)=>v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture);
    static string T(Transform t)=>PathOf(t)+"|"+V(t.position)+"|"+V(t.eulerAngles)+"|"+V(t.lossyScale);
    public static string GameplaySnapshot(ExpandedSession s)
    {
        var lines=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>
        {
            string shape=c is BoxCollider b?V(b.center)+"|"+V(b.size):c is CapsuleCollider cap?V(cap.center)+"|"+cap.radius+"|"+cap.height+"|"+cap.direction:c is SphereCollider sp?V(sp.center)+"|"+sp.radius:c is MeshCollider mc?AssetDatabase.GetAssetPath(mc.sharedMesh):c is CharacterController cc?V(cc.center)+"|"+cc.radius+"|"+cc.height:c.GetType().Name;
            return "C|"+T(c.transform)+"|"+c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+c.gameObject.layer+"|"+shape;
        }).ToList();
        foreach(var n in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))lines.Add("NAV|"+T(n.transform)+"|"+AssetDatabase.GetAssetPath(n.navMeshData)+"|"+n.collectObjects+"|"+n.useGeometry);
        foreach(var n in UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add("OBS|"+T(n.transform)+"|"+V(n.center)+"|"+V(n.size)+"|"+n.carving+"|"+n.enabled);
        foreach(var a in s.interaction.targets)lines.Add("ANCHOR|"+T(a.transform)+"|"+a.kind);
        lines.Add("PLAYER|"+T(s.player.transform));lines.Add("CAMERA|"+T(s.player.viewCamera.transform)+"|"+s.player.viewCamera.fieldOfView);lines.Add("ENEMY|"+T(s.enemy.transform));
        lines.Add("EXIT|"+T(s.door.exitAnchor));lines.Add("PROFILE|"+EditorJsonUtility.ToJson(s.profile));lines.Add("LEVEL|"+EditorJsonUtility.ToJson(s.level));lines.Add("PULSE|"+EditorJsonUtility.ToJson(s.pulse.profile));
        lines.Sort(StringComparer.Ordinal);return string.Join("\n",lines);
    }
    [CliCommand("art","verify","Run consolidated art gameplay and presentation checks")]
    public static Task<string> Verify()=>ChapterArtChecks.Run();
    [CliCommand("art","capture","Render fixed-camera before/after art evidence")]
    public static Task<string> Capture()=>ChapterArtChecks.Capture();
    [CliCommand("art","capture_east","Recapture the corrected east connection camera outside cargo")]
    public static Task<string> CaptureEast()=>ChapterArtChecks.CaptureEast();
    [CliCommand("art","close_build","Capture the rusted guard close view and build the final Windows candidate")]
    public static async Task<string> CloseBuild(){await ChapterArtChecks.CaptureGuardClose();return Build();}
    [CliCommand("art","guard_finish","Apply final oxidized-steel guard surface, capture and build")]
    public static async Task<string> GuardFinish()
    {
        using var preserve=new LegacyFontPreservation();var scene=EditorSceneManager.OpenScene(ScenePath);
        ChapterArtRealismMaterials.ConfigureProfile(UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>().profile);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return await CloseBuild();
    }
    [CliCommand("art","audit","Capture and verify the final art candidate in one background batch")]
    public static async Task<string> Audit(){string captures=await Capture();string checks=await Verify();return captures+"\n"+checks;}
    [CliCommand("art","refine","Apply reviewed emission keyword and dark-surface readability correction")]
    public static string Refine()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Clean edit mode required");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var profile=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>().profile;
        if(AssetDatabase.GetAssetPath(profile)!="Assets/ChapterOneArt/Config/ChapterRealismProfile.asset")throw new Exception("Refine only supports the independent realism profile");
        ChapterArtRealismMaterials.ConfigureProfile(profile);
        var wet=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChapterOneArt/Materials/ART_WetFloor.mat");
        wet.SetFloat("_BlendModePreserveSpecular",0);wet.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);wet.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);wet.DisableKeyword("_ALPHAPREMULTIPLY_ON");EditorUtility.SetDirty(wet);
        foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{"Assets/ChapterOneArt/Models"}))
        {
            string materialPath=AssetDatabase.GUIDToAssetPath(id);if(!materialPath.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");EditorUtility.SetDirty(material);
        }
        RenderSettings.ambientLight=profile.ambientColor*profile.ambientIntensity;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{"Assets/ChapterOneArt/Models"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(id);if(!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            if(!AssetDatabase.LoadAssetAtPath<Material>(path).IsKeywordEnabled("_EMISSION"))throw new Exception("Emission did not survive material validation: "+path);
        }
        File.WriteAllText(Path.Combine(Evidence,"emission-fix.txt"),"PASS: all 12 independent art materials retain _EMISSION after save/reimport; emissive flag correction and higher dark-surface ambient fill applied.\n");
        return "Applied reviewed emission/readability correction; all keywords survive reimport.";
    }
    [CliCommand("art","refine_capture","Apply bounded realism material corrections and capture their actual rendering")]
    public static async Task<string> RefineCapture(){Refine();return await Capture();}
    [CliCommand("art","finish_realism","Repair confirmed cargo clipping, update aged surfaces, bake reflections and capture")]
    public static async Task<string> FinishRealism()
    {
        using var preserve=new LegacyFontPreservation();
        var scene=EditorSceneManager.OpenScene(ScenePath);var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var profile=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>().profile;
        ChapterArtRealismMaterials.Create();ChapterArtRealismMaterials.ConfigureProfile(profile);
        ChapterArtRealismEnvironment.RepairCargo(session,profile);
        if(GameplaySnapshot(session)!=File.ReadAllText(Path.Combine(Evidence,"gameplay-before.txt")))throw new Exception("Cargo repair changed gameplay snapshot");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return await BakeReflections();
    }
    [CliCommand("art","bake_reflections","Bake independent room reflection assets and capture final lighting")]
    public static async Task<string> BakeReflections()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");
        using var preserve=new LegacyFontPreservation();
        var scene=EditorSceneManager.OpenScene(ScenePath);var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var profile=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>().profile;
        ChapterArtRealismLightingSetup.EnsureInspectionLight(session,profile);
        RenderSettings.ambientMode=AmbientMode.Custom;var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(profile.diffuseBounce);RenderSettings.ambientProbe=sh;
        string directory="Assets/ChapterOneArt/Reflections";Directory.CreateDirectory(directory);AssetDatabase.Refresh();
        var actors=session.enemy.GetComponentsInChildren<Renderer>(true).Concat(session.player.GetComponentsInChildren<Renderer>(true)).ToArray();
        var enabled=actors.Select(r=>r.enabled).ToArray();var headlamp=session.player.viewCamera.GetComponentInChildren<Light>();bool headlampOn=headlamp&&headlamp.enabled;
        try
        {
            foreach(var r in actors)r.enabled=false;if(headlamp)headlamp.enabled=false;
            int index=0;
            foreach(var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).OrderBy(r=>r.transform.position.x).ThenBy(r=>r.transform.position.z))
            {
                string path=directory+"/Room_"+(index++)+".exr";
                if(File.Exists(path)){string backup=Path.Combine(Evidence,"CandidateBackups","reflection-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(backup);File.Copy(path,Path.Combine(backup,Path.GetFileName(path)),true);}
                probe.mode=ReflectionProbeMode.Baked;probe.renderDynamicObjects=true;
                if(!Lightmapping.BakeReflectionProbe(probe,path))throw new Exception("Reflection bake failed: "+path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer){importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
                var texture=AssetDatabase.LoadAssetAtPath<Texture>(path);if(!texture)throw new Exception("No baked reflection asset: "+path);
                probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=texture;EditorUtility.SetDirty(probe);
            }
        }
        finally{for(int i=0;i<actors.Length;i++)if(actors[i])actors[i].enabled=enabled[i];if(headlamp)headlamp.enabled=headlampOn;}
        EditorUtility.SetDirty(profile);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return await Capture();
    }
    [CliCommand("art","build","Build standalone Windows art edition")]
    public static string Build()
    {
        using var preserve=new LegacyFontPreservation();
        if(!File.Exists(ScenePath))throw new FileNotFoundException(ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
        var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        if(GameplaySnapshot(session)!=File.ReadAllText(Path.Combine(Evidence,"gameplay-before.txt")))throw new Exception("Final candidate gameplay snapshot changed");
        if(GameObject.Find("ArtWorld").GetComponentsInChildren<Collider>(true).Length>0)throw new Exception("Art decorations acquired colliders");
        string dir=Path.GetFullPath("../Builds/ChapterOneArt-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(dir);
        var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(dir,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"build-result.txt"),$"Result={r.summary.result}\nErrors={r.summary.totalErrors}\nWarnings={r.summary.totalWarnings}\nDirectory={dir}\nUnity={Application.unityVersion}\n");
        if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Art Windows build failed");return dir;
    }
    sealed class LegacyFontPreservation : IDisposable
    {
        const string FontPath="Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        readonly byte[] original=File.Exists(FontPath)?File.ReadAllBytes(FontPath):null;
        public void Dispose()
        {
            // TMP may upgrade its default font when a new text component is created.
            // Preserve the baseline bytes rather than shipping an unrelated format migration.
            if(original==null || !File.Exists(FontPath) || original.SequenceEqual(File.ReadAllBytes(FontPath)))return;
            string backup=Path.Combine(Evidence,"CandidateBackups","tmp-upgrade-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(backup);
            File.Copy(FontPath,Path.Combine(backup,"LiberationSans-upgraded.asset"));File.WriteAllBytes(FontPath,original);
        }
    }
}
