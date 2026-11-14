using System;
using UnityEngine;
namespace LastCellOfPower.EscapeSample
{
    public sealed class EscapeBattery : MonoBehaviour
    {
        public EscapeRules rules;
        public float Current {get; private set;}
        public float LastNetRate {get; private set;}
        public bool Depleted => Current<=0;
        public void Initialize(){if(!rules)throw new InvalidOperationException("Battery rules missing");rules.Validate();Current=rules.initialEnergy;LastNetRate=0;}
        public void Tick(float dt,bool moving,bool charging)
        {
            if(dt<0 || float.IsNaN(dt))throw new ArgumentOutOfRangeException(nameof(dt));
            LastNetRate=(charging?rules.chargingRate:0)-rules.idleDrain-(moving?rules.movementDrain:0);
            Current=Mathf.Clamp(Current+LastNetRate*dt,0,rules.maximumEnergy);
        }
        public bool Spend(float amount)
        {
            if(amount<0)throw new ArgumentOutOfRangeException(nameof(amount));
            if(Current+.001f<amount)return false;
            Current=Mathf.Max(0,Current-amount);return true;
        }
        public void StopRate(){LastNetRate=0;}
    }
}
