using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Pipeline.Commands;
using LastCellOfPower.WarehouseExpanded;

public static class ChapterBatchChecks
{
    [CliCommand("chapter", "verify", "Run consolidated background PlayMode checks")]
    public static Task<string> Run() => RunInternal(false);
    [CliCommand("chapter", "presentation", "Verify chapter UI, audio lifecycle and result presentation")]
    public static Task<string> Presentation() => RunInternal(true);
    [CliCommand("chapter", "preview", "Capture off-screen intro, HUD, settings and results")]
    public static Task<string> Preview() => RunInternal(false, true);
    [CliCommand("chapter", "route_without", "Walk the chapter escape route without pulse")]
    public static Task<string> RouteWithout() => RunInternal(false, false, 1);
    [CliCommand("chapter", "route_with", "Walk the chapter escape route after an emergency pulse")]
    public static Task<string> RouteWith() => RunInternal(false, false, 2);
    static Task<string> RunInternal(bool route, bool lifecycle = false, int walk = 0)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Start verification in edit mode");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Unsaved scene changes must be preserved before verification");
        if(EditorSceneManager.GetActiveScene().path!=ChapterOneBuilder.ScenePath)EditorSceneManager.OpenScene(ChapterOneBuilder.ScenePath);
        ChapterDelivery.Inspect();
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
                if(routine==null)routine=Checks(route,lifecycle,walk);
                if(!routine.MoveNext())Finish(null);
            }
            catch(Exception e){Finish(e);}
        };
        changed=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.update-=update;EditorApplication.playModeStateChanged-=changed;
            EditorSettings.enterPlayModeOptionsEnabled=previousEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
            Directory.CreateDirectory(ChapterDelivery.Evidence);
            if(!finishing)failure=new Exception("PlayMode stopped before verification finished");
            string report=failure==null?(walk > 0 ? "PASS chapter walking integration route; synthetic inputs, no teleport." : lifecycle ? "PASS off-screen chapter layout captures; not desktop acceptance." : route ? "PASS chapter presentation fixtures; not subjective audio/readability acceptance." : "PASS chapter pulse and AI recovery fixtures; not desktop input acceptance."):"FAIL "+failure;
            File.WriteAllText(Path.Combine(ChapterDelivery.Evidence,walk > 0 ? (walk == 1 ? "route-no-pulse-run.txt" : "route-with-pulse-run.txt") : lifecycle ? "preview-run.txt" : route ? "presentation-run.txt" : "batch-checks.txt"),report);
            if(failure==null)completion.TrySetResult(report);else completion.TrySetException(failure);
        };
        EditorApplication.update+=update;EditorApplication.playModeStateChanged+=changed;
        EditorApplication.EnterPlaymode();return completion.Task;
    }

    static IEnumerator Checks(bool route,bool lifecycle,int walk)
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
        if (walk > 0)
        {
            var walkCheck = walk == 1 ? ChapterRouteChecks.RunWithoutPulse() : ChapterRouteChecks.RunWithPulse();
            while(walkCheck.MoveNext()) yield return null;
        }
        else if (lifecycle)
        {
            var lifecycleCheck = ChapterPreview.Run();
            while(lifecycleCheck.MoveNext()) yield return null;
        }
        else if (route)
        {
            var routeCheck = ChapterPresentationChecks.Run();
            while(routeCheck.MoveNext()) yield return null;
        }
        else
        {
        var pulseCheck = ChapterPulseChecks.Run();
        while(pulseCheck.MoveNext()) yield return null;
        }
        if (session.Outcome != LastCellOfPower.EscapeSample.RunOutcome.Running)
            UnityEngine.Object.FindFirstObjectByType<LastCellOfPower.ChapterOne.ChapterPlayerUI>().RestartRun();
        else
            EditorSceneManager.LoadSceneInPlayMode(ChapterOneBuilder.ScenePath,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        int start=Time.frameCount;
        while(Time.frameCount<start+3)yield return null;
        session=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        if(!session || !session.Ready || !session.Paused || session.Failure.Length>0 || Mathf.Abs(session.battery.Current-40)>.001f)
            throw new Exception("Scene reload failed to restore clean initial state");
        if(session.pulse.SuccessfulUses!=0 || session.pulse.CooldownRemaining!=0 || session.statistics.SuccessfulPulseCount!=0 || session.statistics.ActiveSeconds!=0 || session.Summary!=null)
            throw new Exception("Pulse/statistics survived restart");
        if(session.enemy.State!=ExpandedEnemyState.Patrol || session.enemy.DisruptionRemaining!=0 || session.enemy.CaptureElapsed!=0)
            throw new Exception("Guard disruption/capture survived restart");
        if(UnityEngine.Object.FindObjectsByType<LastCellOfPower.ChapterOne.ChapterPlayerUI>(FindObjectsSortMode.None).Length!=1 ||
            UnityEngine.Object.FindFirstObjectByType<LastCellOfPower.ChapterOne.ChapterAudioFeedback>().PlayingGameplaySources!=0)
            throw new Exception("UI or gameplay audio did not reset");
        Debug.Log("CHAPTER_BATCH_CHECKS_PASS");
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

