using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

public static class TacticsLogicChecks
{
    static StringBuilder report;
    static void Require(bool condition,string text){if(!condition)throw new Exception("TACTICS_CHECK: "+text);}
    static void Near(float a,float b,string text){Require(Mathf.Abs(a-b)<.002f,text+" actual="+a+" expected="+b);}
    static void Pass(string text){report.AppendLine("PASS "+text);Debug.Log("TACTICS_CHECK_PASS "+text);}
    public static IEnumerator Ready()
    {
        int frame=Time.frameCount;while(Time.frameCount<frame+4)yield return null;
        while(true){var s=Session();if(s && s.Failure.Length>0)throw new Exception(s.Failure);if(s && s.Ready)break;yield return null;}
    }
    static ExpandedSession Session()=>UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
    static IEnumerator Reload(string path)
    {EditorSceneManager.LoadSceneInPlayMode(path,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));var ready=Ready();while(ready.MoveNext())yield return null;}
    static void Control(ExpandedSession s){s.enabled=false;s.SendMessage("OnApplicationFocus",true);s.Resume();s.player.SetActive(false);}
    static void Player(ExpandedSession s,Vector3 position)
    {s.player.controller.enabled=false;s.player.transform.SetPositionAndRotation(position,Quaternion.identity);s.player.controller.enabled=true;Physics.SyncTransforms();}
    static void Guard(ExpandedEnemy guard,Vector3 position,Vector3 face)
    {guard.SetActive(false);Require(guard.agent.Warp(position),"Guard fixture navigation");guard.transform.rotation=Quaternion.LookRotation(face);guard.Initialize();guard.SetActive(true);Physics.SyncTransforms();}
    static void ResetContract(ExpandedSession s)
    {
        Require(s && s.Ready && s.Guards.Count==2 && s.Paused && s.Outcome==RunOutcome.Running,"Two-guard initialized paused run");
        Near(s.battery.Current,40,"Reset energy");Require(s.tactics.UsedNoiseCount==0 && !s.tactics.ShutterUsed && s.tactics.shutter.State==TacticalShutterState.OpenReady,"Reset one-use devices");
        Require(s.Guards.All(g=>g.State==ExpandedEnemyState.Patrol && g.NoiseEventId==0 && g.DisruptionRemaining==0 && g.CaptureElapsed==0),"Reset both guard memories");
        Require(s.pulse.SuccessfulUses==0 && s.pulse.LastResolvedTarget==null,"Reset selected pulse target/result");
        Require(UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None).Length==1,"Unique session after reload");
    }
    public static IEnumerator Run()
    {
        report=new StringBuilder("SCRIPTED TACTICS FIXTURES: controlled actor positions, virtual keys and explicit ticks; not normal-input route acceptance.\n");
        var ready=Ready();while(ready.MoveNext())yield return null;
        try
        {
            var s=Session();ResetContract(s);Control(s);
            using(var keys=new Keys(s))
            {
                Player(s,new Vector3(-8,0,-9));Guard(s.Guards[0],new Vector3(-8,0,-6),Vector3.back);Guard(s.Guards[1],new Vector3(-6,0,-9),Vector3.left);
                foreach(var guard in s.Guards)guard.Tick(.2f);
                keys.Set();s.pulse.Tick(0);
                Require(s.pulse.SelectedTarget==s.Guards[1],"Q selects nearest valid guard rather than legacy enemy field");
                Guard(s.Guards[0],new Vector3(-8,0,-7),Vector3.back);s.Guards[0].Tick(.2f);
                Require(s.pulse.SelectedTarget==s.Guards[0],"Equal Q distance uses guard id 01");
                var wall=new GameObject("TemporaryPulseOcclusion");wall.transform.position=new Vector3(-8,1,-8);wall.AddComponent<BoxCollider>().size=new Vector3(1,2,.1f);Physics.SyncTransforms();
                Require(s.pulse.SelectedTarget==s.Guards[1],"Occluded nearest guard is skipped");UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
                float energy=s.battery.Current;keys.Set(Key.Q);s.pulse.Tick(0);Require(s.pulse.ResolvePendingRequest()==PulseUseResult.Success,"Q succeeds");
                Require(s.pulse.LastResolvedTarget==s.Guards[0] && s.Guards[0].State==ExpandedEnemyState.Disrupted && s.Guards[1].State==ExpandedEnemyState.Alert,"Only selected guard disrupted");
                Near(energy-s.battery.Current,12,"Single-target Q charges once");
                s.Pause();float cooldown=s.pulse.CooldownRemaining,stun=s.Guards[0].DisruptionRemaining;
                s.pulse.Tick(10);foreach(var guard in s.Guards)guard.Tick(10);s.tactics.Tick(10);
                Near(s.pulse.CooldownRemaining,cooldown,"Pause freezes Q");Near(s.Guards[0].DisruptionRemaining,stun,"Pause freezes each guard");
                Pass("Q nearest/unblocked/id tie, single target and fee; all-guard pause");
            }
            var reload=Reload(TacticsDelivery.ScenePath);while(reload.MoveNext())yield return null;s=Session();ResetContract(s);Control(s);
            Player(s,new Vector3(10,0,10));Guard(s.Guards[0],new Vector3(-7,0,3),Vector3.forward);Guard(s.Guards[1],new Vector3(-7,0,1.5f),Vector3.forward);
            var noise=s.tactics.noiseDevices[0];float before=s.battery.Current;
            Require(s.tactics.TryActivate(noise.interactable,out _),"Noise activates");s.tactics.Tick(.01f);
            Require(noise.Used && noise.SignalRemaining>5.9f && s.tactics.UsedNoiseCount==1,"Noise consumes once and starts six-second signal");
            foreach(var guard in s.Guards)Require(guard.State==ExpandedEnemyState.Investigate && guard.NoiseTarget==noise.investigationAnchor.position && guard.HasHandledNoise(noise.EventId),"Both nearby eligible guards investigate fixed sound point");
            Near(s.battery.Current,before,"Noise has no energy fee");Require(!s.tactics.TryActivate(noise.interactable,out _),"Used noise cannot retrigger");
            float remaining=noise.SignalRemaining;s.Pause();s.tactics.Tick(10);Near(noise.SignalRemaining,remaining,"Pause freezes sound signal");s.Resume();s.player.SetActive(false);
            s.Guards[0].Tick(10.01f);Require(s.Guards[0].State==ExpandedEnemyState.Return || s.Guards[0].State==ExpandedEnemyState.Patrol,"Investigation bounded at ten seconds");
            s.tactics.Tick(0);Require(s.Guards[0].State!=ExpandedEnemyState.Investigate,"Same event cannot re-acquire guard");
            s.tactics.Tick(6);Require(noise.State==NoiseDeviceState.Spent && noise.SignalRemaining==0,"Sound expires after six active seconds");
            Guard(s.Guards[0],noise.investigationAnchor.position,Vector3.forward);
            Require(s.Guards[0].TryInvestigateNoise(905,noise.investigationAnchor.position,12,10,2),"Arrival dwell fixture starts");
            int dwellFrame=Time.frameCount;while(Time.frameCount<dwellFrame+3)yield return null;
            s.Guards[0].Tick(1.99f);Require(s.Guards[0].State==ExpandedEnemyState.Investigate,"Investigation remains for full two-second arrival dwell");
            s.Guards[0].Tick(.02f);Require(s.Guards[0].State==ExpandedEnemyState.Return || s.Guards[0].State==ExpandedEnemyState.Patrol,"Arrival dwell finishes at two seconds");
            Pass("Two-second arrival observation");
            Guard(s.Guards[0],new Vector3(-7,0,3),Vector3.forward);Player(s,new Vector3(-7,0,5));s.Guards[0].Tick(.2f);
            Require(!s.Guards[0].TryInvestigateNoise(900,noise.investigationAnchor.position,12,10,2),"Alert ignores noise");s.Guards[0].Tick(1.01f);
            Require(s.Guards[0].State==ExpandedEnemyState.Chase && !s.Guards[0].TryInvestigateNoise(901,noise.investigationAnchor.position,12,10,2),"Visible chase is not redirected by noise");
            Require(s.Guards[0].TryDisrupt(2.5f) && !s.Guards[0].TryInvestigateNoise(902,noise.investigationAnchor.position,12,10,2),"Disrupted guard ignores noise");
            Pass("One-use free noise, both-guard investigation, fixed point, event deduplication, expiry, pause and state priority");

            // Find a real map detour for which Euclidean proximity is insufficient.
            var rangeGuard=s.Guards[1];bool rangeCase=false;
            foreach(var device in s.tactics.noiseDevices)
            {
                for(float x=-10;x<=10 && !rangeCase;x+=2)for(float z=-10;z<=10 && !rangeCase;z+=2)
                {
                    var point=new Vector3(x,0,z);var delta=point-device.investigationAnchor.position;delta.y=0;
                    if(delta.magnitude>=11.9f || !NavMesh.SamplePosition(point,out var hit,.3f,rangeGuard.agent.areaMask))continue;
                    Guard(rangeGuard,hit.position,Vector3.forward);
                    if(rangeGuard.TryGetNavigationDistance(device.investigationAnchor.position,out float length) && length>12.1f)
                    {Require(!rangeGuard.TryInvestigateNoise(903,device.investigationAnchor.position,12,10,2),"Noise uses path length, not straight distance");rangeCase=true;report.AppendLine($"RANGE straight={delta.magnitude:F2} path={length:F2}");}
                }
                if(rangeCase)break;
            }
            Require(rangeCase,"Map must provide a path-distance boundary fixture");Pass("Twelve-metre navigation-distance range");

            reload=Reload(TacticsDelivery.ScenePath);while(reload.MoveNext())yield return null;s=Session();ResetContract(s);Control(s);
            Player(s,new Vector3(10,0,10));Guard(s.Guards[0],new Vector3(-7,0,2),Vector3.forward);
            var first=s.tactics.noiseDevices[0];var secondNoise=s.tactics.noiseDevices[1];
            secondNoise.investigationAnchor.position=new Vector3(-8.5f,0,2);
            Require(s.tactics.TryActivate(first.interactable,out _) && s.tactics.TryActivate(secondNoise.interactable,out _),"Both one-use sounds may coexist");
            s.tactics.Tick(0);Require(s.Guards[0].NoiseEventId==secondNoise.EventId,"Nearest navigation sound wins among simultaneous sources");
            first.investigationAnchor.position=s.Guards[0].transform.position;s.tactics.Tick(.1f);
            Require(s.Guards[0].NoiseEventId==secondNoise.EventId,"An investigation is not continuously retargeted by another sound");
            Pass("Concurrent sound selection and investigation target stability");

            reload=Reload(TacticsDelivery.ScenePath);while(reload.MoveNext())yield return null;s=Session();ResetContract(s);Control(s);
            var shutter=s.tactics.shutter;Player(s,new Vector3(7,0,0));
            Require(!s.tactics.TryActivate(shutter.switches[0],out _) && !shutter.Used,"Player occupancy refuses close without spending use");
            Player(s,new Vector3(7,0,-1.5f));Guard(s.Guards[1],new Vector3(7,0,0),Vector3.forward);
            Require(!s.tactics.TryActivate(shutter.switches[1],out _) && !shutter.Used,"Second guard occupancy refuses close");
            Guard(s.Guards[1],new Vector3(10,0,-4),Vector3.forward);
            Require(s.tactics.TryActivate(shutter.switches[0],out _),"Vacant gate begins closing");s.tactics.Tick(.1f);
            Require(shutter.State==TacticalShutterState.Closing && !shutter.barrier.enabled && !shutter.Used,"Partial closing is visual only");
            Player(s,new Vector3(7,0,0));s.tactics.Tick(.1f);s.tactics.Tick(.4f);
            Require(shutter.State==TacticalShutterState.OpenReady && !shutter.Used && !shutter.barrier.enabled,"Entering closing safety zone cancels and preserves use");
            Player(s,new Vector3(7,0,-1.5f));Guard(s.Guards[0],new Vector3(7,0,3),Vector3.back);s.Guards[0].Tick(1.21f);
            Require(s.Guards[0].State==ExpandedEnemyState.Chase,"Memory fixture starts a visible chase");Vector3 lastSeen=s.Guards[0].LastSeen;
            before=s.battery.Current;Require(s.tactics.TryActivate(shutter.switches[1],out _),"Opposite button activates the same gate");
            Require(!s.tactics.TryActivate(shutter.switches[0],out _),"Other button cannot duplicate closing");s.tactics.Tick(.35f);Physics.SyncTransforms();
            Require(shutter.State==TacticalShutterState.Closed && shutter.Used && shutter.barrier.enabled && shutter.obstacle.enabled,"Fully closed commits one use and both barriers");
            s.Guards[0].Tick(.02f);Require(s.Guards[0].HasLastSeen && s.Guards[0].LastSeen==lastSeen && s.Guards[0].State==ExpandedEnemyState.Search,"Closing gate preserves pursuit memory rather than returning immediately");
            s.player.controller.Move(Vector3.forward*3);Require(s.player.transform.position.z<-.35f,"Closed shutter blocks actual CharacterController.Move");
            s.Pause();remaining=shutter.ClosedRemaining;s.tactics.Tick(10);Near(shutter.ClosedRemaining,remaining,"Pause freezes closed duration");s.Resume();s.player.SetActive(false);
            int frame=Time.frameCount;while(Time.frameCount<frame+5)yield return null;
            var path=new NavMeshPath();Require(NavMesh.CalculatePath(new Vector3(7,0,-1.5f),new Vector3(7,0,1.5f),NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete,"Closed gate retains alternate ring route");
            float pathLength=0;for(int i=1;i<path.corners.Length;i++)pathLength+=Vector3.Distance(path.corners[i-1],path.corners[i]);Require(pathLength>10,"Closed direct route must detour");
            s.tactics.Tick(4);Require(shutter.State==TacticalShutterState.Opening && shutter.barrier.enabled,"Four-second duration transitions to blocked opening");
            s.tactics.Tick(.34f);Require(shutter.barrier.enabled,"Opening remains physically blocked until complete");s.tactics.Tick(.02f);
            Require(shutter.State==TacticalShutterState.OpenSpent && !shutter.barrier.enabled && !shutter.obstacle.enabled && !s.tactics.TryActivate(shutter.switches[0],out _),"Fully reopened gate is spent and traversable");Near(s.battery.Current,before,"Shutter has no energy fee");
            Pass("Shutter both-side safety, closing cancellation, shared one use, physical block, alternate navigation, last-seen memory, pause and timing");

            reload=Reload(TacticsDelivery.ScenePath);while(reload.MoveNext())yield return null;s=Session();ResetContract(s);Control(s);
            Player(s,s.level.playerSpawn);Guard(s.Guards[1],s.player.transform.position+Vector3.forward*.5f,Vector3.back);
            s.Tick(.6f);s.Tick(.61f);s.Tick(.36f);Require(s.Outcome==RunOutcome.Caught && s.Paused && s.Guards.All(g=>g.agent.isStopped),"Guard 02 capture ends and freezes entire run");
            s.Restart();ready=Ready();while(ready.MoveNext())yield return null;s=Session();ResetContract(s);Pass("Second guard global capture and all-device/all-guard restart reset");
            reload=Reload(TacticsDelivery.SourceScene);while(reload.MoveNext())yield return null;
            Require(Session().Guards.Count==1 && !Session().tactics,"Old art scene uses single-guard compatibility fallback");
            using(var evidence=new LegacyEvidence())
            {
                var legacy=ChapterPulseChecks.Run();while(legacy.MoveNext())yield return legacy.Current;
                evidence.Capture();Pass("Legacy single-guard pulse checks");
            }
            reload=Reload(TacticsDelivery.ScenePath);while(reload.MoveNext())yield return null;ResetContract(Session());
            report.AppendLine("PASS all tactics logic checks");
        }
        finally{Directory.CreateDirectory(TacticsDelivery.Evidence);File.WriteAllText(Path.Combine(TacticsDelivery.Evidence,"logic.txt"),report.ToString());}
    }
    sealed class LegacyEvidence:IDisposable
    {
        readonly string path=Path.GetFullPath("Evidence/ChapterOne/pulse-checks.txt");
        readonly byte[] bytes;readonly DateTime modified;
        public LegacyEvidence(){if(File.Exists(path)){bytes=File.ReadAllBytes(path);modified=File.GetLastWriteTimeUtc(path);}}
        public void Capture(){Directory.CreateDirectory(TacticsDelivery.Evidence);if(File.Exists(path))File.Copy(path,Path.Combine(TacticsDelivery.Evidence,"legacy-pulse-checks.txt"),true);}
        public void Dispose()
        {
            if(bytes!=null){File.WriteAllBytes(path,bytes);File.SetLastWriteTimeUtc(path,modified);}
            else if(File.Exists(path))File.Delete(path); // Created only by this isolated compatibility check.
        }
    }
    sealed class Keys:IDisposable
    {
        readonly ExpandedSession session;readonly Keyboard keyboard,previous;readonly Mouse mouse,previousMouse;
        readonly string pulseOverride,interactOverride;
        public Keys(ExpandedSession s)
        {
            session=s;previous=Keyboard.current;previousMouse=Mouse.current;keyboard=InputSystem.AddDevice<Keyboard>("TacticsFixtureKeyboard");mouse=InputSystem.AddDevice<Mouse>("TacticsFixtureMouse");
            pulseOverride=s.pulse.PulseAction.bindings[0].overridePath;interactOverride=s.interaction.InteractAction.bindings[0].overridePath;
            s.pulse.PulseAction.ApplyBindingOverride(0,keyboard.qKey.path);s.interaction.InteractAction.ApplyBindingOverride(0,keyboard.eKey.path);Set();
        }
        public void Set(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();keyboard.MakeCurrent();mouse.MakeCurrent();}
        public void Dispose()
        {
            if(session){if(pulseOverride==null)session.pulse.PulseAction.RemoveBindingOverride(0);else session.pulse.PulseAction.ApplyBindingOverride(0,pulseOverride);if(interactOverride==null)session.interaction.InteractAction.RemoveBindingOverride(0);else session.interaction.InteractAction.ApplyBindingOverride(0,interactOverride);}
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(previous!=null && previous.added)previous.MakeCurrent();if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
        }
    }
}
