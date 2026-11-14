using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastCellOfPower.StealthSample;
namespace LastCellOfPower.EscapeSample
{
    public enum RunOutcome { Running, Caught, Depleted, Escaped, Error }
    public sealed class EscapeSession : MonoBehaviour
    {
        public StealthPlayerController player;
        public StealthEnemy enemy;
        public EscapeBattery battery;
        public EscapeInteraction interaction;
        public EscapeDoor door;
        public EscapeRules rules;
        public Font uiFont;
        public LineRenderer pathLine,sightLine,lastSeenLine;
        public RunOutcome Outcome {get;private set;}=RunOutcome.Running;
        public bool Ready {get;private set;}
        public bool Paused {get;private set;}=true;
        public bool Focused {get;private set;}
        public bool DebugVisible {get;private set;}
        public string Failure {get;private set;}="";
        int energyBucket=-1;

        void ValidateReferences()
        {
            if(!player || !player.controller || !player.viewCamera || !enemy || !enemy.agent || !enemy.perception || !enemy.perception.observer || !enemy.perception.target || !battery || !interaction || !door || !rules || !pathLine || !sightLine || !lastSeenLine)
                throw new InvalidOperationException("Required escape runtime reference missing");
            if(!door.leftLeaf || !door.rightLeaf || !door.doorCollider || !door.doorObstacle || interaction.targets==null)
                throw new InvalidOperationException("Required door or interaction reference missing");
            foreach(var target in interaction.targets)if(!target || !target.anchor)throw new InvalidOperationException("Required interaction target missing");
        }
        void Start()
        {
            Focused=Application.isFocused;
            try
            {
                ValidateReferences();rules.Validate();
                if(!uiFont)uiFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
                player.Initialize();battery.Initialize();door.Initialize();interaction.Initialize();enemy.Initialize();
                Ready=true;Pause();Debug.Log("ESCAPE_READY energy="+battery.Current+" outcome="+Outcome);
            }
            catch(Exception e){Fail(e.Message);}
        }
        void Update()
        {
            if(Ready){try{ValidateReferences();}catch(Exception e){Fail(e.Message);}}
            var k=Keyboard.current;
            if(Focused && k!=null && k.f1Key.wasPressedThisFrame)DebugVisible=!DebugVisible;
            if(Focused && Outcome!=RunOutcome.Running && k!=null && k.rKey.wasPressedThisFrame){Restart();return;}
            if(!Ready || Outcome!=RunOutcome.Running){DrawDebug();return;}
            if(!Focused || (k!=null && k.escapeKey.wasPressedThisFrame))Pause();
            else if(Paused && Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame)Resume();
            else if(!Paused && Cursor.lockState!=CursorLockMode.Locked)Pause();
            if(!Paused)
            {
                try{Tick(Time.deltaTime);}catch(Exception e){Fail(e.Message);}
            }
            DrawDebug();
        }
        // Only the running session owns simulation time. Public for isolated
        // logic checks; production input never calls state setters or teleports.
        public void Tick(float dt)
        {
            if(!Ready || Paused || Outcome!=RunOutcome.Running)return;
            ValidateReferences();
            interaction.Prepare();player.Tick(dt);enemy.Tick(dt);
            // Resolve capture first; the player cannot finish supplying or win
            // on the same frame the guard catches them.
            if(enemy.State==EnemyState.Caught){End(RunOutcome.Caught);return;}
            door.Tick(dt);
            interaction.Advance(dt,player.HorizontalDistanceLastTick>.0001f);
            if(battery.Depleted){End(RunOutcome.Depleted);return;}
            if(door.HasEscaped(player.controller.transform.position)){End(RunOutcome.Escaped);return;}
            int bucket=Mathf.FloorToInt(battery.Current/5);
            if(bucket!=energyBucket){energyBucket=bucket;Debug.Log($"ESCAPE_ENERGY {battery.Current:F3} charging={interaction.Charging} supplying={interaction.Supplying} position={player.controller.transform.position:F3}");}
        }
        public static RunOutcome ResolveOutcome(bool error,bool caught,bool depleted,bool escaped)
            =>error?RunOutcome.Error:caught?RunOutcome.Caught:depleted?RunOutcome.Depleted:escaped?RunOutcome.Escaped:RunOutcome.Running;
        void End(RunOutcome result)
        {
            if(Outcome!=RunOutcome.Running)return;
            Outcome=result;Pause();Debug.Log($"ESCAPE_OUTCOME {result} energy={battery.Current:F3} position={player.controller.transform.position:F3}");
        }
        public void Fail(string reason)
        {
            if(Outcome==RunOutcome.Error)return;
            Failure=reason;Outcome=RunOutcome.Error;Ready=false;Pause();Debug.LogError("ESCAPE_ERROR "+reason);
        }
        public void Pause()
        {
            Paused=true;if(interaction)interaction.SetActive(false);
            if(player){player.HorizontalMovementLocked=false;player.SetActive(false);}
            if(enemy)enemy.SetActive(false);if(battery)battery.StopRate();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void Resume()
        {
            if(!Ready || !Focused || Outcome!=RunOutcome.Running)return;
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;Paused=false;
            player.SetActive(true);interaction.SetActive(true);enemy.SetActive(true);
            Debug.Log("ESCAPE_RESUME: movement and E require neutral input");
        }
        public void Restart()
        {
            if(Outcome==RunOutcome.Running)return;
            Pause();Debug.Log("ESCAPE_RESTART from="+Outcome);
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
        }
        void OnApplicationFocus(bool value){Focused=value;Debug.Log("ESCAPE_FOCUS "+value);if(!value)Pause();}
        void OnDisable(){Pause();}
        void DrawDebug()
        {
            bool show=DebugVisible && Ready;
            if(pathLine)pathLine.enabled=show;if(sightLine)sightLine.enabled=show;if(lastSeenLine)lastSeenLine.enabled=show && enemy && enemy.HasLastSeen;
            if(!show)return;
            if(enemy.agent.isOnNavMesh){var corners=enemy.agent.path.corners;pathLine.positionCount=corners.Length;for(int i=0;i<corners.Length;i++)pathLine.SetPosition(i,corners[i]+Vector3.up*.15f);}
            sightLine.positionCount=2;sightLine.SetPosition(0,enemy.perception.Eye);sightLine.SetPosition(1,enemy.perception.RayEnd);
            if(enemy.HasLastSeen){lastSeenLine.positionCount=2;lastSeenLine.SetPosition(0,enemy.LastSeen);lastSeenLine.SetPosition(1,enemy.LastSeen+Vector3.up*1.5f);}
        }
        void OnGUI()
        {
            var style=new GUIStyle(GUI.skin.box){fontSize=18,alignment=TextAnchor.UpperLeft,wordWrap=true};if(uiFont)style.font=uiFont;
            float energy=battery?battery.Current:0;
            GUI.Box(new Rect(12,12,410,72),$"最后一格电   电量 {energy:F1} / {(rules?rules.maximumEnergy:100):0}\nWASD 移动 · E 交互 · Esc 暂停 · F1 调试",style);
            var previous=GUI.color;GUI.color=energy<20?new Color(1,.35f,.2f):new Color(.35f,.95f,.7f);
            GUI.DrawTexture(new Rect(20,66,390*Mathf.Clamp01(energy/(rules?rules.maximumEnergy:100)),8),Texture2D.whiteTexture);GUI.color=previous;
            string message;
            if(Outcome==RunOutcome.Error)message="运行错误："+Failure+"\n按 R 重开";
            else if(Outcome==RunOutcome.Caught)message="被抓住了\n按 R 重开";
            else if(Outcome==RunOutcome.Depleted)message="电量耗尽\n按 R 重开";
            else if(Outcome==RunOutcome.Escaped)message="逃生成功\n按 R 再来一局";
            else if(Paused)message="已暂停：点击继续，先松开移动键和 E";
            else if(interaction && interaction.Powered)message=door.State==EscapeDoorState.Open?"目标：穿过北门逃生":"北门正在打开";
            else message=energy<(rules?rules.supplyStartMinimum:48)?$"目标：补充电量（供电需至少 {(rules?rules.supplyStartMinimum:48):0.#}）":"目标：为北门终端供电";
            if(energy<20 && Outcome==RunOutcome.Running)message+="\n低电量！尽快充电";
            GUI.Box(new Rect(12,90,550,68),message,style);
            if(Ready && Outcome==RunOutcome.Running && !Paused)
            {
                GUI.Label(new Rect(Screen.width/2-6,Screen.height/2-12,20,24),"+",style);
                string hint=interaction.Charging?$"充电中 {energy:F1} / {rules.maximumEnergy:0}（松开 E 结束）":interaction.Supplying?$"供电中 {interaction.SupplyElapsed:F1} / {rules.supplySeconds:0} 秒":interaction.Hint;
                if(hint.Length>0)GUI.Box(new Rect(Screen.width/2-230,Screen.height-80,460,58),hint,style);
            }
            if(DebugVisible && Ready)
                GUI.Box(new Rect(12,168,700,240),$"Outcome: {Outcome}  State: {enemy.State}  Patrol: {enemy.PatrolIndex+1}/4\nVisible: {enemy.perception.Visible}  Blocked: {enemy.perception.Obstructed}  Door: {door.State}\nEnergy: {energy:F3} Rate: {battery.LastNetRate:F2} MoveDistance: {player.HorizontalDistanceLastTick:F4}\nInteraction: {(interaction.Current?interaction.Current.label:"none")} Supply: {interaction.SupplyElapsed:F3} Powered: {interaction.Powered}\nE neutral: {interaction.WaitingForRelease} Move neutral: {player.WaitingForNeutral}\nFocused: {Focused} Paused: {Paused} Position: {player.controller.transform.position:F3}\nLastSeen: {enemy.LastSeen:F3} Target: {enemy.Destination:F3}\nPath: {(enemy.agent.isOnNavMesh?enemy.agent.pathStatus.ToString():"OFF NAVMESH")}",style);
        }
    }
}
