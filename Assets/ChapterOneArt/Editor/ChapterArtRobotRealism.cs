using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;

/// <summary>Replace renderer appearance only; preserve the accepted guard's gameplay graph.</summary>
public static class ChapterArtRobotRealism
{
    const string Root="Assets/ChapterOneArt/Models/RustGuard";
    public static void Apply(ExpandedSession session,ChapterArtProfile profile)
    {
        if(!session || !session.enemy || !profile)throw new ArgumentException("Guard art requires session and art profile");
        Transform enemy=session.enemy.transform;
        if(enemy.Find("RustGuardVisual"))throw new InvalidOperationException("RustGuardVisual already exists; generate a fresh scene revision");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RustGuard.fbx");
        if(!source)throw new FileNotFoundException("Generate and import RustGuard.fbx with Tools/BuildRustGuard.py first");
        var materials=CreateMaterials(profile);
        // Mesh renderers are the entire mutation boundary for the old model.
        // Keep every Transform, Collider, Agent, Perception and status Light intact.
        foreach(var renderer in enemy.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
        var container=new GameObject("RustGuardVisual");container.transform.SetParent(enemy,false);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(source,container.transform);
        model.name="RustGuardModel";model.transform.localPosition=Vector3.zero;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            string key=renderer.name.Replace("RustGuard_","");
            if(!materials.TryGetValue(key,out var material))throw new InvalidOperationException("Unmapped RustGuard surface: "+renderer.name);
            renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            renderer.enabled=true;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
            renderer.gameObject.layer=enemy.gameObject.layer;
        }
        var scanner=model.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="RustGuard_Glass");
        if(container.transform.InverseTransformPoint(scanner.bounds.center).z<0)
            model.transform.localRotation=Quaternion.AngleAxis(180,Vector3.up)*model.transform.localRotation;
        // Imported FBX axis/unit conventions can differ across importer releases.
        // Fit the *visual child* uniformly, never compensate on the gameplay root.
        Bounds bounds=LocalBounds(container.transform);
        float scale=Mathf.Min(1.04f/bounds.size.x,1.04f/bounds.size.z,1.27f/bounds.size.y);
        model.transform.localScale*=scale;
        bounds=LocalBounds(container.transform);
        model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        bounds=LocalBounds(container.transform);
        if(bounds.size.x>1.0761f || bounds.size.z>1.0771f || bounds.size.y>1.3231f || Mathf.Abs(bounds.min.y)>.001f)
            throw new InvalidOperationException("RustGuard visual exceeds the accepted silhouette");
        if(container.GetComponentsInChildren<Collider>(true).Length!=0)
            throw new InvalidOperationException("RustGuard FBX must contain no gameplay colliders");
        var state=container.AddComponent<GuardVisualState>();state.enemy=session.enemy;
        state.scannerRenderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="RustGuard_Status").ToArray();
        Debug.Log("RUST_GUARD_APPLIED visualOnly; renderers="+model.GetComponentsInChildren<Renderer>(true).Length+"; bounds="+bounds.size+"; original Agent/Perception/Colliders/Light retained");
    }

    static Dictionary<string,Material> CreateMaterials(ChapterArtProfile profile)
    {
        Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
        var result=new Dictionary<string,Material>();
        result["Paint"]=Surface("Paint",profile.rustedMetal,new Color(.62f,.67f,.62f),.12f,.30f);
        result["Rust"]=Surface("Rust",profile.rustedMetal,new Color(.80f,.64f,.47f),.25f,.16f);
        result["Steel"]=Surface("Steel",profile.paintedMetal,new Color(.36f,.41f,.43f),.85f,.46f,true);
        result["Rubber"]=Surface("Rubber",profile.rubber,new Color(.32f,.34f,.33f),0,.16f);
        result["Warning"]=Surface("Warning",profile.paintedMetal,new Color(.69f,.49f,.16f),.12f,1f);
        result["Wire"]=Surface("Wire",profile.rubber,new Color(.47f,.25f,.09f),0,.25f);
        result["Glass"]=Surface("Glass",profile.paintedMetal,new Color(.018f,.040f,.046f),.45f,.86f,true);
        result["Status"]=Surface("Status",profile.coolEmission,new Color(.20f,.44f,.51f),.1f,.48f,true);
        AssetDatabase.SaveAssets();return result;
    }
    static Material Surface(string name,Material source,Color color,float metal,float smooth,bool clearMaps=false)
    {
        string path=Root+"/Materials/Guard_"+name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
        if(!source)throw new InvalidOperationException("Missing source material for guard "+name);
        var material=new Material(source){name="Guard_"+name};
        material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);
        material.SetColor("_EmissionColor",name=="Status"?new Color(.35f,.65f,.8f)*1.1f:Color.black);
        material.EnableKeyword("_EMISSION");material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        if(clearMaps)
        {
            foreach(string key in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"})
                if(material.HasProperty(key))material.SetTexture(key,null);
            material.DisableKeyword("_NORMALMAP");material.DisableKeyword("_METALLICSPECGLOSSMAP");material.DisableKeyword("_OCCLUSIONMAP");
        }
        if(name=="Rust"){material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Metallic",.08f);material.SetFloat("_Smoothness",.18f);}
        if(name=="Paint"){material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Metallic",.12f);material.SetFloat("_Smoothness",.30f);}
        // These are solid machined surfaces; the scanner's black optical coating
        // is opaque and reflective, so no translucent interior sorting is needed.
        material.SetFloat("_Surface",0);material.SetFloat("_ZWrite",1);
        material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=2000;
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static Bounds LocalBounds(Transform root)
    {
        bool first=true;Bounds result=default;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh)continue;
            Bounds b=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                Vector3 c=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 point=root.InverseTransformPoint(filter.transform.TransformPoint(c));
                if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
            }
        }
        if(first)throw new InvalidOperationException("Guard FBX contains no mesh");return result;
    }
}
