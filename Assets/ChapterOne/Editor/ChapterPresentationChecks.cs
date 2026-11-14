using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using TMPro;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.ChapterOne;

// Presentation wiring and state checks. Does not claim audible quality or human readability.
public static class ChapterPresentationChecks
{
    const string VolumeKey="LastCell.ChapterOne.MasterVolume";
    const string SensitivityKey="LastCell.ChapterOne.Sensitivity";
    public static string LastResult {get;private set;}="NOT_RUN";
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("CHAPTER_PRESENTATION_FAIL: "+message);}
    static IEnumerator Frames(int count){int frame=Time.frameCount;while(Time.frameCount<frame+count)yield return null;}
    static void Keys(Keyboard keyboard,Mouse mouse,params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.zero,delta=Vector2.zero});
        InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();
    }

    public static IEnumerator Run()
    {
        Require(EditorApplication.isPlaying,"PlayMode required");
        var report=new StringBuilder("SCRIPTED PRESENTATION CHECKS: synthetic inputs, simulated focus and enemy position fixture. No audible-quality or real-desktop acceptance claim.\n");
        LastResult="RUNNING";
        bool hadVolume=PlayerPrefs.HasKey(VolumeKey),hadSensitivity=PlayerPrefs.HasKey(SensitivityKey);
        float storedVolume=PlayerPrefs.GetFloat(VolumeKey),storedSensitivity=PlayerPrefs.GetFloat(SensitivityKey);
        float listenerVolume=AudioListener.volume;
        Keyboard priorKeyboard=Keyboard.current;Mouse priorMouse=Mouse.current;
        Keyboard keyboard=null;Mouse mouse=null;
        InputActionAsset uiActions=null;ReadOnlyArray<InputDevice>? savedUiDevices=null;
        ExpandedSession session=null;ChapterPlayerUI ui=null;
        float priorPlayerSensitivity=0;
        bool originalSessionEnabled=true;
        double deadline=EditorApplication.timeSinceStartup+30;
        try
        {
            while(true)
            {
                session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
                if(session && session.Failure.Length>0)throw new Exception(session.Failure);
                if(session && session.Ready && ui && ui.LayoutBuilt)break;
                Require(EditorApplication.timeSinceStartup<deadline,"UI initialization timeout");yield return null;
            }
            var wait=Frames(3);while(wait.MoveNext())yield return null;
            Require(session.Paused && ui.ModalVisible,"Initial modal keeps simulation paused");
            Require(session.externalPlayerUI,"Explicit player UI mode enabled");
            priorPlayerSensitivity=session.player.sensitivity;originalSessionEnabled=session.enabled;
            Require(ui.font && ui.font.HasCharacters("最后一格电充电设备区出口应急干扰警觉设置主音量鼠标灵敏度逃生成功被抓住电量耗尽",out uint[] missing,true,true),"Bundled font contains critical Chinese UI glyphs");
            var signs=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.renderMode==RenderMode.WorldSpace).ToArray();
            Require(signs.Length>=8,"At least eight world-space region signs");
            foreach(var sign in signs)
            {
                var labels=sign.GetComponentsInChildren<TMP_Text>(true);
                Require(labels.Length>0 && labels.All(t=>t.font==ui.font),"Signs use bundled Chinese font");
                Require(!sign.GetComponent<GraphicRaycaster>(),"World signs do not intercept UI input");
            }
            report.AppendLine("PASS bundled Chinese glyphs, world-space sign references and initial modal");

            var filledImages=ui.GetComponentsInChildren<Image>(true).Where(x=>x.type==Image.Type.Filled).ToArray();
            Require(filledImages.Length>=2 && filledImages.All(x=>x.sprite),"Filled bars require a valid sprite to generate filled geometry");
            var energyImage=filledImages.Single(x=>x.transform.parent.name=="EnergyTrack");
            var awarenessImage=filledImages.Single(x=>x.transform.parent.name=="ThreatTrack");
            Require(Mathf.Abs(energyImage.fillAmount-session.battery.Current/session.rules.maximumEnergy)<.001f,"Energy fill reflects actual percentage");
            report.AppendLine("PASS filled-image sprites and initial energy percentage");

            var visual=UnityEngine.Object.FindFirstObjectByType<ChapterVisualFeedback>();
            Require(visual && visual.highlightBindings!=null,"Device highlight bindings exist");
            foreach(var target in session.interaction.targets)
            {
                var binding=visual.highlightBindings.SingleOrDefault(x=>x!=null && x.target==target);
                Require(binding!=null && binding.renderers!=null,"Each interactable has a device highlight binding");
                Require(binding.renderers.Where(r=>r).Distinct().Count()>1,"Device highlight covers model renderers, not only one marker");
                Require(binding.renderers.All(r=>r && !r.transform.IsChildOf(target.anchor)),"Device binding does not substitute the standalone anchor marker");
            }
            Require(visual.HighlightedRendererCount==0,"Paused initial view has no residual device highlight");
            report.AppendLine("PASS every interactable binds multiple actual device renderers");

            keyboard=InputSystem.AddDevice<Keyboard>("ChapterPresentationKeyboard");mouse=InputSystem.AddDevice<Mouse>("ChapterPresentationMouse");
            var module=UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Require(module && module.actionsAsset,"UI input module has an action asset");
            uiActions=module.actionsAsset;savedUiDevices=uiActions.devices;uiActions.devices=new InputDevice[]{keyboard,mouse};
            session.player.MoveAction.actionMap.devices=new InputDevice[]{keyboard};
            session.interaction.InteractAction.ApplyBindingOverride(keyboard.eKey.path);
            session.pulse.PulseAction.ApplyBindingOverride(keyboard.qKey.path);
            Keys(keyboard,mouse);
            session.SendMessage("OnApplicationFocus",true);
            ui.ShowSettings();
            Require(ui.Mode==ChapterViewMode.Settings && session.Paused,"Settings remain paused");
            ui.GetComponentsInChildren<Slider>(true).Single(x=>x.gameObject.name=="Volume" && x.gameObject.activeInHierarchy).value=.31f;
            ui.GetComponentsInChildren<Slider>(true).Single(x=>x.gameObject.name=="Sensitivity" && x.gameObject.activeInHierarchy).value=1.4f;
            Require(session.Paused && ui.Mode==ChapterViewMode.Settings,"Changing settings must not resume or hide modal");
            Require(Mathf.Abs(AudioListener.volume-.31f)<.001f,"Volume setting applied");
            // A click at the screen origin is outside every actionable modal control.
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.zero,buttons=1});InputSystem.Update();
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(session.Paused && ui.Mode==ChapterViewMode.Settings,"Unrelated left click does not capture mouse/resume");
            Keys(keyboard,mouse);
            ui.ReturnToPause();Require(ui.Mode==ChapterViewMode.Paused && session.Paused,"Settings returns to pause");
            ui.ShowHelp();Require(ui.Mode==ChapterViewMode.Help && session.Paused,"Help is modal");
            ui.ReturnToPause();Require(session.Paused,"Help returns without resuming");
            report.AppendLine("PASS modal navigation and settings changes cannot resume gameplay");

            // Avoid batchmode cursor/focus callbacks overriding intentional logic commands.
            session.enabled=false;session.SendMessage("OnApplicationFocus",true);
            ui.GetComponentsInChildren<Button>(true).Single(x=>x.gameObject.name=="继续游戏" && x.gameObject.activeInHierarchy).onClick.Invoke();
            Require(!session.Paused && ui.Mode==ChapterViewMode.Playing,"Explicit continue command resumes");
            Keys(keyboard,mouse);session.Tick(.01f);
            wait=Frames(2);while(wait.MoveNext())yield return null;
            var audio=UnityEngine.Object.FindFirstObjectByType<ChapterAudioFeedback>();
            Require(audio,"Audio feedback component exists");
            var motor=session.enemy.GetComponentsInChildren<AudioSource>().Single(a=>a.gameObject.name=="GuardMotor");
            Require(motor.loop && motor.clip==audio.patrolLoop,"Running Patrol selects configured motor loop");
            ui.ShowSettings();
            Require(session.Paused && audio.PlayingGameplaySources==0,"Pause stops/pauses all gameplay sources");
            ui.ReturnToPause();session.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();
            Keys(keyboard,mouse);session.Tick(.01f);
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(motor.loop && motor.clip==audio.patrolLoop,"Resume restores appropriate patrol-loop selection");
            report.AppendLine("PASS explicit resume, pause audio policy and loop selection (not audible quality)");

            var enemy=session.enemy;
            Require(enemy.agent.Warp(session.player.transform.position+Vector3.forward*2),"Enemy test fixture on navigation");
            enemy.agent.transform.rotation=Quaternion.Euler(0,180,0);enemy.Initialize();enemy.SetActive(true);Physics.SyncTransforms();
            enemy.Tick(.1f);Require(enemy.State==ExpandedEnemyState.Alert,"Fixture enters actual Alert");
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(Mathf.Abs(awarenessImage.fillAmount-enemy.Awareness01)<.001f,"Awareness fill follows the actual Alert value");
            session.pulse.SetActive(true);Keys(keyboard,mouse);session.pulse.Tick(0);
            int before=audio.PlayedPulseCueCount;
            Keys(keyboard,mouse,Key.Q);session.pulse.Tick(.01f);
            Require(session.pulse.ResolvePendingRequest()==PulseUseResult.Success,"Actual valid pulse emits success");
            Require(audio.PlayedPulseCueCount==before+1,"Exactly one pulse sound event");
            Keys(keyboard,mouse,Key.Q);session.pulse.Tick(.1f);session.pulse.ResolvePendingRequest();
            Require(audio.PlayedPulseCueCount==before+1,"Held Q does not duplicate pulse sound");
            Keys(keyboard,mouse);
            report.AppendLine("PASS success cue emitted once and held-Q duplicate suppression");

            session.battery.Tick(1000,false,false);session.Tick(.01f);
            Require(session.Outcome==RunOutcome.Depleted && session.Paused && session.Summary!=null,"Actual outcome freezes a summary");
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(ui.Mode==ChapterViewMode.Results,"Result event opens results modal");
            var summary=ui.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.gameObject.name=="Summary" && t.gameObject.activeInHierarchy);
            Require(summary && summary.text.Contains("本局用时") && summary.text.Contains("剩余电量") && summary.text.Contains("成功干扰"),"Result fields rendered from frozen summary");
            Require(summary.text.Contains(session.Summary.RemainingEnergy.ToString("0.0")) && session.Summary.SuccessfulPulseCount>=1,"Summary values match actual run");
            Require(audio.PlayingGameplaySources==0,"Ending leaves no gameplay loop running");
            ui.SkipResultFade();
            report.AppendLine("PASS real outcome results page and gameplay-audio termination");
            LastResult="PASS";
            Debug.Log("CHAPTER_PRESENTATION_CHECKS_PASS");
        }
        finally
        {
            if(session)
            {
                session.Pause();session.enabled=originalSessionEnabled;
                if(session.player && priorPlayerSensitivity>0)session.player.sensitivity=priorPlayerSensitivity;
            }
            if(uiActions)uiActions.devices=savedUiDevices;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(priorKeyboard!=null && priorKeyboard.added)priorKeyboard.MakeCurrent();
            if(priorMouse!=null && priorMouse.added)priorMouse.MakeCurrent();
            if(hadVolume)PlayerPrefs.SetFloat(VolumeKey,storedVolume);else PlayerPrefs.DeleteKey(VolumeKey);
            if(hadSensitivity)PlayerPrefs.SetFloat(SensitivityKey,storedSensitivity);else PlayerPrefs.DeleteKey(SensitivityKey);
            PlayerPrefs.Save();AudioListener.volume=listenerVolume;
            if(LastResult!="PASS")LastResult="FAIL";
            report.AppendLine(LastResult+" presentation checks; user settings restored in finally");
            string directory=Path.GetFullPath("Evidence/ChapterOne");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"presentation-checks.txt"),report.ToString());
        }
    }
}
