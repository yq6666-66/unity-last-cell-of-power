using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.ChapterTacticsPolish;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

/// <summary>Scoped feedback regression. Posed fixtures and unwarped input routes have separate reports.</summary>
public static class PolishChecks
{
    public const string ScenePath="Assets/ChapterTacticsPolish/Scenes/WarehouseTacticsPolish.unity";
    public static string Evidence=>Path.GetFullPath("Evidence/ChapterTacticsPolish");
    const string ReferenceSha="8693e66d50f40b70f95fec3f77f3a0322ef08eb3";
    internal static StringBuilder Report;
    static int passed;

    [CliCommand("polish","verify","Check committed device feedback, real response counts, lifecycle and narrow legacy fallback")]
    public static Task<string> Verify()=>Launch("verify",Logic);
    [CliCommand("polish","presentation","Capture readable tactical HUD/help at three resolutions and muted E feedback")]
    public static Task<string> Presentation()=>Launch("presentation",PolishPresentationChecks.Run);
    [CliCommand("polish","route","Run one uninterrupted virtual-input route with move cancellation, pause and device feedback")]
    public static Task<string> Route()=>Launch("route",PolishRouteChecks.Run);

    static Task<string> Launch(string group,Func<IEnumerator> factory)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Start checks in edit mode");
        Require(!EditorSceneManager.GetActiveScene().isDirty,"Preserve unsaved scene before checks");
        Require(File.Exists(ScenePath),"Generate the polish scene first");
        EditorSceneManager.OpenScene(ScenePath);
        var completion=new TaskCompletionSource<string>();
        bool oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;var oldOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        passed=0;Report=new StringBuilder("POLISH "+group.ToUpperInvariant()+"\n");
        Report.AppendLine("Reference: yq6666-66/game-development-plugins@"+ReferenceSha);
        Report.AppendLine("Read: game-playloop-validation/SKILL.md; state-and-test-gates.md; recovery-and-audio-information.md; async-and-divergence.md.");
        Report.AppendLine("Adopted: input -> committed state -> feedback, observer-before-action, explicit async outcomes, muted information checks. Not applicable: saves, new systems, other engines, deployment.");
        Report.AppendLine(group=="route"?"EVIDENCE: synthetic keyboard/mouse, real player controller, simulated focus; no pose setters/warp; not a first-time player or desktop acceptance.":"EVIDENCE: disposable PlayMode fixtures with controlled actor poses and explicit ticks; not normal-input route or subjective player acceptance.");
        Directory.CreateDirectory(Evidence);
        IEnumerator routine=null;Exception failure=null;bool finishing=false;double deadline=EditorApplication.timeSinceStartup+(group=="route"?180:240);
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
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Polish "+group+" bounded deadline exceeded");
                if(!EditorApplication.isPlaying)return;
                EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=Context(factory);
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            if(!finishing)failure=new Exception("PlayMode ended before assertions completed");
            string result=failure==null?"PASS polish "+group+"; completed groups="+passed:"FAIL polish "+group+"; completed groups="+passed+"\n"+failure;
            Report.AppendLine(result);File.WriteAllText(Path.Combine(Evidence,group+"-checks.txt"),Report.ToString());
            if(failure==null)completion.TrySetResult(result);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;EditorApplication.EnterPlaymode();return completion.Task;
    }
    static IEnumerator Context(Func<IEnumerator> factory)
    {
        var ready=Ready();while(ready.MoveNext())yield return null;
        var original=InputSystem.settings;string json=JsonUtility.ToJson(original);
        var fixture=UnityEngine.Object.Instantiate(original);fixture.hideFlags=HideFlags.HideAndDontSave;
        fixture.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        fixture.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=fixture;
        bool hadVolume=PlayerPrefs.HasKey("LastCell.ChapterOne.MasterVolume");float volume=PlayerPrefs.GetFloat("LastCell.ChapterOne.MasterVolume",.7f),listener=AudioListener.volume;
        try{var work=factory();try{while(work.MoveNext())yield return work.Current;}finally{(work as IDisposable)?.Dispose();}}
        finally
        {
            if(!original){original=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(json,original);}
            InputSystem.settings=original;UnityEngine.Object.DestroyImmediate(fixture);
            if(hadVolume)PlayerPrefs.SetFloat("LastCell.ChapterOne.MasterVolume",volume);else PlayerPrefs.DeleteKey("LastCell.ChapterOne.MasterVolume");
            AudioListener.volume=listener;
        }
    }

    internal static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("POLISH_CHECK: "+message);}
    internal static void Near(float actual,float expected,string message,float epsilon=.003f)=>Require(Mathf.Abs(actual-expected)<=epsilon,message+" actual="+actual+" expected="+expected);
    internal static void Pass(string message){passed++;Report.AppendLine("PASS "+message);Debug.Log("POLISH_CHECK_PASS "+message);}
    internal static ExpandedSession Session()=>UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
    internal static ChapterPlayerUI UI()=>UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
    internal static IEnumerator Frames(int count){int start=Time.frameCount;while(Time.frameCount<start+count)yield return null;}
    internal static IEnumerator Ready()
    {
        var wait=Frames(4);while(wait.MoveNext())yield return null;
        while(true)
        {
            var s=Session();var ui=UI();
            if(s && s.Failure.Length>0)throw new Exception(s.Failure);
            if(s && s.Ready && ui && ui.LayoutBuilt && ui.Mode!=ChapterViewMode.Loading)yield break;
            yield return null;
        }
    }
    internal static IEnumerator Reload(string path=ScenePath)
    {
        EditorSceneManager.LoadSceneInPlayMode(path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        var ready=Ready();while(ready.MoveNext())yield return null;
    }
    internal static void Control(ExpandedSession s)
    {
        s.enabled=false;s.SendMessage("OnApplicationFocus",true);UI().BeginOrContinue();Require(!s.Paused,"Explicit UI begin/continue starts fixture");s.player.SetActive(false);
        var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();if(module)module.enabled=false;
    }
    internal static void PosePlayer(ExpandedSession s,Vector3 position,Quaternion? rotation=null)
    {
        s.player.controller.enabled=false;s.player.transform.SetPositionAndRotation(position,rotation??Quaternion.identity);
        s.player.viewCamera.transform.localRotation=Quaternion.identity;s.player.controller.enabled=true;Physics.SyncTransforms();
    }
    internal static void PoseGuard(ExpandedEnemy guard,Vector3 position,Vector3 facing)
    {
        guard.SetActive(false);Require(guard.agent.Warp(position),"Controlled guard pose must be on existing navigation");
        guard.transform.rotation=Quaternion.LookRotation(facing);guard.Initialize();guard.SetActive(true);Physics.SyncTransforms();
    }
    internal static void FarGuards(ExpandedSession s)
    {PoseGuard(s.Guards[0],new Vector3(10,0,10),Vector3.forward);PoseGuard(s.Guards[1],new Vector3(10,0,-10),Vector3.back);}
    internal static void ResetContract(ExpandedSession s)
    {
        Require(s.Ready && s.Guards.Count==2 && s.Paused && s.Outcome==RunOutcome.Running,"Fresh polish scene state");
        Require(s.tactics && !s.tactics.HasFeedback && s.tactics.LastFeedback.Sequence==0,"Fresh feedback observation is empty");
        Require(s.tactics.UsedNoiseCount==0 && s.tactics.noiseDevices.All(n=>!n.Used && n.ResponseCount==0 && n.EventId==0),"Noise uses/responses reset");
        Require(!s.tactics.ShutterUsed && s.tactics.shutter.State==TacticalShutterState.OpenReady,"Shutter reset");
        Near(s.battery.Current,40,"Gameplay starting resource unchanged");
        Require(UI().polishProfile && UI().polishFeedback,"Actual polish profile/presenter required; fixtures do not install missing production setup");
    }

    static IEnumerator Logic()
    {
        ResetContract(Session());
        for(int wanted=0;wanted<=2;wanted++)
        {
            if(wanted>0){var reload=Reload();while(reload.MoveNext())yield return null;}
            var s=Session();Control(s);PosePlayer(s,new Vector3(10,0,10));FarGuards(s);
            if(wanted>=1)PoseGuard(s.Guards[0],new Vector3(-7,0,3),Vector3.forward);
            if(wanted>=2)PoseGuard(s.Guards[1],new Vector3(-7,0,1.5f),Vector3.forward);
            var noise=s.tactics.noiseDevices[0];
            using(var observe=new Observations(s.tactics))
            {
                float energy=s.battery.Current;
                Require(s.tactics.TryActivate(noise.interactable,out _),"Noise activation accepted");
                observe.ExpectLast(DeviceFeedbackKind.NoiseActivated,noise.deviceId,true,0);
                s.tactics.Tick(0);
                Require(noise.ResponseCount==wanted,"Only actual successful guard acceptances count: "+wanted);
                Require(observe.Events.Count(e=>e.Kind==DeviceFeedbackKind.NoiseResponseAccepted)==wanted,"One observation per unique accepted robot");
                Require(s.Guards.Count(g=>g.State==ExpandedEnemyState.Investigate && g.NoiseEventId==noise.EventId)==wanted,"Response count matches committed investigation states");
                for(int i=0;i<5;i++)s.tactics.Tick(0);
                Require(noise.ResponseCount==wanted && observe.Events.Count(e=>e.Kind==DeviceFeedbackKind.NoiseResponseAccepted)==wanted,"Repeated ticks do not duplicate the response");
                Require(noise.Used && s.tactics.UsedNoiseCount==1,"Zero responses also consume exactly one device");Near(s.battery.Current,energy,"No fee for environment");
                if(wanted>0)
                {
                    s.Guards[0].Tick(10);s.tactics.Tick(0);
                    Require(noise.ResponseCount==wanted && s.Guards[0].State!=ExpandedEnemyState.Investigate,"Same event cannot accept again after investigation returns");
                }
                if(wanted==2)
                {
                    var p=UI().polishFeedback;
                    Require(!s.tactics.TryActivate(noise.interactable,out _),"Used source publishes a real rejection");
                    int rejected=p.DisplayedFeedbackSequence;string rejection=p.CurrentFeedbackText;
                    Require(rejected==s.tactics.LastFeedback.Sequence && p.FeedbackRemaining>0 && p.FeedbackRemaining<=3,"Failure feedback owns the three-second slot");
                    Require(s.tactics.TryActivate(s.tactics.noiseDevices[1].interactable,out _),"Independent second source actually commits while rejection remains");
                    Require(p.LastFeedbackSequence==s.tactics.LastFeedback.Sequence && p.DisplayedFeedbackSequence==rejected && p.CurrentFeedbackText==rejection,"Lower priority success is observed but does not replace the unread rejection");
                    p.Step(3.01f);p.Step(0);
                    Require(string.IsNullOrEmpty(p.CurrentFeedbackText) && p.DisplayedFeedbackSequence==0,"Suppressed success is discarded, not replayed from a delayed queue");
                }
                observe.Monotonic();
            }
            Pass("Committed noise response count "+wanted+" with independent actual AI acceptance and deduplication");
        }
        var next=Reload();while(next.MoveNext())yield return null;
        var session=Session();Control(session);FarGuards(session);
        PosePlayer(session,new Vector3(-7,0,5));PoseGuard(session.Guards[0],new Vector3(-7,0,3),Vector3.forward);
        session.Guards[0].Tick(1.21f);Require(session.Guards[0].State==ExpandedEnemyState.Chase,"Actual visual chase fixture");
        Vector3 memory=session.Guards[0].LastSeen;var source=session.tactics.noiseDevices[0];
        using(var observe=new Observations(session.tactics))
        {
            Require(session.tactics.TryActivate(source.interactable,out _),"Priority sound activates");session.tactics.Tick(0);
            Require(source.ResponseCount==0 && session.Guards[0].State==ExpandedEnemyState.Chase,"Visible pursuit takes priority over sound");
            PosePlayer(session,new Vector3(10,0,10));session.tactics.Tick(.01f);
            Require(source.ResponseCount==1 && session.Guards[0].State==ExpandedEnemyState.Investigate && session.Guards[0].HasConfirmedPlayer,"Still-live signal accepted after real loss of sight");
            Require(session.Guards[0].LastSeen==memory,"Sound does not overwrite visual memory");
            session.tactics.Tick(6);observe.ExpectLast(DeviceFeedbackKind.NoiseEnded,source.deviceId,true,1);
            PoseGuard(session.Guards[1],new Vector3(-7,0,1.5f),Vector3.forward);session.tactics.Tick(0);
            Require(source.ResponseCount==1 && session.Guards[1].State!=ExpandedEnemyState.Investigate,"Expired signal cannot accept a newly eligible guard");observe.Monotonic();
        }
        Pass("Sight priority, later real acceptance, expiry and unchanged player memory");

        next=Reload();while(next.MoveNext())yield return null;
        FeedbackDurationBounds();

        next=Reload();while(next.MoveNext())yield return null;session=Session();Control(session);FarGuards(session);
        var door=session.tactics.shutter;
        using(var observe=new Observations(session.tactics))
        {
            PosePlayer(session,new Vector3(7,0,0));Require(!session.tactics.TryActivate(door.switches[0],out _),"Occupied gate rejected");
            observe.ExpectLast(DeviceFeedbackKind.ShutterRejectedOccupied,door.deviceId,false,0);Require(!door.Used,"Rejected occupancy preserves use");
            PosePlayer(session,new Vector3(7,0,-1.5f));Require(session.tactics.TryActivate(door.switches[0],out _),"Vacant gate starts");
            observe.ExpectLast(DeviceFeedbackKind.ShutterClosing,door.deviceId,false,0);session.tactics.Tick(.1f);
            PosePlayer(session,new Vector3(7,0,0));session.tactics.Tick(.01f);
            observe.ExpectLast(DeviceFeedbackKind.ShutterCancelledOccupied,door.deviceId,false,0);
            session.tactics.Tick(.4f);Require(door.State==TacticalShutterState.OpenReady && !door.Used,"Cancelled close returns ready without consumption");
            PosePlayer(session,new Vector3(7,0,-1.5f));Require(session.tactics.TryActivate(door.switches[1],out _),"Other physical switch controls same gate");
            Require(!session.tactics.TryActivate(door.switches[0],out _),"Busy gate rejects duplicate start");observe.ExpectLast(DeviceFeedbackKind.ShutterRejectedBusy,door.deviceId,false,0);
            session.tactics.Tick(.35f);observe.ExpectLast(DeviceFeedbackKind.ShutterClosed,door.deviceId,true,0);
            Require(door.Used && door.barrier.enabled && door.obstacle.enabled && session.statistics.ShutterUsed,"Closing feedback follows committed physical/use state");
            session.tactics.Tick(4);Require(door.State==TacticalShutterState.Opening && door.barrier.enabled,"Open transition does not prematurely report traversable");
            session.tactics.Tick(.35f);observe.ExpectLast(DeviceFeedbackKind.ShutterOpened,door.deviceId,true,0);
            Require(door.State==TacticalShutterState.OpenSpent && !door.barrier.enabled,"Opened observation means actually traversable");
            Require(!session.tactics.TryActivate(door.switches[1],out _),"Spent gate refuses repeat");observe.ExpectLast(DeviceFeedbackKind.ShutterRejectedUsed,door.deviceId,true,0);observe.Monotonic();
        }
        Pass("Occupied rejection, closing cancellation, busy rejection, committed close/open and spent feedback agree with use state");

        next=Reload();while(next.MoveNext())yield return null;
        var lifecycle=FeedbackLifecycle();while(lifecycle.MoveNext())yield return lifecycle.Current;
        next=Reload();while(next.MoveNext())yield return null;
        var disable=DisableFallback();while(disable.MoveNext())yield return disable.Current;
        foreach(string legacy in new[]{"Assets/ChapterOneArt/Scenes/WarehouseChapterOneArtRealism.unity","Assets/ChapterTactics/Scenes/WarehouseTactics.unity"})
        {
            next=Reload(legacy);while(next.MoveNext())yield return null;
            var s=Session();var ui=UI();Require(!ui.polishProfile && !ui.polishFeedback,"Legacy scene has no new profile/presenter: "+legacy);
            Require(s.Guards.Count==(legacy.Contains("ChapterTactics/")?2:1),"Legacy guard fallback preserved");
            s.enabled=false;s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();Require(!s.Paused && ui.Mode==ChapterViewMode.Playing,"Legacy explicit resume still works");
            ui.ShowHelp();Require(s.Paused && ui.Mode==ChapterViewMode.Help,"Legacy help fallback still works");
            Pass("Narrow compatibility: "+Path.GetFileNameWithoutExtension(legacy));
        }
        next=Reload();while(next.MoveNext())yield return null;ResetContract(Session());
    }

    static void FeedbackDurationBounds()
    {
        var s=Session();Control(s);FarGuards(s);var ui=UI();var presenter=ui.polishFeedback;
        Near(ui.polishProfile.successToastSeconds,2,"Duration-bound fixture requires the actual default success duration",.00001f);
        Near(ui.polishProfile.failureToastSeconds,3,"Duration-bound fixture requires the actual default rejection duration",.00001f);
        Require(ui.PolishFeedbackActive && !s.Paused,"Duration-bound fixture uses the connected, playing HUD");
        var noise=s.tactics.noiseDevices[0];
        using(var observe=new Observations(s.tactics))
        {
            // All four boundary probes execute synchronously in this one fixture step: there is no
            // yield, no Tactics.Tick and no new AI/device event between activation and each expiry.
            // Only the real presentation clock advances; Update refreshes its actual HUD consumer.
            Require(s.tactics.TryActivate(noise.interactable,out _),"Real successful activation starts the timing probe");
            observe.ExpectLast(DeviceFeedbackKind.NoiseActivated,noise.deviceId,true,0);
            int successSequence=s.tactics.LastFeedback.Sequence;string success=presenter.CurrentFeedbackText;
            ui.SendMessage("Update");Require(!string.IsNullOrEmpty(success) && ui.VisibleFeedbackText==success,"Actual HUD initially shows the committed success");
            presenter.Step(1.99f);ui.SendMessage("Update");
            Require(observe.Events.Count==1 && presenter.DisplayedFeedbackSequence==successSequence && ui.VisibleFeedbackText==success,"Success remains on the actual HUD at 1.99 active seconds");
            presenter.Step(.02f);ui.SendMessage("Update");
            Require(observe.Events.Count==1 && string.IsNullOrEmpty(ui.VisibleFeedbackText) && presenter.DisplayedFeedbackSequence==0,"Success is cleared from the actual HUD at 2.01 active seconds");

            Require(!s.tactics.TryActivate(noise.interactable,out _),"Real used-device rejection starts the failure timing probe");
            observe.ExpectLast(DeviceFeedbackKind.NoiseRejectedUsed,noise.deviceId,true,0);
            int rejectedSequence=s.tactics.LastFeedback.Sequence;string rejected=presenter.CurrentFeedbackText;
            ui.SendMessage("Update");Require(!string.IsNullOrEmpty(rejected) && ui.VisibleFeedbackText==rejected,"Actual HUD initially shows the committed rejection");
            presenter.Step(2.99f);ui.SendMessage("Update");
            Require(observe.Events.Count==2 && presenter.DisplayedFeedbackSequence==rejectedSequence && ui.VisibleFeedbackText==rejected,"Rejection remains on the actual HUD at 2.99 active seconds");
            presenter.Step(.02f);ui.SendMessage("Update");
            Require(observe.Events.Count==2 && string.IsNullOrEmpty(ui.VisibleFeedbackText) && presenter.DisplayedFeedbackSequence==0,"Rejection is cleared from the actual HUD at 3.01 active seconds");
            observe.Monotonic();
        }
        Pass("Actual HUD success visible at 1.99s/cleared at 2.01s and rejection visible at 2.99s/cleared at 3.01s; default 2/3s and no competing observations");
    }

    static IEnumerator FeedbackLifecycle()
    {
        var s=Session();Control(s);FarGuards(s);var ui=UI();var presenter=ui.polishFeedback;
        var noise=s.tactics.noiseDevices[0];
        using(var keys=new Keys(s))
        {
            PosePlayer(s,new Vector3(noise.interactable.Position.x,0,noise.interactable.Position.z+.85f),Quaternion.Euler(0,180,0));
            keys.Set();s.interaction.Prepare();presenter.Step(0);
            Require(PolishFeedbackPresenter.NoiseTutorialSeen && !string.IsNullOrEmpty(presenter.CurrentTutorialText),"Safe live candidate shows one real first-use lesson");
            string tutorial=presenter.CurrentTutorialText;float tutorialTime=presenter.TutorialRemaining;
            s.Pause();presenter.Step(5);Require(presenter.CurrentTutorialText==tutorial,"Pause preserves unread lesson text");Near(presenter.TutorialRemaining,tutorialTime,"Pause freezes lesson duration");
            Require(string.IsNullOrEmpty(ui.VisibleTutorialText) && string.IsNullOrEmpty(ui.VisibleFeedbackText),"Paused HUD hides, rather than displays frozen gameplay messages");
            s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();presenter.Step(.1f);Require(presenter.CurrentTutorialText==tutorial,"Resume continues the existing lesson");
            keys.Set();s.interaction.Prepare();keys.Set(Key.E);s.interaction.Prepare();
            Require(noise.Used && !string.IsNullOrEmpty(presenter.CurrentFeedbackText),"Real E creates visible committed feedback");
            string text=presenter.CurrentFeedbackText;float time=presenter.FeedbackRemaining;int sequence=presenter.DisplayedFeedbackSequence;
            s.Pause();float signal=noise.SignalRemaining;s.tactics.Tick(5);presenter.Step(5);
            Require(presenter.CurrentFeedbackText==text && presenter.DisplayedFeedbackSequence==sequence,"Pause preserves current observation identity");Near(presenter.FeedbackRemaining,time,"Pause freezes unread toast");Near(noise.SignalRemaining,signal,"Pause freezes source clock");
            s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();presenter.Step(.1f);Require(presenter.CurrentFeedbackText==text,"Resume continues same toast");
            float remaining=presenter.FeedbackRemaining;s.SendMessage("OnApplicationFocus",false);presenter.Step(5);s.tactics.Tick(5);
            Require(s.Paused && presenter.CurrentFeedbackText==text,"Focus loss hides gameplay without discarding unread feedback");Near(presenter.FeedbackRemaining,remaining,"Focus loss freezes remaining feedback time");
            s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();keys.Set();s.player.SetActive(false);
            PoseGuard(s.Guards[0],new Vector3(-7,0,3),Vector3.forward);PoseGuard(s.Guards[1],new Vector3(-7,0,1.5f),Vector3.forward);PosePlayer(s,new Vector3(10,0,10));s.tactics.Tick(0);
            Require(noise.ResponseCount==2,"Lifecycle reset starts from genuine two-response state");
            s.battery.Tick(1000,false,false);s.Tick(0);
            Require(s.Outcome==RunOutcome.Depleted && string.IsNullOrEmpty(presenter.CurrentFeedbackText) && string.IsNullOrEmpty(presenter.CurrentTutorialText),"Real outcome clears transient messages");
            Require(s.Summary.UsedNoiseCount==1 && noise.ResponseCount==2,"Outcome preserves committed summary/use data");
            keys.Set(Key.R);s.SendMessage("Update");
            var ready=Ready();while(ready.MoveNext())yield return null;
            ResetContract(Session());Require(PolishFeedbackPresenter.NoiseTutorialSeen,"R preserves app-session lesson seen flag");
            Require(string.IsNullOrEmpty(UI().polishFeedback.CurrentFeedbackText) && string.IsNullOrEmpty(UI().polishFeedback.CurrentTutorialText),"R does not replay old observation or lesson");
            Require(UnityEngine.Object.FindObjectsByType<PolishFeedbackPresenter>(FindObjectsSortMode.None).Length==1,"One feedback presenter after actual R branch");
        }
        Pass("Unread feedback/tutorial freeze on pause/focus loss, resume continuity, actual result clearing and R reset without repeated app lesson");
    }

    static IEnumerator DisableFallback()
    {
        var s=Session();Control(s);FarGuards(s);var ui=UI();var presenter=ui.polishFeedback;var noise=s.tactics.noiseDevices[0];
        using(var keys=new Keys(s))
        {
            PosePlayer(s,new Vector3(noise.interactable.Position.x,0,noise.interactable.Position.z+.85f),Quaternion.Euler(0,180,0));
            keys.Set();s.interaction.Prepare();presenter.Step(0);presenter.enabled=false;
            Require(!ui.PolishFeedbackActive && string.IsNullOrEmpty(presenter.CurrentFeedbackText) && presenter.FeedbackRemaining==0,"Disabling presenter clears only its transient presentation");
            keys.Set(Key.E);s.interaction.Prepare();keys.Set();s.interaction.Prepare();
            Require(noise.Used && noise.State==NoiseDeviceState.Signalling,"Device still commits through actual E with presenter disabled");
            var wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(ui.VisibleInteractionText==s.interaction.Hint && !string.IsNullOrEmpty(ui.VisibleInteractionText),"Disabled presenter falls back to existing truthful interaction hint");
            presenter.enabled=true;presenter.Step(0);
            Require(presenter.LastFeedbackSequence==s.tactics.LastFeedback.Sequence && string.IsNullOrEmpty(presenter.CurrentFeedbackText),"Re-enabling synchronizes sequence without replaying historical toast");
            var view=UnityEngine.Object.FindObjectsByType<DeviceReadabilityView>(FindObjectsSortMode.None).First(v=>v.noiseDevice==noise);
            view.Refresh();Require(view.indicatorRenderers.Length>0,"Configured physical state lamp exists");
            var renderer=view.indicatorRenderers[0];var material=renderer.sharedMaterial;
            Require(renderer.HasPropertyBlock(),"Active state lamp uses its real property block");
            view.enabled=false;
            Require(view.CurrentStateText=="" && view.stateText.text=="" && !renderer.HasPropertyBlock() && renderer.sharedMaterial==material,"Disabling physical state view restores original neutral renderer and clears dynamic words");
            Require(noise.Used && noise.State==NoiseDeviceState.Signalling,"Presentation disable does not reset gameplay use");
            view.enabled=true;view.Refresh();Require(view.CurrentStateText.Contains("发声中"),"Re-enabled physical view reads current committed state");
        }
        Pass("Presenter disabled fallback, historical-event suppression on enable, and physical-view neutral restore without changing device state");
    }

    internal sealed class Observations:IDisposable
    {
        readonly TacticsDevices source;
        public readonly List<DeviceFeedback> Events=new List<DeviceFeedback>();
        public Observations(TacticsDevices devices){source=devices;source.Feedback+=Observe;}
        void Observe(DeviceFeedback value)
        {Events.Add(value);Report.AppendLine($"EVENT frame={value.Frame} sequence={value.Sequence} device={value.DeviceId} kind={value.Kind} consumed={value.Consumed} responses={value.ResponseCount}");}
        public void ExpectLast(DeviceFeedbackKind kind,string id,bool consumed,int responses)
        {
            Require(Events.Count>0,"Expected observable committed/rejected event");var value=Events[Events.Count-1];
            Require(value.Kind==kind && value.DeviceId==id && value.Consumed==consumed && value.ResponseCount==responses,"Observation matches real result: "+kind);
            Require(source.HasFeedback && source.LastFeedback.Sequence==value.Sequence,"LastFeedback agrees with actual published event");
        }
        public void Monotonic(){for(int i=1;i<Events.Count;i++)Require(Events[i].Sequence>Events[i-1].Sequence && Events[i].Frame>=Events[i-1].Frame,"Event sequence strictly increases without rewinding frames");}
        public void Dispose(){if(!ReferenceEquals(source,null))source.Feedback-=Observe;}
    }

    internal sealed class Keys:IDisposable
    {
        readonly ExpandedSession session;readonly Keyboard keyboard,previousKeyboard;readonly Mouse mouse,previousMouse;
        readonly string pulseOverride,interactionOverride;readonly ReadOnlyArray<InputDevice>? previousMoveDevices;
        public Keys(ExpandedSession value)
        {
            session=value;previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            keyboard=InputSystem.AddDevice<Keyboard>("PolishFixtureKeyboard");mouse=InputSystem.AddDevice<Mouse>("PolishFixtureMouse");
            pulseOverride=session.pulse.PulseAction.bindings[0].overridePath;interactionOverride=session.interaction.InteractAction.bindings[0].overridePath;
            previousMoveDevices=session.player.MoveAction.actionMap.devices;session.player.MoveAction.actionMap.devices=new InputDevice[]{keyboard};
            session.pulse.PulseAction.ApplyBindingOverride(0,keyboard.qKey.path);session.interaction.InteractAction.ApplyBindingOverride(0,keyboard.eKey.path);Set();
        }
        public void Set(params Key[] keys)=>SetMouse(Vector2.zero,keys);
        public void SetMouse(Vector2 delta,params Key[] keys)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState{delta=delta});InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();}
        public void Dispose()
        {
            if(session)
            {
                session.player.MoveAction.actionMap.devices=previousMoveDevices;
                if(pulseOverride==null)session.pulse.PulseAction.RemoveBindingOverride(0);else session.pulse.PulseAction.ApplyBindingOverride(0,pulseOverride);
                if(interactionOverride==null)session.interaction.InteractAction.RemoveBindingOverride(0);else session.interaction.InteractAction.ApplyBindingOverride(0,interactionOverride);
            }
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
        }
    }
}
