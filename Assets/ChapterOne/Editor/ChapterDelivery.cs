using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Pipeline.Commands;
using LastCellOfPower.ChapterOne;
using LastCellOfPower.WarehouseExpanded;

public static class ChapterDelivery
{
    public static string Evidence => Path.GetFullPath("Evidence/ChapterOne");
    static void Require(bool value,string message){if(!value)throw new Exception("CHAPTER_CONTRACT: "+message);}
    [CliCommand("chapter","generate","Create the independent first-chapter scene")]
    public static async Task<string> Generate(){await ChapterOneBuilder.EnsurePrerequisitesAsync();ChapterOneBuilder.Create();return Inspect();}
    [CliCommand("chapter","inspect","Inspect chapter runtime and presentation references")]
    public static string Inspect()
    {
        Require(!EditorApplication.isPlaying,"Edit mode required");
        if(EditorSceneManager.GetActiveScene().path!=ChapterOneBuilder.ScenePath)EditorSceneManager.OpenScene(ChapterOneBuilder.ScenePath);
        var s=UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        var ui=UnityEngine.Object.FindFirstObjectByType<ChapterPlayerUI>();
        var audio=UnityEngine.Object.FindFirstObjectByType<ChapterAudioFeedback>();
        var visuals=UnityEngine.Object.FindFirstObjectByType<ChapterVisualFeedback>();
        Require(s && s.pulse && s.statistics && s.externalPlayerUI,"Chapter ability/stats/UI gate references");
        Require(ui && ui.font && ui.session==s && audio && visuals,"Presentation references");
        Require(AssetDatabase.GetAssetPath(ui.font).StartsWith("Assets/ChapterOne/"),"Bundled chapter font");
        Require(Mathf.Abs(s.pulse.profile.cost-12)<.001f && Mathf.Abs(s.pulse.profile.minimumEnergy-13)<.001f,"Pulse energy contract");
        Require(s.interaction.targets.Length==4,"Existing level interactions preserved");
        Require(File.Exists("Assets/ChapterOne/Fonts/LICENSE.txt"),"Font license packaged");
        var clips=typeof(ChapterAudioFeedback).GetFields().Where(f=>f.FieldType==typeof(AudioClip)).ToArray();
        Require(clips.Length>=13,"Expected core audio set");
        foreach(var field in clips){var clip=field.GetValue(audio) as AudioClip;Require(clip && clip.length>0,"Audio cue missing: "+field.Name);}
        Directory.CreateDirectory(Evidence);
        string result="PASS chapter references, bundled font/license, pulse defaults, UI ownership and audio set. Edit-mode contract only.\n";
        File.WriteAllText(Path.Combine(Evidence,"contract.txt"),result);return result;
    }
    [CliCommand("chapter","build","Build the independent Windows first-chapter package")]
    public static string Build()
    {
        Inspect();
        var directory=Path.GetFullPath("../Builds/ChapterOne-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Require(!Directory.Exists(directory),"Output must be new");Directory.CreateDirectory(directory);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ChapterOneBuilder.ScenePath},locationPathName=Path.Combine(directory,"LastCellOfPower.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Path.Combine(directory,"build-result.txt"),$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nUnity={Application.unityVersion}\n");
        Require(report.summary.result==BuildResult.Succeeded,"Windows build failed");return directory;
    }
}
