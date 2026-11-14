using System;
using UnityEngine;
using LastCellOfPower.EscapeSample;

namespace LastCellOfPower.WarehouseExpanded
{
    [CreateAssetMenu(menuName = "Last Cell/Expanded Gameplay Profile")]
    public sealed class ExpandedGameplayProfile : ScriptableObject
    {
        public float playerSpeed = 3.4f, patrolSpeed = 1.5f, chaseSpeed = 3f;
        public float viewDistance = 7f, viewAngle = 90f;
        public float alertSeconds = 1.2f, alertDecaySeconds = 1.5f;
        public float captureDistance = .65f, captureSeconds = .35f, searchSeconds = 6f;
        public float maximumEnergy = 100f, initialEnergy = 40f;
        public float idleDrain = .2f, movementDrain = .3f, chargingRate = 8f;
        public float supplySeconds = 2f, supplyCost = 35f, supplyStartMinimum = 36f;
        public float interactionRange = 1.8f, openingSeconds = 1f;

        public void Validate()
        {
            float[] values = { playerSpeed, patrolSpeed, chaseSpeed, viewDistance, viewAngle,
                alertSeconds, alertDecaySeconds, captureDistance, captureSeconds, searchSeconds,
                maximumEnergy, initialEnergy, idleDrain, movementDrain, chargingRate,
                supplySeconds, supplyCost, supplyStartMinimum, interactionRange, openingSeconds };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Gameplay profile contains a non-finite value");
            if (playerSpeed <= 0 || patrolSpeed <= 0 || chaseSpeed <= 0 || viewDistance <= 0 || viewAngle <= 0 || viewAngle > 360 ||
                alertSeconds <= 0 || alertDecaySeconds <= 0 || captureDistance <= 0 || captureSeconds <= 0 || searchSeconds <= 0 ||
                maximumEnergy <= 0 || initialEnergy < 0 || initialEnergy > maximumEnergy || idleDrain < 0 || movementDrain < 0 ||
                chargingRate <= idleDrain || supplySeconds <= 0 || supplyCost < 0 || supplyStartMinimum < supplyCost + idleDrain * supplySeconds ||
                supplyStartMinimum > maximumEnergy || interactionRange <= 0 || openingSeconds <= 0)
                throw new InvalidOperationException("Invalid expanded gameplay profile");
        }

        // Apply immutable configuration to a scene-owned runtime adapter, never mutate this asset.
        public void ApplyTo(EscapeRules rules)
        {
            Validate();
            if (!rules) throw new InvalidOperationException("Scene rules adapter missing");
            rules.maximumEnergy = maximumEnergy; rules.initialEnergy = initialEnergy;
            rules.idleDrain = idleDrain; rules.movementDrain = movementDrain; rules.chargingRate = chargingRate;
            rules.supplySeconds = supplySeconds; rules.supplyCost = supplyCost; rules.supplyStartMinimum = supplyStartMinimum;
            rules.interactionRange = interactionRange; rules.openingSeconds = openingSeconds;
            rules.Validate();
        }
    }
}
