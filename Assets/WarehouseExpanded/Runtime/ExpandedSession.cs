using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.ChapterTactics;

namespace LastCellOfPower.WarehouseExpanded
{
    public sealed class ExpandedSession : MonoBehaviour
    {
        public ExpandedGameplayProfile profile;
        public ExpandedLevelDefinition level;
        public StealthPlayerController player;
        public ExpandedEnemy enemy;
        public ExpandedEnemy[] enemies = new ExpandedEnemy[0];
        public TacticsDevices tactics;
        ExpandedEnemy[] legacyGuard;
        public IReadOnlyList<ExpandedEnemy> Guards
        {
            get
            {
                if(enemies!=null && enemies.Length>0)return enemies;
                if(!enemy)return Array.Empty<ExpandedEnemy>();
                if(legacyGuard==null || legacyGuard[0]!=enemy)legacyGuard=new[]{enemy};
                return legacyGuard;
            }
        }
        public EscapeBattery battery;
        public EscapeInteraction interaction;
        public EscapeDoor door;
        public EscapeRules rules;
        public Font uiFont;
        public EmergencyPulseController pulse;
        public RunStatistics statistics;
        public bool externalPlayerUI;
        public event Action<bool> PauseChanged;
        public event Action<RunSummary> RunEnded;
        public RunSummary Summary { get; private set; }
        bool resultPublished;
        public LineRenderer pathLine, sightLine, lastSeenLine;
        public RunOutcome Outcome { get; private set; } = RunOutcome.Running;
        public bool Ready { get; private set; }
        public bool Paused { get; private set; } = true;
        public bool Focused { get; private set; }
        public bool DebugVisible { get; private set; }
        public string Failure { get; private set; } = "";
        public string CurrentZone => level && player ? level.ZoneName(player.transform.position) : "";
        int energyBucket = -1;

        void ValidateReferences()
        {
            if (!profile || !level || !player || !player.controller || !player.viewCamera || !enemy || !enemy.agent ||
                !enemy.perception || !enemy.perception.observer || !enemy.perception.target || !battery || !interaction || !door || !rules ||
                !pathLine || !sightLine || !lastSeenLine || !door.leftLeaf || !door.rightLeaf || !door.doorCollider || !door.doorObstacle || !door.exitAnchor)
                throw new InvalidOperationException("Required expanded level reference missing");
            if (externalPlayerUI && (!pulse || !statistics)) throw new InvalidOperationException("Chapter core references missing");
            if (interaction.targets == null) throw new InvalidOperationException("Interaction targets missing");
            var guards=Guards;
            if(guards.Count==0)throw new InvalidOperationException("At least one guard is required");
            for(int i=0;i<guards.Count;i++)
            {
                var guard=guards[i];
                if(!guard || !guard.agent || !guard.perception || !guard.perception.observer || !guard.perception.target)
                    throw new InvalidOperationException("Guard reference missing: "+i);
                for(int j=0;j<i;j++)if(guards[j]==guard || string.Equals(guards[j].guardId,guard.guardId,StringComparison.Ordinal))
                    throw new InvalidOperationException("Guard references and ids must be unique");
            }
            foreach (var target in interaction.targets)
                if (!target || !target.anchor) throw new InvalidOperationException("Interaction anchor destroyed");
        }

        void Start()
        {
            Focused = Application.isFocused;
            try
            {
                if(!enemy && enemies!=null && enemies.Length>0)enemy=enemies[0];
                ValidateReferences(); profile.Validate(); level.Validate(); profile.ApplyTo(rules);
                if (!uiFont) uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
                player.speed = profile.playerSpeed;
                player.controller.enabled = false;
                player.transform.SetPositionAndRotation(level.playerSpawn, Quaternion.identity);
                player.controller.enabled = true;
                foreach(var guard in Guards)
                {
                    guard.profile=profile;guard.level=level;
                    guard.perception.viewDistance=profile.viewDistance;guard.perception.viewAngle=profile.viewAngle;
                }
                interaction.cancelOnMovement = true;
                interaction.tactics=tactics;
                player.Initialize(); battery.Initialize(); door.Initialize(); interaction.Initialize();
                foreach(var guard in Guards)guard.Initialize();
                if (statistics) statistics.Initialize();
                if(tactics)tactics.Initialize();
                if (pulse) { pulse.Initialize(); pulse.Resolved += OnPulseResolved; }
                Summary = null; resultPublished = false;
                Ready = true; Pause();
                Debug.Log("EXPANDED_READY energy=" + battery.Current + " spawn=" + player.transform.position + " patrolStops=" + level.patrolStops.Length);
            }
            catch (Exception e) { Fail(e.Message); }
        }

