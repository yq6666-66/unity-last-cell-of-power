using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.ChapterTacticsPolish;
using LastCellOfPower.WarehouseExpanded;
using static PolishChecks;

// Controlled initial poses + real E/D actions. These images are presentation evidence, not route evidence.
public static class PolishPresentationChecks
{
    public static IEnumerator Run()
    {
        ResetContract(Session());var s=Session();Control(s);FarGuards(s);var ui=UI();ui.SetMasterVolume(0);
        Require(AudioListener.volume==0,"Muted inspection uses the real volume setting");
        var views=UnityEngine.Object.FindObjectsByType<DeviceReadabilityView>(FindObjectsSortMode.None);
        Require(views.Length==6,"Two control/speaker pairs and two shared shutter labels exist");
        using(var keys=new Keys(s))
        {
            var noise=s.tactics.noiseDevices[0];var p=ui.polishFeedback;
            PosePlayer(s,new Vector3(noise.interactable.Position.x,0,noise.interactable.Position.z+.85f),Quaternion.Euler(0,180,0));
            keys.Set();s.interaction.Prepare();p.Step(0);
            Require(s.interaction.Candidate==noise.interactable,"Visible control resolves from actual aim and range");
            AssertNoiseHud(ui,s,noise,false);
            AssertWorldState(views.Where(v=>v.noiseDevice==noise).ToArray(),"就绪",ui.polishProfile.readyColor);
            keys.Set(Key.E);s.interaction.Prepare();keys.Set();s.interaction.Prepare();p.Step(0);
            Require(noise.Used && p.CurrentFeedbackText.Contains("N01") && p.CurrentFeedbackText.Contains("启动"),"Muted E activation identifies the actual device and outcome");
            AssertWorldState(views.Where(v=>v.noiseDevice==noise).ToArray(),"发声中",ui.polishProfile.signallingColor);
            AssertNoiseHud(ui,s,noise,false);
            Require(noise.ResponseCount==0 && ui.VisibleInteractionText.Contains(ui.polishProfile.pendingResponseText),"Actual same-target HUD shows zero confirmed responses while signalling");
            var controlView=views.Single(v=>v.noiseDevice==noise && v.name=="N01_Control");
            Capture(ui,s.player.viewCamera,"muted-noise-activated-1280.png",1280,720,controlView);
            foreach(float distance in new[]{1.2f,1.8f})
            {
                PosePlayer(s,new Vector3(noise.interactable.Position.x,0,noise.interactable.Position.z+distance),Quaternion.Euler(0,180,0));
                keys.Set();s.interaction.Prepare();p.Step(0);
                Capture(ui,s.player.viewCamera,distance<1.5f?"world-sign-1p2m-1280.png":"world-sign-1p8m-1280.png",1280,720,controlView);
            }
            PosePlayer(s,new Vector3(noise.interactable.Position.x,0,noise.interactable.Position.z+.85f),Quaternion.Euler(0,180,0));
            keys.Set();s.interaction.Prepare();s.tactics.Tick(6);p.Step(0);
            AssertWorldState(views.Where(v=>v.noiseDevice==noise).ToArray(),"已用尽",ui.polishProfile.spentColor);
            AssertNoiseHud(ui,s,noise,true);
            Require(noise.ResponseCount==0 && ui.VisibleInteractionText.Contains(ui.polishProfile.noResponseText) && p.CurrentFeedbackText.Contains(ui.polishProfile.noResponseText),"Muted zero-response expiry remains explicit in actual HUD context and committed toast");

            // Detailed counts belong to HUD. Produce one, then two genuine AI acceptances of N02;
            // the paired physical signs only need short state text and their actual indicator change.
            var second=s.tactics.noiseDevices[1];
            PosePlayer(s,new Vector3(second.interactable.Position.x,0,second.interactable.Position.z-.85f),Quaternion.identity);
            PoseGuard(s.Guards[0],new Vector3(6,0,-8),Vector3.back);PoseGuard(s.Guards[1],new Vector3(-7,0,3),Vector3.forward);
            keys.Set();s.interaction.Prepare();Require(s.interaction.Candidate==second.interactable,"N02 HUD reads the actual aimed second control");
            keys.Set(Key.E);s.interaction.Prepare();keys.Set();s.interaction.Prepare();s.tactics.Tick(0);p.Step(0);
            Require(second.ResponseCount==1 && s.Guards[0].State==ExpandedEnemyState.Investigate && s.Guards[0].NoiseEventId==second.EventId,"One real robot accepted N02");
            AssertNoiseHud(ui,s,second,false);
            Require(ui.VisibleInteractionText.Contains(ui.polishProfile.ResponseText(1,false)) && p.CurrentFeedbackText.Contains(ui.polishProfile.ResponseText(1,false)),"Actual same-target HUD and toast show one prior response");
            PoseGuard(s.Guards[1],new Vector3(5,0,-9.8f),Vector3.back);s.tactics.Tick(0);p.Step(0);
            Require(second.ResponseCount==2 && s.Guards.All(g=>g.State==ExpandedEnemyState.Investigate && g.NoiseEventId==second.EventId),"Two distinct real robots accepted N02");
            AssertNoiseHud(ui,s,second,false);
            Require(ui.VisibleInteractionText.Contains(ui.polishProfile.ResponseText(2,false)) && p.CurrentFeedbackText.Contains(ui.polishProfile.ResponseText(2,false)),"Actual same-target HUD and toast show two prior responses");
            AssertWorldState(views.Where(v=>v.noiseDevice==second).ToArray(),"发声中",ui.polishProfile.signallingColor);
            Capture(ui,s.player.viewCamera,"muted-noise-two-responses-1280.png",1280,720);
            s.tactics.Tick(6);p.Step(0);AssertNoiseHud(ui,s,second,true);
            Require(ui.VisibleInteractionText.Contains(ui.polishProfile.ResponseText(2,true)),"Spent source HUD retains factual response history");
            FarGuards(s);

            var door=s.tactics.shutter;var button=door.switches.OrderBy(t=>t.Position.z).First();
            Vector3 start=new Vector3(5.8f,0,-.85f);Vector3 aim=button.Position-(start+Vector3.up*.9f);aim.y=0;
            PosePlayer(s,start,Quaternion.LookRotation(aim));s.player.SetActive(true);keys.Set();s.player.Tick(0);s.interaction.Prepare();
            Require(!door.IsOccupied(s) && s.interaction.Candidate==button,"Controlled shutter approach is genuinely outside the safety zone");
            keys.Set(Key.E);s.interaction.Prepare();s.tactics.Tick(.1f);
            Require(door.State==TacticalShutterState.Closing && !door.Used,"Muted E starts real closing without claiming consumption");
            AssertWorldState(views.Where(v=>v.shutter==door).ToArray(),"关闭中",ui.polishProfile.signallingColor);
            // D is camera-relative right; from this physical approach it enters the real safety zone.
            keys.Set(Key.D);s.player.Tick(.08f);s.tactics.Tick(.08f);p.Step(0);
            Require(door.State==TacticalShutterState.Opening && !door.Used,"Actual CharacterController movement cancels closing without use");
            Require(p.CurrentFeedbackText.Contains("取消") && p.CurrentFeedbackText.Contains("保留"),"Cancellation and retained use remain understandable while muted");
            Require(s.interaction.Candidate==button && p.CurrentHint.Contains("保留") && !p.CurrentHint.Contains("正在关闭"),"Same-frame cancellation context reads the reopened transition, not stale closing text");
            AssertWorldState(views.Where(v=>v.shutter==door).ToArray(),"回开中",ui.polishProfile.signallingColor);
            Capture(ui,s.player.viewCamera,"muted-shutter-cancel-1280.png",1280,720);
            s.tactics.Tick(.4f);s.player.SetActive(false);
            Require(door.State==TacticalShutterState.OpenReady && !door.Used,"Cancelled gate visibly and logically returns ready");
            AssertWorldState(views.Where(v=>v.shutter==door).ToArray(),"就绪",ui.polishProfile.readyColor);
            PosePlayer(s,start,Quaternion.LookRotation(aim));keys.Set();s.interaction.Prepare();keys.Set(Key.E);s.interaction.Prepare();s.tactics.Tick(.35f);
            Require(door.Used && door.State==TacticalShutterState.Closed,"Fresh E after release commits actual closure");
            keys.Set();s.interaction.Prepare();p.Step(0);ui.SendMessage("Update");
            Require(s.interaction.Candidate==button && ui.VisibleInteractionText==p.CurrentHint && ui.VisibleInteractionText.Contains(door.ClosedRemaining.ToString("0.0")) && ui.VisibleInteractionText.Contains(ui.polishProfile.consumedText),"Same-target closed-gate HUD supplies real countdown and committed-use information");
            AssertWorldState(views.Where(v=>v.shutter==door).ToArray(),"闭锁中",ui.polishProfile.signallingColor);
            s.tactics.Tick(4.35f);keys.Set();s.interaction.Prepare();keys.Set(Key.E);s.interaction.Prepare();p.Step(0);
            Require(p.CurrentFeedbackText.Contains("已用尽") && p.CurrentFeedbackText.Contains("未再次消耗"),"Muted repeat attempt communicates spent state and no extra use");
            AssertWorldState(views.Where(v=>v.shutter==door).ToArray(),"已用尽",ui.polishProfile.spentColor);
            keys.Set();s.interaction.Prepare();p.Step(0);ui.SendMessage("Update");
            Require(s.interaction.Candidate==button && ui.VisibleInteractionText==p.CurrentHint &&
                (ui.VisibleInteractionText.Contains("已用完") || ui.VisibleInteractionText.Contains(ui.polishProfile.spentText)) && ui.VisibleInteractionText.Contains("已打开"),
                "Actual same-target HUD retains exhausted-use and actually-open information after physical signs become short");
        }
        Pass("Muted real E activation, physical movement cancellation, later use and rejected repeat all have textual state evidence");

        var reload=Reload();while(reload.MoveNext())yield return null;s=Session();Control(s);FarGuards(s);ui=UI();ui.SetMasterVolume(0);
        using(var keys=new Keys(s))
        {
            var button=s.tactics.shutter.switches.OrderBy(t=>t.Position.z).First();
            Vector3 spot=new Vector3(5.8f,0,-1.2f);Vector3 facing=button.Position-(spot+Vector3.up*.9f);facing.y=0;
            PosePlayer(s,spot,Quaternion.LookRotation(facing));keys.Set();s.interaction.Prepare();keys.Set(Key.E);s.interaction.Prepare();s.tactics.Tick(.35f);
            Require(s.tactics.shutter.State==TacticalShutterState.Closed,"Countdown capture uses actual closed gate");
            PoseGuard(s.Guards[0],new Vector3(7,0,-3),(spot-new Vector3(7,0,-3)).normalized);
            PoseGuard(s.Guards[1],new Vector3(9,0,-2),(spot-new Vector3(9,0,-2)).normalized);
            s.Guards[0].Tick(.9f);s.Guards[1].Tick(1.21f);foreach(var guard in s.Guards)guard.agent.isStopped=true;
            Require(s.Guards[0].State==ExpandedEnemyState.Alert && s.Guards[1].State==ExpandedEnemyState.Chase,"Capture reads two real different threat states");
            Require(s.battery.Spend(20.5f),"Low-energy layout fixture spends through battery interface");
            keys.Set();s.interaction.Prepare();ui.polishFeedback.Step(0);
            var wait=Frames(2);while(wait.MoveNext())yield return null;
            Require(ui.VisibleThreatCount==2,"Both threat rows remain visible with full-length text");
            foreach(int width in new[]{960,1280,1920})Capture(ui,s.player.viewCamera,"dual-threat-countdown-"+width+".png",width,width*9/16);
            // Two rendered frames with a real mouse turn and advancing gate countdown give a limited
            // dynamic-background check; no world animation or material is frozen for these captures.
            s.player.SetActive(true);keys.Set();s.player.Tick(0);
            keys.SetMouse(new Vector2(8/s.player.sensitivity,0));s.player.Tick(.02f);s.tactics.Tick(.12f);s.player.SetActive(false);
            keys.Set();s.interaction.Prepare();
            foreach(var guard in s.Guards)guard.agent.isStopped=true;
            wait=Frames(2);while(wait.MoveNext())yield return null;
            Capture(ui,s.player.viewCamera,"dynamic-background-turn-1280.png",1280,720);
            foreach(int width in new[]{960,1280,1920})
            {
                ui.ShowHelp();wait=Frames(2);while(wait.MoveNext())yield return null;
                Require(ui.HelpRuleCardCount==3,"Help builds exactly three rule cards");
                string help=string.Join("\n",ui.GetComponentsInChildren<TextMeshProUGUI>(false).Select(t=>t.text));
                Require(help.Contains(ui.polishProfile.helpQTitle) && help.Contains(ui.polishProfile.helpNoiseTitle) && help.Contains(ui.polishProfile.helpShutterTitle),"All three actual rule cards are present");
                Capture(ui,s.player.viewCamera,"three-help-cards-"+width+".png",width,width*9/16);
                ui.ReturnToPause();s.SendMessage("OnApplicationFocus",true);ui.BeginOrContinue();foreach(var guard in s.Guards)guard.agent.isStopped=true;
            }
        }
        Pass("960/1280/1920 dual threats, long objective/context text, countdown and all three help cards; one mouse-turn background pair");
    }

