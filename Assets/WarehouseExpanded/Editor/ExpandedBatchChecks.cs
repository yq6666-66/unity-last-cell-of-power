using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;
using LastCellOfPower.WarehouseExpanded;

public static class ExpandedBatchChecks
{
    [CliCommand("warehouse", "verify", "Run consolidated background PlayMode checks")]
    public static Task<string> Run() => RunInternal(false);
    [CliCommand("warehouse", "route", "Run a no-teleport background movement and escape smoke")]
    public static Task<string> Route() => RunInternal(true);
    [CliCommand("warehouse", "lifecycle", "Verify all expanded ending and restart states")]
    public static Task<string> Lifecycle() => RunInternal(false, true);
    static Task<string> RunInternal(bool route, bool lifecycle = false)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Start verification in edit mode");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Unsaved scene changes must be preserved before verification");
        if(EditorSceneManager.GetActiveScene().path!=ExpandedDelivery.ScenePath)EditorSceneManager.OpenScene(ExpandedDelivery.ScenePath);
        ExpandedDelivery.Inspect();
        var completion=new TaskCompletionSource<string>();
        bool previousEnabled=EditorSettings.enterPlayModeOptionsEnabled;
        var previousOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        IEnumerator routine=null;
        Exception failure=null;
        bool finishing=false;
        double deadline=EditorApplication.timeSinceStartup+120;
        EditorApplication.CallbackFunction update=null;
        Action<PlayModeStateChange> changed=null;
        void Finish(Exception exception)
        {
            if(finishing)return;finishing=true;failure=exception;
            if(routine is IDisposable disposable)disposable.Dispose();
            EditorApplication.update-=update;
            EditorApplication.ExitPlaymode();
        }
        update=()=>
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Consolidated PlayMode checks exceeded 120 seconds");
                if(!EditorApplication.isPlaying)return;
                EditorApplication.QueuePlayerLoopUpdate();
                if(routine==null)routine=Checks(route,lifecycle);
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=previousEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
            Directory.CreateDirectory(ExpandedDelivery.Evidence);
            if(!finishing)failure=new Exception("PlayMode stopped before verification finished");
            string report=failure==null?(route ? "PASS background movement/charge/supply/escape route. Synthetic input, not desktop keyboard acceptance." : "PASS consolidated AI, input, resource and scene lifecycle checks. Scripted fixtures, not manual input acceptance."):"FAIL "+failure;
            File.WriteAllText(Path.Combine(ExpandedDelivery.Evidence,lifecycle ? "lifecycle-run.txt" : route ? "route-run.txt" : "batch-checks.txt"),report);
            if(failure==null)completion.TrySetResult(report);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;
        EditorApplication.EnterPlaymode();return completion.Task;
    }

    static IEnumerator Checks(bool route,bool lifecycle)
    {
        ExpandedSession session=null;
        while(!session || !session.Ready)
        {
            session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
            if(session && session.Failure.Length>0)throw new Exception(session.Failure);
            yield return null;
        }
        if(!session.Paused)throw new Exception("New game must start paused");
        if(Mathf.Abs(session.battery.Current-40)>.001f)throw new Exception("Initial energy differs from profile");
        if(Mathf.Abs(session.player.speed-3.4f)>.001f)throw new Exception("Player speed differs from profile");
        var originalInput = UnityEngine.InputSystem.InputSystem.settings;
        var originalInputJson = JsonUtility.ToJson(originalInput);
        var fixtureInput = UnityEngine.Object.Instantiate(originalInput);
        fixtureInput.hideFlags = HideFlags.HideAndDontSave;
        fixtureInput.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        fixtureInput.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEngine.InputSystem.InputSystem.settings = fixtureInput;
        try
        {
        if (lifecycle)
        {
            var lifecycleCheck = ExpandedLifecycleChecks.Run();
            while(lifecycleCheck.MoveNext()) yield return null;
        }
        else if (route)
        {
            var routeCheck = ExpandedRouteChecks.Run();
            while(routeCheck.MoveNext()) yield return null;
        }
        else
        {
        StealthNeutralInputChecks.Run();
        EscapeInteractionChecks.Run();
        ExpandedInteractionChecks.Run();
        var ai=ExpandedAiChecks.Run();
        while(ai.MoveNext())yield return null;
        }
        EditorSceneManager.LoadSceneInPlayMode(ExpandedDelivery.ScenePath,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        int start=Time.frameCount;
        while(Time.frameCount<start+3)yield return null;
        session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        if(!session || !session.Ready || !session.Paused || session.Failure.Length>0 || Mathf.Abs(session.battery.Current-40)>.001f)
            throw new Exception("Scene reload failed to restore clean initial state");
        Debug.Log("EXPANDED_CONSOLIDATED_CHECKS_PASS");
        }
        finally
        {
            if (!originalInput)
            {
                originalInput = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputSettings>();
                JsonUtility.FromJsonOverwrite(originalInputJson, originalInput);
            }
            UnityEngine.InputSystem.InputSystem.settings = originalInput;
            UnityEngine.Object.DestroyImmediate(fixtureInput);
        }
    }
}

