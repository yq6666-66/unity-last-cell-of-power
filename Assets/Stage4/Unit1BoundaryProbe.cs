#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Opt-in observation only. Does not write inputs, focus, movement or runtime state.
public sealed class Unit1BoundaryProbe : MonoBehaviour {
 [Serializable] public sealed class Sample {
  public string stage; public float elapsed; public Vector3 player;
  public Vector2 action,gamepad; public bool focused,w,a,s,d,f1;
  public string failure,control; public long utcTicks; public Vector2 projectAction; public bool projectPresent,gamepadPresent; public long monotonicTicks,inputUpdateTicks; public bool projectEnabled; public bool actionEnabled; public int frame; public float deltaTime,scaledTime;
 }
 Stage4Runtime runtime; InputAction move,projectMove; string directory; float started;
 StreamWriter trace; readonly StringBuilder buffer=new StringBuilder(); bool active; int frames; bool probeError; long inputUpdateTicks;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-unit1-boundary")>=0)new GameObject("Unit1BoundaryObserver").AddComponent<Unit1BoundaryProbe>();}
 IEnumerator Start(){
  yield return new WaitForSecondsRealtime(1.5f);
  runtime=FindFirstObjectByType<Stage4Runtime>();
  if(runtime==null || runtime.Failure.Length>0){Debug.LogError("BOUNDARY_INIT_FAIL");Application.Quit(2);yield break;}
  if(FindObjectsByType<Stage4Runtime>(FindObjectsSortMode.None).Length!=1){Debug.LogError("BOUNDARY_RUNTIME_COUNT_INVALID");Application.Quit(2);yield break;}
  move=ResolveRuntimeMove(runtime);
  if(move==null){Debug.LogError("BOUNDARY_RUNTIME_MOVE_MISSING");Application.Quit(2);yield break;}
  projectMove=InputSystem.actions==null?null:InputSystem.actions.FindAction("Player/Move",false);
  directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Unit1Boundary"));Directory.CreateDirectory(directory);
  trace=new StreamWriter(Path.Combine(directory,"frames.jsonl"),false,Encoding.UTF8);trace.AutoFlush=false;
  var bindings=new StringBuilder("Observed exact Stage4Runtime.move; reflection is diagnostic-only.\n");
  bindings.AppendLine(Identity(move,"runtime"));
  if(projectMove!=null)bindings.AppendLine(Identity(projectMove,"project"));
  int count=0;foreach(var action in InputSystem.ListEnabledActions())if(action.name=="Move" && action.actionMap!=null && action.actionMap.name=="Player"){bindings.AppendLine(Identity(action,"enabled"));count++;}
  bindings.AppendLine("enabledMoveCount="+count);
  foreach(var c in move.controls)bindings.AppendLine("runtimeControl="+c.path);
  File.WriteAllText(Path.Combine(directory,"bindings.txt"),bindings.ToString());
  Debug.Log("BOUNDARY_INPUT_IDENTITIES "+bindings.ToString());
  bindings.AppendLine("inputUpdateMode="+InputSystem.settings.updateMode+" monotonicFrequency="+System.Diagnostics.Stopwatch.Frequency);
  File.WriteAllText(Path.Combine(directory,"bindings.txt"),bindings.ToString());
  InputSystem.onAfterUpdate+=AfterInputUpdate;
  started=Time.realtimeSinceStartup;active=true;Capture("00-ready");
  float[] times={2,4,6,8,10,12,15,18};
  foreach(float t in times){float remaining=t-(Time.realtimeSinceStartup-started);if(remaining>0)yield return new WaitForSecondsRealtime(remaining);Capture("at-"+t.ToString("00"));}
  yield return new WaitForSecondsRealtime(1);
  active=false;InputSystem.onAfterUpdate-=AfterInputUpdate;Flush();trace.Dispose();trace=null;
  Debug.Log("BOUNDARY_OBSERVATION_FINISHED frames="+frames+" failure="+runtime.Failure+" probeError="+probeError);
  Application.Quit(!probeError && runtime!=null && runtime.Failure.Length==0?0:2);
 }
 // Never choose by name across all actions: project-wide and runtime-cloned assets coexist.
 public static InputAction ResolveRuntimeMove(Stage4Runtime owner){
  if(owner==null)return null;
  var field=typeof(Stage4Runtime).GetField("move",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  var selected=field==null?null:field.GetValue(owner) as InputAction;
  return selected!=null && selected.name=="Move" && selected.actionMap!=null && selected.actionMap.name=="Player"?selected:null;
 }
 static string Identity(InputAction action,string role){
  var asset=action.actionMap==null?null:action.actionMap.asset;
  return role+" action="+action.id+" map="+(action.actionMap==null?"":action.actionMap.name)+" asset="+(asset==null?"none":asset.name)+" instance="+(asset==null?0:asset.GetInstanceID())+" enabled="+action.enabled;
 }
 Sample ReadSample(string stage){
  if(runtime==null || runtime.player==null || move==null){probeError=true;return new Sample{stage=stage,elapsed=Time.realtimeSinceStartup-started,frame=Time.frameCount,failure="PROBE_MISSING_REFERENCES"};}
  var k=Keyboard.current;var g=Gamepad.current;
  return new Sample{stage=stage,utcTicks=DateTime.UtcNow.Ticks,monotonicTicks=System.Diagnostics.Stopwatch.GetTimestamp(),inputUpdateTicks=inputUpdateTicks,projectPresent=projectMove!=null,gamepadPresent=Gamepad.current!=null,projectAction=projectMove==null?Vector2.zero:projectMove.ReadValue<Vector2>(),projectEnabled=projectMove!=null && projectMove.enabled,elapsed=Time.realtimeSinceStartup-started,player=runtime.player.transform.position,action=move.ReadValue<Vector2>(),gamepad=g==null?Vector2.zero:g.leftStick.ReadValue(),focused=Application.isFocused,frame=Time.frameCount,deltaTime=Time.deltaTime,scaledTime=Time.time,actionEnabled=move.enabled,control=move.activeControl==null?"":move.activeControl.path,w=k!=null&&k.wKey.isPressed,a=k!=null&&k.aKey.isPressed,s=k!=null&&k.sKey.isPressed,d=k!=null&&k.dKey.isPressed,f1=k!=null&&k.f1Key.isPressed,failure=runtime.Failure};
 }
 void AfterInputUpdate(){inputUpdateTicks=System.Diagnostics.Stopwatch.GetTimestamp();}
 void Record(string stage){try{buffer.AppendLine(JsonUtility.ToJson(ReadSample(stage)));}catch(Exception e){probeError=true;Debug.LogError("BOUNDARY_READ_ERROR "+e.GetType().Name);}}
 void Flush(){if(trace==null)return;trace.Write(buffer.ToString());buffer.Length=0;trace.Flush();}
 void LateUpdate(){if(active && trace!=null){if(frames>=50000){probeError=true;active=false;Flush();Debug.LogError("BOUNDARY_FRAME_CAP");Application.Quit(2);return;}Record("frame");frames++;}}
 void OnApplicationFocus(bool focused){if(active && trace!=null){Record(focused?"focus-gained":"focus-lost");Flush();}}
 void Capture(string stage){Flush();var json=JsonUtility.ToJson(ReadSample(stage),true);File.WriteAllText(Path.Combine(directory,stage+".json"),json);ScreenCapture.CaptureScreenshot(Path.Combine(directory,stage+".png"));Debug.Log("BOUNDARY_SAMPLE "+stage);}
 void OnDestroy(){InputSystem.onAfterUpdate-=AfterInputUpdate;if(trace!=null){Flush();trace.Dispose();trace=null;}}
}
#endif
