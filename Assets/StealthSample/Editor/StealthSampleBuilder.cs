using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using LastCellOfPower.StealthSample;

public static class StealthSampleBuilder
{
    public const string ScenePath = "Assets/StealthSample/Scenes/StealthSample.unity";
    public static void Wire()
    {
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Unsaved scene changes");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (UnityEngine.Object.FindFirstObjectByType<StealthSession>()) throw new Exception("Already wired; inspect before replacing");
        var old = UnityEngine.Object.FindFirstObjectByType<Stage4Runtime>();
        if (!old) throw new Exception("Expected copied Unit1 runtime");
        var controller = old.player; var agent = old.enemy; var input = old.inputAsset;
        if (old.pathLine) UnityEngine.Object.DestroyImmediate(old.pathLine.gameObject);
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach(var t in controller.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=8;
        foreach(var t in agent.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=9;
        controller.transform.SetPositionAndRotation(new Vector3(0,0,-4),Quaternion.identity);
        var camera=Camera.main;
        camera.orthographic=false; camera.fieldOfView=70; camera.nearClipPlane=.05f;
        camera.cullingMask &= ~(1<<8);
        camera.transform.SetParent(controller.transform,false);
        camera.transform.localPosition=new Vector3(0,.9f,0); camera.transform.localRotation=Quaternion.identity;
        // Camera was parented after actor layer assignment, so it remains visible to itself.
        var player=controller.gameObject.AddComponent<StealthPlayerController>();
        player.controller=controller; player.viewCamera=camera; player.inputAsset=input;
        var perception=agent.gameObject.AddComponent<StealthPerception>();
        perception.observer=agent.transform; perception.target=controller.transform; perception.obstructionMask=1;
        var enemy=agent.gameObject.AddComponent<StealthEnemy>(); enemy.agent=agent; enemy.perception=perception;
        var session=new GameObject("StealthSession").AddComponent<StealthSession>(); session.player=player; session.enemy=enemy;
        session.pathLine=Line("NavigationPath",Color.green);
        session.sightLine=Line("SightRay",Color.yellow);
        session.lastSeenLine=Line("LastSeen",Color.cyan);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("STEALTH_SCENE_WIRED: not runtime acceptance");
    }
    static LineRenderer Line(string name,Color color)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>(); line.useWorldSpace=true; line.widthMultiplier=.035f; line.positionCount=0; line.enabled=false;
        line.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stage4/Materials/DebugPath.mat");
        line.startColor=color; line.endColor=color; return line;
    }
}
