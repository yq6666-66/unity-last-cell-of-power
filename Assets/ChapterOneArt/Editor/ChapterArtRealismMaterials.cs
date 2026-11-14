using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using LastCellOfPower.ChapterOneArt;

public static class ChapterArtRealismMaterials
{
    const string Root="Assets/ChapterOneArt";
    [Serializable] sealed class ModelSet { public string assetId; public Entry[] materials; }
    [Serializable] sealed class Entry { public string name,@base,normal,packed; }
    public static void Create()
    {
        foreach(string folder in new[]{Root+"/Models",Root+"/Textures/WallConcrete",Root+"/Textures/PeelingPaint",Root+"/Textures/GuardCorrodedPaint"})
        foreach(string filename in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
        {
            string path=filename.Replace('\\','/');if(!path.EndsWith(".png")&&!path.EndsWith(".jpg"))continue;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
            bool normal=path.Contains("Normal"),packed=path.Contains("Packed");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!packed;importer.maxTextureSize=2048;
            importer.mipmapEnabled=true;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        Surface("WallConcrete",new Color(.84f,.84f,.8f),.7f);
        Surface("PeelingPaint",new Color(.55f,.60f,.54f),.6f);
        const string dampPath=Root+"/Materials/ART_WetConcrete.mat";
        if(!File.Exists(dampPath))
        {
            var damp=new Material(AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ART_WallConcrete.mat")){name="ART_WetConcrete"};
            damp.SetColor("_BaseColor",new Color(.38f,.41f,.36f));damp.SetTexture("_MetallicGlossMap",null);damp.DisableKeyword("_METALLICSPECGLOSSMAP");damp.SetFloat("_Smoothness",.52f);AssetDatabase.CreateAsset(damp,dampPath);
        }
        var paint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ART_PeelingPaint.mat");
        const string yellowPath=Root+"/Materials/ART_WornYellow.mat";
        if(!File.Exists(yellowPath)){var yellow=new Material(paint){name="ART_WornYellow"};yellow.SetColor("_BaseColor",new Color(.64f,.45f,.15f));AssetDatabase.CreateAsset(yellow,yellowPath);}
        foreach(string id in new[]{"wooden_crate_02","old_military_crate","steel_frame_shelves_03"})
        {
            string folder=Root+"/Models/"+id;Directory.CreateDirectory(folder+"/Materials");AssetDatabase.Refresh();
            var data=JsonUtility.FromJson<ModelSet>(File.ReadAllText(folder+"/Materials.json"));
            if(data.materials==null||data.materials.Length==0)throw new Exception("Realism model missing material maps: "+id);
            var importer=AssetImporter.GetAtPath(folder+"/Model.fbx") as ModelImporter;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.importAnimation=false;
            foreach(var entry in data.materials)
            {
                string path=folder+"/Materials/"+entry.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=Pbr(entry.name,folder+"/Textures/"+entry.@base,folder+"/Textures/"+entry.normal,folder+"/Textures/"+entry.packed,Color.white,1);AssetDatabase.CreateAsset(material,path);}
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.name),material);
            }
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }
    public static void ConfigureProfile(ChapterArtProfile profile)
    {
        string packedPath=Root+"/Textures/CoatedMetal/Packed.png";
        var importer=AssetImporter.GetAtPath(packedPath) as TextureImporter;
        if(!importer)throw new FileNotFoundException("Prepare dielectric coated-metal packed map first");
        importer.sRGBTexture=false;importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var oldPaint=profile.paintedMetal;var oldFabric=profile.fabric;
        var paint=CopyMaterial("ART_CoatedMetal",oldPaint);paint.SetColor("_BaseColor",new Color(.90f,.90f,.86f));paint.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
        paint.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/GuardCorrodedPaint/Base.jpg"));paint.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/GuardCorrodedPaint/Normal.png"));
        var canvas=CopyMaterial("ART_CargoCanvas",oldFabric);canvas.SetColor("_BaseColor",new Color(.82f,.84f,.76f));
        profile.paintedMetal=paint;profile.fabric=canvas;EditorUtility.SetDirty(profile);EditorUtility.SetDirty(paint);EditorUtility.SetDirty(canvas);
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var mats=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++){if(mats[i]==oldPaint){mats[i]=paint;changed=true;}else if(mats[i]==oldFabric){mats[i]=canvas;changed=true;}}
            if(changed)renderer.sharedMaterials=mats;
        }
        string guardRoot=Root+"/Models/RustGuard/Materials/";
        foreach(string name in new[]{"Paint","Warning"})
        {
            var guard=AssetDatabase.LoadAssetAtPath<Material>(guardRoot+"Guard_"+name+".mat");if(!guard)continue;
            guard.SetTexture("_MetallicGlossMap",paint.GetTexture("_MetallicGlossMap"));guard.SetFloat("_Smoothness",1);guard.EnableKeyword("_METALLICSPECGLOSSMAP");
            guard.SetTexture("_BaseMap",paint.GetTexture("_BaseMap"));guard.SetTexture("_BumpMap",paint.GetTexture("_BumpMap"));
            if(name=="Paint")
            {
                guard.SetTexture("_BaseMap",profile.rustedMetal.GetTexture("_BaseMap"));guard.SetTexture("_BumpMap",profile.rustedMetal.GetTexture("_BumpMap"));guard.SetTexture("_OcclusionMap",profile.rustedMetal.GetTexture("_OcclusionMap"));
                guard.SetColor("_BaseColor",new Color(.62f,.67f,.62f));guard.SetTexture("_MetallicGlossMap",null);guard.DisableKeyword("_METALLICSPECGLOSSMAP");guard.SetFloat("_Metallic",.12f);guard.SetFloat("_Smoothness",.30f);
            }
            EditorUtility.SetDirty(guard);
        }
        var rust=AssetDatabase.LoadAssetAtPath<Material>(guardRoot+"Guard_Rust.mat");
        if(rust){rust.SetTexture("_MetallicGlossMap",null);rust.DisableKeyword("_METALLICSPECGLOSSMAP");rust.SetFloat("_Metallic",.08f);rust.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(rust);}
    }
    static Material CopyMaterial(string name,Material source)
    {
        string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
        material=new Material(source){name=name};AssetDatabase.CreateAsset(material,path);return material;
    }
    static void Surface(string family,Color color,float tiling)
    {
        string path=Root+"/Materials/ART_"+family+".mat";if(File.Exists(path))return;
        string folder=Root+"/Textures/"+family+"/";var m=Pbr("ART_"+family,folder+"Base.jpg",folder+"Normal.png",folder+"Packed.png",color,tiling);AssetDatabase.CreateAsset(m,path);
    }
    static Material Pbr(string name,string colorPath,string normalPath,string packedPath,Color color,float scale)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
        if(!albedo||!packed)throw new Exception("Missing PBR texture "+colorPath);
        m.SetTexture("_BaseMap",albedo);m.SetColor("_BaseColor",color);m.SetTextureScale("_BaseMap",Vector2.one*scale);
        if(normal){m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.8f);}
        m.SetTexture("_MetallicGlossMap",packed);m.SetTexture("_OcclusionMap",packed);m.SetFloat("_Smoothness",1);m.SetFloat("_OcclusionStrength",.3f);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);return m;
    }
}
