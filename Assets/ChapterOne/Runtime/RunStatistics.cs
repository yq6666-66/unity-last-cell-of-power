using System;
using UnityEngine;
using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.ChapterOne
{
    public sealed class RunStatistics : MonoBehaviour
    {
        public float ActiveSeconds {get;private set;}
        public int SuccessfulPulseCount {get;private set;}
        public int UsedNoiseCount {get;private set;}
        public bool ShutterUsed {get;private set;}
        public RunSummary Summary {get;private set;}
        public void Initialize(){ActiveSeconds=0;SuccessfulPulseCount=0;UsedNoiseCount=0;ShutterUsed=false;Summary=null;}
        public void Advance(float dt)
        {
            if(dt<0 || float.IsNaN(dt)||float.IsInfinity(dt))throw new ArgumentOutOfRangeException(nameof(dt));
            if(Summary==null)ActiveSeconds+=dt;
        }
        public void RecordPulse(){if(Summary==null)SuccessfulPulseCount++;}
        public void RecordNoise(){if(Summary==null)UsedNoiseCount++;}
        public void RecordShutter(){if(Summary==null)ShutterUsed=true;}
        public RunSummary Complete(RunOutcome outcome,float energy,bool powered)
        {
            if(outcome==RunOutcome.Running)throw new ArgumentException("Running is not a completed result");
            if(Summary==null)Summary=new RunSummary(outcome,ActiveSeconds,energy,SuccessfulPulseCount,powered,UsedNoiseCount,ShutterUsed);
            return Summary;
        }
    }
}
