using System;
using UnityEngine;

namespace LastCellOfPower.ChapterOne
{
    [CreateAssetMenu(menuName="Last Cell/Chapter One Pulse Profile")]
    public sealed class PulseProfile : ScriptableObject
    {
        public float minimumEnergy=13, cost=12, range=4, disruptionSeconds=2.5f, cooldownSeconds=15;
        public void Validate()
        {
            foreach(float value in new[]{minimumEnergy,cost,range,disruptionSeconds,cooldownSeconds})
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Non-finite pulse profile");
            if(cost<=0 || minimumEnergy<=cost || range<=0 || disruptionSeconds<=0 || cooldownSeconds<=disruptionSeconds)
                throw new InvalidOperationException("Invalid pulse profile");
        }
    }
}
