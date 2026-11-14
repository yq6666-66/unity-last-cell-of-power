using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.ChapterTacticsPolish;
using LastCellOfPower.WarehouseExpanded;

public static class PolishDelivery
{
    public const string SourceScene="Assets/WallSurfaceFix/Scenes/WarehouseTacticsWallFix.unity";
    public const string ScenePath="Assets/ChapterTacticsPolish/Scenes/WarehouseTacticsPolish.unity";
    public const string Root="Assets/ChapterTacticsPolish";
    public const string Config=Root+"/Config";
    public const string ReadabilityRoot="TacticsPolishReadability";
    public const string ReferenceCommit="8693e66d50f40b70f95fec3f77f3a0322ef08eb3";
    public static string Evidence=>Path.GetFullPath("Evidence/ChapterTacticsPolish");
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("POLISH_CONTRACT: "+message);}

    [CliCommand("polish","generate","Create independent tactics feedback scene without changing layout or gameplay")]
    public static string Generate()
    {
        CleanEditMode();Directory.CreateDirectory(Evidence);
        // A repeated invocation inspects an existing complete candidate; it never overwrites it.
        if(File.Exists(ScenePath))return Inspect();
        Require(!Directory.Exists(Config),"Partial candidate config exists; preserve it before an explicit retry");
        using var preserve=new LegacyFontPreservation();
        string[] sourceFiles=DependencyFiles(SourceScene);string beforeAssets=Hashes(sourceFiles);
        var source=EditorSceneManager.OpenScene(SourceScene);
        string before=ProtectedSnapshot(source),beforeMeshes=OriginalMeshes(source);
        File.WriteAllText(Path.Combine(Evidence,"protected-before.txt"),before);
        File.WriteAllText(Path.Combine(Evidence,"source-assets-before.sha256"),beforeAssets);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));Directory.CreateDirectory(Config);AssetDatabase.Refresh();
        Require(EditorSceneManager.SaveScene(source,ScenePath,true),"Independent scene copy failed");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var session=One<ExpandedSession>(scene);var ui=One<ChapterPlayerUI>(scene);
        Require(session.Guards.Count==2 && session.tactics && session.tactics.noiseDevices.Length==2,"Accepted two-guard/device baseline required");
        var profile=ScriptableObject.CreateInstance<TacticalReadabilityProfile>();profile.name="TacticsReadability";
        AssetDatabase.CreateAsset(profile,Config+"/Readability.asset");
        var font=CreateFont(ui.font,profile);
        var addition=new GameObject(ReadabilityRoot).transform;
        var presenter=addition.gameObject.AddComponent<PolishFeedbackPresenter>();presenter.session=session;presenter.profile=profile;
        ui.polishProfile=profile;ui.polishFeedback=presenter;ui.font=font;
        session.tactics.diagnosticsEnabled=true;
        var plateMaterial=NewMaterial("ReadabilityPlate",new Color(.055f,.065f,.065f),false);
        var indicatorMaterial=NewMaterial("ReadabilityIndicator",new Color(.22f,.24f,.24f),true);
        foreach(var device in session.tactics.noiseDevices)
        {
            bool first=device.deviceId=="Noise_NW";Require(first || device.deviceId=="Noise_SE","Unknown baseline noise id");
            var label=profile.LabelFor(device.deviceId);string shortId=label.shortId,destination=label.destination;
            Require(shortId==(first?"N01":"N02") && destination==(first?"设备区南侧扬声器":"货架区南侧扬声器"),"Readability profile labels do not match approved destination names");
            Vector3 controlFront=first?Vector3.forward:Vector3.back;
            AddSign(addition,shortId+"_Control",device.interactable.anchor.position+Vector3.up*.27f+controlFront*.025f,controlFront,shortId,CompactDestination(destination),device,null,profile,font,plateMaterial,indicatorMaterial);
            Vector3 speakerFront=device.investigationAnchor.position-device.soundAnchor.position;speakerFront.y=0;speakerFront.Normalize();
            Require(speakerFront.sqrMagnitude>.5f,"Speaker has no readable approach direction");
            AddSign(addition,shortId+"_Speaker",device.soundAnchor.position+Vector3.up*.50f+speakerFront*.025f,speakerFront,shortId,CompactDestination(destination),device,null,profile,font,plateMaterial,indicatorMaterial);
        }
        var shutter=session.tactics.shutter;Require(shutter && shutter.switches.Length==2,"Two shutter switches required");
        foreach(var button in shutter.switches)
        {
            Vector3 front=button.anchor.position.z>=shutter.transform.position.z?Vector3.forward:Vector3.back;
            AddSign(addition,front.z>0?"S01_North":"S01_South",button.anchor.position+Vector3.up*.27f+front*.025f,front,profile.shutterShortId,profile.shutterDestination+"\n隔断门",null,shutter,profile,font,plateMaterial,indicatorMaterial);
        }
        foreach(var text in addition.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true,true);
        Physics.SyncTransforms();
        string geometry=PolishSignGeometry.Check(scene,addition);
        File.WriteAllText(Path.Combine(Evidence,"sign-geometry.txt"),geometry);
        string after=ProtectedSnapshot(scene);
        File.WriteAllText(Path.Combine(Evidence,"protected-after.txt"),after);
        Require(before==after,"Original protected scene/config/render state changed");
        Require(beforeMeshes==OriginalMeshes(scene),"Accepted repaired mesh references changed");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        preserve.RestoreIfNeeded();
        string afterAssets=Hashes(sourceFiles);File.WriteAllText(Path.Combine(Evidence,"source-assets-after.sha256"),afterAssets);
        Require(beforeAssets==afterAssets,"Source scene/dependency bytes changed during generation");
        WriteReferenceNotes();
        return Inspect();
    }

    [CliCommand("polish","repair_indicator","Preserve URP emission variant on the independent indicator material and inspect")]
    public static string RepairIndicator()
    {
        CleanEditMode();using var preserve=new LegacyFontPreservation();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Config+"/ReadabilityIndicator.mat");Require(material,"Indicator material missing");
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;material.EnableKeyword("_EMISSION");EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        AssetDatabase.ImportAsset(Config+"/ReadabilityIndicator.mat",ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        Require(material.IsKeywordEnabled("_EMISSION"),"Emission variant did not survive reimport");return Inspect();
    }
    [Serializable] sealed class SignMetric
    {
        public string sign,label,text;public float fontSize,fontSizeMin,fontSizeMax,rectWidth,rectHeight,preferredWidth,preferredHeight,glyphWidth,glyphHeight;public bool overflow;public int lineCount;
    }
    [Serializable] sealed class SignMetrics { public string stage;public SignMetric[] labels; }
    [CliCommand("polish","repair_signs","Apply compact world-sign layout and keep detailed state in HUD, then inspect")]
    public static string RepairSigns()
    {
        CleanEditMode();using var preserve=new LegacyFontPreservation();
        Require(File.Exists(ScenePath),"Existing candidate required");Directory.CreateDirectory(Evidence);
        string backup=Path.Combine(Evidence,"CandidateBackups","sign-layout-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(backup);
        File.Copy(ScenePath,Path.Combine(backup,Path.GetFileName(ScenePath)));File.Copy(ScenePath+".meta",Path.Combine(backup,Path.GetFileName(ScenePath)+".meta"));
        byte[] sourceBytes=File.ReadAllBytes(SourceScene);var scene=EditorSceneManager.OpenScene(ScenePath);
        string before=ProtectedSnapshot(scene),beforeMeshes=OriginalMeshes(scene);
        var root=scene.GetRootGameObjects().Single(go=>go.name==ReadabilityRoot).transform;
        WriteSignMetrics(root,"before",Path.Combine(backup,"sign-metrics-before.json"));
        foreach(Transform sign in root)if(sign.Find("Plate")){RepositionSign(sign);ApplySignLayout(sign);}
        WriteSignMetrics(root,"after",Path.Combine(Evidence,"sign-metrics-after.json"));
        Require(before==ProtectedSnapshot(scene) && beforeMeshes==OriginalMeshes(scene),"Sign repair changed original protected objects or meshes");
        File.WriteAllText(Path.Combine(Evidence,"sign-geometry.txt"),PolishSignGeometry.Check(scene,root));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();preserve.RestoreIfNeeded();
        Require(sourceBytes.SequenceEqual(File.ReadAllBytes(SourceScene)),"Source wall-fix scene changed during sign repair");
        File.WriteAllText(Path.Combine(Evidence,"sign-layout-repair.txt"),"Compact world signs 0.62x0.34 m: full paired ID/destination retained, destination wraps onto two short lines, short device state and emission lamp remain. Detailed response/countdown/use information belongs to real HUD. Control signs centered 0.27 m above existing interaction anchor; speaker signs 0.50 m above existing speaker anchor, each 0.025 m forward. Only newly added decoration transforms change; all device anchors/physics/source meshes untouched. Fixed world font sizes Title0.85/Destination0.60/State0.68 selected for unchanged eye0.9m/FOV70 near0.85m and normal1.2/1.8m views. Text/backing separation34.5mm retained; overflow assertions retained. Backup="+backup);
        return Inspect();
    }
    static string CompactDestination(string full)=>full.EndsWith("扬声器",StringComparison.Ordinal)?full.Substring(0,full.Length-3)+"\n扬声器":full;
    static void RepositionSign(Transform sign)
    {
        var view=sign.GetComponent<DeviceReadabilityView>();Require(view,"Sign has no real device binding");view.compactWorldStatus=true;
        if(view.noiseDevice)
        {
            var device=view.noiseDevice;bool speaker=sign.name.EndsWith("_Speaker",StringComparison.Ordinal);
            Vector3 front=speaker?device.investigationAnchor.position-device.soundAnchor.position:device.deviceId=="Noise_NW"?Vector3.forward:Vector3.back;front.y=0;front.Normalize();
            sign.SetPositionAndRotation((speaker?device.soundAnchor.position:device.interactable.anchor.position)+Vector3.up*(speaker?.50f:.27f)+front*.025f,Quaternion.LookRotation(front));
            sign.Find("Title").GetComponent<TMP_Text>().text=view.profile.LabelFor(device.deviceId).shortId;
            sign.Find("Destination").GetComponent<TMP_Text>().text=CompactDestination(view.profile.LabelFor(device.deviceId).destination);
        }
        else
        {
            var button=view.shutter.switches.OrderBy(t=>Vector3.Distance(t.anchor.position,sign.position)).First();
            Vector3 front=button.anchor.position.z>=view.shutter.transform.position.z?Vector3.forward:Vector3.back;
            sign.SetPositionAndRotation(button.anchor.position+Vector3.up*.27f+front*.025f,Quaternion.LookRotation(front));
            sign.Find("Title").GetComponent<TMP_Text>().text=view.profile.shutterShortId;
            sign.Find("Destination").GetComponent<TMP_Text>().text=view.profile.shutterDestination+"\n隔断门";
        }
        sign.Find("State").GetComponent<TMP_Text>().text="就绪";
    }
    static void ApplySignLayout(Transform sign)
    {
        sign.Find("Plate").localScale=new Vector3(.62f,.34f,.035f);
        sign.Find("Lamp").localPosition=new Vector3(-.245f,-.10f,.052f);sign.Find("Lamp").localScale=new Vector3(.035f,.035f,.025f);
        SetLabelLayout(sign,"Title",new Vector3(-.20f,.055f,.052f),new Vector2(.18f,.15f),.85f);
        SetLabelLayout(sign,"Destination",new Vector3(.10f,.055f,.052f),new Vector2(.36f,.19f),.60f);
        SetLabelLayout(sign,"State",new Vector3(.035f,-.105f,.052f),new Vector2(.46f,.12f),.68f);
    }
    static void SetLabelLayout(Transform sign,string name,Vector3 position,Vector2 size,float pointSize)
    {
        var text=sign.Find(name).GetComponent<TMP_Text>();text.transform.localPosition=position;text.rectTransform.sizeDelta=size;
        text.enableAutoSizing=false;text.fontSizeMin=pointSize;text.fontSizeMax=pointSize;text.fontSize=pointSize;text.textWrappingMode=TextWrappingModes.NoWrap;text.ForceMeshUpdate(true,true);
    }
    static void WriteSignMetrics(Transform root,string stage,string path)
    {
        var values=new List<SignMetric>();
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.ForceMeshUpdate(true,true);var preferred=text.GetPreferredValues(text.text);var bounds=text.textBounds.size;
            values.Add(new SignMetric{sign=text.transform.parent.name,label=text.name,text=text.text,fontSize=text.fontSize,fontSizeMin=text.fontSizeMin,fontSizeMax=text.fontSizeMax,
                rectWidth=text.rectTransform.rect.width,rectHeight=text.rectTransform.rect.height,preferredWidth=preferred.x,preferredHeight=preferred.y,glyphWidth=bounds.x,glyphHeight=bounds.y,overflow=text.isTextOverflowing,lineCount=text.textInfo.lineCount});
        }
        File.WriteAllText(path,JsonUtility.ToJson(new SignMetrics{stage=stage,labels=values.ToArray()},true));
    }

    [CliCommand("polish","inspect","Check preserved gameplay, repaired meshes, references and readable sign geometry")]
    public static string Inspect()
    {
        CleanEditMode();Require(File.Exists(ScenePath),"Polish scene does not exist");Directory.CreateDirectory(Evidence);
        var source=EditorSceneManager.OpenScene(SourceScene);string before=ProtectedSnapshot(source),beforeMeshes=OriginalMeshes(source);
        var scene=EditorSceneManager.OpenScene(ScenePath);string after=ProtectedSnapshot(scene),afterMeshes=OriginalMeshes(scene);
        File.WriteAllText(Path.Combine(Evidence,"inspect-source.txt"),before);File.WriteAllText(Path.Combine(Evidence,"inspect-polish.txt"),after);
        File.WriteAllText(Path.Combine(Evidence,"original-meshrefs-source.txt"),beforeMeshes);File.WriteAllText(Path.Combine(Evidence,"original-meshrefs-polish.txt"),afterMeshes);
        Require(before==after,"Saved candidate changes protected source state");Require(beforeMeshes==afterMeshes,"Saved candidate changes original repaired meshes");
        var session=One<ExpandedSession>(scene);var ui=One<ChapterPlayerUI>(scene);
        var root=scene.GetRootGameObjects().SingleOrDefault(go=>go.name==ReadabilityRoot);Require(root,"Readability root missing");
        Require(session.tactics.diagnosticsEnabled,"Development feedback diagnostics must be enabled");
        Require(ui.polishProfile && ui.polishFeedback && ui.polishFeedback.session==session && ui.polishFeedback.profile==ui.polishProfile,"Polish UI/presenter binding");
        Require(AssetDatabase.GetAssetPath(ui.polishProfile).StartsWith(Config+"/",StringComparison.Ordinal) && AssetDatabase.GetAssetPath(ui.font)==Config+"/PolishChinese.asset","Independent readability profile/font");
        var views=root.GetComponentsInChildren<DeviceReadabilityView>(true);Require(views.Length==6,"Expected six live device signs");
        foreach(var view in views)
        {
            Require(view.compactWorldStatus && view.profile==ui.polishProfile && view.stateText && view.stateText.font==ui.font && view.indicatorRenderers!=null && view.indicatorRenderers.Length>0,"Incomplete sign view");
            Require((view.noiseDevice?1:0)+(view.shutter?1:0)==1,"A sign must bind exactly one real device");
            Require(view.indicatorRenderers.All(r=>r && r.sharedMaterial && r.sharedMaterial.IsKeywordEnabled("_EMISSION")),"Indicator emission material missing");
            string expectedId=view.noiseDevice?view.profile.LabelFor(view.noiseDevice.deviceId).shortId:view.profile.shutterShortId;
            string expectedDestination=view.noiseDevice?CompactDestination(view.profile.LabelFor(view.noiseDevice.deviceId).destination):view.profile.shutterDestination+"\n隔断门";
            Require(view.transform.Find("Title").GetComponent<TMP_Text>().text==expectedId && view.transform.Find("Destination").GetComponent<TMP_Text>().text==expectedDestination,"Sign/profile identification mismatch");
        }
        Require(session.tactics.noiseDevices.All(device=>views.Count(v=>v.noiseDevice==device)==2) && views.Count(v=>v.shutter==session.tactics.shutter)==2,"Matched control/speaker and two-sided shutter signs");
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.ForceMeshUpdate(true,true);
            Require(!text.isTextOverflowing,"World sign text overflow: "+text.name);
            foreach(uint ch in text.text.Select(c=>(uint)c).Distinct())if(!char.IsControl((char)ch))Require(ui.font.HasCharacter((int)ch),"Unwarmed sign glyph "+ch);
        }
        foreach(var view in views)CheckStateText(view);
        string geometry=PolishSignGeometry.Check(scene,root.transform);File.WriteAllText(Path.Combine(Evidence,"sign-geometry.txt"),geometry);
        string assets=Hashes(DependencyFiles(SourceScene));
        var baseline=Path.Combine(Evidence,"source-assets-before.sha256");if(File.Exists(baseline))Require(File.ReadAllText(baseline)==assets,"Source dependency hashes drifted since generation");
        File.WriteAllText(Path.Combine(Evidence,"candidate-assets.sha256"),Hashes(DependencyFiles(ScenePath)));
        string message="PASS preserved original transforms, colliders, navigation, agents, AI/resource/device config, all repaired meshes, lighting/camera/post and original materials. Only optional UI bindings/font and diagnostics flag changed; six nonphysical state signs added. Static inspection does not prove player comprehension, route success or 5-8 minute first completion.\n"+geometry;
        File.WriteAllText(Path.Combine(Evidence,"contract.txt"),message);
        File.WriteAllText(Path.Combine(Evidence,"protection-summary.json"),JsonUtility.ToJson(new ProtectionSummary{sourceScene=SourceScene,candidateScene=ScenePath,protectedSha256=HashText(before),originalMeshRefsSha256=HashText(beforeMeshes),sourceAssetsSha256=HashText(assets),signCount=6},true));
        return message;
    }
    [Serializable] sealed class ProtectionSummary { public string sourceScene,candidateScene,protectedSha256,originalMeshRefsSha256,sourceAssetsSha256;public int signCount; }

    [CliCommand("polish","build","Build independent tactics feedback Windows development candidate")]
    public static string Build()
    {
        using var preserve=new LegacyFontPreservation();Inspect();
        string directory=Path.GetFullPath("../Builds/ChapterTacticsPolish-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Require(!Directory.Exists(directory),"Build directory already exists");Directory.CreateDirectory(directory);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(directory,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        string result=$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nUnity={Application.unityVersion}\nDirectory={directory}\n";
        File.WriteAllText(Path.Combine(Evidence,"build-result.txt"),result);File.WriteAllText(Path.Combine(directory,"build-result.txt"),result);
        preserve.RestoreIfNeeded();Require(report.summary.result==BuildResult.Succeeded,"Windows build failed");return directory;
    }
    [CliCommand("polish","probe","Print explicit development-player graphics-probe usage; does not launch a player")]
    public static string Probe()=>"Use the new development build: LastCellOfPower.exe -batchmode -tactics-render-probe -tactics-evidence-dir=\"<absolute Evidence/ChapterTacticsPolish/PlayerProbe>\" -logFile \"<absolute player log>\". Keep graphics enabled; never add -nographics. Existing TacticsBuildProbe uses 60 warmup + 600 explicit 1920x1080 Camera.Render/readback frames. It measures controlled render-and-sync throughput, not normal gameplay FPS or player understanding. Ordinary launch has no probe.";

    public static string ProtectedSnapshot(Scene scene)
    {
        var excludedGuids=new HashSet<string>(AssetDatabase.FindAssets("",new[]{Root}).Select(g=>g),StringComparer.Ordinal);
        return string.Join("\n",WallFixDelivery.Snapshot(scene,false).Split('\n').Where(row=>
        {
            if(row.Contains(":"+ReadabilityRoot+"#") || row.Contains(":"+ReadabilityRoot+"/") || row.StartsWith("GO|",StringComparison.Ordinal) && row.Contains(":"+ReadabilityRoot+"|") || row.StartsWith("TRANSFORM|",StringComparison.Ordinal) && row.Contains(":"+ReadabilityRoot+"|"))return false;
            if(row.Contains("#LastCellOfPower.ChapterOne.ChapterPlayerUI[") && (row.Contains("|font=") || row.Contains("|polishProfile=") || row.Contains("|polishFeedback=")))return false;
            if(row.Contains("#LastCellOfPower.ChapterTactics.TacticsDevices[") && row.Contains("|diagnosticsEnabled="))return false;
            if(row.StartsWith("ASSET|asset:",StringComparison.Ordinal))
            {int end=row.IndexOf(':',12);if(end>12 && excludedGuids.Contains(row.Substring(12,end-12)))return false;}
            return true;
        }));
    }
    static string OriginalMeshes(Scene scene)
    {
        var rows=new List<string>();foreach(var root in scene.GetRootGameObjects())
        {
            if(root.name==ReadabilityRoot)continue;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))rows.Add(ObjectPath(filter.transform)+"|"+AssetRef(filter.sharedMesh));
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))rows.Add(ObjectPath(renderer.transform)+"|rendererEnabled="+renderer.enabled);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))rows.Add(ObjectPath(renderer.transform)+"|skin|"+AssetRef(renderer.sharedMesh));
        }
        rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows);
    }
    static T One<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).Single();
    static void CleanEditMode(){Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene first");}
    static string ObjectPath(Transform t)=>t.parent?ObjectPath(t.parent)+"/"+t.GetSiblingIndex()+":"+t.name:t.GetSiblingIndex()+":"+t.name;
    static string AssetRef(UnityEngine.Object value){if(!value)return "null";AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long id);return guid+":"+id;}
    static string[] DependencyFiles(string scene)=>AssetDatabase.GetDependencies(scene,true).Where(File.Exists).SelectMany(path=>File.Exists(path+".meta")?new[]{path,path+".meta"}:new[]{path}).Distinct().OrderBy(path=>path,StringComparer.Ordinal).ToArray();
    static string Hashes(IEnumerable<string> paths)=>string.Join("\n",paths.Select(path=>path.Replace('\\','/')+"|"+HashBytes(File.ReadAllBytes(path))));
    static string HashText(string value)=>HashBytes(Encoding.UTF8.GetBytes(value));
    static string HashBytes(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void CheckStateText(DeviceReadabilityView view)
    {
        var profile=view.profile;string original=view.stateText.text;
        string[] samples=view.noiseDevice?new[]{profile.worldReadyText,profile.worldSignallingText,profile.spentText}:new[]{profile.worldReadyText,profile.worldClosingText,profile.worldClosedText,profile.worldOpeningText,profile.spentText};
        try{foreach(string text in samples){view.stateText.text=text;view.stateText.ForceMeshUpdate(true,true);Require(!view.stateText.isTextOverflowing,"World state label overflow: "+view.name+" / "+text);}}
        finally{view.stateText.text=original;view.stateText.ForceMeshUpdate(true,true);}
    }
    static TMP_FontAsset CreateFont(TMP_FontAsset oldFont,TacticalReadabilityProfile profile)
    {
        Require(oldFont,"Baseline font missing");var source=AssetDatabase.LoadAssetAtPath<Font>(ChapterOneBuilder.SourceFontPath);Require(source,"Bundled Noto font missing");
        var font=TMP_FontAsset.CreateFontAsset(source,72,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="PolishChinese";
        AssetDatabase.CreateAsset(font,Config+"/PolishChinese.asset");
        var serialized=new SerializedObject(font);serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();
        string required=ChapterOneBuilder.RequiredCharacters()+TacticalReadabilityProfile.PreloadText+"N01 N02 S01 诱导控制 扬声器 设备区南侧扬声器 货架区南侧扬声器 临时封门 东侧通道 本局一次 状态见交互提示";
        required+=EditorJsonUtility.ToJson(profile);
        required+=new string(oldFont.characterTable.Select(c=>c.unicode).Where(c=>c<=char.MaxValue && !char.IsSurrogate((char)c)).Select(c=>(char)c).ToArray());
        string characters=new string(required.Where(c=>!char.IsControl(c) && !char.IsSurrogate(c) && char.GetUnicodeCategory(c)!=UnicodeCategory.Format).Distinct().ToArray());
        Require(font.TryAddCharacters(characters,out string missing,true) || string.IsNullOrEmpty(missing),"New font missing glyphs: "+missing);
        if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var atlas in font.atlasTextures)if(atlas && !AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
        EditorUtility.SetDirty(font);return font;
    }
    static Material NewMaterial(string name,Color color,bool emission)
    {
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage4/Materials/MAT_Graphite.mat");Require(source,"Baseline URP material missing");
        var material=new Material(source){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",.25f);material.SetFloat("_Smoothness",.3f);
        material.SetColor("_EmissionColor",emission?new Color(.025f,.028f,.028f):Color.black);
        material.globalIlluminationFlags=emission?MaterialGlobalIlluminationFlags.BakedEmissive:MaterialGlobalIlluminationFlags.None;
        if(emission)material.EnableKeyword("_EMISSION");else material.DisableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(material,Config+"/"+name+".mat");return material;
    }
    static void AddSign(Transform parent,string name,Vector3 center,Vector3 front,string title,string destination,NoiseDevice noise,TacticalShutter shutter,TacticalReadabilityProfile profile,TMP_FontAsset font,Material plateMaterial,Material indicatorMaterial)
    {
        var sign=new GameObject(name).transform;sign.SetParent(parent,false);sign.SetPositionAndRotation(center,Quaternion.LookRotation(front,Vector3.up));
        Box(sign,"Plate",Vector3.zero,new Vector3(1.10f,.78f,.035f),plateMaterial);
        var lamp=Box(sign,"Lamp",new Vector3(.46f,.28f,.052f),new Vector3(.09f,.09f,.025f),indicatorMaterial).GetComponent<Renderer>();
        Label(sign,"Title",title,new Vector3(-.055f,.28f,.052f),new Vector2(.86f,.14f),2.1f,font);
        Label(sign,"Destination",destination,new Vector3(0,.08f,.052f),new Vector2(1.0f,.14f),1.65f,font);
        var state=Label(sign,"State","就绪",new Vector3(0,-.19f,.052f),new Vector2(.98f,.30f),1.9f,font);
        ApplySignLayout(sign);
        var view=sign.gameObject.AddComponent<DeviceReadabilityView>();view.profile=profile;view.noiseDevice=noise;view.shutter=shutter;view.stateText=state;view.indicatorRenderers=new[]{lamp};view.compactWorldStatus=true;
    }
    static GameObject Box(Transform parent,string name,Vector3 local,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;return go;
    }
    static TMP_Text Label(Transform parent,string name,string content,Vector3 local,Vector2 size,float fontSize,TMP_FontAsset font)
    {
        var go=new GameObject(name);go.SetActive(false);go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localRotation=Quaternion.Euler(0,180,0);
        var text=go.AddComponent<TextMeshPro>();text.font=font;text.text=content;text.rectTransform.sizeDelta=size;text.fontSize=fontSize;text.enableAutoSizing=true;text.fontSizeMin=1.05f;text.fontSizeMax=fontSize;
        text.alignment=TextAlignmentOptions.Center;text.color=new Color(.87f,.89f,.82f);text.textWrappingMode=TextWrappingModes.NoWrap;text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        go.SetActive(true);return text;
    }
    static void WriteReferenceNotes()
    {
        File.WriteAllText(Path.Combine(Evidence,"MethodReference.md"),"# 本批方法参考\n\n仓库 https://github.com/yq6666-66/game-development-plugins ，main 经只读 API 核对并锁定完整提交 `"+ReferenceCommit+"`。\n\n本生成器作者已读 `skills/game-visual-iteration/SKILL.md` 与 `skills/game-playloop-validation/SKILL.md`。主 Agent 另读 `references/input-text-evidence.md`、`references/recovery-and-audio-information.md`。\n\n采用：固定原布局、物理规则及有效渲染配置；新增编号与目的地对应；状态文字和颜色并用；呈现只读真实设备状态；检查新标识与底板实际几何间距；源场景及依赖哈希保护。\n\n不适用：引擎迁移、安装上游工具、付费素材、重建地图、改变AI/电量/设备次数、发布。静态几何/构建/probe只证明各自范围，不证明玩家理解、正常游玩性能或5～8分钟首通。保留集中后台检查、最终同版集中试玩的节奏。\n");
    }
    sealed class LegacyFontPreservation:IDisposable
    {
        const string PathName="Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        readonly byte[] original=File.Exists(PathName)?File.ReadAllBytes(PathName):null;
        public void RestoreIfNeeded()
        {
            if(original==null || !File.Exists(PathName) || original.SequenceEqual(File.ReadAllBytes(PathName)))return;
            string dir=Path.Combine(Evidence,"CandidateBackups","tmp-upgrade-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(dir);File.Copy(PathName,Path.Combine(dir,"LiberationSans-upgraded.asset"));File.WriteAllBytes(PathName,original);
        }
        public void Dispose()=>RestoreIfNeeded();
    }
}
