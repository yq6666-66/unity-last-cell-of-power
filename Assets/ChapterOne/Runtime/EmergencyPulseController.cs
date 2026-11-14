using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.ChapterOne
{
    public enum PulseUseResult { None, Success, NotRunning, WaitingForRelease, CoolingDown, InsufficientEnergy, NoValidTarget }
    public sealed class EmergencyPulseController : MonoBehaviour
    {
        public PulseProfile profile;
        public ExpandedSession session;
        public StealthPlayerController player;
        public ExpandedEnemy enemy;
        public EscapeBattery battery;
        public EscapeInteraction interaction;
        public LayerMask obstructionMask=1;
        public float CooldownRemaining {get;private set;}
        public int SuccessfulUses {get;private set;}
        public PulseUseResult LastResult {get;private set;}
        public bool WaitingForRelease {get;private set;}
        public InputAction PulseAction=>action;
        public ExpandedEnemy LastResolvedTarget {get;private set;}
        public ExpandedEnemy SelectedTarget => SelectTarget();
        public bool ReadyToUse => active && session && session.Ready && !session.Paused && session.Outcome==RunOutcome.Running &&
            !WaitingForRelease && CooldownRemaining<=0 && battery && profile && battery.Current>=profile.minimumEnergy && HasValidTarget;
        public bool HasValidTarget
        {
            get
            {
                return SelectTarget()!=null;
            }
        }
        ExpandedEnemy SelectTarget()
        {
            if(!profile||!player||!player.controller||!player.viewCamera)return null;
            ExpandedEnemy selected=null;float nearest=float.MaxValue;
            var guards=session?session.Guards:null;
            int count=guards!=null?guards.Count:(enemy?1:0);
            for(int i=0;i<count;i++)
            {
                var candidate=guards!=null?guards[i]:enemy;
                if(!candidate || !candidate.perception || !candidate.perception.observer || !candidate.CanBeDisrupted)continue;
                var delta=candidate.transform.position-player.controller.transform.position;delta.y=0;float distance=delta.sqrMagnitude;
                if(distance>profile.range*profile.range || Physics.Linecast(player.viewCamera.transform.position,candidate.perception.Eye,obstructionMask,QueryTriggerInteraction.Ignore))continue;
                if(!selected || distance<nearest-.00001f || (Mathf.Abs(distance-nearest)<=.00001f && string.CompareOrdinal(candidate.guardId,selected.guardId)<0))
                {selected=candidate;nearest=distance;}
            }
            return selected;
        }
        public event Action<PulseUseResult> Resolved;
        InputAction action;
        bool initialized,active,pending,subscribed;
        ulong generation,enabledGeneration,consumedGeneration=ulong.MaxValue;
        float feedbackRemaining;

        public void Initialize()
        {
            if(initialized)throw new InvalidOperationException("Pulse already initialized");
            if(!profile||!session||!player||!player.viewCamera||!enemy||!enemy.perception||!battery||!interaction||obstructionMask.value==0)
                throw new InvalidOperationException("Pulse references missing");
            profile.Validate();
            action=new InputAction("ChapterPulse",InputActionType.Button,"<Keyboard>/q");
            action.wantsInitialStateCheck=true;
            InputSystem.onAfterUpdate+=AfterInputUpdate;subscribed=true;
            CooldownRemaining=0;SuccessfulUses=0;LastResult=PulseUseResult.None;LastResolvedTarget=null;feedbackRemaining=0;
            initialized=true;SetActive(false);
        }
        void AfterInputUpdate()
        {
            var type=InputState.currentUpdateType;
            if(type==InputUpdateType.Dynamic||type==InputUpdateType.Fixed||type==InputUpdateType.Manual)generation++;
        }
        public void SetActive(bool value)
        {
            active=value;pending=false;WaitingForRelease=value;
            if(action==null)return;
            action.Disable();
            if(value){enabledGeneration=generation;action.Enable();}
        }
        // Called once by the running session before ResolvePendingRequest.
        public void Tick(float dt)
        {
            if(!initialized||!active)return;
            if(dt<0||float.IsNaN(dt)||float.IsInfinity(dt))throw new ArgumentOutOfRangeException(nameof(dt));
            CooldownRemaining=Mathf.Max(0,CooldownRemaining-dt);
            feedbackRemaining=Mathf.Max(0,feedbackRemaining-dt);
            if(WaitingForRelease)
            {
                if(generation!=enabledGeneration&&!action.IsPressed())WaitingForRelease=false;
                else return;
            }
            if(action.WasPressedThisFrame() && consumedGeneration!=generation){pending=true;consumedGeneration=generation;}
        }
        public PulseUseResult ResolvePendingRequest()
        {
            if(!pending)return PulseUseResult.None;
            pending=false;
            if(!active||!session||!session.Ready||session.Paused||session.Outcome!=RunOutcome.Running)return Publish(PulseUseResult.NotRunning);
            if(WaitingForRelease)return Publish(PulseUseResult.WaitingForRelease);
            if(CooldownRemaining>0)return Publish(PulseUseResult.CoolingDown);
            if(!battery||!profile||!enemy||!player||!player.viewCamera||!enemy.perception||!interaction)
                throw new InvalidOperationException("Pulse reference destroyed");
            if(battery.Current<profile.minimumEnergy)return Publish(PulseUseResult.InsufficientEnergy);
            var target=SelectTarget();
            if(!target)return Publish(PulseUseResult.NoValidTarget);

            // All validation is synchronous; no callback runs between spending and applying.
            if(!battery.Spend(profile.cost))throw new InvalidOperationException("Validated pulse fee could not be committed");
            if(!target.TryDisrupt(profile.disruptionSeconds))throw new InvalidOperationException("Validated pulse target rejected disruption");
            LastResolvedTarget=target;
            interaction.CancelUntilRelease();
            CooldownRemaining=profile.cooldownSeconds;SuccessfulUses++;
            Debug.Log($"CHAPTER_PULSE_SUCCESS guard={target.guardId} energy={battery.Current:F3} uses={SuccessfulUses}");
            return Publish(PulseUseResult.Success);
        }
        PulseUseResult Publish(PulseUseResult result)
        {
            LastResult=result;
            if(result==PulseUseResult.Success||feedbackRemaining<=0)
            {
                feedbackRemaining=.5f;
                if(Resolved!=null)foreach(Action<PulseUseResult> listener in Resolved.GetInvocationList())
                    try{listener(result);}catch(Exception e){Debug.LogException(e);}
            }
            return result;
        }
        void OnDestroy()
        {
            if(subscribed)InputSystem.onAfterUpdate-=AfterInputUpdate;
            if(action!=null){action.Disable();action.Dispose();}
        }
    }
}
