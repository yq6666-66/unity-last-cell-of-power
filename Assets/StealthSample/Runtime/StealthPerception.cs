using System;
using UnityEngine;

namespace LastCellOfPower.StealthSample
{
    public sealed class StealthPerception : MonoBehaviour
    {
        public Transform observer, target;
        public LayerMask obstructionMask = 1;
        public float viewDistance = 6, viewAngle = 90, eyeHeight = .9f, targetHeight = .55f;
        public bool Visible { get; private set; }
        public bool Obstructed { get; private set; }
        public Vector3 Eye => observer.position + Vector3.up * eyeHeight;
        public Vector3 TargetPoint => target.position + Vector3.up * targetHeight;
        public Vector3 RayEnd { get; private set; }

        public void Initialize()
        {
            if (!observer || !target || obstructionMask.value == 0)
                throw new InvalidOperationException("Perception observer/target/obstruction mask missing");
            if ((obstructionMask.value & (1 << target.gameObject.layer)) != 0 ||
                (obstructionMask.value & (1 << observer.gameObject.layer)) != 0)
                throw new InvalidOperationException("Perception obstruction mask must exclude both actor layers");
        }

        public void Evaluate()
        {
            if (!observer || !target) throw new InvalidOperationException("Perception observer or target was destroyed");
            Vector3 delta = TargetPoint - Eye;
            Obstructed = Physics.Raycast(Eye, delta.normalized, out var hit, delta.magnitude, obstructionMask, QueryTriggerInteraction.Ignore);
            RayEnd = Obstructed ? hit.point : TargetPoint;
            var flat = target.position - observer.position; flat.y = 0;
            Visible = !Obstructed && flat.magnitude <= viewDistance &&
                (flat.sqrMagnitude < .0001f || Vector3.Angle(observer.forward, flat) <= viewAngle * .5f);
        }
    }
}
