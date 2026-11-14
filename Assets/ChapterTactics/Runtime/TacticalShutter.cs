using System;
using UnityEngine;
using UnityEngine.AI;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterTactics
{
    public enum TacticalShutterState { OpenReady, Closing, Closed, Opening, OpenSpent }
    public sealed class TacticalShutter : MonoBehaviour
    {
        public string deviceId="S01";
        public EscapeInteractable[] switches=new EscapeInteractable[0];
        public Transform leaf;
        public Vector3 openOffset=new Vector3(0,2.6f,0);
        public Collider barrier;
        public NavMeshObstacle obstacle;
        public Transform safetyVolume;
        public Vector3 safetySize=new Vector3(3.2f,2.4f,.8f);
        public TacticalShutterState State {get;internal set;}
        public bool Used {get;internal set;}
        public float ClosedRemaining {get;internal set;}
        public float MotionElapsed {get;internal set;}
        public float CloseFraction {get;internal set;}
        internal float openingStart;
        Vector3 openLocal,closedLocal;

        internal void Initialize()
        {
            if(!leaf || !barrier || !obstacle || !safetyVolume || switches==null || switches.Length!=2)
                throw new InvalidOperationException("Temporary shutter references missing");
            if(safetySize.x<=0 || safetySize.y<=0 || safetySize.z<=0 || openOffset.sqrMagnitude<.01f)
                throw new InvalidOperationException("Temporary shutter dimensions invalid");
            foreach(var button in switches)
                if(!button || !button.anchor || button.kind!=InteractionKind.Shutter)throw new InvalidOperationException("Shutter switch reference invalid");
            if(switches[0]==switches[1])throw new InvalidOperationException("Shutter requires two distinct switches");
            openLocal=leaf.localPosition;closedLocal=openLocal-openOffset;
            State=TacticalShutterState.OpenReady;Used=false;ClosedRemaining=MotionElapsed=CloseFraction=openingStart=0;
            SetBarrier(false);ApplyVisual();
        }
        internal void ApplyVisual(){leaf.localPosition=Vector3.Lerp(openLocal,closedLocal,Mathf.Clamp01(CloseFraction));}
        internal void SetBarrier(bool enabled)
        {
            if(!barrier || !obstacle)throw new InvalidOperationException("Shutter navigation/physical barrier destroyed");
            barrier.enabled=enabled;obstacle.carving=true;obstacle.carveOnlyStationary=false;obstacle.enabled=enabled;
            Physics.SyncTransforms();
        }
        public bool IsOccupied(ExpandedSession session)
        {
            if(!session || !safetyVolume)throw new InvalidOperationException("Shutter safety references missing");
            var controller=session.player.controller;
            var pc=controller.transform.TransformPoint(controller.center);
            if(Occupies(pc,controller.radius,controller.height))return true;
            foreach(var guard in session.Guards)
            {
                if(!guard || !guard.agent)throw new InvalidOperationException("Shutter safety guard missing");
                var agent=guard.agent;
                var center=agent.transform.position+Vector3.up*(agent.baseOffset+agent.height*.5f);
                if(Occupies(center,agent.radius,agent.height))return true;
            }
            return false;
        }
        bool Occupies(Vector3 center,float radius,float height)
        {
            Vector3 q=safetyVolume.InverseTransformPoint(center);
            Vector3 scale=safetyVolume.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            return Mathf.Abs(q.x)<=safetySize.x*.5f+radius/Mathf.Max(.001f,scale.x) &&
                Mathf.Abs(q.z)<=safetySize.z*.5f+radius/Mathf.Max(.001f,scale.z) &&
                Mathf.Abs(q.y)<=safetySize.y*.5f+height*.5f/Mathf.Max(.001f,scale.y);
        }
        public bool ShouldBrake(ExpandedEnemy guard,float lookahead)
        {
            if(!barrier || !barrier.enabled || !guard || !guard.agent || !guard.agent.enabled || !guard.agent.isOnNavMesh)return false;
            var agent=guard.agent;
            Vector3 origin=agent.transform.position+Vector3.up*Mathf.Max(.1f,agent.height*.5f);
            Vector3 desired=agent.desiredVelocity;
            if(desired.sqrMagnitude<.0001f && agent.hasPath)desired=agent.steeringTarget-agent.transform.position;
            desired.y=0;
            if(desired.sqrMagnitude<.0001f)return false;
            Vector3 direction=desired.normalized;
            var bounds=barrier.bounds;bounds.Expand(new Vector3(agent.radius*2+.04f,0,agent.radius*2+.04f));
            // A stopped guard may start a newly carved detour pointing away from the gate.
            float before=(origin-bounds.center).sqrMagnitude;
            if(bounds.Contains(origin) && (origin+direction*.12f-bounds.center).sqrMagnitude>before)return false;
            float distance=Mathf.Max(.18f,agent.speed*lookahead+.08f);
            return bounds.Contains(origin) || bounds.IntersectRay(new Ray(origin,direction),out float hit) && hit<=distance;
        }
    }
}
