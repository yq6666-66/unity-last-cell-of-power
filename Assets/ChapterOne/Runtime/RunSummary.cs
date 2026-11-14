using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.ChapterOne
{
    public sealed class RunSummary
    {
        public RunOutcome Outcome {get;}
        public float ActiveSeconds {get;}
        public float RemainingEnergy {get;}
        public int SuccessfulPulseCount {get;}
        public bool WasPowered {get;}
        public int UsedNoiseCount {get;}
        public bool ShutterUsed {get;}
        public RunSummary(RunOutcome outcome,float seconds,float energy,int pulses,bool powered,int noises=0,bool shutterUsed=false)
        {Outcome=outcome;ActiveSeconds=seconds;RemainingEnergy=energy;SuccessfulPulseCount=pulses;WasPowered=powered;UsedNoiseCount=noises;ShutterUsed=shutterUsed;}
    }
}
