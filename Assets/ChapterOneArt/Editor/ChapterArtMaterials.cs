using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LastCellOfPower.ChapterOneArt;

public static class ChapterArtMaterials
{
    public const string Root="Assets/ChapterOneArt";
    public static ChapterArtProfile CreateProfile()
    {
        var existing=AssetDatabase.LoadAssetAtPath<ChapterArtProfile>(Root+"/Config/ChapterArtProfile.asset");
        if(existing)return existing;
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Config");AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles(Root+"/Textures","*",SearchOption.AllDirectories))
        {
            if(!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)&&!path.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase))continue;
            var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(!importer)continue;
            bool normal=path.Contains("Normal"),data=path.Contains("Packed");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal&&!data;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
            importer.maxTextureSize=2048;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if(path.Contains("Grime")&&path.Contains("Base")){importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;}
            importer.SaveAndReimport();
        }
        var p=ScriptableObject.CreateInstance<ChapterArtProfile>();
        p.concrete=Surface("Concrete",new Color(.88f,.87f,.84f),1f/3f);
        p.paintedMetal=Surface("PaintedMetal",new Color(.78f,.79f,.76f),.8f);
        p.rustedMetal=Surface("RustedMetal",new Color(.65f,.66f,.64f),.65f);
        p.wood=Surface("Wood",new Color(.8f,.76f,.67f),.75f);
        p.cardboard=Surface("Cardboard",Color.white,1f);
        p.fabric=Surface("Fabric",new Color(.36f,.40f,.35f),1.5f);
        p.rubber=Surface("Rubber",Color.white,1f);
        p.grime=Surface("Grime",Color.white,1f);Transparent(p.grime);
        p.mintEmission=Emission("MintEmission",new Color(.29f,.72f,.52f),1.7f);
        p.warmEmission=Emission("WarmEmission",new Color(1,.72f,.4f),2.4f);
        p.coolEmission=Emission("CoolEmission",new Color(.5f,.68f,.9f),1.7f);
        p.glass=Emission("DustyGlass",new Color(.16f,.22f,.26f,.2f),.15f);Transparent(p.glass);
        p.volume=ScriptableObject.CreateInstance<VolumeProfile>();p.volume.name="AbandonedWarehouseVolume";
        AssetDatabase.CreateAsset(p.volume,Root+"/Config/ArtVolume.asset");
        var tonemap=p.volume.Add<Tonemapping>(true);tonemap.mode.Override(TonemappingMode.ACES);
        var color=p.volume.Add<ColorAdjustments>(true);color.postExposure.Override(.35f);color.contrast.Override(8);color.saturation.Override(-16);
        var bloom=p.volume.Add<Bloom>(true);bloom.intensity.Override(.14f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.5f);
        var vignette=p.volume.Add<Vignette>(true);vignette.intensity.Override(.13f);vignette.smoothness.Override(.5f);
        foreach(var component in p.volume.components)AssetDatabase.AddObjectToAsset(component,p.volume);
        EditorUtility.SetDirty(p.volume);AssetDatabase.CreateAsset(p,Root+"/Config/ChapterArtProfile.asset");AssetDatabase.SaveAssets();return p;
    }
    static Material Surface(string family,Color tint,float tiling)
    {
        string folder=Root+"/Textures/"+family+"/";
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ART_"+family};
        var baseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Base.jpg");
        if(!baseTexture)baseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Base.png");
        var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Normal.png");var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Packed.png");
        if(!baseTexture||!normal||!packed)throw new Exception("Missing prepared PBR maps for "+family);
        m.SetTexture("_BaseMap",baseTexture);m.SetColor("_BaseColor",tint);m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.65f);
        m.SetTexture("_MetallicGlossMap",packed);m.SetFloat("_Smoothness",1f);m.SetTexture("_OcclusionMap",packed);m.SetFloat("_OcclusionStrength",.35f);
        m.SetTextureScale("_BaseMap",Vector2.one*tiling);
        m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);
        AssetDatabase.CreateAsset(m,Root+"/Materials/"+m.name+".mat");return m;
    }
    static Material Emission(string name,Color color,float strength)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ART_"+name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.4f);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*strength);
        AssetDatabase.CreateAsset(m,Root+"/Materials/"+m.name+".mat");return m;
    }
    static void Transparent(Material m)
    {
        m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;EditorUtility.SetDirty(m);
    }
}
