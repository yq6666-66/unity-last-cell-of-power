using System;
using UnityEngine;
using UnityEngine.AI;

namespace LastCellOfPower.StealthSample
{
    public enum EnemyState { Patrol, Chase, Search, Return, Caught }

    public sealed class StealthEnemy : MonoBehaviour
    {
        public NavMeshAgent agent;
        public StealthPerception perception;
        public Vector3[] patrolPoints = { new Vector3(-4,0,3), new Vector3(4,0,3), new Vector3(4,0,4.5f), new Vector3(-4,0,4.5f) };
        public float patrolSpeed = 1.6f, chaseSpeed = 3.3f, searchDuration = 4, captureDistance = .85f;
        public EnemyState State { get; private set; }
        public Vector3 LastSeen { get; private set; }
        public bool HasLastSeen { get; private set; }
        public Vector3 Destination { get; private set; }
        public int PatrolIndex { get; private set; }
        public float SearchElapsed { get; private set; }
        public bool SearchingAtPoint { get; private set; }
        float pendingTime, stalledTime, repathTime;
        Vector3 lastPosition;
        bool active;

        public void Initialize()
        {
            if (!agent || !agent.isOnNavMesh || !perception || patrolPoints == null || patrolPoints.Length != 4)
                throw new InvalidOperationException("Enemy requires baked navigation, perception and four patrol points");
            perception.Initialize();
            foreach (var point in patrolPoints)
                if (!NavMesh.SamplePosition(point, out _, .3f, agent.areaMask)) throw new InvalidOperationException("Patrol point outside NavMesh: " + point);
            State = EnemyState.Patrol;
            agent.speed = patrolSpeed;
            Go(patrolPoints[0]);
            lastPosition = agent.transform.position;
            SetActive(false);
        }

        public void SetActive(bool value)
        {
            active = value;
            if (agent && agent.isOnNavMesh) agent.isStopped = !value || State == EnemyState.Caught || SearchingAtPoint;
        }

        void Change(EnemyState state)
        {
            State = state;
            SearchingAtPoint = false;
            SearchElapsed = 0;
            stalledTime = 0;
            agent.updateRotation = true;
            agent.isStopped = !active;
            Debug.Log($"STEALTH_STATE {state} lastSeen={LastSeen} patrol={PatrolIndex}");
        }

        void Go(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out var hit, .5f, agent.areaMask)) throw new InvalidOperationException("Target is outside NavMesh: " + point);
            Destination = hit.position;
            if (!agent.SetDestination(Destination)) throw new InvalidOperationException("Cannot set navigation destination");
        }

        bool Arrived()
        {
            var delta = agent.transform.position - Destination; delta.y = 0;
            return !agent.pathPending && delta.magnitude <= agent.stoppingDistance + .18f && agent.remainingDistance <= agent.stoppingDistance + .18f;
        }

        public void Tick(float dt)
        {
            if (!active || State == EnemyState.Caught) return;
            if (!agent || !agent.isOnNavMesh) throw new InvalidOperationException("Enemy left NavMesh");
            if (!perception) throw new InvalidOperationException("Enemy perception was destroyed");
            perception.Evaluate();
            var distance = perception.target.position - agent.transform.position; distance.y = 0;
            if (distance.magnitude <= captureDistance && !perception.Obstructed)
            {
                Change(EnemyState.Caught); agent.isStopped = true; return;
            }
            if (perception.Visible)
            {
                LastSeen = perception.target.position; HasLastSeen = true;
                if (State != EnemyState.Chase) { Change(EnemyState.Chase); repathTime = 0; }
                agent.speed = chaseSpeed;
                repathTime -= dt;
                if (repathTime <= 0) { Go(LastSeen); repathTime = .15f; }
            }
            else if (State == EnemyState.Chase)
            {
                Change(EnemyState.Search); agent.speed = patrolSpeed; Go(LastSeen);
            }
            if (State == EnemyState.Search && (SearchingAtPoint || Arrived()))
            {
                SearchingAtPoint = true; agent.isStopped = true; agent.updateRotation = false;
                agent.transform.Rotate(0, 90 * dt, 0);
                SearchElapsed += dt;
                if (SearchElapsed >= searchDuration) { Change(EnemyState.Return); agent.speed = patrolSpeed; Go(patrolPoints[PatrolIndex]); }
            }
            else if ((State == EnemyState.Patrol || State == EnemyState.Return) && Arrived())
            {
                PatrolIndex = (PatrolIndex + 1) % patrolPoints.Length;
                if (State == EnemyState.Return) Change(EnemyState.Patrol);
                Go(patrolPoints[PatrolIndex]);
            }
            if (!SearchingAtPoint)
            {
                pendingTime = agent.pathPending ? pendingTime + dt : 0;
                if (pendingTime > 5) throw new InvalidOperationException("Navigation computation timed out");
                if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) throw new InvalidOperationException("Navigation path is " + agent.pathStatus);
                var movement = agent.transform.position - lastPosition; movement.y = 0;
                stalledTime = !Arrived() && movement.magnitude < .01f * dt ? stalledTime + dt : 0;
                if (stalledTime > 5) throw new InvalidOperationException("Enemy stalled before destination");
            }
            lastPosition = agent.transform.position;
        }
    }
}
