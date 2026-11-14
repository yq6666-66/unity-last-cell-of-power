using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using LastCellOfPower.StealthSample;

namespace LastCellOfPower.WarehouseExpanded
{
    public enum ExpandedEnemyState { Patrol, Alert, Chase, Search, Return, Caught, Disrupted, Investigate }

    /// <summary>Session-driven guard. Only sensed positions enter its navigation memory.</summary>
    public sealed class ExpandedEnemy : MonoBehaviour
    {
        public ExpandedGameplayProfile profile;
        public ExpandedLevelDefinition level;
        public string guardId = "01";
        public ExpandedLevelDefinition.PatrolStop[] routeOverride = new ExpandedLevelDefinition.PatrolStop[0];
        public ExpandedLevelDefinition.PatrolStop[] PatrolRoute => routeOverride!=null && routeOverride.Length>0?routeOverride:level?level.patrolStops:Array.Empty<ExpandedLevelDefinition.PatrolStop>();
        public NavMeshAgent agent;
        public StealthPerception perception;
        public ExpandedEnemyState State { get; private set; }
        public float Awareness01 { get; private set; }
        public Vector3 LastSeen { get; private set; }
        public bool HasLastSeen { get; private set; }
        public Vector3 Destination { get; private set; }
        public int PatrolIndex { get; private set; }
        public string Failure { get; private set; } = "";
        public string NavigationDiagnostic { get; private set; } = "";
        public float CaptureElapsed { get; private set; }
        public float SearchElapsed { get; private set; }
        public bool SensedThisTick { get; private set; }
        public float DisruptionRemaining { get; private set; }
        public Vector3 NoiseTarget { get; private set; }
        public int NoiseEventId { get; private set; }
        public float InvestigationElapsed { get; private set; }
        public float InvestigationDwellElapsed { get; private set; }
        public bool HasConfirmedPlayer {get;private set;}
        public bool NavigationBarrierHeld=>navigationBarrierHold;
        public bool CanBeDisrupted => initialized && active && string.IsNullOrEmpty(Failure) &&
            (State == ExpandedEnemyState.Alert || State == ExpandedEnemyState.Chase) && agent && agent.enabled && agent.isOnNavMesh;
        public event Action<ExpandedEnemyState, ExpandedEnemyState> StateChanged;
        ExpandedEnemyState disruptedFrom;
        float disruptedAwareness;
        bool disruptionAppliedThisTick;
        float investigationLimit,investigationDwell;
        bool investigationWasConfirmed;
        readonly HashSet<int> handledNoiseEvents=new HashSet<int>();

        readonly Vector3[] searchPoints = new Vector3[3];
        NavMeshPath candidatePath;
        readonly HashSet<string> reportedNavigationIssues = new HashSet<string>();
        bool initialized, active, holding, hasDestination, patrolWaiting, reacquireArmed,navigationBarrierHold;
        int searchIndex;
        float waitElapsed, retryClock, chaseFailureElapsed, stallElapsed;
        Vector3 previousPosition;

        public void Initialize()
        {
            ValidateReferences();
            if (candidatePath == null) candidatePath = new NavMeshPath();
            profile.Validate();
            level.Validate();
            if (profile.alertSeconds <= 0 || profile.alertDecaySeconds <= 0 || profile.captureSeconds <= 0 || profile.searchSeconds <= 0)
                throw new InvalidOperationException("Guard timing must be positive");
            perception.viewDistance = profile.viewDistance;
            perception.viewAngle = profile.viewAngle;
            perception.Initialize();
            State = ExpandedEnemyState.Patrol;
            Awareness01 = CaptureElapsed = SearchElapsed = DisruptionRemaining = 0;
            disruptionAppliedThisTick = false;
            LastSeen = Destination = Vector3.zero;
            HasLastSeen = SensedThisTick = false;
            Failure = NavigationDiagnostic = "";
            reportedNavigationIssues.Clear();
            handledNoiseEvents.Clear();NoiseEventId=0;NoiseTarget=Vector3.zero;InvestigationElapsed=InvestigationDwellElapsed=0;HasConfirmedPlayer=false;navigationBarrierHold=false;
            PatrolIndex = 0;
            retryClock = chaseFailureElapsed = stallElapsed = waitElapsed = 0;
            holding = hasDestination = patrolWaiting = false;
            reacquireArmed = true;
            active = false;
            initialized = true;
            previousPosition = agent.transform.position;
            agent.speed = profile.patrolSpeed;
            foreach(var stop in PatrolRoute)
                if(float.IsNaN(stop.position.x)||float.IsNaN(stop.position.y)||float.IsNaN(stop.position.z)||float.IsNaN(stop.waitSeconds)||stop.waitSeconds<0)
                    throw new InvalidOperationException("Invalid patrol override");
            SelectPatrolDestination();
            ApplyMotion();
        }

