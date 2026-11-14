using UnityEngine;
using System.Collections.Generic;
using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.ChapterTactics
{
    public enum NoiseDeviceState { Ready, Signalling, Spent }
    public sealed class NoiseDevice : MonoBehaviour
    {
        public string deviceId="N01";
        public EscapeInteractable interactable;
        public Transform soundAnchor;
        public Transform investigationAnchor;
        public NoiseDeviceState State {get;internal set;}
        public bool Used {get;internal set;}
        public float SignalRemaining {get;internal set;}
        public float ActiveRemaining=>SignalRemaining;
        public int EventId {get;internal set;}
        readonly HashSet<string> responders=new HashSet<string>();
        public int ResponseCount=>responders.Count;
        internal bool RecordResponse(string guardId)=>!string.IsNullOrEmpty(guardId)&&responders.Add(guardId);
        internal void ResetState(){State=NoiseDeviceState.Ready;Used=false;SignalRemaining=0;EventId=0;responders.Clear();}
    }
}
