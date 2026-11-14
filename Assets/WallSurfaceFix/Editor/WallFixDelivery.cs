using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.Pipeline.Commands;
using LastCellOfPower.WarehouseExpanded;

public static class WallFixDelivery
{
    public const string SourceScene="Assets/ChapterTactics/Scenes/WarehouseTactics.unity";
    public const string ScenePath="Assets/WallSurfaceFix/Scenes/WarehouseTacticsWallFix.unity";
    public const string MeshRoot="Assets/WallSurfaceFix/Meshes";
    public const string ReflectionRoot="Assets/WallSurfaceFix/Reflections";
    public static string Evidence=>Path.GetFullPath("Evidence/WallSurfaceFix");
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("WALL_FIX_CONTRACT: "+message);}

    [CliCommand("wallfix","generate","Copy tactics scene, repair wall render meshes and bake independent reflections")]
    public static string Generate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene first");
        Require(!File.Exists(ScenePath),"Wall-fix candidate already exists; preserve and repair explicitly");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));Directory.CreateDirectory(Evidence);
        byte[] sourceBytes=File.ReadAllBytes(SourceScene);
        var source=EditorSceneManager.OpenScene(SourceScene);
        string before=Snapshot(source,false),beforeReflections=Snapshot(source,true);
        File.WriteAllText(Path.Combine(Evidence,"protected-before.txt"),before);
        Require(EditorSceneManager.SaveScene(source,ScenePath,true),"Independent scene copy failed");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        string afterCopy=Snapshot(scene,false);File.WriteAllText(Path.Combine(Evidence,"protected-after-copy.txt"),afterCopy);
        Require(before==afterCopy,"Source copy differs before mesh repair");
        WallSurfaceRepair.Apply(scene,MeshRoot);
        string afterRepair=Snapshot(scene,false);
        File.WriteAllText(Path.Combine(Evidence,"protected-after-mesh.txt"),afterRepair);
        Require(before==afterRepair,"Mesh repair changed a protected transform/component/config/light/camera/post property");
        Require(WallSurfaceRepair.LastReport!=null && WallSurfaceRepair.LastReport.changedMeshReferences>0,"Repair did not replace any render mesh");
        File.WriteAllText(Path.Combine(Evidence,"mesh-repair.json"),JsonUtility.ToJson(WallSurfaceRepair.LastReport,true));
        BakeReflections(scene);
        string afterBake=Snapshot(scene,true);
        File.WriteAllText(Path.Combine(Evidence,"protected-after-reflections.txt"),afterBake);
        File.WriteAllText(Path.Combine(Evidence,"protected-before-reflections.txt"),beforeReflections);
        Require(beforeReflections==afterBake,"Reflection baking changed protected scene state beyond output cubemap bindings");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Require(sourceBytes.SequenceEqual(File.ReadAllBytes(SourceScene)),"Accepted tactics scene was modified");
        File.WriteAllText(Path.Combine(Evidence,"generation.txt"),"PASS independent scene; complete protected component/config/transform snapshot unchanged by mesh repair; only reflection output bindings differ after independent four-probe bake.\n");
        return Verify();
    }

    static void BakeReflections(Scene scene)
    {
        Directory.CreateDirectory(ReflectionRoot);AssetDatabase.Refresh();
        var probes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ReflectionProbe>(true)).OrderBy(p=>PathOf(p.transform),StringComparer.Ordinal).ToArray();
        Require(probes.Length==4,"Expected four existing reflection probes");
        var session=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ExpandedSession>(true)).Single();
        var actors=session.Guards.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Concat(session.player.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
        var actorLights=session.Guards.SelectMany(g=>g.GetComponentsInChildren<Light>(true)).Concat(session.player.GetComponentsInChildren<Light>(true)).Distinct().ToArray();
        bool[] rendererEnabled=actors.Select(r=>r.enabled).ToArray(),lightEnabled=actorLights.Select(l=>l.enabled).ToArray();
        // Probe rendering flushes TMP serialized caches. Restore their exact source state afterward.
        var labels=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMPro.TMP_Text>(true)).ToArray();
        var cacheFields=new[]{"m_TextStyleHashCode","m_fontColor32"}.Select(name=>typeof(TMPro.TMP_Text).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)).ToArray();
        Require(cacheFields.All(field=>field!=null),"TMP cache schema changed");
        var labelStates=labels.Select(label=>cacheFields.Select(field=>field.GetValue(label)).ToArray()).ToArray();
        try
        {
            foreach(var renderer in actors)renderer.enabled=false;
            foreach(var light in actorLights)light.enabled=false;
            for(int i=0;i<probes.Length;i++)
            {
                var probe=probes[i];Require(probe.mode==ReflectionProbeMode.Custom,"Accepted reflection mode must already be Custom");
                string path=ReflectionRoot+"/Room_"+i+".exr";Require(!File.Exists(path),"Refusing to overwrite reflection output "+path);
                bool dynamic=probe.renderDynamicObjects;
                try
                {
                    probe.mode=ReflectionProbeMode.Baked;probe.renderDynamicObjects=true;
                    Require(Lightmapping.BakeReflectionProbe(probe,path),"Reflection bake failed: "+path);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                    if(importer){importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
                    var texture=AssetDatabase.LoadAssetAtPath<Texture>(path);Require(texture,"Missing baked cubemap "+path);
                    probe.customBakedTexture=texture;EditorUtility.SetDirty(probe);
                }
                finally{probe.mode=ReflectionProbeMode.Custom;probe.renderDynamicObjects=dynamic;}
            }
        }
        finally
        {
            for(int i=0;i<actors.Length;i++)if(actors[i])actors[i].enabled=rendererEnabled[i];
            for(int i=0;i<actorLights.Length;i++)if(actorLights[i])actorLights[i].enabled=lightEnabled[i];
            for(int i=0;i<labels.Length;i++)if(labels[i])for(int j=0;j<cacheFields.Length;j++)cacheFields[j].SetValue(labels[i],labelStates[i][j]);
        }
    }

    [CliCommand("wallfix","verify","Compare source and repaired scene protected state")]
    public static string Verify()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene first");
        Require(File.Exists(SourceScene)&&File.Exists(ScenePath),"Both baseline and candidate scenes required");
        string before=Snapshot(EditorSceneManager.OpenScene(SourceScene),true);
        var scene=EditorSceneManager.OpenScene(ScenePath);string after=Snapshot(scene,true);
        Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"verify-source.txt"),before);File.WriteAllText(Path.Combine(Evidence,"verify-fixed.txt"),after);
        Require(before==after,"Protected source/current snapshots differ");
        var probes=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ReflectionProbe>(true)).ToArray();
        Require(probes.Length==4 && probes.All(p=>p.customBakedTexture && AssetDatabase.GetAssetPath(p.customBakedTexture).StartsWith(ReflectionRoot+"/",StringComparison.Ordinal)),"Independent baked reflections missing");
        var filters=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).ToArray();
        int repaired=filters.Count(f=>f.sharedMesh && AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(MeshRoot+"/",StringComparison.Ordinal));
        Require(repaired>0,"No independent repaired render meshes in candidate");
        string result="PASS all transforms, game objects, colliders, agents, surfaces/obstacles, gameplay configuration, materials, lights, camera and post components match source. Only render-mesh/enabled fields and four independent reflection cubemap bindings are permitted differences. Repaired meshes="+repaired+". Static contract; motion capture remains separate.\n";
        File.WriteAllText(Path.Combine(Evidence,"contract.txt"),result);return result;
    }
    [Serializable] sealed class IdempotenceResult
    {
        public string scene,meshRoot,beforeProtectedSha256,afterProtectedSha256,beforeMeshRefsSha256,afterMeshRefsSha256;
        public int changedMeshReferences,createdMeshes,reusedMeshes,beforeMeshFiles,afterMeshFiles;
        public bool protectedStateUnchanged,meshReferencesUnchanged,meshFilesUnchanged,sceneFileUnchanged,sceneDirty;
    }
    [CliCommand("wallfix","idempotence","Apply repair to the already repaired scene and prove a no-op without saving")]
    public static string Idempotence()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Edit mode required");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene first");
        Require(File.Exists(ScenePath),"Repaired scene required");Directory.CreateDirectory(Evidence);
        byte[] sceneBytes=File.ReadAllBytes(ScenePath);
        var scene=EditorSceneManager.OpenScene(ScenePath);
        string before=Snapshot(scene,false),beforeRefs=MeshReferences(scene),beforeFiles=MeshFiles();
        int beforeCount=Directory.GetFiles(MeshRoot,"*.asset",SearchOption.AllDirectories).Length;
        WallSurfaceRepair.Apply(scene,MeshRoot);
        var report=WallSurfaceRepair.LastReport;
        string after=Snapshot(scene,false),afterRefs=MeshReferences(scene),afterFiles=MeshFiles();
        var result=new IdempotenceResult{scene=ScenePath,meshRoot=MeshRoot,
            beforeProtectedSha256=HashText(before),afterProtectedSha256=HashText(after),
            beforeMeshRefsSha256=HashText(beforeRefs),afterMeshRefsSha256=HashText(afterRefs),
            changedMeshReferences=report.changedMeshReferences,createdMeshes=report.createdMeshes,reusedMeshes=report.reusedMeshes,
            beforeMeshFiles=beforeCount,afterMeshFiles=Directory.GetFiles(MeshRoot,"*.asset",SearchOption.AllDirectories).Length,
            protectedStateUnchanged=before==after,meshReferencesUnchanged=beforeRefs==afterRefs,meshFilesUnchanged=beforeFiles==afterFiles,
            sceneFileUnchanged=sceneBytes.SequenceEqual(File.ReadAllBytes(ScenePath)),sceneDirty=scene.isDirty};
        File.WriteAllText(Path.Combine(Evidence,"idempotence.json"),JsonUtility.ToJson(result,true));
        File.WriteAllText(Path.Combine(Evidence,"idempotence-repair.json"),JsonUtility.ToJson(report,true));
        File.WriteAllText(Path.Combine(Evidence,"idempotence-meshrefs-before.txt"),beforeRefs);
        File.WriteAllText(Path.Combine(Evidence,"idempotence-meshrefs-after.txt"),afterRefs);
        File.WriteAllText(Path.Combine(Evidence,"idempotence-meshfiles-before.txt"),beforeFiles);
        File.WriteAllText(Path.Combine(Evidence,"idempotence-meshfiles-after.txt"),afterFiles);
        Require(result.changedMeshReferences==0 && result.createdMeshes==0,"Second Apply changed or created a mesh");
        Require(result.protectedStateUnchanged && result.meshReferencesUnchanged && result.meshFilesUnchanged,"Second Apply changed protected state, references or mesh files");
        Require(result.beforeMeshFiles==result.afterMeshFiles && result.sceneFileUnchanged && !result.sceneDirty,"Second Apply modified scene or mesh count");
        string message="PASS second Apply on repaired scene: changedMeshReferences=0; createdMeshes=0; reusedMeshes="+report.reusedMeshes+"; mesh count="+beforeCount+" unchanged; all references, mesh file hashes and protected state unchanged; scene remained clean and was not saved.";
        File.WriteAllText(Path.Combine(Evidence,"idempotence.txt"),message);return message;
    }
    static string MeshReferences(Scene scene)
    {
        var rows=new List<string>();
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))rows.Add(ObjectReference(filter)+"|mesh="+ObjectReference(filter.sharedMesh));
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))rows.Add(ObjectReference(renderer)+"|enabled="+renderer.enabled);
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))rows.Add(ObjectReference(renderer)+"|mesh="+ObjectReference(renderer.sharedMesh));
        }
        rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows);
    }
    static string MeshFiles()=>string.Join("\n",Directory.GetFiles(MeshRoot,"*",SearchOption.AllDirectories).OrderBy(path=>path,StringComparer.Ordinal).Select(path=>path.Replace('\\','/')+"|"+HashBytes(File.ReadAllBytes(path))));
    static string HashText(string value)=>HashBytes(Encoding.UTF8.GetBytes(value));
    static string HashBytes(byte[] bytes){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}

    [CliCommand("wallfix","motion","Capture matched before/after continuous camera trajectories")]
    public static Task<string> Motion()=>WallFixMotionCapture.Run();
    [CliCommand("wallfix","build","Build an independent wall-surface-fix Windows candidate")]
    public static string Build()
    {
        Verify();string directory=Path.GetFullPath("../Builds/WallSurfaceFix-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Require(!Directory.Exists(directory),"New build directory required");Directory.CreateDirectory(directory);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(directory,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        string result=$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nUnity={Application.unityVersion}\n";
        File.WriteAllText(Path.Combine(directory,"build-result.txt"),result);File.WriteAllText(Path.Combine(Evidence,"build-result.txt"),result+"Directory="+directory+"\n");
        Require(report.summary.result==BuildResult.Succeeded,"Build failed");return directory;
    }

    // Stable scene-local references: instance ids are deliberately never used.
    public static string Snapshot(Scene scene,bool allowReflectionTextures)
    {
        var rows=new List<string>();var assets=new HashSet<UnityEngine.Object>();
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            string path=PathOf(t);rows.Add("GO|"+path+"|"+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+t.gameObject.tag+"|"+(int)GameObjectUtility.GetStaticEditorFlags(t.gameObject));
            rows.Add("TRANSFORM|"+path+"|local="+V(t.localPosition)+"|"+Q(t.localRotation)+"|"+V(t.localScale)+"|world="+M(t.localToWorldMatrix));
            foreach(var c in t.GetComponents<Component>())
            {
                if(!c){rows.Add("MISSING|"+path);continue;}
                string id=ObjectReference(c);rows.Add("COMPONENT|"+id);
                var serialized=new SerializedObject(c);var p=serialized.GetIterator();bool enterChildren=true;
                while(p.Next(enterChildren))
                {
                    // References are normalized above; their internal m_FileID children are transient instance ids.
                    enterChildren=p.propertyType!=SerializedPropertyType.ObjectReference && p.propertyType!=SerializedPropertyType.ExposedReference;
                    if(c is Renderer && p.propertyPath=="m_Enabled")continue;
                    if((c is MeshFilter || c is SkinnedMeshRenderer) && p.propertyPath=="m_Mesh")continue;
                    if(allowReflectionTextures && c is ReflectionProbe && (p.propertyPath=="m_CustomBakedTexture" || p.propertyPath=="m_BakedTexture"))continue;
                    string value=Property(p);if(value!=null)rows.Add(id+"|"+p.propertyPath+"="+value);
                    if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue && (p.objectReferenceValue is Material || p.objectReferenceValue is ScriptableObject))assets.Add(p.objectReferenceValue);
                }
            }
        }
        var inspected=new HashSet<UnityEngine.Object>();
        while(assets.Count>0)
        {
            var asset=assets.First();assets.Remove(asset);if(!asset || !inspected.Add(asset))continue;
            var serialized=new SerializedObject(asset);var p=serialized.GetIterator();string id="ASSET|"+ObjectReference(asset);bool enterChildren=true;
            while(p.Next(enterChildren))
            {
                enterChildren=p.propertyType!=SerializedPropertyType.ObjectReference && p.propertyType!=SerializedPropertyType.ExposedReference;
                string value=Property(p);if(value!=null)rows.Add(id+"|"+p.propertyPath+"="+value);
                if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue && (p.objectReferenceValue is Material || p.objectReferenceValue is ScriptableObject) && !inspected.Contains(p.objectReferenceValue))assets.Add(p.objectReferenceValue);
            }
        }
        rows.Add("RENDER_SETTINGS|"+RenderSettings.ambientMode+"|"+F(RenderSettings.ambientIntensity)+"|"+C(RenderSettings.ambientLight)+"|"+RenderSettings.fog+"|"+RenderSettings.fogMode+"|"+F(RenderSettings.fogDensity)+"|"+F(RenderSettings.fogStartDistance)+"|"+F(RenderSettings.fogEndDistance)+"|"+C(RenderSettings.fogColor)+"|"+ObjectReference(RenderSettings.skybox));
        rows.Add("PIPELINE|"+ObjectReference(GraphicsSettings.currentRenderPipeline));
        rows.Sort(StringComparer.Ordinal);return string.Join("\n",rows);
    }
    static string Property(SerializedProperty p)
    {
        switch(p.propertyType)
        {
            case SerializedPropertyType.Generic:return null;
            case SerializedPropertyType.ObjectReference:return ObjectReference(p.objectReferenceValue);
            case SerializedPropertyType.ExposedReference:return ObjectReference(p.exposedReferenceValue);
            case SerializedPropertyType.String:return p.stringValue.Replace("\r","\\r").Replace("\n","\\n");
            case SerializedPropertyType.Boolean:return p.boolValue?"true":"false";
            case SerializedPropertyType.Float:return p.doubleValue.ToString("R",CultureInfo.InvariantCulture);
            case SerializedPropertyType.Integer:return p.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Enum:case SerializedPropertyType.ArraySize:case SerializedPropertyType.LayerMask:case SerializedPropertyType.Character:return p.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Color:return C(p.colorValue);
            case SerializedPropertyType.Vector2:return F(p.vector2Value.x)+","+F(p.vector2Value.y);
            case SerializedPropertyType.Vector3:return V(p.vector3Value);
            case SerializedPropertyType.Vector4:return F(p.vector4Value.x)+","+F(p.vector4Value.y)+","+F(p.vector4Value.z)+","+F(p.vector4Value.w);
            case SerializedPropertyType.Quaternion:return Q(p.quaternionValue);
            case SerializedPropertyType.Rect:return p.rectValue.ToString("R",CultureInfo.InvariantCulture);
            case SerializedPropertyType.Bounds:return V(p.boundsValue.center)+"/"+V(p.boundsValue.size);
            case SerializedPropertyType.Vector2Int:return p.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int:return p.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt:return p.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt:return p.boundsIntValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                var curve=p.animationCurveValue;return curve.preWrapMode+"/"+curve.postWrapMode+"/"+string.Join(";",curve.keys.Select(k=>F(k.time)+","+F(k.value)+","+F(k.inTangent)+","+F(k.outTangent)+","+F(k.inWeight)+","+F(k.outWeight)+","+k.weightedMode));
            case SerializedPropertyType.Gradient:
                var gradient=p.gradientValue;return gradient.mode+"/"+string.Join(";",gradient.colorKeys.Select(k=>F(k.time)+","+C(k.color)))+"/"+string.Join(";",gradient.alphaKeys.Select(k=>F(k.time)+","+F(k.alpha)));
            case SerializedPropertyType.ManagedReference:return p.managedReferenceFullTypename;
            case SerializedPropertyType.Hash128:return p.hash128Value.ToString();
            default:return p.propertyType.ToString();
        }
    }
    static string ObjectReference(UnityEngine.Object value)
    {
        if(!value)return "null";
        if(EditorUtility.IsPersistent(value) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string assetGuid,out long assetId))return "asset:"+assetGuid+":"+assetId;
        if(value is Component component)
        {
            var same=component.GetComponents(component.GetType());int index=Array.IndexOf(same,component);
            return "scene:"+PathOf(component.transform)+"#"+component.GetType().FullName+"["+index+"]";
        }
        if(value is GameObject go)return "scene:"+PathOf(go.transform);
        if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long id))return "asset:"+guid+":"+id;
        return value.GetType().FullName+":"+value.name;
    }
    static string PathOf(Transform t)
    {var path=new List<string>();while(t){path.Add(t.GetSiblingIndex().ToString("D4",CultureInfo.InvariantCulture)+":"+t.name);t=t.parent;}path.Reverse();return string.Join("/",path);}
    static string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
    static string V(Vector3 v)=>F(v.x)+","+F(v.y)+","+F(v.z);
    static string Q(Quaternion q)=>F(q.x)+","+F(q.y)+","+F(q.z)+","+F(q.w);
    static string C(Color c)=>F(c.r)+","+F(c.g)+","+F(c.b)+","+F(c.a);
    static string M(Matrix4x4 m){var parts=new string[16];for(int i=0;i<16;i++)parts[i]=F(m[i]);return string.Join(",",parts);}
}