        void ValidateReferences()
        {
            if (!profile || !level || !agent || !perception || !perception.observer || !perception.target)
                throw new InvalidOperationException("Expanded guard references missing");
            if (PatrolRoute == null || PatrolRoute.Length < 2)
                throw new InvalidOperationException("Expanded guard needs at least two patrol stops");
            if(string.IsNullOrWhiteSpace(guardId))throw new InvalidOperationException("Guard id is required");
            if (!agent.enabled || !agent.isOnNavMesh)
                throw new InvalidOperationException("Expanded guard requires an enabled agent on baked navigation");
        }

        public void SetActive(bool value)
        {
            active = value;
            if (agent) previousPosition = agent.transform.position;
            ApplyMotion();
        }
        public void SetNavigationBarrierHold(bool value){navigationBarrierHold=value;ApplyMotion();}

        void ApplyMotion()
        {
            if (!agent || !agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = !active || holding || navigationBarrierHold || State == ExpandedEnemyState.Alert || State == ExpandedEnemyState.Caught || State == ExpandedEnemyState.Disrupted;
            agent.updateRotation = !(holding || State == ExpandedEnemyState.Alert || State == ExpandedEnemyState.Caught || State == ExpandedEnemyState.Disrupted);
        }

        public void Tick(float dt)
        {
            if (!initialized || !active || State == ExpandedEnemyState.Caught) return;
            try
            {
                if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
                ValidateReferences();
                if (State == ExpandedEnemyState.Disrupted)
                {
                    TickDisrupted(dt);
                    return;
                }
                perception.Evaluate();
                float range = FlatDistance(agent.transform.position, perception.target.position);
                // Close contact can raise suspicion from behind, but never bypasses Alert.
                SensedThisTick = perception.Visible || (!perception.Obstructed && range <= .9f);
                if (!SensedThisTick) reacquireArmed = true;
                if (SensedThisTick)
                {
                    LastSeen = perception.target.position;
                    HasLastSeen = true;
                }

                if ((State == ExpandedEnemyState.Search || State == ExpandedEnemyState.Return) && SensedThisTick && reacquireArmed)
                {
                    if(HasConfirmedPlayer)BeginChase();else ChangeState(ExpandedEnemyState.Alert);
                    return; // Capture confirmation starts on a subsequent simulation tick.
                }
                if (State == ExpandedEnemyState.Patrol && SensedThisTick)
                    ChangeState(ExpandedEnemyState.Alert);
                if(State==ExpandedEnemyState.Investigate && SensedThisTick)
                {
                    if(investigationWasConfirmed){BeginChase();return;}
                    ChangeState(ExpandedEnemyState.Alert);
                }

                switch (State)
                {
                    case ExpandedEnemyState.Alert: TickAlert(dt); break;
                    case ExpandedEnemyState.Chase: TickChase(dt, range); break;
                    case ExpandedEnemyState.Search: TickSearch(dt); break;
                    case ExpandedEnemyState.Investigate: TickInvestigation(dt); break;
                    case ExpandedEnemyState.Patrol:
                    case ExpandedEnemyState.Return: TickPatrol(dt); break;
                }
                previousPosition = agent.transform.position;
                ApplyMotion();
            }
            catch (Exception e)
            {
                Failure = e.Message;
                active = false;
                ApplyMotion();
                throw;
            }
        }

        public bool HasHandledNoise(int eventId)=>handledNoiseEvents.Contains(eventId);
        public void IgnoreNoise(int eventId){if(eventId>0)handledNoiseEvents.Add(eventId);}
        public bool TryGetNavigationDistance(Vector3 point,out float distance)
        {
            distance=float.PositiveInfinity;
            if(!initialized || !agent || !agent.enabled || !agent.isOnNavMesh)return false;
            if(!NavMesh.SamplePosition(point,out var hit,.6f,agent.areaMask))return false;
            if(!agent.CalculatePath(hit.position,candidatePath)||candidatePath.status!=NavMeshPathStatus.PathComplete)return false;
            distance=0;var corners=candidatePath.corners;
            for(int i=1;i<corners.Length;i++)distance+=Vector3.Distance(corners[i-1],corners[i]);
            return true;
        }
        public bool LastCalculatedPathIntersects(Bounds barrier)
        {
            if(candidatePath==null || candidatePath.status!=NavMeshPathStatus.PathComplete)return false;
            var points=candidatePath.corners;
            for(int i=1;i<points.Length;i++)
            {
                var from=points[i-1]+Vector3.up*Mathf.Max(.1f,agent.height*.5f);
                var to=points[i]+Vector3.up*Mathf.Max(.1f,agent.height*.5f);
                var delta=to-from;
                if(barrier.Contains(from) || barrier.Contains(to) || delta.sqrMagnitude>.00001f && barrier.IntersectRay(new Ray(from,delta.normalized),out float distance) && distance<=delta.magnitude)
                    return true;
            }
            return false;
        }
        public bool TryInvestigateNoise(int eventId,Vector3 point,float maximumPath,float maximumSeconds,float dwellSeconds)
        {
            if(eventId<=0 || maximumPath<=0 || maximumSeconds<=0 || dwellSeconds<0)throw new ArgumentOutOfRangeException(nameof(eventId));
            if(!initialized || !active || handledNoiseEvents.Contains(eventId) || State==ExpandedEnemyState.Caught || State==ExpandedEnemyState.Disrupted || State==ExpandedEnemyState.Investigate)return false;
            // Current visual/contact evidence wins. A live signal can still be considered after loss of sight.
            perception.Evaluate();
            if(perception.Visible || (!perception.Obstructed && FlatDistance(agent.transform.position,perception.target.position)<=.9f))return false;
            if(!TryGetNavigationDistance(point,out var distance) || distance>maximumPath)return false;
            if(!TryDestination(point))return false;
            handledNoiseEvents.Add(eventId);NoiseEventId=eventId;NoiseTarget=point;InvestigationElapsed=InvestigationDwellElapsed=0;
            investigationLimit=maximumSeconds;investigationDwell=dwellSeconds;investigationWasConfirmed=HasConfirmedPlayer;
            ChangeState(ExpandedEnemyState.Investigate);hasDestination=true;holding=false;retryClock=.35f;agent.speed=profile.patrolSpeed;ApplyMotion();
            Debug.Log($"TACTICS_NOISE_ACCEPTED guard={guardId} event={eventId} destination={NoiseTarget:F3} path={distance:F3}");return true;
        }
        void TickInvestigation(float dt)
        {
            InvestigationElapsed=Mathf.Min(investigationLimit,InvestigationElapsed+dt);
            if(InvestigationElapsed>=investigationLimit){FinishInvestigation();return;}
            if(hasDestination && Arrived())
            {
                holding=true;InvestigationDwellElapsed=Mathf.Min(investigationDwell,InvestigationDwellElapsed+dt);agent.transform.Rotate(0,75*Mathf.Min(dt,investigationDwell),0);
                if(InvestigationDwellElapsed>=investigationDwell)FinishInvestigation();
                return;
            }
            retryClock-=dt;
            if(PathBroken() || UpdateStall(dt)>=1 || !hasDestination)
            {
                holding=true;
                if(retryClock<=0){hasDestination=TryDestination(NoiseTarget);holding=!hasDestination;retryClock=.35f;stallElapsed=0;}
            }
        }
        void FinishInvestigation()
        {
            ChangeState(ExpandedEnemyState.Return);agent.speed=profile.patrolSpeed;SelectPatrolDestination();
        }

        public bool TryDisrupt(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!CanBeDisrupted) return false;
            disruptedFrom = State; disruptedAwareness = Awareness01;
            DisruptionRemaining = seconds; disruptionAppliedThisTick = true;
            SensedThisTick = false;
            ChangeState(ExpandedEnemyState.Disrupted);
            CaptureElapsed = 0;
            ApplyMotion();
            return true;
        }

