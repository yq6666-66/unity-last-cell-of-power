using System;
using UnityEngine;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterTacticsPolish
{
    /// <summary>One presentation clock and one toast owner; consumes committed device observations only.</summary>
    public sealed class PolishFeedbackPresenter : MonoBehaviour
    {
        public ExpandedSession session;
        public TacticalReadabilityProfile profile;
        public string CurrentFeedbackText {get;private set;}="";
        public float FeedbackRemaining {get;private set;}
        public string CurrentTutorialText {get;private set;}="";
        public float TutorialRemaining {get;private set;}
        public bool TutorialVisible=>TutorialRemaining>0 && currentPriority<2 && !HasThreat();
        public int LastFeedbackSequence {get;private set;}
        public int DisplayedFeedbackSequence {get;private set;}
        public static bool NoiseTutorialSeen {get;private set;}
        public static bool ShutterTutorialSeen {get;private set;}
        public bool IsAvailable=>isActiveAndEnabled && profile && session && session.tactics;
        public string CurrentHint=>IsAvailable && session.Ready && session.Outcome==RunOutcome.Running?HintForCandidate():"";
        TacticsDevices observed;
        ExpandedSession subscribedSession;
        int currentPriority;
        EscapeInteractable tutorialTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetApplicationTutorials(){NoiseTutorialSeen=ShutterTutorialSeen=false;}
        void OnEnable(){Bind();}
        void Start(){Bind();}
        void Update(){Step(Time.deltaTime);}
        void OnDisable(){Unbind();ClearTransient();}
        void OnDestroy(){Unbind();}
        void Bind()
        {
            if(!IsAvailable || observed==session.tactics)return;
            Unbind();observed=session.tactics;subscribedSession=session;
            // Enabling a view synchronizes state through CurrentHint; it must not
            // replay an old last event as a new toast.
            LastFeedbackSequence=observed.HasFeedback?observed.LastFeedback.Sequence:0;
            observed.Feedback+=OnFeedback;subscribedSession.RunEnded+=OnEnded;
        }
        void Unbind()
        {
            if(observed)observed.Feedback-=OnFeedback;
            if(subscribedSession)subscribedSession.RunEnded-=OnEnded;
            observed=null;subscribedSession=null;
        }
        void OnEnded(RunSummary summary){ClearTransient();}
        void ClearTransient()
        {
            CurrentFeedbackText=CurrentTutorialText="";FeedbackRemaining=TutorialRemaining=0;
            DisplayedFeedbackSequence=0;currentPriority=0;tutorialTarget=null;
        }
        public void Step(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
            if(!IsAvailable){Unbind();ClearTransient();return;}Bind();
            if(session.Outcome!=RunOutcome.Running){ClearTransient();return;}
            // Pause and focus loss freeze unread text; only outcome/disable/R clears it.
            if(!session.Ready || session.Paused || !session.Focused)return;
            FeedbackRemaining=Mathf.Max(0,FeedbackRemaining-dt);
            if(FeedbackRemaining<=0){CurrentFeedbackText="";DisplayedFeedbackSequence=0;currentPriority=0;}
            if(HasThreat())
            {
                CurrentTutorialText="";TutorialRemaining=0;tutorialTarget=null;return;
            }
            // A device lesson belongs to the approached control, not a later
            // interaction on the other side of the warehouse.
            if(tutorialTarget && session.player && session.player.viewCamera)
            {
                var candidate=session.interaction?session.interaction.Candidate:null;
                bool different=candidate && candidate!=tutorialTarget && (candidate.kind==InteractionKind.Noise || candidate.kind==InteractionKind.Shutter);
                if(different || Vector3.Distance(session.player.viewCamera.transform.position,tutorialTarget.Position)>Mathf.Max(.1f,profile.tutorialApproachDistance))
                {CurrentTutorialText="";TutorialRemaining=0;tutorialTarget=null;}
            }
            TutorialRemaining=Mathf.Max(0,TutorialRemaining-dt);
            if(TutorialRemaining<=0){CurrentTutorialText="";tutorialTarget=null;if(currentPriority<2)TryDeviceTutorial();}
        }
        bool HasThreat()
        {
            if(!session)return false;
            foreach(var guard in session.Guards)
                if(guard && (guard.State==ExpandedEnemyState.Alert || guard.State==ExpandedEnemyState.Chase))return true;
            return false;
        }
        void TryDeviceTutorial()
        {
            var target=NearbyTeachingTarget();if(!target)return;
            if(target.kind==InteractionKind.Noise && !NoiseTutorialSeen)
            {NoiseTutorialSeen=true;SetTutorial(profile.noiseTutorial,profile.tutorialSeconds,target);}
            else if(target.kind==InteractionKind.Shutter && !ShutterTutorialSeen)
            {ShutterTutorialSeen=true;SetTutorial(profile.shutterTutorial,profile.tutorialSeconds,target);}
        }
        EscapeInteractable NearbyTeachingTarget()
        {
            if(!session.interaction || !session.player || !session.player.viewCamera)return null;
            var candidate=session.interaction.Candidate;
            if(candidate && (candidate.kind==InteractionKind.Noise || candidate.kind==InteractionKind.Shutter))return candidate;
            EscapeInteractable nearest=null;float best=Mathf.Max(.1f,profile.tutorialApproachDistance);
            Vector3 eye=session.player.viewCamera.transform.position;
            foreach(var target in session.interaction.targets)
            {
                if(!target || (target.kind!=InteractionKind.Noise && target.kind!=InteractionKind.Shutter))continue;
                if(target.kind==InteractionKind.Noise && NoiseTutorialSeen || target.kind==InteractionKind.Shutter && ShutterTutorialSeen)continue;
                Vector3 delta=target.Position-eye;float distance=delta.magnitude;if(distance>best)continue;
                bool blocked=false;
                if(distance>.001f)foreach(var hit in Physics.RaycastAll(eye,delta/distance,distance,session.interaction.obstructionMask,QueryTriggerInteraction.Ignore))
                    if(!target.Ignores(hit.collider)){blocked=true;break;}
                if(!blocked){nearest=target;best=distance;}
            }
            return nearest;
        }
        public void RequestTutorial(string text,float seconds)
        {
            if(!IsAvailable || !session.Ready || session.Paused || !session.Focused || session.Outcome!=RunOutcome.Running || HasThreat() || TutorialRemaining>0 || currentPriority>1)return;
            SetTutorial(text,seconds);
        }
        void SetTutorial(string text,float seconds,EscapeInteractable target=null)
        {CurrentTutorialText=text??"";TutorialRemaining=string.IsNullOrEmpty(CurrentTutorialText)?0:Duration(seconds,6);tutorialTarget=target;}
        void OnFeedback(DeviceFeedback feedback)
        {
            if(!IsAvailable || !session.Ready || session.Outcome!=RunOutcome.Running || feedback.Sequence<=LastFeedbackSequence)return;
            LastFeedbackSequence=feedback.Sequence;
            bool rejected=feedback.Kind==DeviceFeedbackKind.NoiseRejectedUsed || feedback.Kind==DeviceFeedbackKind.ShutterRejectedOccupied ||
                feedback.Kind==DeviceFeedbackKind.ShutterRejectedUsed || feedback.Kind==DeviceFeedbackKind.ShutterRejectedBusy || feedback.Kind==DeviceFeedbackKind.ShutterCancelledOccupied;
            Show(Format(feedback),rejected?2:1,feedback.Sequence,feedback.Frame,feedback.Kind.ToString());
        }
        public void ShowPulseFeedback(PulseUseResult result,string text)
        {
            if(!IsAvailable || !session.Ready || session.Outcome!=RunOutcome.Running)return;
            bool success=result==PulseUseResult.Success;
            Show(text,success?1:2,0,Time.frameCount,"Pulse_"+result);
        }
        void Show(string text,int priority,int sequence,int sourceFrame,string kind)
        {
            if(string.IsNullOrEmpty(text))return;
            if(FeedbackRemaining>0 && currentPriority>priority)
            {
                Diagnostic(sequence,sourceFrame,kind,"suppressed",text);return;
            }
            CurrentFeedbackText=text;currentPriority=priority;DisplayedFeedbackSequence=sequence;
            FeedbackRemaining=priority>1?Duration(profile.failureToastSeconds,3):Duration(profile.successToastSeconds,2);
            Diagnostic(sequence,sourceFrame,kind,"selected",text);
        }
        static float Duration(float value,float maximum)=>float.IsNaN(value)||float.IsInfinity(value)||value<=0?maximum:Mathf.Min(value,maximum);
        void Diagnostic(int sequence,int sourceFrame,string kind,string decision,string text)
        {
            if(observed && observed.diagnosticsEnabled && Debug.isDebugBuild)
                Debug.Log($"POLISH_FEEDBACK frame={Time.frameCount} sourceFrame={sourceFrame} seq={sequence} kind={kind} decision={decision} text={text.Replace("\n"," / ")}");
        }
        NoiseDevice Noise(string id)
        {
            if(session && session.tactics && session.tactics.noiseDevices!=null)
                foreach(var noise in session.tactics.noiseDevices)if(noise && noise.deviceId==id)return noise;
            return null;
        }
        string Format(DeviceFeedback feedback)
        {
            var label=profile.LabelFor(feedback.DeviceId);string id=label.shortId;
            string consumed=feedback.Consumed?profile.consumedText:profile.reservedText;
            switch(feedback.Kind)
            {
                case DeviceFeedbackKind.NoiseActivated:return string.Format(profile.noiseActivatedFormat,id,label.destination,session.tactics.profile.noiseSignalSeconds);
                case DeviceFeedbackKind.NoiseResponseAccepted:return string.Format(profile.noiseResponseFormat,id,feedback.ResponseCount);
                case DeviceFeedbackKind.NoiseEnded:return string.Format(profile.noiseEndedFormat,id,profile.ResponseText(feedback.ResponseCount,true));
                case DeviceFeedbackKind.NoiseRejectedUsed:
                    var noise=Noise(feedback.DeviceId);
                    return string.Format(noise && noise.State==NoiseDeviceState.Signalling?profile.noiseRejectedActiveFormat:profile.noiseRejectedFormat,id);
                case DeviceFeedbackKind.ShutterRejectedOccupied:return string.Format(profile.shutterRejectedOccupiedFormat,profile.shutterShortId);
                case DeviceFeedbackKind.ShutterRejectedUsed:return string.Format(profile.shutterRejectedUsedFormat,profile.shutterShortId);
                case DeviceFeedbackKind.ShutterRejectedBusy:return string.Format(profile.shutterRejectedBusyFormat,profile.shutterShortId,consumed);
                case DeviceFeedbackKind.ShutterClosing:return string.Format(profile.shutterClosingFormat,profile.shutterShortId);
                case DeviceFeedbackKind.ShutterCancelledOccupied:return string.Format(profile.shutterCancelledFormat,profile.shutterShortId);
                case DeviceFeedbackKind.ShutterClosed:return string.Format(profile.shutterClosedFormat,profile.shutterShortId);
                case DeviceFeedbackKind.ShutterOpened:return string.Format(profile.shutterOpenedFormat,profile.shutterShortId,consumed);
                default:return "";
            }
        }
        string HintForCandidate()
        {
            var interaction=session.interaction;if(!interaction || !interaction.Candidate)return "";
            var target=interaction.Candidate;
            if(target.kind==InteractionKind.Noise)
            {
                NoiseDevice noise=null;
                foreach(var item in session.tactics.noiseDevices)if(item && item.interactable==target){noise=item;break;}
                if(!noise)return "";var label=profile.LabelFor(noise.deviceId);
                string heading=label.shortId+" → "+label.destination;
                if(noise.State==NoiseDeviceState.Ready)return heading+" · "+profile.readyText+"\n"+profile.noiseReadyAction;
                if(noise.State==NoiseDeviceState.Signalling)return heading+" · "+string.Format(profile.signallingFormat,noise.SignalRemaining)+"\n"+profile.ResponseText(noise.ResponseCount,false)+" · "+profile.consumedText;
                return heading+" · "+profile.spentText+"\n"+profile.ResponseText(noise.ResponseCount,true);
            }
            if(target.kind==InteractionKind.Shutter && session.tactics.shutter)
            {
                // Closing can cancel later in the same session tick than
                // interaction.Prepare. Query the authoritative read-only rule
                // rather than echoing Prepare's now-stale cached hint.
                session.tactics.CanActivate(target,out string liveHint);
                var shutter=session.tactics.shutter;
                if(shutter.State==TacticalShutterState.OpenSpent)liveHint=profile.openSpentState;
                else if(shutter.State!=TacticalShutterState.OpenReady)
                    liveHint+=" · "+(shutter.Used?profile.consumedText:profile.reservedText);
                return profile.shutterShortId+" · "+profile.shutterDestination+"\n"+liveHint;
            }
            return "";
        }
    }
}
