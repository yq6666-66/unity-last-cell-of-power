using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;

// Isolated synthetic input and coordinate fixtures. Not normal Windows input acceptance.
public static class EscapeInteractionChecks
{
    public static string LastResult { get; private set; } = "NOT_RUN";
    static StringBuilder report;
    static void Require(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException("ESCAPE_INTERACTION_CHECK_FAIL: "+message);
    }
    static void Near(float actual,float expected,string message,float tolerance=.002f)
    { Require(Mathf.Abs(actual-expected)<=tolerance,message+" actual="+actual+" expected="+expected); }
    static void Pass(string message){report.AppendLine("PASS "+message);Debug.Log("ESCAPE_INTERACTION_CHECK_PASS "+message);}

    public static void Run()
    {
        Require(EditorApplication.isPlaying,"Enter PlayMode before running synthetic checks");
        foreach(var s in UnityEngine.Object.FindObjectsByType<EscapeSession>(FindObjectsSortMode.None))Require(s.Paused,"Pause escape session before synthetic checks");
        foreach(var s in UnityEngine.Object.FindObjectsByType<StealthSession>(FindObjectsSortMode.None))Require(s.Paused,"Pause stealth session before synthetic checks");
        report=new StringBuilder("Synthetic-device fixtures; no actual focus or Windows input acceptance.\n");
        LastResult="RUNNING";
        try
        {
            NeutralAndCharging();
            TargetLockAndObstruction();
            ThresholdAndInterruption();
            SupplyAndDoor();
            LastResult="PASS";report.AppendLine("PASS All interaction checks finished");
            Debug.Log("ESCAPE_INTERACTION_LOGIC_PASS");
        }
        catch(Exception e){LastResult="FAIL";report.AppendLine(e.ToString());Debug.LogError("ESCAPE_INTERACTION_LOGIC_FAIL "+e.Message);throw;}
        finally
        {
            string directory=Path.GetFullPath("Evidence/EscapeSample");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"interaction-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".txt"),report.ToString());
        }
    }

    static void NeutralAndCharging()
    {
        using(var f=new Fixture(35,InteractionKind.Charger))
        {
            f.Keys(Key.E);
            f.interaction.SetActive(true);f.interaction.Prepare();
            Require(f.interaction.WaitingForRelease && !f.interaction.Current,"Enable same-frame default zero cannot release E gate");
            InputSystem.Update();f.interaction.Prepare();
            Require(f.interaction.InteractAction.IsPressed() && f.interaction.WaitingForRelease && !f.interaction.Current,"Held E after initial input check remains blocked");
            f.Keys();f.interaction.Prepare();
            Require(!f.interaction.WaitingForRelease && !f.interaction.Current,"Release E opens gate without beginning interaction");
            f.player.SetActive(true);f.Keys();f.player.Tick(0);
            Require(!f.player.WaitingForNeutral,"Movement neutral fixture established");
            f.Keys(Key.E,Key.W);f.interaction.Prepare();
            Require(f.interaction.Charging && f.player.HorizontalMovementLocked,"Fresh held E begins charging and locks horizontal movement");
            Vector3 start=f.player.transform.position;
            f.player.Tick(.02f);
            Require(f.player.InputValue.y>.9f,"W reaches player input while charging");
            Near(f.player.HorizontalDistanceLastTick,0,"Charging prevents horizontal displacement",.0001f);
            Near(f.player.transform.position.z,start.z,"Charging holds horizontal position",.0001f);
            float pitch=f.player.Pitch;
            InputSystem.QueueStateEvent(f.mouse,new MouseState{delta=new Vector2(20,15)});InputSystem.Update();
            f.player.Tick(.02f);f.interaction.Prepare();
            Require(f.player.Pitch!=pitch && f.interaction.Current==f.target,"Camera can turn without switching latched target");
            f.interaction.Advance(1,false);Near(f.battery.Current,42,"Charging is +8 minus baseline 1");
            f.interaction.Advance(20,false);Near(f.battery.Current,100,"Charging clamps at maximum");
            f.interaction.Advance(10,false);Near(f.battery.Current,100,"Holding charge maintains full energy");
            f.interaction.SetActive(false);
            Require(!f.interaction.Current && !f.player.HorizontalMovementLocked,"Pause cancels charge and movement lock");
            f.interaction.Advance(5,false);Near(f.battery.Current,100,"Inactive interaction does not advance energy");
            f.interaction.SetActive(true);f.interaction.Prepare();InputSystem.Update();f.interaction.Prepare();
            Require(f.interaction.WaitingForRelease && !f.interaction.Current,"Resume while E held cannot automatically resume charging");
            f.Keys();f.interaction.Prepare();f.Keys(Key.E);f.interaction.Prepare();
            Require(f.interaction.Charging,"Release then fresh E can charge after resume");
            f.Keys();f.interaction.Prepare();
            Require(!f.interaction.Current && !f.player.HorizontalMovementLocked,"Release ends charging");
            Pass("E same-frame/held-resume/release gates; movement lock; camera freedom; charging net rate and full cap");
        }
    }

