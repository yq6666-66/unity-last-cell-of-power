using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

// Art validation and posed comparisons. Camera/awareness poses are not input acceptance.
public static class ChapterArtChecks
{
    public static Task<string> Run() => Launch(false);
    public static Task<string> Capture() => Launch(true);
    public static Task<string> CaptureEast() => Launch(true,true);
    public static Task<string> CaptureGuardClose() => Launch(true,false,true);
    static string Evidence => ChapterArtDelivery.Evidence;
    static void Require(bool value,string text) { if(!value)throw new InvalidOperationException("CHAPTER_ART_CHECK: "+text); }

    static Task<string> Launch(bool capture,bool onlyEast=false,bool onlyClose=false)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Start in edit mode");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene before art checks");
        string initial=capture?ChapterOneBuilder.ScenePath:ChapterArtDelivery.ScenePath;
        EditorSceneManager.OpenScene(initial);
        var completion=new TaskCompletionSource<string>();
        bool previousEnabled=EditorSettings.enterPlayModeOptionsEnabled;
        var previousOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        IEnumerator routine=null;Exception failure=null;bool finishing=false;
        double deadline=EditorApplication.timeSinceStartup+(capture?180:240);
        EditorApplication.CallbackFunction update=null;
        Action<PlayModeStateChange> changed=null;
        void Finish(Exception error)
        {
            if(finishing)return;finishing=true;failure=error;
            try{(routine as IDisposable)?.Dispose();}catch(Exception disposeError){if(failure==null)failure=disposeError;}
            EditorApplication.update-=update;
            EditorApplication.ExitPlaymode();
        }
        update=()=>
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Art batch timed out");
                if(!EditorApplication.isPlaying)return;
                EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=capture?Captures(onlyEast,onlyClose):Checks();
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=previousEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
            if(!finishing)failure=new Exception("Art PlayMode exited before completion");
            Directory.CreateDirectory(Evidence);
            string result=failure==null?(capture?"PASS fixed-camera and UI off-screen art comparisons; posed presentation, not gameplay/performance acceptance.":"PASS art references, decoration collider isolation, atmosphere pause, post toggle, and both synthetic chapter routes."):"FAIL "+failure;
            File.WriteAllText(Path.Combine(Evidence,capture?"capture-result.txt":"checks-result.txt"),result);
            if(failure==null)completion.TrySetResult(result);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;
        EditorApplication.EnterPlaymode();return completion.Task;
    }

    static IEnumerator Ready()
    {
        int frame=Time.frameCount;
        while(Time.frameCount<frame+3)yield return null;
        while(true)
        {
            var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
            var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
            if(s && s.Failure.Length>0)throw new Exception(s.Failure);
            if(s && s.Ready && ui && ui.LayoutBuilt && ui.Mode!=ChapterViewMode.Loading)break;
            yield return null;
        }
    }
    static IEnumerator Reload(string scene)
    {
        EditorSceneManager.LoadSceneInPlayMode(scene,new LoadSceneParameters(LoadSceneMode.Single));
        var ready=Ready();while(ready.MoveNext())yield return null;
    }

    static IEnumerator Checks()
    {
        var ready=Ready();while(ready.MoveNext())yield return null;
        var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var atmosphere=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>();
        var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
        Require(s.Paused && s.Outcome==RunOutcome.Running,"Clean art scene starts paused");
        Require(atmosphere && atmosphere.session==s && atmosphere.profile && ui.artProfile==atmosphere.profile,"Art profile/session/UI references");
        var artWorld=GameObject.Find("ArtWorld");Require(artWorld,"ArtWorld exists");
        Require(artWorld.GetComponentsInChildren<Collider>(true).Length==0,"ArtWorld must not add physical blockers");
        Require(artWorld.GetComponentsInChildren<NavMeshObstacle>(true).Length==0,"ArtWorld must not add navigation blockers");
        foreach(string rootName in new[]{"ArtRealism","ArtRealismLighting","RustGuardVisual"})
        {
            var visual=GameObject.Find(rootName);Require(visual,"Realism visual root: "+rootName);
            Require(visual.GetComponentsInChildren<Collider>(true).Length==0 && visual.GetComponentsInChildren<NavMeshObstacle>(true).Length==0,"Realism visual-only contract: "+rootName);
        }
        var guardVisual=UnityEngine.Object.FindFirstObjectByType<GuardVisualState>();
        Require(guardVisual && guardVisual.enemy==s.enemy && guardVisual.scannerRenderers.Length>0,"Rusted guard scanner bindings");
        var reflections=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
        Require(reflections.Length==4 && reflections.All(r=>r.boxProjection && r.resolution==128),"Four room reflection probes");
        var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.gameObject.scene==s.gameObject.scene && l.enabled && l.type!=LightType.Directional && l.shadows!=LightShadows.None).ToArray();
        Require(lights.Length<=4,"At most four shadow-casting local lights");
        foreach(var field in typeof(ChapterArtProfile).GetFields().Where(f=>f.FieldType==typeof(Material)))
        {
            var material=field.GetValue(atmosphere.profile) as Material;
            Require(material && material.shader && material.shader.name!="Hidden/InternalErrorShader","Art material: "+field.Name);
            Require(material.IsKeywordEnabled("_EMISSION"),"Saved/reloaded art emission variant: "+field.Name);
        }
        Require(atmosphere.profile.volume,"Art post profile reference");
        Require(UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Any(v=>v.enabled && v.sharedProfile==atmosphere.profile.volume),"Art post volume is active");
        var cameraData=s.player.viewCamera.GetUniversalAdditionalCameraData();
        bool post=cameraData.renderPostProcessing;Require(post,"Art main camera post enabled");
        cameraData.renderPostProcessing=false;Require(!cameraData.renderPostProcessing,"Post may be disabled for comparison");cameraData.renderPostProcessing=post;
        float before=atmosphere.Elapsed;atmosphere.Step(2);Require(Mathf.Abs(atmosphere.Elapsed-before)<.0001f,"Paused atmosphere stays frozen");
        s.enabled=false;s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
        s.player.SetActive(false);s.enemy.SetActive(false);
        before=atmosphere.Elapsed;atmosphere.Step(.25f);Require(Mathf.Abs(atmosphere.Elapsed-before-.25f)<.001f,"Active atmosphere advances");
        s.Pause();before=atmosphere.Elapsed;atmosphere.Step(2);Require(Mathf.Abs(atmosphere.Elapsed-before)<.0001f,"Pause freezes atmosphere again");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence,"art-structure.txt"),"PASS ArtWorld has no Collider/NavMeshObstacle; local shadow lights="+lights.Length+"; profile/material/UI/post references; post toggle; atmosphere pause/advance.\n");

        var oldInput=InputSystem.settings;string oldInputJson=JsonUtility.ToJson(oldInput);
        var fixtureInput=UnityEngine.Object.Instantiate(oldInput);fixtureInput.hideFlags=HideFlags.HideAndDontSave;
        fixtureInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        fixtureInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=fixtureInput;
        string oldEvidence=ChapterRouteChecks.EvidenceDirectory;
        try
        {
            ChapterRouteChecks.EvidenceDirectory=Evidence;
            var reload=Reload(ChapterArtDelivery.ScenePath);while(reload.MoveNext())yield return null;
            var route=ChapterRouteChecks.RunWithoutPulse();while(route.MoveNext())yield return route.Current;
            reload=Reload(ChapterArtDelivery.ScenePath);while(reload.MoveNext())yield return null;
            route=ChapterRouteChecks.RunWithPulse();while(route.MoveNext())yield return route.Current;
            reload=Reload(ChapterArtDelivery.ScenePath);while(reload.MoveNext())yield return null;
            s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
            Require(s.Paused && s.Outcome==RunOutcome.Running && Mathf.Abs(s.battery.Current-40)<.001f,"Art scene route reset");
            Require(s.pulse.SuccessfulUses==0 && s.statistics.SuccessfulPulseCount==0 && s.Summary==null,"Art restart clears ability and result state");
        }
        finally
        {
            ChapterRouteChecks.EvidenceDirectory=oldEvidence;
            if(!oldInput){oldInput=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(oldInputJson,oldInput);}
            InputSystem.settings=oldInput;UnityEngine.Object.DestroyImmediate(fixtureInput);
        }
    }

    struct Shot
    {
        public string name;public Vector3 position,target;
        public Shot(string name,Vector3 position,Vector3 target){this.name=name;this.position=position;this.target=target;}
    }
    static Shot[] FixedShots()=>new[] {
        new Shot("spawn",new Vector3(-8,.9f,-9),new Vector3(-8,.9f,-4)),
        new Shot("connection-south",new Vector3(-3,.9f,-7),new Vector3(3,.9f,-7)),
        new Shot("connection-north",new Vector3(-3,.9f,7),new Vector3(3,.9f,7)),
        new Shot("connection-west",new Vector3(-7,.9f,-3),new Vector3(-7,.9f,3)),
        new Shot("connection-east",new Vector3(7,.9f,-1.6f),new Vector3(7,.9f,3)),
        new Shot("terminal",new Vector3(-8,.9f,5.2f),new Vector3(-8,.9f,8)),
        new Shot("exit",new Vector3(8,.9f,9.8f),new Vector3(8,.9f,12.16f)),
        new Shot("guard",new Vector3(7,1.0f,9.8f),new Vector3(7,.65f,7)),
        new Shot("guard-close",new Vector3(6.1f,1.0f,8.5f),new Vector3(7,.65f,7))
    };

    static IEnumerator Captures(bool onlyEast=false,bool onlyClose=false)
    {
        Directory.CreateDirectory(Evidence);
        var report=new StringBuilder("FIXED CAMERA COMPARISONS. All scene views use FOV70 at 1280x720. HUD captured at 960x540,1280x720,1920x1080. Posed cameras/guard perception are not gameplay acceptance.\n");
        foreach(string prefix in new[]{"before","after"})
        {
            var ready=prefix=="before"?Ready():Reload(ChapterArtDelivery.ScenePath);while(ready.MoveNext())yield return null;
            var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
            var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
            var camera=s.player.viewCamera;
            var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();if(module)module.enabled=false;
            s.enabled=false;s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
            s.player.SetActive(false);s.enemy.SetActive(false);
            var atmosphere=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>();if(atmosphere){atmosphere.enabled=false;atmosphere.Step(.5f);}
            foreach(var shot in FixedShots().Where(shot=>(!onlyEast||shot.name=="connection-east")&&(!onlyClose||shot.name=="guard-close")))
            {
                camera.transform.SetPositionAndRotation(shot.position,Quaternion.LookRotation(shot.target-shot.position));camera.fieldOfView=70;
                yield return null;
                Save(ui,camera,prefix+"-"+shot.name+".png",1280,720,false);
            }
            if(onlyEast||onlyClose){report.AppendLine(prefix+" targeted posed comparison; "+s.gameObject.scene.path);s.Pause();continue;}
            // Pose the existing guard through its public sense/tick path. No screenshot is claimed as a route test.
            var originalTarget=s.enemy.perception.target;
            var target=new GameObject("ArtPreviewPerceptionTarget");
            target.layer=s.player.gameObject.layer;target.transform.position=s.enemy.transform.position+s.enemy.transform.forward*2;
            try
            {
                s.enemy.perception.target=target.transform;s.enemy.SetActive(true);s.enemy.Tick(.3f);s.enemy.SetActive(false);
                var shot=FixedShots().Last();camera.transform.SetPositionAndRotation(shot.position,Quaternion.LookRotation(shot.target-shot.position));
                yield return null;Require(s.enemy.State==ExpandedEnemyState.Alert,"Alert screenshot fixture");
                Save(ui,camera,prefix+"-guard-alert.png",1280,720,false);
                s.enemy.SetActive(true);s.enemy.Tick(1.0f);s.enemy.SetActive(false);
                yield return null;Require(s.enemy.State==ExpandedEnemyState.Chase,"Chase screenshot fixture");
                Save(ui,camera,prefix+"-guard-chase.png",1280,720,false);
            }
            finally{s.enemy.perception.target=originalTarget;UnityEngine.Object.DestroyImmediate(target);s.enemy.SetActive(false);}

            var spawn=FixedShots()[0];camera.transform.SetPositionAndRotation(spawn.position,Quaternion.LookRotation(spawn.target-spawn.position));
            foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1920,1080)})
            {yield return null;Save(ui,camera,$"{prefix}-hud-{size.x}x{size.y}.png",size.x,size.y,true);}
            ui.ShowSettings();yield return null;Save(ui,camera,prefix+"-settings-960x540.png",960,540,true);ui.ReturnToPause();
            var data=camera.GetUniversalAdditionalCameraData();bool post=data.renderPostProcessing;
            try{data.renderPostProcessing=false;Save(ui,camera,prefix+"-spawn-post-off.png",1280,720,false);}finally{data.renderPostProcessing=post;}
            var roof=GameObject.Find("ArtRoof");bool roofActive=roof && roof.activeSelf;
            try
            {
                if(roof)roof.SetActive(false);
                camera.transform.SetPositionAndRotation(new Vector3(0,38,-17),Quaternion.LookRotation(new Vector3(0,-38,17)));
                camera.orthographic=true;camera.orthographicSize=17;
                Save(ui,camera,prefix+"-overview-roof-hidden.png",1280,800,false);
            }
            finally{camera.orthographic=false;if(roof)roof.SetActive(roofActive);}
            report.AppendLine(prefix+" scene="+s.gameObject.scene.path+" post="+post+" rendererCount="+UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length);
            if(prefix=="after")CaptureLightingEvidence();
            s.Pause();
        }
        File.WriteAllText(Path.Combine(Evidence,onlyEast?"capture-east-manifest.txt":onlyClose?"capture-guard-close-manifest.txt":"capture-manifest.txt"),report.ToString());
    }

    static void CaptureLightingEvidence()
    {
        var lighting=UnityEngine.Object.FindFirstObjectByType<ChapterRealismLighting>();Require(lighting,"Realism diffuse lighting component");
        var expected=lighting.ExpectedProbe();var actual=RenderSettings.ambientProbe;float delta=0;
        for(int c=0;c<3;c++)for(int k=0;k<9;k++)delta=Mathf.Max(delta,Mathf.Abs(expected[c,k]-actual[c,k]));
        var report=new StringBuilder("Ambient mode="+RenderSettings.ambientMode+"; SH max coefficient difference after render="+delta+"; expectedR0="+expected[0,0]+"; actualR0="+actual[0,0]+"\n");
        File.WriteAllText(Path.Combine(Evidence,"lighting-runtime.txt"),report.ToString());
        foreach(var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
        {
            var texture=probe.texture;Require(texture && texture.width==128,"Reflection texture generated: "+probe.transform.position);
            var desc=new RenderTextureDescriptor(128,128){graphicsFormat=texture.graphicsFormat,depthBufferBits=0};
            var target=RenderTexture.GetTemporary(desc);var old=RenderTexture.active;var pixels=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);float maximum=0;
            try
            {
                for(int face=0;face<6;face++)
                {
                    Graphics.CopyTexture(texture,face,0,target,0,0);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0,false);
                    foreach(var color in pixels.GetPixels())maximum=Mathf.Max(maximum,Mathf.Max(color.r,Mathf.Max(color.g,color.b)));
                }
            }
            finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);}
            Require(maximum>.0001f,"Room reflection texture must contain rendered lighting");
            report.AppendLine("Probe "+probe.transform.position+" format="+texture.graphicsFormat+" maxLinearRGB="+maximum);
        }
        File.WriteAllText(Path.Combine(Evidence,"lighting-runtime.txt"),report.ToString());
        Require(delta<.0001f,"Ambient SH retained after camera render; max delta="+delta);
    }
    static void Save(ChapterPlayerUI ui,Camera camera,string name,int width,int height,bool showUI)
    {
        var canvas=ui.GetComponentInChildren<Canvas>();Require(canvas,"Canvas exists");
        bool enabled=canvas.enabled;var mode=canvas.renderMode;var canvasCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var target=new RenderTexture(width,height,24);Texture2D image=null;
        try
        {
            camera.targetTexture=target;canvas.enabled=showUI;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.1f,camera.nearClipPlane+.02f);
            Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
            camera.Render();RenderTexture.active=target;image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Evidence,name),image.EncodeToPNG());
        }
        finally
        {
            canvas.enabled=enabled;canvas.renderMode=mode;canvas.worldCamera=canvasCamera;canvas.planeDistance=plane;
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            if(image)UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
