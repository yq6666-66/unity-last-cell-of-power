using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
[InitializeOnLoad]
public static class Stage4Playtest {
 static Stage4Runtime runtime;
 static Keyboard keyboard;
 static InputSettings originalSettings,testSettings;
 static double start,phaseStart;
 static int phase,patrolMask;
 static Vector3 initial,afterMove,focusStart;
 static bool lastDebug;
 static Stage4Playtest(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool("Unit1Playtest",false))EditorApplication.update+=Tick;}
 public static void Run(){if(!Application.isBatchMode)throw new Exception("Automated playtest is batch-mode only and exits its editor process");SessionState.SetBool("Unit1Playtest",true);EditorSceneManager.OpenScene("Assets/Stage4/Scenes/WarehouseUnit1.unity");EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange state){if(!SessionState.GetBool("Unit1Playtest",false))return;if(state==PlayModeStateChange.EnteredPlayMode){phase=0;start=EditorApplication.timeSinceStartup;phaseStart=start;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Tick(){
  if(!EditorApplication.isPlaying || !SessionState.GetBool("Unit1Playtest",false))return;
  try {
   double now=EditorApplication.timeSinceStartup;
   if(now-start>65)throw new Exception("Playtest timed out before completing patrol loop");
   if(phase==0){if(now-phaseStart<1.5)return;runtime=UnityEngine.Object.FindFirstObjectByType<Stage4Runtime>();Require(runtime!=null,"Runtime missing");Require(runtime.Failure.Length==0,"Initialization: "+runtime.Failure);originalSettings=InputSystem.settings;testSettings=UnityEngine.Object.Instantiate(originalSettings);testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=testSettings;keyboard=InputSystem.AddDevice<Keyboard>();runtime.SendMessage("OnApplicationFocus",true);initial=runtime.player.transform.position;Keys(Key.D);phase=1;phaseStart=now;}
   if(runtime==null)return;
   Require(runtime.Failure.Length==0,runtime.Failure);patrolMask|=1<<runtime.PatrolIndex;
   if(phase==1 && now-phaseStart>=1){Keys();afterMove=runtime.player.transform.position;Debug.Log("INPUT_DIAG time="+Time.time+" delta="+Time.deltaTime+" key="+keyboard.dKey.isPressed+" deviceEnabled="+keyboard.enabled+" pos="+afterMove+" focus="+Application.isFocused+" background="+Application.runInBackground);Require(afterMove.x-initial.x>1.5f,"D input failed to move player");Require(afterMove.x-initial.x<3.8f,"Move speed exceeded expected speed");Debug.Log("UNIT1_TEST_MOVE_PASS "+initial+" -> "+afterMove);Keys(Key.W,Key.D);phase=2;phaseStart=now;}
   else if(phase==2 && now-phaseStart>=.5){Keys();var distance=Vector3.Distance(runtime.player.transform.position,afterMove);Require(distance<2f,"Diagonal input was not clamped");Debug.Log("UNIT1_TEST_DIAGONAL_PASS distance="+distance);lastDebug=runtime.DebugVisible;Keys(Key.F1);phase=3;phaseStart=now;}
   else if(phase==3 && now-phaseStart>=.2){Keys();Require(runtime.DebugVisible!=lastDebug,"F1 did not toggle debug");Require(runtime.pathLine.enabled,"Runtime path line not enabled");Stage4Contract.Capture("RuntimeDebug.png");Debug.Log("UNIT1_TEST_F1_PASS");Keys(Key.D);runtime.SendMessage("OnApplicationFocus",false);focusStart=runtime.player.transform.position;phase=4;phaseStart=now;}
   else if(phase==4 && now-phaseStart>=.5){Require(Vector3.Distance(runtime.player.transform.position,focusStart)<.08f,"Movement continued without focus");Keys();runtime.SendMessage("OnApplicationFocus",true);Debug.Log("UNIT1_TEST_SIMULATED_FOCUS_PASS");phase=5;phaseStart=now;}
   else if(phase==5 && now-phaseStart>=.5){Require(Vector3.Distance(runtime.player.transform.position,focusStart)<.08f,"Stale input after focus restored");if(patrolMask==15 && now-start>20){Debug.Log("UNIT1_TEST_PATROL_ALL_FOUR_PASS");Finish(0);}}
  }catch(Exception e){Debug.LogError("UNIT1_PLAYTEST_FAIL: "+e);Finish(1);}
 }
 static void Finish(int code){if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(originalSettings!=null)InputSystem.settings=originalSettings;if(testSettings!=null)UnityEngine.Object.Destroy(testSettings);SessionState.SetBool("Unit1Playtest",false);EditorApplication.update-=Tick;Debug.Log("UNIT1_PLAYTEST_EXIT "+code);EditorApplication.Exit(code);}
}
