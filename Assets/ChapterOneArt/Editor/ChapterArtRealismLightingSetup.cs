using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;

public static class ChapterArtRealismLightingSetup
{
    public static void Apply(ExpandedSession session,ChapterArtProfile profile)
    {
        EnsureInspectionLight(session,profile);
        var root=new GameObject("ArtRealismLighting");root.AddComponent<ChapterRealismLighting>().profile=profile;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if(canvas.renderMode!=RenderMode.WorldSpace)continue;
            canvas.gameObject.AddComponent<RealismWayfindingFacing>().viewCamera=session.player.viewCamera;
            foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))text.color=new Color(.72f,.73f,.66f);
            foreach(var image in canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if(image.color.r<.1f&&image.color.g<.1f)image.color=new Color(.055f,.06f,.055f,.96f);
        }
        foreach(var center in new[]{new Vector3(-6,2,-6),new Vector3(6,2,-6),new Vector3(-6,2,6),new Vector3(6,2,6)})
        {
            var go=new GameObject("RoomReflection");go.transform.SetParent(root.transform);go.transform.position=center;
            var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution=profile.reflectionResolution;probe.size=new Vector3(13,4.4f,13);probe.boxProjection=true;probe.blendDistance=1;probe.intensity=profile.reflectionIntensity;probe.hdr=true;probe.nearClipPlane=.12f;probe.farClipPlane=27;
            int index=(center.x>0?2:0)+(center.z>0?1:0);
            var saved=AssetDatabase.LoadAssetAtPath<Texture>("Assets/ChapterOneArt/Reflections/Room_"+index+".exr");
            if(saved){probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=saved;}
        }
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if(light.name.StartsWith("StableWorkLight_")){light.spotAngle=125;light.innerSpotAngle=92;}
        // Quiet wet patches sit millimetres above the physical flat floor. No displacement/collider.
        const string matPath="Assets/ChapterOneArt/Materials/ART_WetFloor.mat";
        var wet=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!wet)
        {
            wet=new Material(profile.concrete){name="ART_WetFloor"};wet.SetTexture("_MetallicGlossMap",null);wet.DisableKeyword("_METALLICSPECGLOSSMAP");wet.SetFloat("_Metallic",.05f);wet.SetFloat("_Smoothness",.84f);
            wet.SetTexture("_BaseMap",profile.grime.GetTexture("_BaseMap"));wet.SetTextureScale("_BaseMap",Vector2.one);
            wet.SetColor("_BaseColor",new Color(.45f,.51f,.50f,1));wet.SetFloat("_Surface",1);wet.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);wet.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);wet.SetFloat("_ZWrite",0);
            wet.SetFloat("_BlendModePreserveSpecular",0);wet.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            wet.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");wet.renderQueue=3000;wet.SetOverrideTag("RenderType","Transparent");AssetDatabase.CreateAsset(wet,matPath);
        }
        Vector3[] positions={new Vector3(-8,.006f,-6.5f),new Vector3(7,.006f,-.7f),new Vector3(-7.6f,.006f,6.3f),new Vector3(8.3f,.006f,10.4f)};
        Directory.CreateDirectory("Assets/ChapterOneArt/Meshes");
        for(int i=0;i<positions.Length;i++)
        {
            string path="Assets/ChapterOneArt/Meshes/WetPatch_"+i+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                const int n=24;var v=new Vector3[n+1];var uv=new Vector2[n+1];var normals=new Vector3[n+1];var t=new int[n*3];normals[0]=Vector3.up;uv[0]=Vector2.one*.5f;
                for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=.82f+.13f*Mathf.Sin(k*2.31f+i);v[k+1]=new Vector3(Mathf.Cos(a)*r*(1.6f+i*.13f),0,Mathf.Sin(a)*r*.8f);uv[k+1]=new Vector2(.5f+Mathf.Cos(a)*r*.5f,.5f+Mathf.Sin(a)*r*.5f);normals[k+1]=Vector3.up;t[k*3]=0;t[k*3+1]=(k+1)%n+1;t[k*3+2]=k+1;}
                mesh=new Mesh{name="WetPatch_"+i};mesh.vertices=v;mesh.uv=uv;mesh.normals=normals;mesh.triangles=t;mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,path);
            }
            var go=new GameObject("DampFloorPatch_"+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform);go.transform.position=positions[i];go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=wet;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    public static void EnsureInspectionLight(ExpandedSession session,ChapterArtProfile profile)
    {
        if(session.player.viewCamera.transform.Find("MaintenanceInspectionLamp"))return;
        var go=new GameObject("MaintenanceInspectionLamp");go.transform.SetParent(session.player.viewCamera.transform,false);go.transform.localPosition=new Vector3(.08f,-.12f,.04f);
        var light=go.AddComponent<Light>();light.type=LightType.Spot;light.range=profile.inspectionLightRange;light.intensity=profile.inspectionLightIntensity;
        light.color=new Color(.9f,.94f,.86f);light.spotAngle=70;light.innerSpotAngle=43;light.shadows=LightShadows.None;
    }
}
