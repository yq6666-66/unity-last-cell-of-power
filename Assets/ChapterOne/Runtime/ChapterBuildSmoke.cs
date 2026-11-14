#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterOne
{
    // Package startup observation only: no inputs, teleports or gameplay state edits.
    public sealed class ChapterBuildSmoke : MonoBehaviour
    {
        bool error;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-chapter-startup-smoke")>=0)
                new GameObject("ChapterStartupObserver").AddComponent<ChapterBuildSmoke>();
        }
        void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=true;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            float deadline=Time.realtimeSinceStartup+20;
            ExpandedSession session=null;ChapterPlayerUI ui=null;
            while(Time.realtimeSinceStartup<deadline)
            {
                session=FindFirstObjectByType<ExpandedSession>();ui=FindFirstObjectByType<ChapterPlayerUI>();
                if(session && session.Ready && ui && ui.LayoutBuilt && ui.Mode!=ChapterViewMode.Loading)break;
                if(session && session.Failure.Length>0)break;
                yield return null;
            }
            yield return null;yield return null;
            bool pass=!error && session && session.Ready && session.Paused && session.externalPlayerUI &&
                session.Failure.Length==0 && session.pulse && session.statistics &&
                session.pulse.SuccessfulUses==0 && session.pulse.CooldownRemaining==0 &&
                Mathf.Abs(session.battery.Current-40)<.001f && session.enemy.agent.isOnNavMesh &&
                ui && ui.LayoutBuilt && ui.font && ui.ModalVisible;
            string result=(pass?"PASS":"FAIL")+" Chapter Windows package startup only, not audible-quality or desktop-input acceptance.\n"+
                "Frame="+Time.frameCount+"\nUI="+(ui?ui.Mode.ToString():"missing")+"\nFailure="+(session?session.Failure:"missing session")+"\n";
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../startup-smoke.txt")),result);
            Debug.Log("CHAPTER_PACKAGE_STARTUP_"+(pass?"PASS":"FAIL"));
            Application.logMessageReceived-=OnLog;Application.Quit(pass?0:2);
        }
    }
}
#endif