        void TickDisrupted(float dt)
        {
            // A pulse is committed before this same frame's enemy Tick. Start
            // timing on the next step; never let that frame also capture.
            SensedThisTick = false;
            if (disruptionAppliedThisTick) { disruptionAppliedThisTick = false; ApplyMotion(); return; }
            DisruptionRemaining = Mathf.Max(0, DisruptionRemaining - dt);
            if (DisruptionRemaining > 0) { ApplyMotion(); return; }
            perception.Evaluate();
            float distance = FlatDistance(agent.transform.position, perception.target.position);
            SensedThisTick = perception.Visible || (!perception.Obstructed && distance <= .9f);
            if (SensedThisTick) { LastSeen = perception.target.position; HasLastSeen = true; }
            if (disruptedFrom == ExpandedEnemyState.Chase)
            {
                if (SensedThisTick) BeginChase(); else BeginSearch();
            }
            else
            {
                ChangeState(ExpandedEnemyState.Alert);
                Awareness01 = disruptedAwareness;
            }
            CaptureElapsed = 0;
            previousPosition = agent.transform.position;
            ApplyMotion();
        }

        void TickAlert(float dt)
        {
            if (SensedThisTick)
            {
                Face(LastSeen - agent.transform.position, dt);
                Awareness01 = Mathf.Min(1, Awareness01 + dt / profile.alertSeconds);
                if (Awareness01 >= 1) BeginChase();
            }
            else
            {
                Awareness01 = Mathf.Max(0, Awareness01 - dt / profile.alertDecaySeconds);
                if (Awareness01 <= 0)
                {
                    ChangeState(ExpandedEnemyState.Patrol);
                    SelectPatrolDestination();
                }
            }
        }

