using UnityEngine;
using System;
using System.Collections.Generic;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.ChapterTactics;

namespace LastCellOfPower.ChapterOne
{
    /// <summary>Events drive one-shots; state drives mutually exclusive loops.</summary>
    public sealed class ChapterAudioFeedback : MonoBehaviour
    {
        public ExpandedSession session;
        public EmergencyPulseController pulse;
        public AudioClip patrolLoop, alert, chaseLoop, pulseHit, recover, chargeLoop, chargeFull, supply, doorOpen, success, failure, uiClick, ambientLoop;
        public bool ChargingLoopPlaying => charging && charging.isPlaying;
        public int GuardAudioCount => guardChannels.Count;
        public int PlayingGameplaySources
        {
            get
            {
                int count=Playing(charging)+Playing(deviceSignals)+Playing(doorSignals)+Playing(ambience)+Playing(shutterSignals);
                foreach(var channel in guardChannels)count+=Playing(channel.motor)+Playing(channel.signals);
                foreach(var channel in noiseChannels)count+=Playing(channel.source);
                return count;
            }
        }
        public int PlayedPulseCueCount { get; private set; }
        AudioSource charging, deviceSignals, doorSignals, ambience, ui;
        AudioSource shutterSignals;
        sealed class GuardChannel
        {
            public ExpandedEnemy guard;
            public AudioSource motor,signals;
            public Action<ExpandedEnemyState,ExpandedEnemyState> changed;
        }
        sealed class NoiseChannel { public NoiseDevice device;public AudioSource source; }
        readonly List<GuardChannel> guardChannels=new List<GuardChannel>();
        readonly List<NoiseChannel> noiseChannels=new List<NoiseChannel>();
        bool built, subscribed, fullyCharged, lastPaused;
        EscapeDoorState lastDoor;
        float previousEnergy;