    static void TargetLockAndObstruction()
    {
        using(var f=new Fixture(60,InteractionKind.Charger))
        {
            f.EnableReleased();
            var blocker=f.Child("UnrelatedWall",new Vector3(0,.9f,.6f));
            blocker.AddComponent<BoxCollider>().size=new Vector3(.7f,.7f,.12f);Physics.SyncTransforms();
            f.Keys(Key.E);f.interaction.Prepare();Require(!f.interaction.Current,"Unrelated solid wall prevents interaction");
            UnityEngine.Object.DestroyImmediate(blocker);Physics.SyncTransforms();
            var own=f.Child("TargetOwnCollider",new Vector3(0,.9f,1));var ownCollider=own.AddComponent<BoxCollider>();ownCollider.size=new Vector3(.4f,.4f,.1f);
            f.target.ignoredColliders=new Collider[]{ownCollider};Physics.SyncTransforms();
            f.Keys();f.interaction.Prepare();f.Keys(Key.E);f.interaction.Prepare();Require(f.interaction.Current==f.target,"Configured target collider does not block its exposed anchor");
            f.target.anchor.position+=Vector3.forward*3;Physics.SyncTransforms();f.interaction.Prepare();
            Require(!f.interaction.Current && f.interaction.WaitingForRelease,"Out-of-range latched target cancels and re-arms release gate");
            f.target.anchor.position-=Vector3.forward*3;Physics.SyncTransforms();f.interaction.Prepare();
            Require(!f.interaction.Current,"Returning into range while held cannot restart cancelled interaction");
            f.Keys();f.interaction.Prepare();f.Keys(Key.E);f.interaction.Prepare();Require(f.interaction.Current==f.target,"Fresh interaction after range cancellation succeeds");
            Pass("Selection range, external occlusion, own-collider exclusion and cancellation re-arm");
        }
    }

    static void ThresholdAndInterruption()
    {
        using(var f=new Fixture(47.9f,InteractionKind.Terminal))
        {
            f.EnableReleased();f.Keys(Key.E);f.interaction.Prepare();
            Require(!f.interaction.Supplying && !f.player.HorizontalMovementLocked && f.interaction.Hint.Contains("48"),"47.9 cannot start and displays threshold");
        }
        using(var f=new Fixture(48,InteractionKind.Terminal))
        {
            f.EnableReleased();f.Keys(Key.E);f.interaction.Prepare();Require(f.interaction.Supplying,"Exactly 48 can start before frame drain");
            f.interaction.Advance(3,false);
            Require(f.interaction.Powered && f.battery.Depleted,"Exactly 48 pays 3 baseline plus 45, with no gifted energy");
            Require(EscapeSession.ResolveOutcome(false,false,true,true)==RunOutcome.Depleted,"Depletion outranks escape at zero energy");
        }
        using(var f=new Fixture(60,InteractionKind.Terminal))
        {
            f.EnableReleased();f.Keys(Key.E);f.interaction.Prepare();f.interaction.Advance(1,false);
            Near(f.battery.Current,59,"Partial supply consumes baseline only");Near(f.interaction.SupplyElapsed,1,"Supply progress accumulates");
            f.Keys();f.interaction.Prepare();
            Near(f.interaction.SupplyElapsed,0,"Release discards partial supply progress");Near(f.battery.Current,59,"Interruption does not deduct supply fee");
            Require(!f.interaction.Powered && f.door.State==EscapeDoorState.Closed,"Interrupted supply leaves door closed");
            f.Keys(Key.E);f.interaction.Prepare();f.interaction.Advance(.5f,false);f.interaction.SetActive(false);
            Near(f.interaction.SupplyElapsed,0,"Pause resets supply progress");Near(f.battery.Current,58.5f,"Pause does not refund baseline or charge fee");
        }
        Pass("47.9/48 supply threshold; exact-zero outcome; release/pause interruption resets without fee");
    }

