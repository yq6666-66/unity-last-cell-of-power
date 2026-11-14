using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LastCellOfPower.StealthSample
{
    public sealed class StealthSession : MonoBehaviour
    {
        public StealthPlayerController player;
        public StealthEnemy enemy;
        public Font uiFont;
        public LineRenderer pathLine, sightLine, lastSeenLine;
        public bool Paused { get; private set; } = true;
        public bool DebugVisible { get; private set; }
        public bool Focused { get; private set; }
        public string Failure { get; private set; } = "";
        public bool Ready { get; private set; }
        public bool Caught => enemy && enemy.State == EnemyState.Caught;

        void Start()
        {
            Focused = Application.isFocused;
            if (!uiFont) uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
            try
            {
                if (!player || !enemy || !pathLine || !sightLine || !lastSeenLine)
                    throw new InvalidOperationException("Session player/enemy/debug lines missing");
                player.Initialize(); enemy.Initialize();
                Ready = true;
                Pause();
                Debug.Log("STEALTH_READY: click to capture mouse; WASD/arrows, Esc, F1, R after capture");
            }
            catch (Exception e) { Fail(e.Message); }
        }

        void Update()
        {
            if (Ready && (!player || !player.controller || !player.viewCamera || !enemy || !enemy.agent ||
                !enemy.perception || !enemy.perception.observer || !enemy.perception.target ||
                !pathLine || !sightLine || !lastSeenLine)) Fail("A required runtime reference was destroyed");
            var keyboard = Keyboard.current;
            if (Focused && keyboard != null && keyboard.f1Key.wasPressedThisFrame) DebugVisible = !DebugVisible;
            if (Focused && Caught && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                #if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
                return;
            }
            if (!Ready || Failure.Length > 0 || Caught) { DrawDebug(); return; }
            if (!Focused || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) Pause();
            else if (Paused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Resume();
            else if (!Paused && Cursor.lockState != CursorLockMode.Locked) Pause();
            if (!Paused)
            {
                try
                {
                    if (!player || !enemy) throw new InvalidOperationException("Session player or enemy was destroyed");
                    player.Tick(Time.deltaTime);
                    enemy.Tick(Time.deltaTime);
                    if (Caught) Pause();
                }
                catch (Exception e) { Fail(e.Message); }
            }
            DrawDebug();
        }

        public void Pause()
        {
            Paused = true;
            if (player) player.SetActive(false);
            if (enemy) enemy.SetActive(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Resume()
        {
            if (!Ready || !Focused || Caught || Failure.Length > 0) return;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Paused = false;
            player.SetActive(true); enemy.SetActive(true);
            Debug.Log("STEALTH_RESUME: waiting for neutral movement input");
        }

        public void Fail(string reason)
        {
            if (Failure.Length > 0) return;
            Failure = reason; Ready = false; Pause();
            Debug.LogError("STEALTH_ERROR: " + reason);
        }

        void OnApplicationFocus(bool value)
        {
            Focused = value;
            Debug.Log("STEALTH_FOCUS " + value);
            if (!value) Pause();
        }

        void OnDisable() { Pause(); }

        void DrawDebug()
        {
            bool show = DebugVisible && Ready;
            if (pathLine) pathLine.enabled = show;
            if (sightLine) sightLine.enabled = show;
            if (lastSeenLine) lastSeenLine.enabled = show && enemy.HasLastSeen;
            if (!show) return;
            if (enemy.agent && enemy.agent.isOnNavMesh)
            {
                var points = enemy.agent.path.corners;
                pathLine.positionCount = points.Length;
                for (int i = 0; i < points.Length; i++) pathLine.SetPosition(i, points[i] + Vector3.up * .15f);
            }
            sightLine.positionCount = 2;
            sightLine.SetPosition(0, enemy.perception.Eye);
            sightLine.SetPosition(1, enemy.perception.RayEnd);
            if (enemy.HasLastSeen)
            {
                lastSeenLine.positionCount = 2;
                lastSeenLine.SetPosition(0, enemy.LastSeen);
                lastSeenLine.SetPosition(1, enemy.LastSeen + Vector3.up * 1.5f);
            }
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true };
            if (uiFont) style.font = uiFont;
            GUI.Box(new Rect(12,12,650,54), "最后一格电 / 潜行样本\nWASD / 方向键移动 · 鼠标转向 · Esc 暂停 · F1 调试", style);
            if (Failure.Length > 0) GUI.Box(new Rect(12,74,700,120), "运行错误 / 本局已停止\n" + Failure, style);
            else if (Caught) GUI.Box(new Rect(12,74,650,66), "被抓住了\n按 R 重开", style);
            else if (Paused) GUI.Box(new Rect(12,74,650,66), "已暂停\n点击画面继续；先松开移动键", style);
            if (DebugVisible && Ready)
            {
                var p = enemy.perception;
                GUI.Box(new Rect(12,150,700,210), $"State: {enemy.State}   Patrol: {enemy.PatrolIndex + 1}/4\nVisible: {p.Visible}   Obstructed: {p.Obstructed}\nDestination: {enemy.Destination:F2}\nLast seen: {(enemy.HasLastSeen ? enemy.LastSeen.ToString("F2") : "none")}   Search: {enemy.SearchElapsed:F2}\nFocused: {Focused}   Paused: {Paused}   Neutral gate: {player.WaitingForNeutral}\nInput: {player.InputValue}   Position: {player.controller.transform.position:F2}\nPath: {(enemy.agent.isOnNavMesh ? enemy.agent.pathStatus.ToString() : "OFF NAVMESH")}", style);
            }
        }
    }
}
