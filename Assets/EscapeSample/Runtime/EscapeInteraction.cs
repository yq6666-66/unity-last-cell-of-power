using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.StealthSample;
using LastCellOfPower.ChapterTactics;
namespace LastCellOfPower.EscapeSample
{
    public sealed class EscapeInteraction : MonoBehaviour
    {
        public StealthPlayerController player;
        public EscapeBattery battery;
        public EscapeDoor door;
        public EscapeRules rules;
        public EscapeInteractable[] targets;
        public TacticsDevices tactics;
        public LayerMask obstructionMask=1;
        public bool cancelOnMovement; // Opt-in; legacy samples keep their original hold-to-interact behavior.
        public EscapeInteractable Candidate {get;private set;}
        public EscapeInteractable Current {get;private set;}
        public bool WaitingForRelease {get;private set;}
        public float SupplyElapsed {get;private set;}
        public bool Powered {get;private set;}
        public float EnvironmentElapsed {get;private set;}
        public bool UsingEnvironment=>Current && IsEnvironmental(Current);
        public float EnvironmentDuration=>UsingEnvironment && tactics?tactics.HoldSeconds(Current):0;
        public string Hint {get;private set;}="";
        public bool Charging=>Current && Current.kind==InteractionKind.Charger;
        public bool Supplying=>Current && Current.kind==InteractionKind.Terminal;
        public InputAction InteractAction=>action;
        public event Action<EscapeInteractable> InteractionStarted, InteractionEnded, InteractionCompleted;
        InputAction action;
        ulong generation,enabledGeneration,consumedEnvironmentGeneration=ulong.MaxValue;
        bool active,subscribed;

