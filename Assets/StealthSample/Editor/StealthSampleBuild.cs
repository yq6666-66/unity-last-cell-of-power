using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class StealthSampleBuild
{
    public static string LastOutput { get; private set; }
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Build requires edit mode");
        StealthSampleChecks.RunContract();
        var stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var directory=Path.GetFullPath("../Builds/StealthSample-"+stamp);
        if(Directory.Exists(directory)) throw new Exception("Output already exists");
        Directory.CreateDirectory(directory);
        LastOutput=Path.Combine(directory,"LastCellOfPower.exe");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{StealthSampleBuilder.ScenePath},
            locationPathName=LastOutput,
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(directory,"build-result.txt"),
            "Unity="+Application.unityVersion+"\nScene="+StealthSampleBuilder.ScenePath+"\nResult="+report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nBytes="+report.summary.totalSize+"\n");
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Stealth build failed: "+report.summary.result);
        Debug.Log("STEALTH_WINDOWS_BUILD_PASS "+LastOutput);
    }
}
