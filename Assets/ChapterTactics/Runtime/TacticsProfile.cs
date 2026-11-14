using System;
using UnityEngine;

namespace LastCellOfPower.ChapterTactics
{
    [CreateAssetMenu(menuName="Last Cell/Tactics Profile")]
    public sealed class TacticsProfile : ScriptableObject
    {
        public float noiseSignalSeconds=6f;
        public float noisePathRange=12f;
        public float investigationSeconds=10f;
        public float investigationDwellSeconds=2f;
        public float shutterClosingSeconds=.35f;
        public float shutterClosedSeconds=4f;
        public float shutterOpeningSeconds=.35f;
        public float shutterBrakeLookahead=.25f;
        public void Validate()
        {
            foreach(float value in new[]{noiseSignalSeconds,noisePathRange,investigationSeconds,investigationDwellSeconds,shutterClosingSeconds,shutterClosedSeconds,shutterOpeningSeconds,shutterBrakeLookahead})
                if(value<=0 || float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Tactics durations/ranges must be positive and finite");
            if(investigationDwellSeconds>investigationSeconds)throw new InvalidOperationException("Investigation dwell exceeds total time");
        }
    }
}