        void BeginChase()
        {
            HasConfirmedPlayer=true;
            ChangeState(ExpandedEnemyState.Chase);
            Awareness01 = 1;
            agent.speed = profile.chaseSpeed;
            chaseFailureElapsed = 0;
            retryClock = .2f;
            hasDestination = TryDestination(LastSeen);
            holding = !hasDestination;
            ApplyMotion();
        }

        void TickChase(float dt, float range)
        {
            if (!SensedThisTick)
            {
                BeginSearch();
                return;
            }
            CaptureElapsed = range <= profile.captureDistance && !perception.Obstructed ? CaptureElapsed + dt : 0;
            if (CaptureElapsed >= profile.captureSeconds)
            {
                ChangeState(ExpandedEnemyState.Caught);
                holding = true;
                return;
            }

            retryClock -= dt;
            if (retryClock <= 0)
            {
                // LastSeen was updated above only when the target was actually sensed.
                hasDestination = TryDestination(LastSeen);
                retryClock = .2f;
            }
            bool healthy = hasDestination && !PathBroken();
            if (healthy && !Arrived() && NotMoving(dt)) stallElapsed += dt;
            else stallElapsed = 0;
            if (!healthy || stallElapsed >= .4f) chaseFailureElapsed += dt;
            else chaseFailureElapsed = 0;
            holding = !healthy;
            if (chaseFailureElapsed >= 1)
            {
                Diagnose("Chase path unavailable for 1 second; searching last sighting");
                BeginSearch(true);
            }
        }

        void BeginSearch(bool navigationFailure = false)
        {
            ChangeState(ExpandedEnemyState.Search);
            // A continuously visible but unreachable target is not a new sighting.
            // Re-arm on actual loss of sight, rather than resetting the retry budget
            // by cycling Search -> Chase every frame.
            reacquireArmed = !navigationFailure;
            agent.speed = profile.patrolSpeed;
            SearchElapsed = 0;
            searchIndex = -1;
            Vector3 approach = LastSeen - agent.transform.position;
            approach.y = 0;
            if (approach.sqrMagnitude < .01f) approach = agent.transform.forward;
            approach.Normalize();
            var side = Vector3.Cross(Vector3.up, approach);
            searchPoints[0] = LastSeen;
            searchPoints[1] = LastSeen + side * 1.5f + approach * .5f;
            searchPoints[2] = LastSeen - side * 1.5f - approach * .5f;
            NextSearchPoint();
        }

        void NextSearchPoint()
        {
            waitElapsed = stallElapsed = 0;
            holding = false;
            while (++searchIndex < searchPoints.Length)
            {
                if (TryDestination(searchPoints[searchIndex])) { hasDestination = true; return; }
                Diagnose("Skipping unreachable search point " + searchIndex);
            }
            hasDestination = false;
            holding = true;
        }

        void TickSearch(float dt)
        {
            SearchElapsed += dt; // Includes travel time; it can never extend an unseen pursuit indefinitely.
            if (SearchElapsed >= profile.searchSeconds)
            {
                ChangeState(ExpandedEnemyState.Return);
                agent.speed = profile.patrolSpeed;
                SelectPatrolDestination();
                return;
            }
            if (hasDestination && (PathBroken() || (!Arrived() && UpdateStall(dt) >= 2)))
            {
                Diagnose("Skipping interrupted search path " + searchIndex);
                NextSearchPoint();
            }
            if (!hasDestination || Arrived())
            {
                holding = true;
                agent.transform.Rotate(0, 90 * dt, 0);
                waitElapsed += dt;
                if (hasDestination && waitElapsed >= .6f) NextSearchPoint();
            }
        }