    static void SupplyAndDoor()
    {
        using(var f=new Fixture(80,InteractionKind.Terminal))
        {
            f.EnableReleased();f.Keys(Key.E);f.interaction.Prepare();
            f.interaction.Advance(2.9f,false);Require(!f.interaction.Powered,"Cannot complete early");
            f.interaction.Advance(.2f,false);Near(f.battery.Current,31.9f,"Completion-boundary split deducts exactly 45 plus 3.1 baseline");
            Require(f.interaction.Powered && f.door.State==EscapeDoorState.Opening,"Completed supply starts opening once");
            Require(f.door.doorCollider.enabled && f.door.doorObstacle.enabled,"Door stays physically and navigationally closed before animation finishes");
            f.door.Tick(.99f);Require(f.door.State==EscapeDoorState.Opening && f.door.doorCollider.enabled,"Door remains blocking at .99 seconds");
            f.door.Tick(.02f);Require(f.door.State==EscapeDoorState.Open && !f.door.doorCollider.enabled && !f.door.doorObstacle.enabled,"Opening completion releases physical and carving blockers");
            Vector3 opened=f.door.leftLeaf.position;f.door.BeginOpening();f.door.Tick(5);Require(f.door.leftLeaf.position==opened,"Repeated opening request does not move leaves again");
            f.Keys();f.interaction.Prepare();f.Keys(Key.E);f.interaction.Prepare();f.interaction.Advance(1,false);
            Require(!f.interaction.Supplying,"Powered terminal cannot begin supply again");Near(f.battery.Current,30.9f,"Powered terminal charges no second fee");
            Require(f.door.HasEscaped(new Vector3(1,0,7)) && !f.door.HasEscaped(new Vector3(1,0,6.7f)) && !f.door.HasEscaped(new Vector3(3,0,7)),"Exit needs open door, correct region and actual crossing");
            Pass("Supply completion/overshoot/one-time fee; opening obstruction timing; exit region");
        }
    }

    sealed class Fixture : IDisposable
    {
        public readonly GameObject root;
        public readonly Keyboard keyboard;
        public readonly Mouse mouse;
        readonly Keyboard previousKeyboard;readonly Mouse previousMouse;
        readonly InputActionAsset input;
        public readonly StealthPlayerController player;
        public readonly EscapeRules rules;
        public readonly EscapeBattery battery;
        public readonly EscapeDoor door;
        public readonly EscapeInteraction interaction;
        public readonly EscapeInteractable target;
        public Fixture(float energy,InteractionKind kind)
        {
            previousKeyboard=Keyboard.current;previousMouse=Mouse.current;
            keyboard=InputSystem.AddDevice<Keyboard>("EscapeSyntheticKeyboard");mouse=InputSystem.AddDevice<Mouse>("EscapeSyntheticMouse");
            root=new GameObject("EscapeInteractionFixture");root.transform.position=new Vector3(30,2,30);
            rules=root.AddComponent<EscapeRules>();rules.initialEnergy=energy;
            battery=root.AddComponent<EscapeBattery>();battery.rules=rules;battery.Initialize();
            var body=Child("FixturePlayer",Vector3.zero);body.layer=8;
            player=body.AddComponent<StealthPlayerController>();player.controller=body.AddComponent<CharacterController>();player.controller.height=1.1f;player.controller.radius=.42f;player.controller.center=new Vector3(0,.55f,0);
            var cam=Child("FixtureCamera",new Vector3(0,.9f,0));player.viewCamera=cam.AddComponent<Camera>();player.viewCamera.enabled=false;
            input=ScriptableObject.CreateInstance<InputActionAsset>();var map=input.AddActionMap("Player");var move=map.AddAction("Move",InputActionType.Value);move.expectedControlType="Vector2";
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            player.inputAsset=input;player.Initialize();player.MoveAction.actionMap.devices=new InputDevice[]{keyboard};
            var floor=Child("FixtureFloor",new Vector3(0,-.06f,0));floor.AddComponent<BoxCollider>().size=new Vector3(10,.1f,10);
            door=root.AddComponent<EscapeDoor>();door.rules=rules;door.leftLeaf=Child("FixtureLeft",new Vector3(5,0,5)).transform;door.rightLeaf=Child("FixtureRight",new Vector3(6,0,5)).transform;
            var doorBlock=Child("FixtureDoorBlock",new Vector3(5,1,5));door.doorCollider=doorBlock.AddComponent<BoxCollider>();door.doorObstacle=doorBlock.AddComponent<NavMeshObstacle>();door.Initialize();
            target=Target("FixtureTarget",kind,new Vector3(0,.9f,1.2f));
            var other1=Target("OtherCharger",InteractionKind.Charger,new Vector3(-4,.9f,1));
            var other2=Target("OtherTarget",kind==InteractionKind.Charger?InteractionKind.Terminal:InteractionKind.Charger,new Vector3(4,.9f,1));
            interaction=root.AddComponent<EscapeInteraction>();interaction.player=player;interaction.battery=battery;interaction.rules=rules;interaction.door=door;interaction.targets=new[]{target,other1,other2};interaction.Initialize();
            interaction.InteractAction.ApplyBindingOverride(keyboard.eKey.path);
            Physics.SyncTransforms();
        }
        public GameObject Child(string name,Vector3 local){var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=local;return go;}
        EscapeInteractable Target(string name,InteractionKind kind,Vector3 local){var go=Child(name,local);var t=go.AddComponent<EscapeInteractable>();t.anchor=go.transform;t.kind=kind;t.label=name;return t;}
        public void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
        public void EnableReleased(){Keys();interaction.SetActive(true);InputSystem.Update();interaction.Prepare();Require(!interaction.WaitingForRelease,"Fixture E released after effective input update");}
        public void Dispose()
        {
            if(root)UnityEngine.Object.DestroyImmediate(root);if(input)UnityEngine.Object.DestroyImmediate(input);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
        }
    }
}
