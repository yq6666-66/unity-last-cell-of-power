using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.StealthSample;
using LastCellOfPower.WarehouseExpanded;

// Synthetic, coordinate-controlled core checks. These are NOT desktop input evidence.
public static class ChapterPulseChecks
{
    public static string EvidenceDirectory {get;set;}
    public static string LastResult {get;private set;}="NOT_RUN";
    static StringBuilder report;
    static void Require(bool value,string label){if(!value)throw new Exception("CHAPTER_PULSE_CHECK: "+label);}
    static void Near(float value,float expected,string label,float tolerance=.003f)
        =>Require(Mathf.Abs(value-expected)<=tolerance,label+" actual="+value+" expected="+expected);
    static void Pass(string label){report.AppendLine("PASS "+label);Debug.Log("CHAPTER_PULSE_CHECK_PASS "+label);}

    public static IEnumerator Run()
    {
        Require(Application.isPlaying,"PlayMode required");
        var session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        Require(session && session.Ready && session.Paused && session.pulse && session.statistics,"Initialized paused Chapter session required");
        report=new StringBuilder("SCRIPTED CORE LOGIC: virtual devices, controlled actor coordinates, public enemy timing and session Tick. Not normal input acceptance.\n");
        LastResult="RUNNING";
        try
        {
            using(var f=new Fixture(session))
            {
                InputGate(f);
                EnergyAndCapture(f);
                EligibleTargets(f);
                InteractionTransaction(f);
                DisruptionRecovery(f);
                Pass("Statistics count successful releases only and exclude paused simulation time");
            }
            LastResult="PASS";report.AppendLine("PASS all Chapter pulse checks finished");
        }
        catch(Exception e){LastResult="FAIL";report.AppendLine(e.ToString());throw;}
        finally
        {
            var dir=string.IsNullOrEmpty(EvidenceDirectory)?Path.GetFullPath("Evidence/ChapterOne"):EvidenceDirectory;Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"pulse-checks.txt"),report.ToString());
        }
        yield return null;
        Debug.Log("CHAPTER_PULSE_CORE_PASS; parent runner must reload the scene before further gameplay");
    }

    static void InputGate(Fixture f)
    {
        f.Configure(40,2,ExpandedEnemyState.Chase);
        int uses=f.Pulse.SuccessfulUses;
        f.Session.Pause();f.Keys(Key.Q);
        f.Session.Resume();f.Session.Tick(0);
        Require(f.Pulse.WaitingForRelease,"Same-update zero after Enable cannot release Q gate");
        InputSystem.Update();f.Session.Tick(0);
        Require(f.Pulse.PulseAction.IsPressed() && f.Pulse.WaitingForRelease,"Held Q after deferred state check stays blocked");
        Near(f.Battery.Current,40,"Held resume does not spend");
        f.Keys();f.Session.Tick(0);
        Require(!f.Pulse.WaitingForRelease,"Observed release clears Q gate");
        f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.SuccessfulUses==uses+1,"Fresh edge releases once");
        Near(f.Battery.Current,28,"Single fee");
        // Advance public cooldown without an input update: a stale edge must not fire again.
        f.Pulse.Tick(20);f.Pulse.ResolvePendingRequest();
        Require(f.Pulse.SuccessfulUses==uses+1,"Same input generation cannot repeat after cooldown");
        InputSystem.Update();f.Pulse.Tick(0);f.Pulse.ResolvePendingRequest();
        Require(f.Pulse.SuccessfulUses==uses+1,"Held Q on subsequent update cannot repeat");
        Require(f.Session.statistics.SuccessfulPulseCount==1,"One successful use counted");
        Pass("Q deferred-release gate, fresh edge and held-key deduplication");
    }

    static void EnergyAndCapture(Fixture f)
    {
        f.Configure(12.999f,2,ExpandedEnemyState.Chase);
        f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.InsufficientEnergy,"12.999 rejects");
        Near(f.Battery.Current,12.999f,"Rejected fee unchanged");Near(f.Pulse.CooldownRemaining,0,"Rejected fee no cooldown");
        Require(f.Session.statistics.SuccessfulPulseCount==0,"Rejected request not counted");

        f.Configure(13,.5f,ExpandedEnemyState.Chase);
        f.Enemy.Tick(.34f);
        Require(f.Enemy.CaptureElapsed>.33f && f.Enemy.State==ExpandedEnemyState.Chase,"Capture nearly confirmed before Q");
        f.Keys(Key.Q);f.Session.Tick(.02f);
        Require(f.Session.Outcome==RunOutcome.Running && f.Enemy.State==ExpandedEnemyState.Disrupted,"Q resolves before same-frame capture");
        Near(f.Battery.Current,.996f,"13 pays12 plus normal frame drain");
        Near(f.Enemy.CaptureElapsed,0,"Pulse clears capture confirmation");
        Require(f.Session.statistics.SuccessfulPulseCount==1,"Successful rescue counted once");
        Pass("13-energy threshold, zero-cost rejection and same-frame capture rescue");
    }

    static void EligibleTargets(Fixture f)
    {
        f.Configure(40,2,ExpandedEnemyState.Patrol);
        f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.NoValidTarget,"Patrol not eligible");Near(f.Battery.Current,40,"Patrol rejection no fee");
        f.Configure(40,4.01f,ExpandedEnemyState.Chase);
        f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.NoValidTarget,"Outside four metres rejected");Near(f.Battery.Current,40,"Range rejection no fee");
        f.Configure(40,3.99f,ExpandedEnemyState.Chase);
        f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.Success,"Inside four metres accepted");

        f.Configure(40,2,ExpandedEnemyState.Chase);
        f.BlockSight();f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.NoValidTarget,"Wall blocks pulse");Near(f.Battery.Current,40,"Occlusion rejection no fee");
        Near(f.Pulse.CooldownRemaining,0,"Occlusion rejection no cooldown");
        Require(f.Enemy.State==ExpandedEnemyState.Search,"Fixture lost normal sight into Search");
        f.Keys();f.Session.Tick(0);f.Keys(Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.NoValidTarget,"Search not eligible");
        Pass("Only eligible threats, exact range neighbourhood and physical occlusion");
    }

    static void InteractionTransaction(Fixture f)
    {
        f.Configure(40,5,ExpandedEnemyState.Chase);f.InstallCharger();
        f.Keys(Key.E);f.Session.Tick(0);
        Require(f.Interaction.Charging && f.Session.player.HorizontalMovementLocked,"Charger fixture active");
        f.Keys(Key.E,Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.NoValidTarget && f.Interaction.Charging,"Invalid Q leaves E charging intact");
        Near(f.Battery.Current,40,"No-target Q does not spend");
        f.Session.rules.initialEnergy=12.999f;f.Battery.Initialize();
        f.Keys(Key.E);f.Session.Tick(0);f.Keys(Key.E,Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.InsufficientEnergy && f.Interaction.Charging,"Low-energy Q leaves E intact");

        f.Configure(40,2,ExpandedEnemyState.Chase);f.InstallCharger();
        f.Keys(Key.E);f.Session.Tick(0);Require(f.Interaction.Charging,"Charge before valid pulse");
        f.Keys(Key.E,Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.Success && !f.Interaction.Current && f.Interaction.WaitingForRelease,"Valid Q cancels E and arms release gate");
        Require(!f.Session.player.HorizontalMovementLocked,"Successful Q frees movement lock");
        f.Keys(Key.E);f.Session.Tick(0);Require(!f.Interaction.Current,"Held E cannot immediately resume");
        f.Keys();f.Session.Tick(0);f.Keys(Key.E);f.Session.Tick(0);
        Require(f.Interaction.Charging,"Fresh E after release can charge again");
        float before=f.Battery.Current;
        f.Keys(Key.E,Key.Q);f.Session.Tick(0);
        Require(f.Pulse.LastResult==PulseUseResult.CoolingDown && f.Interaction.Charging,"Cooldown rejection preserves E");
        Near(f.Battery.Current,before,"Cooldown request no fee");
        Require(f.Session.statistics.SuccessfulPulseCount==1,"Rejected follow-up not counted");
        Pass("Q/E transaction: rejection preserves interaction; success cancels and requires release");
    }

    static void DisruptionRecovery(Fixture f)
    {
        f.Configure(40,2,ExpandedEnemyState.Chase);f.Keys(Key.Q);f.Session.Tick(0);
        Vector3 seen=f.Enemy.LastSeen;
        float cooldown=f.Pulse.CooldownRemaining, remaining=f.Enemy.DisruptionRemaining, time=f.Session.statistics.ActiveSeconds;
        f.Session.Pause();f.Session.Tick(10);f.Enemy.Tick(10);f.Pulse.Tick(10);
        Near(f.Pulse.CooldownRemaining,cooldown,"Pause freezes cooldown");Near(f.Enemy.DisruptionRemaining,remaining,"Pause freezes disruption");
        Near(f.Session.statistics.ActiveSeconds,time,"Pause excludes active time");
        f.PlacePlayer(3);f.BlockSight();f.Session.Resume();f.Keys();f.Session.Tick(2.49f);
        Require(f.Enemy.State==ExpandedEnemyState.Disrupted && !f.Enemy.SensedThisTick,"Disrupted does not sense or recover early");
        Require(f.Enemy.LastSeen==seen,"Hidden moved player cannot refresh last sighting while disrupted");
        Near(f.Enemy.CaptureElapsed,0,"No capture while disrupted");
        f.Session.Tick(.02f);
        Require(f.Enemy.State==ExpandedEnemyState.Search && f.Enemy.LastSeen==seen,"Previously confirmed but hidden target resumes original memory search");

        f.Configure(40,2,ExpandedEnemyState.Chase);f.Keys(Key.Q);f.Session.Tick(0);f.Session.Tick(2.51f);
        Require(f.Enemy.State==ExpandedEnemyState.Chase && f.Enemy.Awareness01==1,"Visible confirmed target resumes Chase without free Alert");
        Near(f.Enemy.CaptureElapsed,0,"Recovery starts a new capture confirmation");

        f.Configure(40,2,ExpandedEnemyState.Alert);float awareness=f.Enemy.Awareness01;
        f.Keys(Key.Q);f.Session.Tick(0);f.Session.Tick(2.51f);
        Require(f.Enemy.State==ExpandedEnemyState.Alert,"Partial alert resumes Alert");
        Near(f.Enemy.Awareness01,awareness,"Partial awareness retained rather than reset");
        Pass("Disruption freezes senses and timers, preserves memory and restores prior decision stage");
    }

    sealed class Fixture : IDisposable
    {
        public ExpandedSession Session {get;}
        public EmergencyPulseController Pulse=>Session.pulse;
        public EscapeBattery Battery=>Session.battery;
        public ExpandedEnemy Enemy=>Session.enemy;
        public EscapeInteraction Interaction=>Session.interaction;
        readonly Keyboard keyboard,previousKeyboard;
        readonly Mouse mouse,previousMouse;
        readonly EscapeInteractable[] originalTargets;
        readonly float initialEnergy;
        readonly Vector3 guardPosition;
        GameObject wall,charger;

        public Fixture(ExpandedSession s)
        {
            Session=s;s.enabled=false;
            originalTargets=s.interaction.targets;initialEnergy=s.rules.initialEnergy;
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            keyboard=InputSystem.AddDevice<Keyboard>("ChapterPulseFixtureKeyboard");
            mouse=InputSystem.AddDevice<Mouse>("ChapterPulseFixtureMouse");
            s.player.MoveAction.actionMap.devices=new InputDevice[]{keyboard};
            s.interaction.InteractAction.ApplyBindingOverride(keyboard.eKey.path);
            s.pulse.PulseAction.ApplyBindingOverride(keyboard.qKey.path);
            var desired=s.level.playerSpawn+new Vector3(-2.5f,0,-1);
            Require(NavMesh.SamplePosition(desired,out var hit,.5f,s.enemy.agent.areaMask),"Fixture corridor has navigation");
            guardPosition=hit.position;
        }
        public void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();mouse.MakeCurrent();
        }
        public void Configure(float energy,float distance,ExpandedEnemyState state)
        {
            Require(Session.Outcome==RunOutcome.Running,"Previous case may not terminate shared session");
            Session.Pause();RemoveWall();
            Interaction.targets=originalTargets;
            if(charger)UnityEngine.Object.DestroyImmediate(charger);
            Require(Enemy.agent.Warp(guardPosition),"Guard fixture placement");
            Enemy.transform.rotation=state==ExpandedEnemyState.Patrol?Quaternion.Euler(0,180,0):Quaternion.identity;
            PlacePlayer(distance);Enemy.Initialize();
            Session.rules.initialEnergy=energy;Battery.Initialize();Session.statistics.Initialize();
            Session.SendMessage("OnApplicationFocus",true);Session.Resume();Keys();
            Pulse.Tick(Pulse.profile.cooldownSeconds+1);Pulse.ResolvePendingRequest();
            Session.Tick(0);
            if(state==ExpandedEnemyState.Alert)Enemy.Tick(.4f);
            else if(state==ExpandedEnemyState.Chase)Enemy.Tick(Enemy.profile.alertSeconds+.01f);
            Require(Enemy.State==state,"Configured threat stage "+state+", actual "+Enemy.State);
        }
        public void PlacePlayer(float distance)
        {
            var controller=Session.player.controller;controller.enabled=false;
            controller.transform.SetPositionAndRotation(guardPosition+Vector3.forward*distance,Quaternion.identity);
            controller.enabled=true;Session.player.viewCamera.transform.localRotation=Quaternion.identity;Physics.SyncTransforms();
        }
        public void BlockSight()
        {
            RemoveWall();wall=new GameObject("ChapterPulseFixtureWall");wall.layer=0;
            wall.transform.position=guardPosition+Vector3.forward+Vector3.up;
            wall.AddComponent<BoxCollider>().size=new Vector3(1.5f,2,.15f);Physics.SyncTransforms();
        }
        void RemoveWall(){if(wall)UnityEngine.Object.DestroyImmediate(wall);wall=null;Physics.SyncTransforms();}
        public void InstallCharger()
        {
            charger=new GameObject("ChapterPulseFixtureCharger");
            charger.transform.position=Session.player.viewCamera.transform.position+Vector3.forward*1.2f;
            var target=charger.AddComponent<EscapeInteractable>();target.kind=InteractionKind.Charger;target.anchor=charger.transform;target.label="Fixture charger";
            EscapeInteractable terminal=null;foreach(var t in originalTargets)if(t.kind==InteractionKind.Terminal){terminal=t;break;}
            Require(terminal,"Scene has terminal");Interaction.targets=new[]{target,terminal};Physics.SyncTransforms();
        }
        public void Dispose()
        {
            Session.Pause();Interaction.targets=originalTargets;Session.rules.initialEnergy=initialEnergy;
            RemoveWall();if(charger)UnityEngine.Object.DestroyImmediate(charger);
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
            if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
        }
    }
}
