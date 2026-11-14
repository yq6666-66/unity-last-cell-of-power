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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

// Posed presentation fixtures, NOT normal-input route or subjective audio acceptance.
// Uses a disposable PlayMode scene and virtual keys; never saves scene/config assets.
public static class TacticsPresentationChecks
{
    static string Evidence=>Path.Combine(TacticsDelivery.Evidence,"Presentation");
    static StringBuilder report;
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("TACTICS_PRESENTATION: "+message);}
    static void Pass(string message){report.AppendLine("PASS "+message);Debug.Log("TACTICS_PRESENTATION_PASS "+message);}
    static IEnumerator Frames(int count){int frame=Time.frameCount;while(Time.frameCount<frame+count)yield return null;}

    [CliCommand("tactics","presentation","Capture two-guard HUD and check environmental-audio pause/resume in disposable PlayMode")]
    public static Task<string> Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Start in edit mode");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene before presentation fixture");
        EditorSceneManager.OpenScene(TacticsDelivery.ScenePath);TacticsDelivery.Inspect();
        var completion=new TaskCompletionSource<string>();
        bool oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;var oldOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        report=new StringBuilder("TACTICS PRESENTATION FIXTURES. Controlled player/guard poses, explicit state ticks and virtual keys; screenshots are layout evidence, not normal gameplay routes. Audio assertions cover source playback state, not subjective audible quality. No scene/config save.\n");
        Directory.CreateDirectory(Evidence);
        IEnumerator routine=null;Exception failure=null;bool finishing=false;
        double deadline=EditorApplication.timeSinceStartup+150;
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
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Presentation exceeded 150 seconds");
                if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=Checks();
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            if(!finishing)failure=new Exception("Presentation PlayMode ended early");
            string summary=failure==null?"PASS dual-guard HUD, selected Q id, Chinese glyphs, device hints, result fields and repeated audio pause/resume; off-screen posed evidence only.":"FAIL "+failure;
            report.AppendLine(summary);File.WriteAllText(Path.Combine(Evidence,"presentation-checks.txt"),report.ToString());
            if(failure==null)completion.TrySetResult(summary);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;EditorApplication.EnterPlaymode();return completion.Task;
    }

    static IEnumerator Checks()
    {
        var wait=TacticsLogicChecks.Ready();while(wait.MoveNext())yield return null;
        var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
        while(!ui || !ui.LayoutBuilt || ui.Mode==ChapterViewMode.Loading)yield return null;
        Require(session.Guards.Count==2 && session.tactics && session.Paused,"Fresh two-guard tactics scene");
        var audio=UnityEngine.Object.FindFirstObjectByType<ChapterAudioFeedback>();
        var visuals=UnityEngine.Object.FindFirstObjectByType<ChapterVisualFeedback>();
        wait=Frames(2);while(wait.MoveNext())yield return null;
        Require(audio && audio.GuardAudioCount==2 && visuals && visuals.GuardStatusLightCount==2,"Two independent guard audio/light bindings");
        Require(ui.font.HasCharacters("巡逻机警觉追击正在被发现尽快遮断视线诱导扬声器装置使用隔断门已使用未使用本局一次不耗电最近有效目标临时封门",out uint[] missing,true,true),"Bundled dynamic CJK font includes all new vocabulary");
        Pass("Two guard bindings and new CJK glyph coverage");

        var originalInput=InputSystem.settings;string originalJson=JsonUtility.ToJson(originalInput);
        var fixtureInput=UnityEngine.Object.Instantiate(originalInput);fixtureInput.hideFlags=HideFlags.HideAndDontSave;
        fixtureInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        fixtureInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=fixtureInput;
        var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();bool moduleEnabled=module && module.enabled;
        bool sessionEnabled=session.enabled;
        if(module)module.enabled=false;
        Keys keys=null;
        try
        {
            session.enabled=false;keys=new Keys(session);Resume(session,ui);
            Player(session,new Vector3(-8,0,-9),Quaternion.Euler(0,22.5f,0));
            Guard(session.Guards[0],new Vector3(-8,0,-6),session.player.transform.position);
            Guard(session.Guards[1],new Vector3(-6,0,-7),session.player.transform.position);
            session.Guards[0].Tick(.3f);session.Guards[1].Tick(.3f);session.Guards[1].Tick(1.01f);
            foreach(var guard in session.Guards)guard.agent.isStopped=true;
            Require(session.Guards[0].State==ExpandedEnemyState.Alert && session.Guards[1].State==ExpandedEnemyState.Chase,"Posed actual Alert and Chase states");
            keys.Set();session.pulse.Tick(0);
            wait=Frames(3);while(wait.MoveNext())yield return null;
            Require(ui.VisibleThreatCount==2,"Both visible threats have separate rows");
            Require(session.pulse.SelectedTarget==session.Guards[1] && ui.DisplayedPulseTargetId==session.Guards[1].guardId,"Q HUD identifies closest valid guard 02");
            Require(ActiveText(ui,"ThreatText").text.Contains("01") && ActiveText(ui,"SecondThreatText").text.Contains("02"),"Both threat ids are rendered");
            var fills=ui.GetComponentsInChildren<Image>(true).Where(x=>x.type==Image.Type.Filled).ToArray();
            Require(Mathf.Abs(fills.Single(x=>x.transform.parent.name=="ThreatTrack").fillAmount-session.Guards[0].Awareness01)<.001f,"Guard 01 bar follows its own awareness");
            Require(Mathf.Abs(fills.Single(x=>x.transform.parent.name=="SecondThreatTrack").fillAmount-session.Guards[1].Awareness01)<.001f,"Guard 02 bar follows its own awareness");
            Capture(ui,session.player.viewCamera,"dual-threats-960.png",960,540);
            Capture(ui,session.player.viewCamera,"dual-threats-1280.png",1280,720);
            var motors=session.Guards.Select(g=>g.GetComponentsInChildren<AudioSource>().Single(s=>s.name=="GuardMotor_"+g.guardId)).ToArray();
            var signals=session.Guards.Select(g=>g.GetComponentsInChildren<AudioSource>().Single(s=>s.name=="GuardSignals_"+g.guardId)).ToArray();
            Require(motors.All(s=>s.spatialBlend==1) && signals.All(s=>s.spatialBlend==1),"Each guard has distinct fully spatial sources");
            Require(motors.Sum(s=>s.volume)<=.2801f && signals.Sum(s=>s.volume)<=.5501f,"Two guards share the original aggregate gain budget");
            Pass("Two independent threat rows at 960/1280, Q target 02 and spatial audio gain cap");

            int pulseCues=audio.PlayedPulseCueCount;
            keys.Set(Key.Q);session.pulse.Tick(0);Require(session.pulse.ResolvePendingRequest()==PulseUseResult.Success,"Actual Q pulse resolves");
            Require(session.pulse.LastResolvedTarget==session.Guards[1] && audio.PlayedPulseCueCount==pulseCues+1,"Success sound follows resolved guard 02 exactly once");
            keys.Set();wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(ui.VisibleThreatCount==1,"Disrupted guard disappears from dual threat rows");
            Require(ActiveText(ui,"Toast").text.Contains("02"),"Pulse toast uses resolved target id");
            Pass("Q actual result, cue count and no persistent disrupted-target direction row");

            // This fixture repositions actors only to inspect UI. Normal-input
            // routes are covered by the separate tactics route commands.
            foreach(var guard in session.Guards){guard.Initialize();guard.SetActive(false);}
            var noise=session.tactics.noiseDevices[0];
            Player(session,noise.interactable.Position+new Vector3(0,-noise.interactable.Position.y,.85f),Quaternion.Euler(0,180,0));
            keys.Set();session.interaction.Prepare();
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(session.interaction.Candidate==noise.interactable && session.interaction.Hint.Contains("诱导"),"Noise control supplies live candidate/hint");
            Capture(ui,session.player.viewCamera,"noise-hint-1280.png",1280,720);
            keys.Set(Key.E);session.interaction.Prepare();keys.Set();session.interaction.Prepare();
            Require(noise.Used && session.tactics.UsedNoiseCount==1,"Noise activated through real E edge");
            wait=Frames(3);while(wait.MoveNext())yield return null;
            var noiseAudio=noise.soundAnchor.GetComponentsInChildren<AudioSource>().Single(a=>a.name=="NoiseSignal_"+noise.deviceId);
            Require(noiseAudio.clip && noiseAudio.loop && noiseAudio.isPlaying,"Environmental sound starts as a spatial loop");
            Capture(ui,session.player.viewCamera,"noise-working-1280.png",1280,720);
            for(int cycle=1;cycle<=2;cycle++)
            {
                float remaining=noise.SignalRemaining;
                session.Pause();session.tactics.Tick(.5f);
                wait=Frames(2);while(wait.MoveNext())yield return null;
                Require(!noiseAudio.isPlaying && audio.PlayingGameplaySources==0,"Pause cycle "+cycle+" stops all gameplay playback");
                Require(Mathf.Abs(noise.SignalRemaining-remaining)<.001f,"Pause cycle "+cycle+" freezes signal time");
                Resume(session,ui);keys.Set();
                foreach(var guard in session.Guards)guard.agent.isStopped=true;
                wait=Frames(3);while(wait.MoveNext())yield return null;
                Require(noiseAudio.isPlaying && noiseAudio.loop && noiseAudio.clip,"Resume cycle "+cycle+" restores the existing environmental source");
            }
            Pass("Noise live Hint/real E activation and source playback resumes after two pause cycles");

            var shutter=session.tactics.shutter;
            var button=shutter.switches[1];Player(session,button.Position+new Vector3(0,-button.Position.y,.9f),Quaternion.Euler(0,180,0));
            keys.Set();session.interaction.Prepare();wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(session.interaction.Candidate==button && session.interaction.Hint.Contains("封门"),"Shutter control supplies live Hint");
            Capture(ui,session.player.viewCamera,"shutter-hint-1280.png",1280,720);
            keys.Set(Key.E);session.interaction.Prepare();keys.Set();
            Require(shutter.State==TacticalShutterState.Closing,"Shutter starts through actual E edge");
            session.tactics.Tick(session.tactics.profile.shutterClosingSeconds+.01f);
            Require(shutter.Used && shutter.State==TacticalShutterState.Closed,"Completed closure records use");
            session.battery.Tick(10000,false,false);session.Tick(0);
            wait=Frames(3);while(wait.MoveNext())yield return null;ui.SkipResultFade();
            Require(session.Outcome==RunOutcome.Depleted && ui.Mode==ChapterViewMode.Results,"Actual depletion opens results");
            Require(session.Summary.UsedNoiseCount==1 && session.Summary.ShutterUsed,"Frozen summary holds actual device results");
            string summary=ActiveText(ui,"Summary").text;
            Require(summary.Contains("诱导装置使用  1 次") && summary.Contains("隔断门  已使用"),"Both environmental result fields shown");
            Require(audio.PlayingGameplaySources==0,"Result stops every gameplay audio source");
            Capture(ui,session.player.viewCamera,"results-1280.png",1280,720);
            Pass("Shutter Hint/actual E closure, immutable environmental summary and result audio stop");
        }
        finally
        {
            if(session){session.Pause();session.enabled=sessionEnabled;}
            keys?.Dispose();if(module)module.enabled=moduleEnabled;
            if(!originalInput){originalInput=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(originalJson,originalInput);}
            InputSystem.settings=originalInput;UnityEngine.Object.DestroyImmediate(fixtureInput);
        }
    }

    static void Resume(ExpandedSession session,ChapterPlayerUI ui)
    {
        session.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
        Require(!session.Paused,"Explicit UI command resumes fixture");session.player.SetActive(false);
    }
    static void Player(ExpandedSession session,Vector3 position,Quaternion rotation)
    {
        session.player.controller.enabled=false;session.player.transform.SetPositionAndRotation(position,rotation);
        session.player.viewCamera.transform.localRotation=Quaternion.identity;session.player.controller.enabled=true;Physics.SyncTransforms();
    }
    static void Guard(ExpandedEnemy guard,Vector3 position,Vector3 target)
    {
        guard.SetActive(false);Require(guard.agent.Warp(position),"Guard pose on navigation");
        Vector3 direction=target-position;direction.y=0;guard.transform.rotation=Quaternion.LookRotation(direction);
        guard.Initialize();guard.SetActive(true);Physics.SyncTransforms();
    }
    static TextMeshProUGUI ActiveText(ChapterPlayerUI ui,string name)
    {
        var text=ui.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.name==name && t.gameObject.activeInHierarchy);
        Require(text,"Visible UI text "+name);return text;
    }
    static void Capture(ChapterPlayerUI ui,Camera camera,string name,int width,int height)
    {
        var canvas=ui.GetComponentInChildren<Canvas>();Require(canvas,"Player canvas exists");
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var target=new RenderTexture(width,height,24);Texture2D pixels=null;
        try
        {
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
            canvas.planeDistance=Mathf.Max(.1f,camera.nearClipPlane+.02f);Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
            camera.Render();RenderTexture.active=target;
            foreach(var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(string.IsNullOrEmpty(text.text))continue;
                Require(!text.isTextOverflowing,"Text overflow at "+width+" px: "+text.name+" / "+text.text);
                Require(text.font.HasCharacters(text.text,out uint[] missing,true,true),"No missing glyphs in "+text.name);
            }
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(Evidence,name),pixels.EncodeToPNG());
            report.AppendLine("CAPTURE "+name+" "+width+"x"+height+"; no TMP overflow/missing glyphs");
        }
        finally
        {
            canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
    sealed class Keys:IDisposable
    {
        readonly ExpandedSession session;readonly Keyboard keyboard,previousKeyboard;readonly Mouse mouse,previousMouse;
        readonly string pulseOverride,interactionOverride;
        public Keys(ExpandedSession value)
        {
            session=value;previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            keyboard=InputSystem.AddDevice<Keyboard>("TacticsPresentationKeyboard");mouse=InputSystem.AddDevice<Mouse>("TacticsPresentationMouse");
            pulseOverride=session.pulse.PulseAction.bindings[0].overridePath;interactionOverride=session.interaction.InteractAction.bindings[0].overridePath;
            session.pulse.PulseAction.ApplyBindingOverride(0,keyboard.qKey.path);session.interaction.InteractAction.ApplyBindingOverride(0,keyboard.eKey.path);Set();
        }
        public void Set(params Key[] keys)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();}
        public void Dispose()
        {
            if(session)
            {
                if(pulseOverride==null)session.pulse.PulseAction.RemoveBindingOverride(0);else session.pulse.PulseAction.ApplyBindingOverride(0,pulseOverride);
                if(interactionOverride==null)session.interaction.InteractAction.RemoveBindingOverride(0);else session.interaction.InteractAction.ApplyBindingOverride(0,interactionOverride);
            }
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
        }
    }
}
