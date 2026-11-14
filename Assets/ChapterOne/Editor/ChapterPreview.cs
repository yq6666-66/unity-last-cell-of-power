using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.WarehouseExpanded;

// Off-screen presentation captures, not physical-input or audible-quality evidence.
public static class ChapterPreview
{
    public static IEnumerator Run()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
        while(!ui || !ui.LayoutBuilt || !s.Ready)yield return null;
        var input=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
        bool inputEnabled=input && input.enabled;
        if(input)input.enabled=false;
        s.enabled=false;
        try
        {
            yield return null;
            Capture(ui,s.player.viewCamera,"intro-1280.png",1280,720);
            ui.ShowSettings();yield return null;
            Capture(ui,s.player.viewCamera,"settings-960.png",960,540);
            ui.ReturnToPause();s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
            s.player.SetActive(false);s.enemy.SetActive(false);
            yield return null;
            Capture(ui,s.player.viewCamera,"hud-1920.png",1920,1080);
            s.battery.Tick(1000,false,false);s.Tick(0);yield return null;
            ui.SkipResultFade();yield return null;
            Capture(ui,s.player.viewCamera,"results-1280.png",1280,720);
        }
        finally {s.Pause();if(input)input.enabled=inputEnabled;}
    }
    static void Capture(ChapterPlayerUI ui,Camera camera,string name,int width,int height)
    {
        var canvas=ui.GetComponentInChildren<Canvas>();if(!canvas)throw new Exception("Chapter canvas missing");
        var mode=canvas.renderMode;var canvasCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var target=new RenderTexture(width,height,24);Texture2D image=null;
        try
        {
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.1f,camera.nearClipPlane+.02f);
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
            camera.Render();RenderTexture.active=target;
            image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            Directory.CreateDirectory(ChapterDelivery.Evidence);File.WriteAllBytes(Path.Combine(ChapterDelivery.Evidence,name),image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode=mode;canvas.worldCamera=canvasCamera;canvas.planeDistance=plane;
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