        public void Initialize()
        {
            if(!player || !battery || !door || !rules || targets==null || obstructionMask.value==0)throw new InvalidOperationException("Interaction references missing");
            int chargers=0,terminals=0;
            foreach(var target in targets)
            {
                if(!target || !target.anchor)throw new InvalidOperationException("Interaction anchor missing");
                if(target.kind==InteractionKind.Charger)chargers++;
                if(target.kind==InteractionKind.Terminal)terminals++;
                if(IsEnvironmental(target) && !tactics)throw new InvalidOperationException("Environmental interaction requires tactics devices");
            }
            if(chargers<1 || terminals<1)throw new InvalidOperationException("At least one charger and terminal are required");
            action=new InputAction("EscapeInteract",InputActionType.Button,"<Keyboard>/e");
            action.wantsInitialStateCheck=true;
            InputSystem.onAfterUpdate+=AfterUpdate;subscribed=true;SetActive(false);
        }
        void AfterUpdate()
        {
            var type=InputState.currentUpdateType;
            if(type==InputUpdateType.Dynamic || type==InputUpdateType.Fixed || type==InputUpdateType.Manual)generation++;
        }
        public void SetActive(bool value)
        {
            active=value;Cancel();Candidate=null;WaitingForRelease=value;
            if(action==null)return;
            action.Disable();if(value){enabledGeneration=generation;action.Enable();}
        }
        public void Cancel()
        {
            var previous=Current;
            if(Current)Debug.Log("ESCAPE_INTERACTION Cancel "+Current.label);
            Current=null;SupplyElapsed=0;EnvironmentElapsed=0;
            if(player)player.HorizontalMovementLocked=false;
            if(previous)Notify(InteractionEnded,previous);
        }
        public void CancelUntilRelease()
        {
            Cancel();WaitingForRelease=true;enabledGeneration=generation;
        }
        static void Notify(Action<EscapeInteractable> handlers,EscapeInteractable target)
        {
            if(handlers!=null)foreach(Action<EscapeInteractable> handler in handlers.GetInvocationList())
                try{handler(target);}catch(Exception e){Debug.LogException(e);}
        }
        static bool IsEnvironmental(EscapeInteractable target)=>target && (target.kind==InteractionKind.Noise || target.kind==InteractionKind.Shutter);
        bool Accessible(EscapeInteractable target,bool requireAim)
        {
            if(!target || !target.anchor || !target.isActiveAndEnabled)return false;
            var camera=player.viewCamera;
            Vector3 delta=target.Position-camera.transform.position;
            if(delta.magnitude>rules.interactionRange)return false;
            if(requireAim && Vector3.Angle(camera.transform.forward,delta)>rules.aimAngle)return false;
            foreach(var hit in Physics.RaycastAll(camera.transform.position,delta.normalized,delta.magnitude,obstructionMask,QueryTriggerInteraction.Ignore))
                if(!target.Ignores(hit.collider))return false;
            return true;
        }
        public void Prepare()
        {
            Hint="";Candidate=null;
            if(!active)return;
            if(!player || !player.viewCamera || !battery || !door || !rules)throw new InvalidOperationException("Interaction reference destroyed");
            bool held=action.IsPressed();
            // Read the current input action, not player.InputValue from the preceding frame:
            // Prepare runs before player.Tick and must release the lock in this same frame.
            bool moving=cancelOnMovement && player.MoveAction!=null && player.MoveAction.enabled &&
                player.MoveAction.ReadValue<Vector2>().sqrMagnitude>.0001f;
            if(moving)
            {
                if(Current)
                {
                    Cancel();WaitingForRelease=true;enabledGeneration=generation;
                    Hint="已停止交互，松开 E 后可重新操作";
                }
                if(!tactics)
                {
                    if(held){Cancel();WaitingForRelease=true;enabledGeneration=generation;Hint="已停止交互，松开 E 后可重新操作";}
                    return;
                }
            }
            if(WaitingForRelease)
            {
                if(generation!=enabledGeneration && !held)WaitingForRelease=false;
                else {Hint="先松开 E";Cancel();return;}
            }
            if(Current && !held)Cancel();
            if(Current && !Accessible(Current,false))
            {Cancel();WaitingForRelease=true;enabledGeneration=generation;Hint="交互中断，先松开 E";return;}
            bool environmentPressed=action.WasPressedThisFrame() && consumedEnvironmentGeneration!=generation;
            if(environmentPressed)consumedEnvironmentGeneration=generation;
            if(Current)return;
            float best=float.MaxValue;
            foreach(var target in targets)
            {
                if(!target || !target.anchor)throw new InvalidOperationException("Interaction target destroyed");
                if(!Accessible(target,true))continue;
                float angle=Vector3.Angle(player.viewCamera.transform.forward,target.Position-player.viewCamera.transform.position);
                if(angle<best){best=angle;Candidate=target;}
            }
            if(!Candidate)
            {
                if(moving && held){WaitingForRelease=true;enabledGeneration=generation;Hint="已停止交互，松开 E 后可重新操作";}
                return;
            }
            if(IsEnvironmental(Candidate))
            {
                if(!tactics)throw new InvalidOperationException("Tactics interaction handler missing");
                bool available=tactics.CanActivate(Candidate,out var environmentalHint);Hint=environmentalHint;
                if(!available)
                {
                    if(environmentPressed){tactics.ReportRejectedAttempt(Candidate);WaitingForRelease=true;enabledGeneration=generation;}
                    return;
                }
                if(environmentPressed)
                {
                    var target=Candidate;
                    bool succeeded=tactics.TryActivate(target,out var resultHint);
                    if(succeeded)Notify(InteractionCompleted,target);
                    WaitingForRelease=true;enabledGeneration=generation;Hint=resultHint;
                }
                return;
            }
            if(moving)
            {
                if(held){WaitingForRelease=true;enabledGeneration=generation;Hint="先停下并松开 E，再进行持续交互";}
                return;
            }
            if(Candidate.kind==InteractionKind.Terminal && Powered){Hint="供电完成，穿过北门逃生";return;}
            if(Candidate.kind==InteractionKind.Terminal && battery.Current+.0001f<rules.supplyStartMinimum)
            {Hint=$"需要至少 {rules.supplyStartMinimum:0} 电量才能供电";return;}
            Hint=Candidate.kind==InteractionKind.Charger?$"按住 E 充电（净增 {rules.chargingRate-rules.idleDrain:0.#}/秒）":$"按住 E 供电 {rules.supplySeconds:0.#} 秒（消耗 {rules.supplyCost:0.#}）";
            if(held){Current=Candidate;SupplyElapsed=0;player.HorizontalMovementLocked=true;Debug.Log("ESCAPE_INTERACTION Begin "+Current.label+" energy="+battery.Current.ToString("F3"));Notify(InteractionStarted,Current);}
        }
        public void Advance(float dt,bool moving)
        {
            if(!active)return;
            if(UsingEnvironment)
            {
                float duration=tactics.HoldSeconds(Current);
                float used=Mathf.Min(dt,Mathf.Max(0,duration-EnvironmentElapsed));
                battery.Tick(used,false,false);EnvironmentElapsed+=used;
                if(EnvironmentElapsed+.00001f>=duration)
                {
                    var completed=Current;
                    bool succeeded=tactics.TryActivate(completed,out var resultHint);
                    if(succeeded)Notify(InteractionCompleted,completed);
                    CancelUntilRelease();Hint=resultHint;
                    if(dt>used)battery.Tick(dt-used,false,false);
                }
                return;
            }
            if(!Supplying){battery.Tick(dt,moving,Charging);return;}
            // Split at the completion boundary: frame overshoot must not consume
            // the reserved fee before a valid 48-energy supply can finish.
            float slice=Mathf.Min(dt,Mathf.Max(0,rules.supplySeconds-SupplyElapsed));
            battery.Tick(slice,false,false);SupplyElapsed+=slice;
            if(SupplyElapsed+.00001f>=rules.supplySeconds)
            {
                if(!battery.Spend(rules.supplyCost))throw new InvalidOperationException("Insufficient reserved supply energy");
                Powered=true;door.BeginOpening();Debug.Log("ESCAPE_SUPPLIED energy="+battery.Current.ToString("F3"));
                Notify(InteractionCompleted,Current);
                Cancel();WaitingForRelease=true;enabledGeneration=generation;
                if(dt>slice)battery.Tick(dt-slice,false,false);
            }
        }
        void OnDestroy(){if(subscribed)InputSystem.onAfterUpdate-=AfterUpdate;if(action!=null){action.Disable();action.Dispose();}}
    }
}
