#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem.UI;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterOneArt
{
    /// <summary>Opt-in graphics probe. Normal startup never installs this observer.</summary>
    public sealed class ChapterArtBuildProbe : MonoBehaviour
    {
        const int Width=1920,Height=1080,WarmupFrames=60,SampleFrames=600;
        readonly List<double> frameTimes=new List<double>(SampleFrames);
        readonly List<double> cpuTimes=new List<double>(SampleFrames);
        readonly List<double> gpuTimes=new List<double>(SampleFrames);
        readonly FrameTiming[] frameTiming=new FrameTiming[1];
        string outputDirectory;
        bool runtimeError;
        Camera probeCamera;
        RenderTexture renderTarget,previousTarget;
        Texture2D synchronizationPixel;
        Canvas canvas;
        RenderMode previousCanvasMode;
        Camera previousCanvasCamera;
        float previousPlane;
        bool previousCanvasEnabled;
        ExpandedSession session;
        bool previousSessionEnabled;
        bool performanceOverrides;
        bool previousRunInBackground;
        int previousVSyncCount, previousTargetFrameRate;
        [Serializable] sealed class ViewReport
        {
            public string name,screenshot;
            public Vector3 position,lookAt;
            public int firstSample,sampleCount,pixelRange,nonBlackPixelSamples;
        }
        [Serializable] sealed class ProbeReport
        {
            public string status,scope,unityVersion,graphicsDeviceType,graphicsDeviceName,graphicsDeviceVersion,scene,error;
            public int outputWidth,outputHeight,screenWidth,screenHeight,warmupFrames,sampleFrames,gpuTimingSamples,cpuTimingSamples;
            public float renderScale;
            public bool originalRunInBackground,probeRunInBackground;
            public int originalVSyncCount,originalTargetFrameRate,probeVSyncCount,probeTargetFrameRate;
            public bool graphicsActive,renderTargetCreated,gpuTimingAvailable,meetsFrameTarget,postProcessing;
            public double averageFrameMs,p95FrameMs,averageFramesPerSecond,averageCpuFrameMs,p95CpuFrameMs,averageGpuFrameMs,p95GpuFrameMs;
            public ViewReport[] views;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-art-render-probe")>=0)
                new GameObject("ChapterArtRenderProbe").AddComponent<ChapterArtBuildProbe>();
        }
        static string EvidencePath()
        {
            foreach(var arg in Environment.GetCommandLineArgs())
                if(arg.StartsWith("-art-evidence-dir=",StringComparison.Ordinal))
                {
                    string value=arg.Substring("-art-evidence-dir=".Length).Trim('"');
                    if(value.Length==0)throw new ArgumentException("Empty -art-evidence-dir");
                    return Path.GetFullPath(value);
                }
            return Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtRenderEvidence"));
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)runtimeError=true;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            ProbeReport report=new ProbeReport { status="FAIL",scope="Explicit 1920x1080 Camera.Render plus synchronous 1-pixel GPU readback per sampled frame. Fixed cameras, gameplay motion frozen, atmosphere/HUD active. Wall-clock render-and-sync throughput is conservative and includes GPU synchronization; it is not normal gameplay FPS or standalone GPU timing. No subjective art or normal-input acceptance.",
                unityVersion=Application.unityVersion,graphicsDeviceType=SystemInfo.graphicsDeviceType.ToString(),graphicsDeviceName=SystemInfo.graphicsDeviceName,
                graphicsDeviceVersion=SystemInfo.graphicsDeviceVersion,outputWidth=Width,outputHeight=Height,screenWidth=Screen.width,screenHeight=Screen.height,
                warmupFrames=WarmupFrames,sampleFrames=0 };
            string failure=null;
            try{outputDirectory=EvidencePath();Directory.CreateDirectory(outputDirectory);}
            catch(Exception e){failure=e.ToString();UnityEngine.Debug.LogError("ART_RENDER_PROBE_PATH_FAIL "+e.Message);}
            if(failure!=null){Application.logMessageReceived-=OnLog;Application.Quit(2);yield break;}
            previousRunInBackground=Application.runInBackground;
            previousVSyncCount=QualitySettings.vSyncCount;previousTargetFrameRate=Application.targetFrameRate;
            report.originalRunInBackground=previousRunInBackground;report.originalVSyncCount=previousVSyncCount;report.originalTargetFrameRate=previousTargetFrameRate;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;performanceOverrides=true;
            report.probeRunInBackground=true;report.probeVSyncCount=0;report.probeTargetFrameRate=-1;
            report.graphicsActive=SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
            if(!report.graphicsActive || Array.IndexOf(Environment.GetCommandLineArgs(),"-nographics")>=0)
                failure="Graphics device is unavailable. -nographics is forbidden for this probe.";
            if(failure==null)
            {
                double deadline=Time.realtimeSinceStartupAsDouble+30;
                ChapterPlayerUI ui=null;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    session=FindFirstObjectByType<ExpandedSession>();ui=FindFirstObjectByType<ChapterPlayerUI>();
                    if(session && session.Ready && ui && ui.LayoutBuilt && ui.Mode!=ChapterViewMode.Loading)break;
                    if(session && session.Failure.Length>0){failure=session.Failure;break;}
                    yield return null;
                }
                if(!session || !session.Ready || !ui || !ui.LayoutBuilt)failure=failure??"Chapter UI/session startup timed out";
                if(failure==null)
                {
                    IEnumerator probe=null;
                    try{probe=Measure(ui,report);}
                    catch(Exception e){failure=e.ToString();}
                    if(probe!=null)
                    {
                        while(true)
                        {
                            bool more=false;object current=null;
                            try{more=probe.MoveNext();if(more)current=probe.Current;}
                            catch(Exception e){failure=e.ToString();}
                            if(failure!=null || !more)break;
                            yield return current;
                        }
                        try{(probe as IDisposable)?.Dispose();}catch(Exception e){failure=failure??e.ToString();}
                    }
                }
            }
            try{Restore();}catch(Exception e){failure=failure??e.ToString();}
            report.averageFrameMs=Average(frameTimes);report.p95FrameMs=Percentile95(frameTimes);
            report.averageFramesPerSecond=report.averageFrameMs>0?1000.0/report.averageFrameMs:0;
            report.gpuTimingSamples=gpuTimes.Count;report.cpuTimingSamples=cpuTimes.Count;
            report.gpuTimingAvailable=gpuTimes.Count>0;
            report.averageGpuFrameMs=Average(gpuTimes);report.p95GpuFrameMs=Percentile95(gpuTimes);
            report.averageCpuFrameMs=Average(cpuTimes);report.p95CpuFrameMs=Percentile95(cpuTimes);
            report.sampleFrames=frameTimes.Count;
            report.meetsFrameTarget=frameTimes.Count==SampleFrames && report.averageFramesPerSecond>=60 && report.p95FrameMs<=20;
            if(runtimeError)failure=failure??"Runtime error/exception observed during graphics probe";
            if(!report.renderTargetCreated)failure=failure??"1920x1080 render target was not created";
            if(Mathf.Abs(report.renderScale-1)> .001f)failure=failure??"URP render scale is not 1; full-resolution GPU workload not proven";
            if(!report.meetsFrameTarget)failure=failure??"Overall frame target missed (average >=60 FPS and P95 <=20 ms)";
            report.error=failure??"";report.status=failure==null?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(outputDirectory,"art-render-probe.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived-=OnLog;
            UnityEngine.Debug.Log("ART_RENDER_PROBE_"+report.status+" averageFPS="+report.averageFramesPerSecond.ToString("F2")+" p95FrameMs="+report.p95FrameMs.ToString("F3")+" gpuTimingAvailable="+report.gpuTimingAvailable);
            Application.Quit(failure==null?0:2);
        }

        IEnumerator Measure(ChapterPlayerUI ui,ProbeReport report)
        {
            probeCamera=session.player.viewCamera;
            if(!probeCamera)throw new Exception("No main chapter camera");
            var atmosphere=FindFirstObjectByType<ChapterArtAtmosphere>();
            if(!atmosphere || !atmosphere.profile)throw new Exception("Art atmosphere/profile missing from package");
            var input=FindFirstObjectByType<InputSystemUIInputModule>();if(input)input.enabled=false;
            previousSessionEnabled=session.enabled;session.enabled=false;
            session.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
            session.player.SetActive(false);session.enemy.SetActive(false);
            canvas=ui.GetComponentInChildren<Canvas>();if(!canvas)throw new Exception("No chapter canvas");
            previousCanvasMode=canvas.renderMode;previousCanvasCamera=canvas.worldCamera;previousPlane=canvas.planeDistance;previousCanvasEnabled=canvas.enabled;
            previousTarget=probeCamera.targetTexture;
            renderTarget=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){name="ArtProbe1920x1080"};
            renderTarget.Create();report.renderTargetCreated=renderTarget.IsCreated();
            synchronizationPixel=new Texture2D(1,1,TextureFormat.RGB24,false);
            probeCamera.targetTexture=renderTarget;probeCamera.fieldOfView=70;
            canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=probeCamera;canvas.planeDistance=Mathf.Max(.1f,probeCamera.nearClipPlane+.02f);
            Canvas.ForceUpdateCanvases();
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report.renderScale=pipeline?pipeline.renderScale:1;
            report.postProcessing=probeCamera.GetUniversalAdditionalCameraData().renderPostProcessing;
            report.scene=session.gameObject.scene.path;
            var views=new[] {
                View("spawn",new Vector3(-8,.9f,-9),new Vector3(-8,.9f,-4)),
                View("connection-south",new Vector3(-3,.9f,-7),new Vector3(3,.9f,-7)),
                View("connection-north",new Vector3(-3,.9f,7),new Vector3(3,.9f,7)),
                View("connection-west",new Vector3(-7,.9f,-3),new Vector3(-7,.9f,3)),
                View("connection-east",new Vector3(7,.9f,-1.6f),new Vector3(7,.9f,3)),
                View("terminal",new Vector3(-8,.9f,5.2f),new Vector3(-8,.9f,8)),
                View("exit",new Vector3(8,.9f,9.8f),new Vector3(8,.9f,12.16f)),
                View("guard-detail",session.enemy.transform.position+new Vector3(-.9f,1.0f,1.5f),session.enemy.transform.position+Vector3.up*.65f)
            };
            report.views=views;
            Pose(views[0]);
            for(int i=0;i<WarmupFrames;i++){RenderAndSynchronize();yield return null;}
            double deadline=Time.realtimeSinceStartupAsDouble+90;
            for(int sample=0;sample<SampleFrames;sample++)
            {
                if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Graphics sampling exceeded 90 seconds");
                int index=Mathf.Min(views.Length-1,sample*views.Length/SampleFrames);
                if(views[index].sampleCount==0){views[index].firstSample=sample;Pose(views[index]);}
                FrameTimingManager.CaptureFrameTimings();
                long frameStart=Stopwatch.GetTimestamp();
                RenderAndSynchronize();
                yield return null;
                double frameMs=(Stopwatch.GetTimestamp()-frameStart)*1000.0/Stopwatch.Frequency;
                if(frameMs<=0 || double.IsNaN(frameMs) || double.IsInfinity(frameMs))throw new Exception("Invalid frame interval");
                frameTimes.Add(frameMs);views[index].sampleCount++;
                if(FrameTimingManager.GetLatestTimings(1,frameTiming)>0)
                {
                    if(ValidTiming(frameTiming[0].cpuFrameTime))cpuTimes.Add(frameTiming[0].cpuFrameTime);
                    if(ValidTiming(frameTiming[0].gpuFrameTime))gpuTimes.Add(frameTiming[0].gpuFrameTime);
                }
            }
            // Readback and PNG compression are excluded from the sampled frame interval.
            foreach(var view in views)
            {
                Pose(view);yield return null;yield return null;
                RenderAndSynchronize();
                view.screenshot="gpu-"+view.name+"-1920x1080.png";
                var previous=RenderTexture.active;Texture2D image=null;
                try
                {
                    RenderTexture.active=renderTarget;image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();
                    var pixels=image.GetRawTextureData<byte>();int low=255,high=0,lit=0;
                    for(int offset=0;offset<pixels.Length;offset+=3072)
                    {int value=pixels[offset];low=Mathf.Min(low,value);high=Mathf.Max(high,value);if(value>2)lit++;}
                    view.pixelRange=high-low;view.nonBlackPixelSamples=lit;
                    File.WriteAllBytes(Path.Combine(outputDirectory,view.screenshot),image.EncodeToPNG());
                    if(view.pixelRange<2 || lit==0)throw new Exception("Blank/unwritten graphics capture: "+view.name);
                }
                finally{RenderTexture.active=previous;if(image)Destroy(image);}
            }
        }
        static ViewReport View(string name,Vector3 position,Vector3 lookAt)=>new ViewReport{name=name,position=position,lookAt=lookAt};
        void Pose(ViewReport view){probeCamera.transform.SetPositionAndRotation(view.position,Quaternion.LookRotation(view.lookAt-view.position));}
        void RenderAndSynchronize()
        {
            // Batch-mode players do not automatically draw cameras. Reading a
            // single pixel fences this explicit render without timing PNG encoding.
            probeCamera.Render();var previous=RenderTexture.active;
            try{RenderTexture.active=renderTarget;synchronizationPixel.ReadPixels(new Rect(0,0,1,1),0,0,false);}
            finally{RenderTexture.active=previous;}
        }
        void Restore()
        {
            if(canvas){canvas.renderMode=previousCanvasMode;canvas.worldCamera=previousCanvasCamera;canvas.planeDistance=previousPlane;canvas.enabled=previousCanvasEnabled;}
            if(probeCamera)probeCamera.targetTexture=previousTarget;
            if(renderTarget){renderTarget.Release();Destroy(renderTarget);}
            if(synchronizationPixel)Destroy(synchronizationPixel);
            if(session){session.Pause();session.enabled=previousSessionEnabled;}
            if(performanceOverrides)
            {
                Application.runInBackground=previousRunInBackground;
                QualitySettings.vSyncCount=previousVSyncCount;Application.targetFrameRate=previousTargetFrameRate;
                performanceOverrides=false;
            }
        }
        static bool ValidTiming(double value)=>value>0 && !double.IsNaN(value) && !double.IsInfinity(value);
        static double Average(List<double> values){if(values.Count==0)return 0;double sum=0;foreach(double v in values)sum+=v;return sum/values.Count;}
        static double Percentile95(List<double> values){if(values.Count==0)return 0;var sorted=values.ToArray();Array.Sort(sorted);return sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*.95)-1)];}
    }
}
#endif
