using UnityEngine;
namespace LastCellOfPower.EscapeSample
{
    public enum InteractionKind { Charger, Terminal, Noise, Shutter }
    public sealed class EscapeInteractable : MonoBehaviour
    {
        public InteractionKind kind;
        public Transform anchor;
        public string label="充电座";
        public Collider[] ignoredColliders = new Collider[0];
        public Vector3 Position => anchor ? anchor.position : transform.position;
        public bool Ignores(Collider collider)
        {
            foreach(var own in ignoredColliders)if(own && own==collider)return true;
            return false;
        }
    }
}