        void Start()
        {
            if (!session || !pulse) { Debug.LogError("CHAPTER_AUDIO: session or pulse missing"); enabled=false;return; }
            int guardCount=Mathf.Max(1,session.Guards.Count);
            for(int i=0;i<session.Guards.Count;i++)
            {
                var guard=session.Guards[i];if(!guard)continue;
                var channel=new GuardChannel{guard=guard};
                string suffix=guardCount>1?"_"+guard.guardId:"";
                // The aggregate gain budget equals the accepted single guard.
                channel.motor=Source("GuardMotor"+suffix,guard.transform,true,.28f/guardCount);
                channel.signals=Source("GuardSignals"+suffix,guard.transform,true,.55f/guardCount);
                channel.motor.pitch=guardCount>1?(i==0 ? .96f : 1.04f):1;
                channel.signals.pitch=channel.motor.pitch;
                channel.changed=(before,after)=>GuardChanged(channel,before,after);guardChannels.Add(channel);
            }
            charging=Source("ChargingLoop",transform,true,.38f);
            deviceSignals=Source("DeviceSignals",transform,true,.55f);
            doorSignals=Source("DoorSignals",transform,true,.55f);
            ambience=Source("WarehouseAmbience",transform,false,.14f);
            ui=Source("InterfaceAudio",transform,false,.55f);ui.ignoreListenerPause=true;
            if(session.tactics)
            {
                var devices=session.tactics.noiseDevices;
                if(devices!=null)foreach(var device in devices)
                {
                    if(!device)continue;
                    noiseChannels.Add(new NoiseChannel{device=device,source=Source("NoiseSignal_"+device.deviceId,device.soundAnchor?device.soundAnchor:device.transform,true,.32f/Mathf.Max(1,devices.Length))});
                }
                shutterSignals=Source("TacticalShutterAudio",transform,true,.45f);
            }
            built=true;Subscribe();lastPaused=session.Paused;
            lastDoor=session.door.State;previousEnergy=session.battery.Current;
        }
        void OnEnable(){if(built)Subscribe();}
        void OnDisable(){Unsubscribe();StopAll();}
        void OnDestroy(){Unsubscribe();StopAll();}
        void Subscribe()
        {
            if(subscribed)return;
            foreach(var channel in guardChannels)if(channel.guard)channel.guard.StateChanged+=channel.changed;
            session.PauseChanged+=PauseChanged;session.RunEnded+=RunEnded;
            session.interaction.InteractionStarted+=InteractionStarted;session.interaction.InteractionEnded+=InteractionEnded;
            session.interaction.InteractionCompleted+=InteractionCompleted;pulse.Resolved+=PulseResolved;
            if(session.tactics){session.tactics.NoiseActivated+=NoiseActivated;session.tactics.ShutterStateChanged+=ShutterChanged;}
            subscribed=true;
        }
        void Unsubscribe()
        {
            if(!subscribed)return;
            if(session)
            {
                foreach(var channel in guardChannels)if(channel.guard)channel.guard.StateChanged-=channel.changed;
                session.PauseChanged-=PauseChanged;session.RunEnded-=RunEnded;
                if(session.interaction){session.interaction.InteractionStarted-=InteractionStarted;session.interaction.InteractionEnded-=InteractionEnded;session.interaction.InteractionCompleted-=InteractionCompleted;}
                if(session.tactics){session.tactics.NoiseActivated-=NoiseActivated;session.tactics.ShutterStateChanged-=ShutterChanged;}
            }
            if(pulse)pulse.Resolved-=PulseResolved;subscribed=false;
        }
        void Update()
        {
            if(!built || !session || !session.Ready)return;
            if(session.Outcome!=RunOutcome.Running){StopGameplay();return;}
            if(session.Paused){previousEnergy=session.battery.Current;return;}
            SetLoop(ambience,ambientLoop);
            foreach(var channel in guardChannels)
            {
                if(!channel.guard)continue;var state=channel.guard.State;
                SetLoop(channel.motor,state==ExpandedEnemyState.Patrol || state==ExpandedEnemyState.Return || state==ExpandedEnemyState.Search || state==ExpandedEnemyState.Investigate?patrolLoop:state==ExpandedEnemyState.Chase?chaseLoop:null);
            }
            foreach(var channel in noiseChannels)
                SetLoop(channel.source,channel.device && channel.device.SignalRemaining>0?(alert?alert:patrolLoop):null);
            if(session.interaction.Charging && session.interaction.Current)
            {
                charging.transform.position=session.interaction.Current.Position;
                SetLoop(charging,chargeLoop);
                if(!fullyCharged && previousEnergy<session.rules.maximumEnergy-.001f && session.battery.Current>=session.rules.maximumEnergy-.001f)
                { OneShot(deviceSignals,chargeFull,charging.transform.position);fullyCharged=true; }
            }
            else {SetLoop(charging,null);fullyCharged=false;}
            if(lastDoor!=session.door.State)
            {
                if(session.door.State==EscapeDoorState.Opening)OneShot(doorSignals,doorOpen,session.door.doorCollider.transform.position);
                lastDoor=session.door.State;
            }
            previousEnergy=session.battery.Current;
        }
        void GuardChanged(GuardChannel channel,ExpandedEnemyState oldState,ExpandedEnemyState newState)
        {
            if(!built || session.Paused || session.Outcome!=RunOutcome.Running)return;
            if(newState==ExpandedEnemyState.Alert && oldState!=ExpandedEnemyState.Disrupted)
            {if(guardChannels.Count>1)channel.signals.Stop();OneShot(channel.signals,alert,channel.guard.transform.position);}
            if(oldState==ExpandedEnemyState.Disrupted)
            {if(guardChannels.Count>1)channel.signals.Stop();OneShot(channel.signals,recover,channel.guard.transform.position);}
            if(newState==ExpandedEnemyState.Disrupted){channel.motor.Stop();channel.signals.Stop();}
        }
        void PulseResolved(PulseUseResult result)
        {
            if(!built)return;
            if(result==PulseUseResult.Success)
            {
                GuardChannel selected=null;
                foreach(var channel in guardChannels)if(channel.guard==pulse.LastResolvedTarget){selected=channel;break;}
                if(selected==null && guardChannels.Count==1)selected=guardChannels[0];
                if(selected!=null){if(guardChannels.Count>1)selected.signals.Stop();OneShot(selected.signals,pulseHit,selected.guard.transform.position);if(pulseHit)PlayedPulseCueCount++;}
            }
            else if(result==PulseUseResult.NoValidTarget || result==PulseUseResult.CoolingDown || result==PulseUseResult.InsufficientEnergy)PlayUiClick(.35f);
        }
        void InteractionStarted(EscapeInteractable target)
        {
            if(!built || !target)return;
            previousEnergy=session.battery.Current;fullyCharged=false;
            if(target.kind==InteractionKind.Charger){charging.transform.position=target.Position;if(!session.Paused)SetLoop(charging,chargeLoop);}
        }
        void InteractionEnded(EscapeInteractable target){if(built)charging.Stop();fullyCharged=false;}
        void InteractionCompleted(EscapeInteractable target)
        {if(built && target && target.kind==InteractionKind.Terminal)OneShot(deviceSignals,supply,target.Position);}
        void NoiseActivated(NoiseDevice device)
        {
            if(!built || session.Paused)return;
            foreach(var channel in noiseChannels)if(channel.device==device)
            {
                channel.source.pitch=.76f;SetLoop(channel.source,alert?alert:patrolLoop);break;
            }
        }
        void ShutterChanged(TacticalShutterState state)
        {
            if(!built || !shutterSignals || session.Paused || !session.tactics || !session.tactics.shutter)return;
            if(state!=TacticalShutterState.Closing && state!=TacticalShutterState.Opening)return;
            var shutter=session.tactics.shutter;Vector3 position=shutter.transform.position;
            if(shutter.switches!=null && shutter.switches.Length>0 && shutter.switches[0])position=shutter.switches[0].Position;
            shutterSignals.Stop();OneShot(shutterSignals,doorOpen,position);
        }
        void PauseChanged(bool paused)
        {
            if(!built || lastPaused==paused)return;lastPaused=paused;
            foreach(var channel in guardChannels)
                if(paused){channel.motor.Pause();channel.signals.Pause();}else{channel.motor.UnPause();channel.signals.UnPause();}
            foreach(var channel in noiseChannels)if(paused)channel.source.Pause();else channel.source.UnPause();
            if(shutterSignals){if(paused)shutterSignals.Pause();else shutterSignals.UnPause();}
            if(paused){charging.Stop();deviceSignals.Pause();doorSignals.Pause();ambience.Pause();}
            else {deviceSignals.UnPause();doorSignals.UnPause();ambience.UnPause();}
        }
        void RunEnded(RunSummary summary)
        {
            if(!built)return;StopGameplay();
            if(summary!=null){var clip=summary.Outcome==RunOutcome.Escaped?success:failure;if(clip)ui.PlayOneShot(clip);}
        }
        public void PlayUiClick(float scale=1){if(built && uiClick)ui.PlayOneShot(uiClick,scale);}
        public void StopGameplay()
        {
            foreach(var channel in guardChannels){if(channel.motor)channel.motor.Stop();if(channel.signals)channel.signals.Stop();}
            foreach(var channel in noiseChannels)if(channel.source)channel.source.Stop();
            if(shutterSignals)shutterSignals.Stop();if(charging)charging.Stop();if(deviceSignals)deviceSignals.Stop();if(doorSignals)doorSignals.Stop();if(ambience)ambience.Stop();
        }
        void StopAll(){StopGameplay();if(ui)ui.Stop();}
        static int Playing(AudioSource source)=>source && source.isPlaying?1:0;
        static AudioSource Source(string name,Transform parent,bool spatial,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=spatial?1:0;source.volume=volume;source.minDistance=1;source.maxDistance=14;
            source.rolloffMode=AudioRolloffMode.Logarithmic;source.dopplerLevel=0;return source;
        }
        static void SetLoop(AudioSource source,AudioClip clip)
        {
            if(!clip){if(source.isPlaying || source.clip)source.Stop();source.clip=null;return;}
            if(source.clip!=clip){source.Stop();source.clip=clip;source.loop=true;source.Play();}
            else if(!source.isPlaying)source.Play();
        }
        static void OneShot(AudioSource source,AudioClip clip,Vector3 position)
        {if(!clip)return;source.transform.position=position;source.PlayOneShot(clip);}
    }
}