        void Update()
        {
            if (Ready) { try { ValidateReferences(); } catch (Exception e) { Fail(e.Message); } }
            var keyboard = Keyboard.current;
            if (Focused && keyboard != null && keyboard.f1Key.wasPressedThisFrame) DebugVisible = !DebugVisible;
            if (Focused && Outcome != RunOutcome.Running && keyboard != null && keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
            if (!Ready || Outcome != RunOutcome.Running) { DrawDebug(); return; }
            if (!Focused || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) Pause();
            else if (!externalPlayerUI && Paused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Resume();
            else if (!Paused && Cursor.lockState != CursorLockMode.Locked) Pause();
            if (!Paused) { try { Tick(Time.deltaTime); } catch (Exception e) { Fail(e.Message); } }
            DrawDebug();
        }

        public void Tick(float dt)
        {
            if (!Ready || Paused || Outcome != RunOutcome.Running) return;
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            ValidateReferences();
            if (statistics) statistics.Advance(dt);
            interaction.Prepare(); player.Tick(dt);
            if(tactics)tactics.Tick(dt);
            if (pulse) { pulse.Tick(dt); pulse.ResolvePendingRequest(); }
            foreach(var guard in Guards)
            {
                guard.Tick(dt);
                if(!string.IsNullOrEmpty(guard.Failure))throw new InvalidOperationException("Guard "+guard.guardId+": "+guard.Failure);
                if(guard.State==ExpandedEnemyState.Caught){End(RunOutcome.Caught);return;}
            }
            door.Tick(dt);
            interaction.Advance(dt, player.HorizontalDistanceLastTick > .0001f);
            if (battery.Depleted) { End(RunOutcome.Depleted); return; }
            if (door.HasEscaped(player.controller.transform.position)) { End(RunOutcome.Escaped); return; }
            int bucket = Mathf.FloorToInt(battery.Current / 5);
            if (bucket != energyBucket)
            {
                energyBucket = bucket;
                Debug.Log($"EXPANDED_ENERGY {battery.Current:F3} charging={interaction.Charging} supplying={interaction.Supplying} zone={CurrentZone} position={player.transform.position:F3}");
            }
        }

        void End(RunOutcome result)
        {
            if (Outcome != RunOutcome.Running) return;
            Outcome = result; Pause(); PublishResult();
            Debug.Log($"EXPANDED_OUTCOME {result} energy={battery.Current:F3} position={player.transform.position:F3}");
        }
        public void Fail(string reason)
        {
            if (Outcome == RunOutcome.Error) return;
            Failure = reason; Outcome = RunOutcome.Error; Ready = false; Pause(); PublishResult();
            Debug.LogError("EXPANDED_ERROR " + reason);
        }
        public void Pause()
        {
            bool changed = !Paused;
            Paused = true;
            if (pulse) pulse.SetActive(false);
            if (interaction) interaction.SetActive(false);
            if (player) { player.HorizontalMovementLocked = false; player.SetActive(false); }
            foreach(var guard in Guards)if(guard)guard.SetActive(false);
            if(tactics)tactics.SetActive(false);
            if (battery) battery.StopRate();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (changed) NotifyPause(true);
        }
        public void Resume()
        {
            if (!Ready || !Focused || Outcome != RunOutcome.Running || !Paused) return;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; Paused = false;
            player.SetActive(true); interaction.SetActive(true);
            foreach(var guard in Guards)guard.SetActive(true);
            if(tactics)tactics.SetActive(true);
            if (pulse) pulse.SetActive(true);
            NotifyPause(false);
            Debug.Log("EXPANDED_RESUME: release movement and E before accepting input");
        }
        public void Restart()
        {
            if (Outcome == RunOutcome.Running) return;
            Pause(); Debug.Log("EXPANDED_RESTART from=" + Outcome);
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
        }
        void OnPulseResolved(PulseUseResult result)
        {
            if (result == PulseUseResult.Success && statistics) statistics.RecordPulse();
        }
        void NotifyPause(bool value)
        {
            if (PauseChanged != null) foreach (Action<bool> listener in PauseChanged.GetInvocationList())
                try { listener(value); } catch (Exception e) { Debug.LogException(e); }
        }
        void PublishResult()
        {
            if (resultPublished) return;
            resultPublished = true;
            Summary = statistics ? statistics.Complete(Outcome, battery ? battery.Current : 0, interaction && interaction.Powered) :
                new RunSummary(Outcome, 0, battery ? battery.Current : 0, pulse ? pulse.SuccessfulUses : 0, interaction && interaction.Powered,
                    tactics?tactics.UsedNoiseCount:0,tactics&&tactics.ShutterUsed);
            if (RunEnded != null) foreach (Action<RunSummary> listener in RunEnded.GetInvocationList())
                try { listener(Summary); } catch (Exception e) { Debug.LogException(e); }
        }
        void OnDestroy() { if (pulse) pulse.Resolved -= OnPulseResolved; }
        void OnApplicationFocus(bool value) { Focused = value; Debug.Log("EXPANDED_FOCUS " + value); if (!value) Pause(); }
        void OnDisable() { Pause(); }

        void DrawDebug()
        {
            bool show = DebugVisible && Ready;
            if (pathLine) pathLine.enabled = show;
            if (sightLine) sightLine.enabled = show;
            if (lastSeenLine) lastSeenLine.enabled = show && enemy && enemy.HasLastSeen;
            if (!show) return;
            pathLine.positionCount = 0;
            if (enemy.agent.isOnNavMesh)
            {
                var corners = enemy.agent.path.corners; pathLine.positionCount = corners.Length;
                for (int i = 0; i < corners.Length; i++) pathLine.SetPosition(i, corners[i] + Vector3.up * .15f);
            }
            sightLine.positionCount = 2;
            sightLine.SetPosition(0, enemy.perception.Eye); sightLine.SetPosition(1, enemy.perception.RayEnd);
            if (enemy.HasLastSeen)
            {
                lastSeenLine.positionCount = 2;
                lastSeenLine.SetPosition(0, enemy.LastSeen); lastSeenLine.SetPosition(1, enemy.LastSeen + Vector3.up * 1.5f);
            }
        }

        string ThreatDirection()
        {
            Vector3 local = player.transform.InverseTransformDirection(enemy.transform.position - player.transform.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(yaw) <= 35) return "前方";
            if (Mathf.Abs(yaw) >= 145) return "后方";
            return yaw > 0 ? "右侧" : "左侧";
        }

        string MultiGuardDebug()
        {
            var rows=new List<string>{$"Outcome: {Outcome}  Energy: {battery.Current:F3}  Paused: {Paused}  Focused: {Focused}"};
            foreach(var guard in Guards)
                rows.Add($"[{guard.guardId}] {guard.State} awareness={guard.Awareness01:F2} confirmed={guard.HasConfirmedPlayer} capture={guard.CaptureElapsed:F2}\n  destination={guard.Destination:F2} lastSeen={guard.LastSeen:F2} noise={guard.NoiseEventId}/{guard.InvestigationElapsed:F2}s gateHold={guard.NavigationBarrierHeld}");
            if(tactics)
            {
                rows.Add($"Shutter: {tactics.shutter.State} remaining={tactics.shutter.ClosedRemaining:F2} used={tactics.ShutterUsed}");
                foreach(var noise in tactics.noiseDevices)rows.Add($"Noise {noise.deviceId}: {noise.State} remaining={noise.SignalRemaining:F2} event={noise.EventId}");
            }
            rows.Add("E release="+interaction.WaitingForRelease+" Q target="+(pulse && pulse.SelectedTarget?pulse.SelectedTarget.guardId:"none"));
            return string.Join("\n",rows);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (uiFont) style.font = uiFont;
            if (externalPlayerUI)
            {
                if(DebugVisible && Ready && Guards.Count>1)
                    GUI.Box(new Rect(12,170,Mathf.Min(940,Screen.width-24),340),MultiGuardDebug(),style);
                else if (DebugVisible && Ready)
                    GUI.Box(new Rect(12, 170, 700, 240), $"Outcome: {Outcome} State: {enemy.State} Awareness: {enemy.Awareness01:F2}\nEnergy: {battery.Current:F3} Rate: {battery.LastNetRate:F2}\nPulse: {(pulse ? pulse.LastResult.ToString() : "none")} CD: {(pulse ? pulse.CooldownRemaining : 0):F2} Disrupted: {enemy.DisruptionRemaining:F2}\nInteraction: {(interaction.Current ? interaction.Current.label : "none")} Supply: {interaction.SupplyElapsed:F2}\nPaused: {Paused} Focused: {Focused} E release: {interaction.WaitingForRelease}\nPosition: {player.transform.position:F3} LastSeen: {enemy.LastSeen:F3}\nTarget: {enemy.Destination:F3} Sensed: {enemy.SensedThisTick}", style);
                return;
            }
            float energy = battery ? battery.Current : 0;
            float maximum = rules ? rules.maximumEnergy : 100;
            GUI.Box(new Rect(12, 12, 430, 76), $"最后一格电 · {CurrentZone}\n电量 {energy:F1} / {maximum:0}   WASD 移动 · E 交互 · Esc 暂停", style);
            Color color = GUI.color; GUI.color = energy < 20 ? new Color(1, .4f, .2f) : new Color(.35f, .95f, .7f);
            GUI.DrawTexture(new Rect(20, 72, 410 * Mathf.Clamp01(energy / Mathf.Max(1, maximum)), 7), Texture2D.whiteTexture); GUI.color = color;
            string message;
            if (Outcome == RunOutcome.Error) message = "运行错误：" + Failure + "\n按 R 重开";
            else if (Outcome == RunOutcome.Caught) message = "被抓住了 · 按 R 重开";
            else if (Outcome == RunOutcome.Depleted) message = "电量耗尽 · 按 R 重开";
            else if (Outcome == RunOutcome.Escaped) message = "逃生成功 · 按 R 再来一局";
            else if (Paused) message = "已暂停 · 点击继续，先松开移动键和 E";
            else if (interaction && interaction.Powered) message = door.State == EscapeDoorState.Open ? "目标：从装卸出口区的北门离开" : "北门正在打开";
            else message = energy < (rules ? rules.supplyStartMinimum : 36) ? "目标：补充电量，再前往设备区供电终端" : "目标：前往设备区，为北门终端供电";
            if (energy < 20 && Outcome == RunOutcome.Running) message += "\n低电量：请寻找充电座";
            GUI.Box(new Rect(12, 94, 530, 66), message, style);
            if (Ready && Outcome == RunOutcome.Running)
            {
                bool threat = enemy.State == ExpandedEnemyState.Alert || enemy.State == ExpandedEnemyState.Chase;
                if (threat)
                {
                    float width = Mathf.Min(400, Screen.width - 24);
                    string warning = enemy.State == ExpandedEnemyState.Alert ? "正在被发现" : "正在被追击";
                    GUI.Box(new Rect(Screen.width - width - 12, 12, width, 58), warning + " · " + ThreatDirection() + "\n利用货架或墙角切断视线", style);
                    GUI.color = new Color(1, .55f, .2f);
                    GUI.DrawTexture(new Rect(Screen.width - width - 4, 62, (width - 16) * Mathf.Clamp01(enemy.Awareness01), 5), Texture2D.whiteTexture);
                    GUI.color = color;
                }
                if (!Paused)
                {
                    GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 12, 16, 24), "+", style);
                    string hint = interaction.Charging ? $"充电中 {energy:F1} / {maximum:0} · 移动或松开 E 可离开" :
                        interaction.Supplying ? $"供电 {interaction.SupplyElapsed:F1} / {rules.supplySeconds:0.#} 秒 · 移动可取消" : interaction.Hint;
                    if (!string.IsNullOrEmpty(hint)) GUI.Box(new Rect(Screen.width / 2 - 240, Screen.height - 80, 480, 58), hint, style);
                }
            }
            if (DebugVisible && Ready)
                GUI.Box(new Rect(12, 170, 690, 220), $"Outcome: {Outcome} State: {enemy.State} Awareness: {enemy.Awareness01:F2}\nPatrol: {enemy.PatrolIndex + 1}/{level.patrolStops.Length} Door: {door.State}\nEnergy: {energy:F3} Rate: {battery.LastNetRate:F2} Move: {player.HorizontalDistanceLastTick:F4}\nInteraction: {(interaction.Current ? interaction.Current.label : "none")} Supply: {interaction.SupplyElapsed:F2}\nPaused: {Paused} Focused: {Focused} E release: {interaction.WaitingForRelease}\nPosition: {player.transform.position:F3} LastSeen: {enemy.LastSeen:F3}\nTarget: {enemy.Destination:F3} Visible: {enemy.perception.Visible}", style);
        }
    }
}