        void TickPatrol(float dt)
        {
            agent.speed = profile.patrolSpeed;
            if (!hasDestination)
            {
                retryClock -= dt;
                if (retryClock <= 0) SelectPatrolDestination();
                return;
            }
            if (!patrolWaiting && (PathBroken() || (!Arrived() && UpdateStall(dt) >= 3)))
            {
                Diagnose("Skipping unreachable or stalled patrol stop " + PatrolIndex);
                PatrolIndex = (PatrolIndex + 1) % PatrolRoute.Length;
                SelectPatrolDestination();
                return;
            }
            if (!patrolWaiting && Arrived())
            {
                if (State == ExpandedEnemyState.Return) ChangeState(ExpandedEnemyState.Patrol);
                patrolWaiting = true;
                holding = true;
                waitElapsed = 0;
            }
            if (!patrolWaiting) return;
            var stop = PatrolRoute[PatrolIndex];
            Face(Quaternion.Euler(0, stop.lookYaw, 0) * Vector3.forward, dt);
            waitElapsed += dt;
            if (waitElapsed >= Mathf.Max(0, stop.waitSeconds))
            {
                PatrolIndex = (PatrolIndex + 1) % PatrolRoute.Length;
                SelectPatrolDestination();
            }
        }

        void SelectPatrolDestination()
        {
            patrolWaiting = false;
            waitElapsed = stallElapsed = 0;
            for (int checkedStops = 0; checkedStops < PatrolRoute.Length; checkedStops++)
            {
                if (TryDestination(PatrolRoute[PatrolIndex].position))
                {
                    hasDestination = true;
                    holding = false;
                    ApplyMotion();
                    return;
                }
                Diagnose("Patrol stop not reachable: " + PatrolIndex);
                PatrolIndex = (PatrolIndex + 1) % PatrolRoute.Length;
            }
            // Dynamic doors may temporarily disconnect a region. Wait instead of
            // teleporting, throwing a gameplay-wide error or spinning forever.
            hasDestination = false;
            holding = true;
            retryClock = 1;
            Diagnose("No patrol stop currently reachable; retrying after 1 second");
        }

        bool TryDestination(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out var hit, .6f, agent.areaMask)) return false;
            if (!agent.CalculatePath(hit.position, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete) return false;
            if (!agent.SetPath(candidatePath)) return false;
            Destination = hit.position;
            return true;
        }
        bool PathBroken() => !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete;
        bool Arrived() => hasDestination && !agent.pathPending &&
            FlatDistance(agent.transform.position, Destination) <= agent.stoppingDistance + .18f &&
            agent.remainingDistance <= agent.stoppingDistance + .18f;
        bool NotMoving(float dt) => FlatDistance(agent.transform.position, previousPosition) < .01f * dt;
        float UpdateStall(float dt) { stallElapsed = NotMoving(dt) ? stallElapsed + dt : 0; return stallElapsed; }
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        void Face(Vector3 direction, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
                agent.transform.rotation = Quaternion.RotateTowards(agent.transform.rotation, Quaternion.LookRotation(direction), 180 * dt);
        }
        void Diagnose(string message)
        {
            if (NavigationDiagnostic == message) return;
            NavigationDiagnostic = message;
            if (reportedNavigationIssues.Add(message)) Debug.LogWarning("EXPANDED_NAV " + message);
        }
        void ChangeState(ExpandedEnemyState next)
        {
            if (State == next) return;
            var previous = State;
            State = next;
            holding = patrolWaiting = false;
            CaptureElapsed = waitElapsed = stallElapsed = 0;
            if (next == ExpandedEnemyState.Patrol) {Awareness01 = 0;HasConfirmedPlayer=false;}
            Debug.Log($"EXPANDED_STATE {next} awareness={Awareness01:F2} lastSeen={LastSeen:F2} patrol={PatrolIndex}");
            ApplyMotion();
            if (StateChanged != null) foreach (Action<ExpandedEnemyState, ExpandedEnemyState> listener in StateChanged.GetInvocationList())
                try { listener(previous, next); } catch (Exception e) { Debug.LogException(e); }
        }
    }
}
