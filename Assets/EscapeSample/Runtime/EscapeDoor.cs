using System;
using UnityEngine;
using UnityEngine.AI;
namespace LastCellOfPower.EscapeSample
{
    public enum EscapeDoorState { Closed, Opening, Open }
    public sealed class EscapeDoor : MonoBehaviour
    {
        public EscapeRules rules;
        public Transform leftLeaf,rightLeaf;
        public Collider doorCollider;
        public NavMeshObstacle doorObstacle;
        public Vector3 leftOpenOffset=new Vector3(-.85f,0,0),rightOpenOffset=new Vector3(.85f,0,0);
        public Vector3 exitBoundsCenter=new Vector3(1,0,7.2f),exitBoundsSize=new Vector3(1.6f,2,2.4f);
        public float exitZ=6.8f;
        public Transform exitAnchor;
        public Vector3 exitLocalSize=new Vector3(2.4f,2,2.4f);
        public EscapeDoorState State {get;private set;}
        public float OpeningElapsed {get;private set;}
        Vector3 leftClosed,rightClosed;
        public void Initialize()
        {
            if(!rules || !leftLeaf || !rightLeaf || !doorCollider || !doorObstacle)throw new InvalidOperationException("Door references missing");
            if(exitAnchor && (exitLocalSize.x<=0 || exitLocalSize.y<=0 || exitLocalSize.z<=0))throw new InvalidOperationException("Invalid local exit region");
            leftClosed=leftLeaf.position;rightClosed=rightLeaf.position;State=EscapeDoorState.Closed;OpeningElapsed=0;
            doorCollider.enabled=true;doorObstacle.carving=true;doorObstacle.enabled=true;
        }
        public void BeginOpening(){if(State!=EscapeDoorState.Closed)return;State=EscapeDoorState.Opening;Debug.Log("ESCAPE_DOOR Opening");}
        public void Tick(float dt)
        {
            if(!leftLeaf || !rightLeaf || !doorCollider || !doorObstacle)throw new InvalidOperationException("Door reference destroyed");
            if(State!=EscapeDoorState.Opening)return;
            OpeningElapsed=Mathf.Min(rules.openingSeconds,OpeningElapsed+dt);float t=OpeningElapsed/rules.openingSeconds;
            leftLeaf.position=leftClosed+leftOpenOffset*t;rightLeaf.position=rightClosed+rightOpenOffset*t;
            if(t>=1){doorCollider.enabled=false;doorObstacle.enabled=false;State=EscapeDoorState.Open;Debug.Log("ESCAPE_DOOR Open");}
        }
        public bool HasEscaped(Vector3 position)
        {
            if(State!=EscapeDoorState.Open)return false;
            if(!exitAnchor)return position.z>=exitZ && new Bounds(exitBoundsCenter,exitBoundsSize).Contains(position);
            Vector3 local=exitAnchor.InverseTransformPoint(position);
            return local.z>=0 && new Bounds(new Vector3(0,0,exitLocalSize.z*.5f),exitLocalSize).Contains(local);
        }
    }
}
