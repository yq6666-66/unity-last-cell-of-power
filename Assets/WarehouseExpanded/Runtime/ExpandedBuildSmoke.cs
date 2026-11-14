#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LastCellOfPower.WarehouseExpanded
{
    // Opt-in package startup observation. Never injects input or changes gameplay state.
    public sealed class ExpandedBuildSmoke : MonoBehaviour
    {
        bool runtimeError;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-warehouse-startup-smoke")>=0)
                new GameObject("ExpandedStartupObserver").AddComponent<ExpandedBuildSmoke>();
        }
        void OnLog(string message,string stack,LogType type)
        { if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)runtimeError=true; }
        IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            float deadline=Time.realtimeSinceStartup+15;
            ExpandedSession s=null;
            while(Time.realtimeSinceStartup<deadline)
            {
                s=FindFirstObjectByType<ExpandedSession>();
                if(s && (s.Ready || s.Failure.Length>0))break;
                yield return null;
            }
            yield return null;yield return null;
            bool passed=s && s.Ready && s.Paused && s.Failure.Length==0 && !runtimeError &&
                Mathf.Abs(s.battery.Current-40)<.001f && Mathf.Abs(s.player.speed-3.4f)<.001f &&
                s.enemy.agent.isOnNavMesh && s.interaction.targets.Length==4;
            string result=(passed?"PASS":"FAIL")+" Windows package startup only; no input or complete gameplay claim.\n"+
                "Frame="+Time.frameCount+"\nFailure="+(s?s.Failure:"missing session")+"\n";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../startup-smoke.txt")),result);
            Debug.Log("EXPANDED_PACKAGE_STARTUP_"+(passed?"PASS":"FAIL"));
            Application.logMessageReceived-=OnLog;
            Application.Quit(passed?0:2);
        }
    }
}
#endif