    static void AssertNoiseHud(ChapterPlayerUI ui,ExpandedSession s,NoiseDevice noise,bool finished)
    {
        s.interaction.Prepare();ui.polishFeedback.Step(0);ui.SendMessage("Update");
        Require(s.interaction.Candidate==noise.interactable,"Detailed HUD must correspond to the real aimed device");
        string actual=ui.VisibleInteractionText;var label=ui.polishProfile.LabelFor(noise.deviceId);
        Require(!string.IsNullOrEmpty(actual) && actual==ui.polishFeedback.CurrentHint && actual.Contains(label.shortId) && actual.Contains(label.destination),"Actual HUD shows correct device identity and destination");
        if(noise.State==NoiseDeviceState.Ready)
            Require(actual.Contains(ui.polishProfile.readyText) && actual.Contains(ui.polishProfile.noiseReadyAction),"Ready HUD retains one-use/action information");
        else
        {
            Require(actual.Contains(ui.polishProfile.ResponseText(noise.ResponseCount,finished)),"HUD response wording matches actual accepted count and finished state");
            if(noise.State==NoiseDeviceState.Signalling)
                Require(actual.Contains(string.Format(ui.polishProfile.signallingFormat,noise.SignalRemaining)) && actual.Contains(ui.polishProfile.consumedText),"Detailed HUD retains live countdown and consumed-use information");
            else Require(actual.Contains(ui.polishProfile.spentText),"Detailed HUD retains exhausted-use state");
        }
    }
    static void AssertWorldState(DeviceReadabilityView[] views,string shortState,Color expected)
    {
        Require(views.Length==2,"Both paired physical views must exist");
        int emission=Shader.PropertyToID("_EmissionColor");
        foreach(var view in views)
        {
            view.Refresh();Require(view.CurrentStateText.Contains(shortState) && view.stateText.text==view.CurrentStateText,"World sign reflects short committed state "+shortState);
            Require(Vector4.Distance(view.CurrentIndicatorColor,expected)<.001f,"World indicator follows corresponding ready/working/spent color");
            foreach(var renderer in view.indicatorRenderers)
            {
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                Require(Vector4.Distance(block.GetColor(emission),expected*Mathf.Max(0,view.profile.indicatorEmission))<.001f,"Actual renderer emission changes with world status");
            }
        }
    }

