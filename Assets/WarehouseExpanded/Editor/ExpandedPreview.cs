using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;

public static class ExpandedPreview
{
    [CliCommand("warehouse","preview","Save off-screen level overview and starting-camera previews")]
    public static string Capture()
    {
        if(EditorApplication.isPlaying)throw new Exception("Preview requires edit mode");
        if(EditorSceneManager.GetActiveScene().path!=ExpandedDelivery.ScenePath)EditorSceneManager.OpenScene(ExpandedDelivery.ScenePath);
        Directory.CreateDirectory(ExpandedDelivery.Evidence);
        var main=Camera.main;if(!main)throw new Exception("No main camera");
        Render(main,"starting-view.png");
        var go=new GameObject("TemporaryOverviewCamera");
        try
        {
            var camera=go.AddComponent<Camera>();camera.CopyFrom(main);
            camera.transform.position=new Vector3(0,38,-17);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=17;camera.cullingMask=~0;
            Render(camera,"warehouse-overview.png");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
        return "Saved offline Editor camera previews (not manual gameplay screenshots)";
    }
    static void Render(Camera camera,string filename)
    {
        var target=new RenderTexture(1600,1000,24);var prior=camera.targetTexture;var active=RenderTexture.active;
        Texture2D image=null;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(ExpandedDelivery.Evidence,filename),image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=prior;RenderTexture.active=active;
            if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
