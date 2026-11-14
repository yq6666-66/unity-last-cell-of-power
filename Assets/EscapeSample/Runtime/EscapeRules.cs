using System;
using UnityEngine;
namespace LastCellOfPower.EscapeSample
{
    public sealed class EscapeRules : MonoBehaviour
    {
        public float maximumEnergy=100, initialEnergy=35, idleDrain=1, movementDrain=.5f, chargingRate=8;
        public float interactionRange=1.8f, aimAngle=18, supplySeconds=3, supplyCost=45, supplyStartMinimum=48, openingSeconds=1;
        public void Validate()
        {
            if(maximumEnergy<=0 || initialEnergy<0 || initialEnergy>maximumEnergy || idleDrain<0 || movementDrain<0 || chargingRate<=idleDrain || interactionRange<=0 || supplySeconds<=0 || openingSeconds<=0 || supplyCost<0 || supplyStartMinimum<supplyCost || supplyStartMinimum>maximumEnergy)
                throw new InvalidOperationException("Invalid escape configuration");
        }
    }
}