    internal static void Capture(ChapterPlayerUI ui,Camera camera,string file,int width,int height,DeviceReadabilityView focusSign=null)
    {
        var canvas=ui.GetComponentInChildren<Canvas>();Require(canvas,"Actual player canvas exists");
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var target=new RenderTexture(width,height,24);Texture2D pixels=null;
        try
        {
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.1f,camera.nearClipPlane+.02f);
            // Refresh the real derived UI after this fixture's synchronous committed-state operations;
            // never replace TMP contents or inject a fabricated preview observation.
            ui.SendMessage("Update");
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
            camera.Render();RenderTexture.active=target;
            if(focusSign)
            {
                foreach(string name in new[]{"Title","Destination","State"})
                {
                    var label=focusSign.transform.Find(name).GetComponent<TMP_Text>();label.ForceMeshUpdate();var renderer=label.GetComponent<Renderer>();
                    Require(label.gameObject.activeInHierarchy && renderer.enabled && (camera.cullingMask&(1<<label.gameObject.layer))!=0 && !string.IsNullOrWhiteSpace(label.text),"Physical sign label is active and camera-visible: "+name);
                    Require(!label.isTextOverflowing,"Short world label must fit its own text rectangle: "+name);
                    var bounds=label.textBounds;
                    foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float y in new[]{bounds.min.y,bounds.max.y})foreach(float z in new[]{bounds.min.z,bounds.max.z})
                    {
                        var viewport=camera.WorldToViewportPoint(label.transform.TransformPoint(new Vector3(x,y,z)));
                        Require(viewport.z>camera.nearClipPlane && viewport.x>=0 && viewport.x<=1 && viewport.y>=0 && viewport.y<=1,"Physical "+name+" glyphs must remain inside viewport in "+file+" at "+viewport);
                    }
                }
                Report.AppendLine("WORLD_LABEL_VIEWPORT "+file+" Title/Destination/State glyph bounds contained; original eye height/FOV retained");
            }
            foreach(var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if(string.IsNullOrWhiteSpace(text.text))continue;
                Require(!text.isTextOverflowing,"Text overflow at "+width+"px: "+text.name+" / "+text.text);
                Require(text.font.HasCharacters(text.text,out uint[] missing,true,false),"Rendered font is missing glyphs in "+text.name);
                var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var screen=RectTransformUtility.WorldToScreenPoint(camera,corner);
                    Require(screen.x>=-2 && screen.x<=width+2 && screen.y>=-2 && screen.y<=height+2,"Text rectangle escapes viewport: "+text.name+" at "+width);
                }
            }
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            string directory=Path.Combine(Evidence,"Presentation");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,file),pixels.EncodeToPNG());
            Report.AppendLine("CAPTURE "+file+" actual camera/Canvas "+width+"x"+height+"; original canvas="+oldMode+" temporarily ScreenSpaceCamera for off-screen target; checked active TMP overflow, glyphs and viewport containment");
        }
        finally
        {
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            if(pixels)UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
