using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterOneArt;
using LastCellOfPower.WarehouseExpanded;

// Continuous render evidence at controlled poses, deliberately not a gameplay route test.
public static class WallFixMotionCapture
{
    const int FramesPerSecond=30,Width=1280,Height=720,Frames=360;
    [Serializable] sealed class Segment
    {
        public string label;public float duration=1.5f;public Vector3 from,to,lookFrom,lookTo;
        public Segment(string label,Vector3 from,Vector3 to,Vector3 lookFrom,Vector3 lookTo)
        {this.label=label;this.from=from;this.to=to;this.lookFrom=lookFrom;this.lookTo=lookTo;}
    }
    [Serializable] sealed class Manifest
    {
        public string scope="Continuous fixed-pose PlayMode camera render; gameplay frozen, lights/SSAO/post remain enabled. Same trajectories/FOV and deterministic atmosphere stepping before/after. Not a player-input route.";
        public string beforeScene=WallFixDelivery.SourceScene,afterScene=WallFixDelivery.ScenePath;
        public int fps=FramesPerSecond,width=Width,height=Height,framesPerScene=Frames;
        public float secondsPerScene=12,fov;
        public bool roofHidden=false;
        public Segment[] segments;
    }
    public static Task<string> Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Motion capture requires edit mode");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Preserve unsaved scene before capture");
        if(!File.Exists(WallFixDelivery.SourceScene)||!File.Exists(WallFixDelivery.ScenePath))throw new Exception("Both wall comparison scenes must exist");
        EditorSceneManager.OpenScene(WallFixDelivery.SourceScene);
        var result=new TaskCompletionSource<string>();
        bool oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;var oldOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        IEnumerator routine=null;Exception failure=null;bool finishing=false;
        double deadline=EditorApplication.timeSinceStartup+285;
        EditorApplication.CallbackFunction update=null;Action<PlayModeStateChange> changed=null;
        void Finish(Exception error)
        {
            if(finishing)return;finishing=true;failure=error;
            try{(routine as IDisposable)?.Dispose();}catch(Exception e){if(failure==null)failure=e;}
            EditorApplication.update-=update;EditorApplication.ExitPlaymode();
        }
        update=()=>
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Wall motion capture exceeded 285 seconds");
                if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=Capture();
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            if(!finishing)failure=new Exception("Motion PlayMode exited early");
            Directory.CreateDirectory(WallFixDelivery.Evidence);
            string text=failure==null?"PASS captured matching continuous before/after sequences: 360 frames each, 1280x720, 30 fps, 12 seconds each. Footage must still be visually reviewed for temporal wall artifacts; this is not gameplay acceptance.":"FAIL "+failure;
            File.WriteAllText(Path.Combine(WallFixDelivery.Evidence,"motion-result.txt"),text);
            if(failure==null)result.TrySetResult(text);else result.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;EditorApplication.EnterPlaymode();return result.Task;
    }
    static Segment[] Trajectory()
    {
        var neFar=new Vector3(3.4f,.9f,7.8f);var neNear=new Vector3(1.6f,.9f,7.8f);var neSide=new Vector3(2.8f,.9f,6.5f);var northEnd=new Vector3(0,.9f,8.6f);
        return new[]{
            new Segment("NE_north_end_forward",neFar,neNear,northEnd,northEnd),
            new Segment("NE_north_end_backward",neNear,neFar,northEnd,northEnd),
            new Segment("NE_north_end_strafe",neFar,neSide,northEnd,northEnd),
            new Segment("NE_north_end_turn",neSide,neSide,northEnd,new Vector3(1,.9f,10)),
            new Segment("south_connection",new Vector3(-2.4f,.9f,-6.8f),new Vector3(-1.2f,.9f,-7.6f),new Vector3(0,.9f,-8.6f),new Vector3(0,.9f,-8.6f)),
            new Segment("west_connection",new Vector3(-7.8f,.9f,-2.4f),new Vector3(-6.5f,.9f,-1.1f),new Vector3(-8.6f,.9f,0),new Vector3(-8.6f,.9f,0)),
            new Segment("east_connection",new Vector3(7.8f,.9f,-1.7f),new Vector3(6.3f,.9f,-1.1f),new Vector3(5.4f,.9f,0),new Vector3(5.4f,.9f,0)),
            new Segment("exit_left_jamb",new Vector3(8.7f,.9f,10.7f),new Vector3(7.3f,.9f,11.3f),new Vector3(6.8f,.9f,12.16f),new Vector3(6.8f,.9f,12.16f))
        };
    }
    static IEnumerator Ready()
    {
        int frame=Time.frameCount;while(Time.frameCount<frame+3)yield return null;
        while(true)
        {
            var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
            if(session && session.Failure.Length>0)throw new Exception(session.Failure);
            if(session && session.Ready && ui && ui.LayoutBuilt && ui.Mode!=ChapterViewMode.Loading)break;
            yield return null;
        }
    }
    static IEnumerator Capture()
    {
        var manifest=new Manifest{segments=Trajectory()};
        string motionRoot=Path.Combine(WallFixDelivery.Evidence,"Motion");Directory.CreateDirectory(motionRoot);
        foreach(string version in new[]{"before","after"})
        {
            if(version=="after")EditorSceneManager.LoadSceneInPlayMode(WallFixDelivery.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            var ready=Ready();while(ready.MoveNext())yield return null;
            var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
            var camera=session.player.viewCamera;var atmosphere=UnityEngine.Object.FindFirstObjectByType<ChapterArtAtmosphere>();
            var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
            var canvas=ui.GetComponentInChildren<Canvas>();
            var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            bool oldSession=session.enabled,oldAtmosphere=atmosphere&&atmosphere.enabled,oldCanvas=canvas&&canvas.enabled,oldModule=module&&module.enabled,oldAudioPause=AudioListener.pause;
            var oldTarget=camera.targetTexture;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldFov=camera.fieldOfView;
            if(version=="before")manifest.fov=oldFov;
            if(Mathf.Abs(oldFov-manifest.fov)>.0001f)throw new Exception("Source/fixed camera FOV differs");
            string directory=Path.Combine(motionRoot,version);Directory.CreateDirectory(directory);
            if(Directory.GetFiles(directory,"frame_*.png").Length>0)throw new Exception("Motion output exists; preserve prior sequence before recapture: "+directory);
            var texture=new RenderTexture(Width,Height,24);var pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            try
            {
                if(module)module.enabled=false;if(canvas)canvas.enabled=false;AudioListener.pause=true;
                if(atmosphere)atmosphere.enabled=false;
                session.enabled=false;session.SendMessage("OnApplicationFocus",true);session.Resume();
                session.player.SetActive(false);foreach(var guard in session.Guards)guard.SetActive(false);
                if(session.pulse)session.pulse.SetActive(false);if(session.tactics)session.tactics.SetActive(false);
                camera.targetTexture=texture;camera.fieldOfView=manifest.fov;
                for(int frame=0;frame<Frames;frame++)
                {
                    float t=frame/(float)FramesPerSecond;int index=Mathf.Min(manifest.segments.Length-1,Mathf.FloorToInt(t/1.5f));
                    var segment=manifest.segments[index];float fraction=Mathf.Clamp01((t-index*1.5f)/segment.duration);
                    var position=Vector3.Lerp(segment.from,segment.to,fraction);var look=Vector3.Lerp(segment.lookFrom,segment.lookTo,fraction);
                    camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position));
                    if(atmosphere)atmosphere.Step(1f/FramesPerSecond);
                    camera.Render();var oldActive=RenderTexture.active;
                    try{RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0,false);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,"frame_"+frame.ToString("D6")+".png"),pixels.EncodeToPNG());}
                    finally{RenderTexture.active=oldActive;}
                    yield return null;
                }
            }
            finally
            {
                camera.targetTexture=oldTarget;camera.fieldOfView=oldFov;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                if(canvas)canvas.enabled=oldCanvas;if(module)module.enabled=oldModule;AudioListener.pause=oldAudioPause;
                session.Pause();session.enabled=oldSession;if(atmosphere)atmosphere.enabled=oldAtmosphere;
                texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        File.WriteAllText(Path.Combine(motionRoot,"manifest.json"),JsonUtility.ToJson(manifest,true));
    }
}
