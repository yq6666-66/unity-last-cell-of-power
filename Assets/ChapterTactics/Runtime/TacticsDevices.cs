using System;
using UnityEngine;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.ChapterTactics
{
    /// <summary>Session-ticked devices; no independent clock, input action, or hidden-player targeting.</summary>
    public sealed class TacticsDevices : MonoBehaviour
    {
        public ExpandedSession session;
        public TacticsProfile profile;
        public NoiseDevice[] noiseDevices=new NoiseDevice[0];
        public TacticalShutter shutter;
        public int UsedNoiseCount {get;private set;}
        public bool ShutterUsed=>shutter && shutter.Used;
        public bool Active=>active;
        public event Action<NoiseDevice> NoiseActivated;
        public event Action<TacticalShutterState> ShutterStateChanged;
        public event Action<DeviceFeedback> Feedback;
        public DeviceFeedback LastFeedback {get;private set;}
        public bool HasFeedback {get;private set;}
        public bool diagnosticsEnabled;
        int feedbackSequence;
        bool initialized,active;
        int nextEventId;

        public void Initialize()
        {
            if(initialized)throw new InvalidOperationException("Tactics devices already initialized");
            if(!session || !profile || !shutter || noiseDevices==null || noiseDevices.Length!=2)throw new InvalidOperationException("Tactics device references missing");
            profile.Validate();
            for(int i=0;i<noiseDevices.Length;i++)
            {
                var device=noiseDevices[i];
                if(!device || !device.interactable || device.interactable.kind!=InteractionKind.Noise || !device.soundAnchor || !device.investigationAnchor || string.IsNullOrWhiteSpace(device.deviceId))
                    throw new InvalidOperationException("Noise device reference missing: "+i);
                for(int j=0;j<i;j++)if(noiseDevices[j]==device || noiseDevices[j].deviceId==device.deviceId || noiseDevices[j].interactable==device.interactable)
                    throw new InvalidOperationException("Noise devices must have distinct ids and interactions");
                device.ResetState();
            }
            shutter.Initialize();UsedNoiseCount=0;nextEventId=0;feedbackSequence=0;HasFeedback=false;LastFeedback=default;initialized=true;active=false;
        }
        public void SetActive(bool value)
        {
            active=value;
            if(!value && session)foreach(var guard in session.Guards)if(guard)guard.SetNavigationBarrierHold(false);
        }
        NoiseDevice NoiseFor(EscapeInteractable target)
        {if(noiseDevices!=null)foreach(var d in noiseDevices)if(d && d.interactable==target)return d;return null;}
        bool ShutterFor(EscapeInteractable target)
        {if(shutter && shutter.switches!=null)foreach(var button in shutter.switches)if(button==target)return true;return false;}
        public float HoldSeconds(EscapeInteractable target)=>0;
        public bool CanActivate(EscapeInteractable target,out string hint)
        {
            hint="";
            if(!initialized || !active || !session || !session.Ready || session.Paused || session.Outcome!=RunOutcome.Running){hint="当前不能操作";return false;}
            var noise=NoiseFor(target);
            if(noise)
            {
                if(noise.Used){hint=noise.SignalRemaining>0?$"诱导声源工作中 {noise.SignalRemaining:0.0}s":"该诱导装置本局已用完";return false;}
                hint="按 E 启动诱导声源 · 本局一次 · 不耗电";return true;
            }
            if(ShutterFor(target))
            {
                if(shutter.State!=TacticalShutterState.OpenReady)
                {
                    hint=shutter.State==TacticalShutterState.Closed?$"临时门闭锁中 {shutter.ClosedRemaining:0.0}s":
                        shutter.State==TacticalShutterState.Closing?"临时门正在关闭":shutter.State==TacticalShutterState.Opening?"临时门正在打开":"临时门本局已用完";
                    return false;
                }
                if(shutter.IsOccupied(session)){hint="门口有人，无法关闭 · 次数保留";return false;}
                hint="按 E 暂时封门 4 秒 · 本局一次 · 不耗电";return true;
            }
            throw new InvalidOperationException("Unknown environmental interaction target");
        }
        public bool TryActivate(EscapeInteractable target,out string reason)
        {
            if(!CanActivate(target,out reason)){ReportRejectedAttempt(target);return false;}
            var noise=NoiseFor(target);
            if(noise)
            {
                noise.Used=true;noise.State=NoiseDeviceState.Signalling;noise.SignalRemaining=profile.noiseSignalSeconds;noise.EventId=++nextEventId;UsedNoiseCount++;
                if(session.statistics)session.statistics.RecordNoise();
                reason="诱导声源已启动";NotifyNoise(noise);Publish(DeviceFeedbackKind.NoiseActivated,noise);
                Debug.Log($"TACTICS_NOISE_ACTIVATE device={noise.deviceId} event={noise.EventId} used={UsedNoiseCount}");return true;
            }
            shutter.MotionElapsed=0;shutter.CloseFraction=0;ChangeShutterState(TacticalShutterState.Closing);
            reason="临时门正在关闭";return true;
        }
        public void Tick(float dt)
        {
            if(!initialized || !active)return;
            if(dt<0 || float.IsNaN(dt)||float.IsInfinity(dt))throw new ArgumentOutOfRangeException(nameof(dt));
            TickShutter(dt);
            foreach(var guard in session.Guards)guard.SetNavigationBarrierHold(shutter.ShouldBrake(guard,Mathf.Max(dt,profile.shutterBrakeLookahead)));
            foreach(var noise in noiseDevices)
            {
                if(noise.State!=NoiseDeviceState.Signalling)continue;
                noise.SignalRemaining=Mathf.Max(0,noise.SignalRemaining-dt);
                if(noise.SignalRemaining<=0){noise.State=NoiseDeviceState.Spent;Publish(DeviceFeedbackKind.NoiseEnded,noise);}
            }
            foreach(var guard in session.Guards)
            {
                if(!guard || guard.State==ExpandedEnemyState.Disrupted || guard.State==ExpandedEnemyState.Caught || guard.State==ExpandedEnemyState.Investigate)continue;
                NoiseDevice selected=null;float nearest=float.PositiveInfinity;
                foreach(var noise in noiseDevices)
                {
                    if(noise.State!=NoiseDeviceState.Signalling || guard.HasHandledNoise(noise.EventId))continue;
                    if(!guard.TryGetNavigationDistance(noise.investigationAnchor.position,out float path) || path>profile.noisePathRange)continue;
                    if(shutter.barrier.enabled && guard.LastCalculatedPathIntersects(shutter.barrier.bounds))continue;
                    if(!selected || path<nearest-.0001f || Mathf.Abs(path-nearest)<=.0001f && string.CompareOrdinal(noise.deviceId,selected.deviceId)<0)
                    {selected=noise;nearest=path;}
                }
                if(selected && guard.TryInvestigateNoise(selected.EventId,selected.investigationAnchor.position,profile.noisePathRange,profile.investigationSeconds,profile.investigationDwellSeconds) && selected.RecordResponse(guard.guardId))
                    Publish(DeviceFeedbackKind.NoiseResponseAccepted,selected);
            }
        }
        void TickShutter(float dt)
        {
            float remaining=dt;
            for(int transition=0;transition<5;transition++)
            {
                if(shutter.State==TacticalShutterState.Closing)
                {
                    if(shutter.IsOccupied(session)){BeginOpening();Publish(DeviceFeedbackKind.ShutterCancelledOccupied);continue;}
                    float slice=Mathf.Min(remaining,profile.shutterClosingSeconds-shutter.MotionElapsed);
                    shutter.MotionElapsed+=slice;remaining-=slice;shutter.CloseFraction=Mathf.Clamp01(shutter.MotionElapsed/profile.shutterClosingSeconds);shutter.ApplyVisual();
                    if(shutter.CloseFraction<1)return;
                    if(shutter.IsOccupied(session)){BeginOpening();Publish(DeviceFeedbackKind.ShutterCancelledOccupied);continue;}
                    shutter.SetBarrier(true);shutter.Used=true;shutter.ClosedRemaining=profile.shutterClosedSeconds;shutter.MotionElapsed=0;
                    if(session.statistics)session.statistics.RecordShutter();ChangeShutterState(TacticalShutterState.Closed);
                }
                else if(shutter.State==TacticalShutterState.Closed)
                {
                    float slice=Mathf.Min(remaining,shutter.ClosedRemaining);shutter.ClosedRemaining-=slice;remaining-=slice;
                    if(shutter.ClosedRemaining>.00001f)return;
                    shutter.ClosedRemaining=0;BeginOpening();
                }
                else if(shutter.State==TacticalShutterState.Opening)
                {
                    float slice=Mathf.Min(remaining,profile.shutterOpeningSeconds-shutter.MotionElapsed);shutter.MotionElapsed+=slice;remaining-=slice;
                    shutter.CloseFraction=shutter.openingStart*(1-Mathf.Clamp01(shutter.MotionElapsed/profile.shutterOpeningSeconds));shutter.ApplyVisual();
                    if(shutter.MotionElapsed+.00001f<profile.shutterOpeningSeconds)return;
                    shutter.CloseFraction=0;shutter.MotionElapsed=0;shutter.ApplyVisual();shutter.SetBarrier(false);
                    ChangeShutterState(shutter.Used?TacticalShutterState.OpenSpent:TacticalShutterState.OpenReady);
                }
                else return;
                if(remaining<=0)return;
            }
        }
        void BeginOpening()
        {
            shutter.openingStart=shutter.CloseFraction;shutter.MotionElapsed=0;
            ChangeShutterState(TacticalShutterState.Opening);
            if(shutter.CloseFraction<=.00001f)
            {shutter.CloseFraction=0;shutter.ApplyVisual();shutter.SetBarrier(false);ChangeShutterState(shutter.Used?TacticalShutterState.OpenSpent:TacticalShutterState.OpenReady);}
        }
        void ChangeShutterState(TacticalShutterState next)
        {
            if(shutter.State==next)return;shutter.State=next;
            Debug.Log($"TACTICS_SHUTTER {next} used={shutter.Used} closedRemaining={shutter.ClosedRemaining:F3}");
            if(ShutterStateChanged!=null)foreach(Action<TacticalShutterState> handler in ShutterStateChanged.GetInvocationList())
                try{handler(next);}catch(Exception e){Debug.LogException(e);}
            if(next==TacticalShutterState.Closing)Publish(DeviceFeedbackKind.ShutterClosing);
            else if(next==TacticalShutterState.Closed)Publish(DeviceFeedbackKind.ShutterClosed);
            else if(next==TacticalShutterState.OpenSpent)Publish(DeviceFeedbackKind.ShutterOpened);
        }
        public void ReportRejectedAttempt(EscapeInteractable target)
        {
            if(!initialized||!active||!session||!session.Ready||session.Paused||session.Outcome!=RunOutcome.Running)return;
            var noise=NoiseFor(target);
            if(noise){if(noise.Used)Publish(DeviceFeedbackKind.NoiseRejectedUsed,noise);return;}
            if(!ShutterFor(target))return;
            if(shutter.State==TacticalShutterState.OpenSpent)Publish(DeviceFeedbackKind.ShutterRejectedUsed);
            else if(shutter.State!=TacticalShutterState.OpenReady)Publish(DeviceFeedbackKind.ShutterRejectedBusy);
            else if(shutter.IsOccupied(session))Publish(DeviceFeedbackKind.ShutterRejectedOccupied);
        }
        void Publish(DeviceFeedbackKind kind,NoiseDevice noise=null)
        {
            var observation=new DeviceFeedback(++feedbackSequence,Time.frameCount,noise?noise.deviceId:shutter.deviceId,
                noise?noise.EventId:0,noise?noise.ResponseCount:0,noise?noise.Used:shutter.Used,kind);
            LastFeedback=observation;HasFeedback=true;
            if(diagnosticsEnabled && Debug.isDebugBuild)
                Debug.Log($"TACTICS_FEEDBACK frame={observation.Frame} seq={observation.Sequence} device={observation.DeviceId} event={observation.NoiseEventId} kind={kind} responses={observation.ResponseCount} consumed={observation.Consumed}");
            if(Feedback!=null)foreach(Action<DeviceFeedback> handler in Feedback.GetInvocationList())
                try{handler(observation);}catch(Exception e){Debug.LogException(e);}
        }
        void NotifyNoise(NoiseDevice device)
        {
            if(NoiseActivated!=null)foreach(Action<NoiseDevice> handler in NoiseActivated.GetInvocationList())
                try{handler(device);}catch(Exception e){Debug.LogException(e);}
        }
    }
}
