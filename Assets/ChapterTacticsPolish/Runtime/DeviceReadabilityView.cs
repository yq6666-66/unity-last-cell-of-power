using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastCellOfPower.ChapterTactics;

namespace LastCellOfPower.ChapterTacticsPolish
{
    /// <summary>Read-only state labels and emission; no gameplay method is invoked.</summary>
    public sealed class DeviceReadabilityView : MonoBehaviour
    {
        public TacticalReadabilityProfile profile;
        public NoiseDevice noiseDevice;
        public TacticalShutter shutter;
        public TMP_Text stateText;
        public Renderer[] indicatorRenderers=new Renderer[0];
        public bool compactWorldStatus;
        public string CurrentStateText {get;private set;}="";
        public Color CurrentIndicatorColor {get;private set;}
        sealed class Snapshot {public Renderer renderer;public MaterialPropertyBlock global;public MaterialPropertyBlock[] indexed;}
        readonly List<Snapshot> snapshots=new List<Snapshot>();
        MaterialPropertyBlock block;
        bool initialized;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");
        void Start(){Refresh();}
        void Update(){Refresh();}
        public void Refresh()
        {
            if(!isActiveAndEnabled || !profile){Restore();return;}
            if(!initialized)Capture();
            Color color=profile.spentColor;string text="";
            if(noiseDevice)
            {
                if(noiseDevice.State==NoiseDeviceState.Ready){text=compactWorldStatus?profile.worldReadyText:profile.readyText;color=profile.readyColor;}
                else if(noiseDevice.State==NoiseDeviceState.Signalling)
                {text=compactWorldStatus?profile.worldSignallingText:string.Format(profile.signallingFormat,noiseDevice.SignalRemaining)+"\n"+profile.ResponseText(noiseDevice.ResponseCount,false);color=profile.signallingColor;}
                else text=compactWorldStatus?profile.spentText:profile.spentText+"\n"+profile.ResponseText(noiseDevice.ResponseCount,true);
            }
            else if(shutter)
            {
                switch(shutter.State)
                {
                    case TacticalShutterState.OpenReady:text=compactWorldStatus?profile.worldReadyText:profile.readyText;color=profile.readyColor;break;
                    case TacticalShutterState.Closing:text=compactWorldStatus?profile.worldClosingText:profile.closingState;color=profile.signallingColor;break;
                    case TacticalShutterState.Closed:text=compactWorldStatus?profile.worldClosedText:string.Format(profile.closedStateFormat,shutter.ClosedRemaining)+"\n"+profile.consumedText;color=profile.signallingColor;break;
                    case TacticalShutterState.Opening:text=compactWorldStatus?profile.worldOpeningText:shutter.Used?profile.openingUsedState:profile.openingReservedState;color=profile.signallingColor;break;
                    case TacticalShutterState.OpenSpent:text=compactWorldStatus?profile.spentText:profile.openSpentState;break;
                }
            }
            if(CurrentStateText==text && CurrentIndicatorColor==color)return;
            CurrentStateText=text;CurrentIndicatorColor=color;if(stateText)stateText.text=text;
            foreach(var snapshot in snapshots)
            {
                if(!snapshot.renderer)continue;
                snapshot.renderer.GetPropertyBlock(block);Tint(block,color);snapshot.renderer.SetPropertyBlock(block);
                for(int i=0;i<snapshot.indexed.Length;i++)if(!snapshot.indexed[i].isEmpty)
                {snapshot.renderer.GetPropertyBlock(block,i);Tint(block,color);snapshot.renderer.SetPropertyBlock(block,i);}
            }
        }
        void Capture()
        {
            initialized=true;block=new MaterialPropertyBlock();var seen=new HashSet<Renderer>();
            if(indicatorRenderers==null)return;
            foreach(var renderer in indicatorRenderers)
            {
                if(!renderer || !seen.Add(renderer))continue;
                var snapshot=new Snapshot{renderer=renderer,global=new MaterialPropertyBlock(),indexed=new MaterialPropertyBlock[renderer.sharedMaterials.Length]};
                renderer.GetPropertyBlock(snapshot.global);
                for(int i=0;i<snapshot.indexed.Length;i++){snapshot.indexed[i]=new MaterialPropertyBlock();renderer.GetPropertyBlock(snapshot.indexed[i],i);}
                snapshots.Add(snapshot);
            }
        }
        void Tint(MaterialPropertyBlock properties,Color color)
        {properties.SetColor(BaseColor,new Color(color.r*.35f,color.g*.35f,color.b*.35f,1));properties.SetColor(Emission,color*Mathf.Max(0,profile.indicatorEmission));}
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
        void Restore()
        {
            foreach(var snapshot in snapshots)
            {
                if(!snapshot.renderer)continue;snapshot.renderer.SetPropertyBlock(snapshot.global.isEmpty?null:snapshot.global);
                for(int i=0;i<Mathf.Min(snapshot.indexed.Length,snapshot.renderer.sharedMaterials.Length);i++)snapshot.renderer.SetPropertyBlock(snapshot.indexed[i].isEmpty?null:snapshot.indexed[i],i);
            }
            snapshots.Clear();initialized=false;CurrentStateText="";CurrentIndicatorColor=Color.clear;if(stateText)stateText.text="";
        }
    }
}
