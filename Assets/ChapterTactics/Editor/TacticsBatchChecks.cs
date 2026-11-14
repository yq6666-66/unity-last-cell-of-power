using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using LastCellOfPower.WarehouseExpanded;

public static class TacticsBatchChecks
{
    public static Task<string> Run()=>Launch(false);
    public static Task<string> Routes()=>Launch(true);
    public static Task<string> Route(TacticsRouteMode mode)=>Launch(true,mode);
    static Task<string> Launch(bool routes,TacticsRouteMode? only=null)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Start tactics batch in edit mode");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Preserve unsaved scene before tactics batch");
        EditorSceneManager.OpenScene(TacticsDelivery.ScenePath);TacticsDelivery.Inspect();
        var completion=new TaskCompletionSource<string>();bool oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;var oldOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        IEnumerator routine=null;Exception failure=null;bool finishing=false;double deadline=EditorApplication.timeSinceStartup+(routes?330:180);
        EditorApplication.CallbackFunction update=null;Action<PlayModeStateChange> changed=null;
        void Finish(Exception error)
        {
            if(finishing)return;finishing=true;failure=error;
            try{(routine as IDisposable)?.Dispose();}catch(Exception e){if(failure==null)failure=e;}
            EditorApplication.update-=update;EditorApplication.ExitPlaymode();
        }
        update=()=>
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Tactics consolidated batch deadline exceeded");
                if(!EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=Checks(routes,only);
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            if(!finishing)failure=new Exception("PlayMode exited before tactics batch finished");
            Directory.CreateDirectory(TacticsDelivery.Evidence);
            string report=failure==null?(routes?(only.HasValue?"PASS synthetic input route: "+only.Value+"; no coordinate setters or warp.":"PASS three synthetic input routes: no environment, noise-assisted terminal, shutter escape. No coordinate setters or player/guard warp."):"PASS two-guard/Q/noise/shutter/priority/restart and legacy-single-guard scripted fixtures."):"FAIL "+failure;
            File.WriteAllText(Path.Combine(TacticsDelivery.Evidence,only.HasValue?"route-"+only.Value.ToString().ToLowerInvariant()+"-run.txt":routes?"routes-run.txt":"batch-checks.txt"),report);
            if(failure==null)completion.TrySetResult(report);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;EditorApplication.EnterPlaymode();return completion.Task;
    }
    static IEnumerator Checks(bool routes,TacticsRouteMode? only)
    {
        var ready=TacticsLogicChecks.Ready();while(ready.MoveNext())yield return null;
        var originalInput=InputSystem.settings;string originalJson=JsonUtility.ToJson(originalInput);
        var fixtureInput=UnityEngine.Object.Instantiate(originalInput);fixtureInput.hideFlags=HideFlags.HideAndDontSave;
        fixtureInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        fixtureInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=fixtureInput;
        try
        {
            if(routes)
            {
                foreach(TacticsRouteMode mode in only.HasValue?new[]{only.Value}:(TacticsRouteMode[])Enum.GetValues(typeof(TacticsRouteMode)))
                {
                    var route=TacticsRouteChecks.Run(mode);while(route.MoveNext())yield return route.Current;
                    EditorSceneManager.LoadSceneInPlayMode(TacticsDelivery.ScenePath,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                    ready=TacticsLogicChecks.Ready();while(ready.MoveNext())yield return null;
                    var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
                    if(!s.Paused || s.Guards.Count!=2 || s.tactics.UsedNoiseCount!=0 || s.tactics.ShutterUsed || Mathf.Abs(s.battery.Current-40)>.001f)
                        throw new Exception("Route reload did not reset tactics devices and resource state");
                }
            }
            else
            {
                var logic=TacticsLogicChecks.Run();while(logic.MoveNext())yield return logic.Current;
            }
        }
        finally
        {
            if(!originalInput){originalInput=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(originalJson,originalInput);}
            InputSystem.settings=originalInput;UnityEngine.Object.DestroyImmediate(fixtureInput);
        }
    }
}
