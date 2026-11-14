#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Observes normal OS-driven inputs; it never changes actions, devices, position or debug state.
public sealed class Stage4DevelopmentProbe : MonoBehaviour {
 [Serializable] public sealed class Snapshot {
  public string stage; public float elapsed; public Vector3 player,enemy;
  public bool debug,focused,followersIntact;public int playerParts,enemyParts;public string failure;
 }
 Stage4Runtime runtime;string directory;float started;
 readonly Dictionary<Transform,Vector3> playerParts=new Dictionary<Transform,Vector3>();
 readonly Dictionary<Transform,Vector3> enemyParts=new Dictionary<Transform,Vector3>();
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-unit1-regression")>=0)new GameObject("DevelopmentRegressionObserver").AddComponent<Stage4DevelopmentProbe>();}
 IEnumerator Start(){
  yield return new WaitForSecondsRealtime(1.5f);
  runtime=FindFirstObjectByType<Stage4Runtime>();
  if(runtime==null || runtime.Failure.Length>0){Debug.LogError("UNIT1_PROBE_INIT_FAIL");Application.Quit(2);yield break;}
  directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Unit1Regression"));Directory.CreateDirectory(directory);
  Remember(runtime.player.transform,playerParts);Remember(runtime.enemy.transform,enemyParts);
  started=Time.realtimeSinceStartup;Capture("00-baseline");Debug.Log("UNIT1_PROBE_READY");
  string[] stages={"01-D","02-W","03-A","04-S","05-F1-on","06-F1-off","07-diagonal","08-wall","09-F1-on-final"};
  float[] times={3,6,9,12,15,18,21,27,31};
  for(int i=0;i<stages.Length;i++){
   float remaining=times[i]-(Time.realtimeSinceStartup-started);if(remaining>0)yield return new WaitForSecondsRealtime(remaining);
   Capture(stages[i]);
  }
  yield return new WaitForSecondsRealtime(1);
  bool valid=runtime.Failure.Length==0 && Check(runtime.player.transform,playerParts) && Check(runtime.enemy.transform,enemyParts);
  Debug.Log("UNIT1_PROBE_FINISHED "+(valid?"OBSERVATION_OK":"FAIL")+"; direction/F1 verdicts require snapshot comparison");Application.Quit(valid?0:2);
 }
 static void Remember(Transform root,Dictionary<Transform,Vector3> parts){foreach(var r in root.GetComponentsInChildren<Renderer>())parts.Add(r.transform,root.InverseTransformPoint(r.transform.position));}
 static bool Check(Transform root,Dictionary<Transform,Vector3> parts){if(parts.Count==0)return false;foreach(var p in parts)if(p.Key==null || !p.Key.IsChildOf(root) || Vector3.Distance(root.InverseTransformPoint(p.Key.position),p.Value)>.002f)return false;return true;}
 void Capture(string stage){
  var s=new Snapshot{stage=stage,elapsed=Time.realtimeSinceStartup-started,player=runtime.player.transform.position,enemy=runtime.enemy.transform.position,debug=runtime.DebugVisible,focused=Application.isFocused,followersIntact=Check(runtime.player.transform,playerParts)&&Check(runtime.enemy.transform,enemyParts),playerParts=playerParts.Count,enemyParts=enemyParts.Count,failure=runtime.Failure};
  File.WriteAllText(Path.Combine(directory,stage+".json"),JsonUtility.ToJson(s,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,stage+".png"));Debug.Log("UNIT1_PROBE_SNAPSHOT "+JsonUtility.ToJson(s));
 }
}
#endif
