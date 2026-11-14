using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public sealed class Stage4Runtime : MonoBehaviour {
 public CharacterController player;
 public NavMeshAgent enemy;
 public InputActionAsset inputAsset;
 public LineRenderer pathLine;
 public Vector3[] patrolPoints = {new Vector3(-4,0,3),new Vector3(4,0,3),new Vector3(4,0,4.5f),new Vector3(-4,0,4.5f)};
 public float playerSpeed=3f;
 public bool DebugVisible {get; private set;}
 public string Failure {get; private set;} = "";
 public int PatrolIndex {get; private set;}
 InputActionAsset actions;
 InputAction move;
 bool ready,focused;
 float verticalSpeed,pathStarted;
 int arrivals;
 Vector3 patrolDestination;
 void Start() {
  focused=Application.isFocused;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-unit1-smoke")>=0) StartCoroutine(DevelopmentSmoke());
#endif
  try {
   if(player==null || !player.enabled || enemy==null || inputAsset==null || pathLine==null) throw new Exception("Required player, navigation, input or debug reference missing");
   if(patrolPoints==null || patrolPoints.Length!=4) throw new Exception("Expected four patrol points");
   foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) foreach(var mat in renderer.sharedMaterials)
    if(mat==null || mat.shader==null || !mat.shader.isSupported || mat.shader.name=="Hidden/InternalErrorShader") throw new Exception("Missing or unsupported material on "+renderer.name);
   if(!enemy.isOnNavMesh) throw new Exception("Enemy is not on a baked NavMesh");
   foreach(var point in patrolPoints) if(!NavMesh.SamplePosition(point,out var hit,.3f,enemy.areaMask)) throw new Exception("Patrol point is outside NavMesh: "+point);
   actions=Instantiate(inputAsset); move=actions.FindAction("Player/Move",true);
   if(focused) move.Enable();
   ready=true; pathLine.enabled=false; SetPatrolDestination();
   if(ready) Debug.Log("UNIT1_READY: WASD, four-point Patrol, F1; no other gameplay enabled");
  } catch(Exception e) { Fail(e.Message); }
 }
 void Update() {
  if(focused && Keyboard.current!=null && Keyboard.current.f1Key.wasPressedThisFrame) DebugVisible=!DebugVisible;
  if(!ready) return;
  if(!player.enabled) {Fail("Player controller was disabled");return;}
  var input=focused ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(),1f) : Vector2.zero;
  verticalSpeed=player.isGrounded ? -2f : verticalSpeed+Physics.gravity.y*Time.deltaTime;
  var horizontal=new Vector3(input.x,0,input.y);
  player.Move((horizontal*playerSpeed+Vector3.up*verticalSpeed)*Time.deltaTime);
  if(horizontal.sqrMagnitude>.001f) player.transform.rotation=Quaternion.LookRotation(horizontal);
  if(!enemy.isOnNavMesh) {Fail("Enemy left NavMesh"); return;}
  if(enemy.pathPending) {if(Time.time-pathStarted>5f) Fail("Navigation path computation timed out");}
  else if(enemy.pathStatus!=NavMeshPathStatus.PathComplete) {Fail("Navigation path is "+enemy.pathStatus);}
  else {
   var delta=enemy.transform.position-patrolDestination;delta.y=0;
   if(delta.magnitude<=enemy.stoppingDistance+.18f && enemy.remainingDistance<=enemy.stoppingDistance+.18f) {
    arrivals|=1<<PatrolIndex;
    Debug.Log("UNIT1_PATROL_ARRIVED "+PatrolIndex+" position="+enemy.transform.position);
    PatrolIndex=(PatrolIndex+1)%patrolPoints.Length; SetPatrolDestination();
   } else if(!enemy.hasPath && Time.time-pathStarted>.2f) {Fail("No complete path to current patrol point");}
   else if(Time.time-pathStarted>15f) {Fail("Enemy stalled before patrol destination");}
  }
  pathLine.enabled=DebugVisible;
  if(DebugVisible && enemy.hasPath) {
   var corners=enemy.path.corners; pathLine.positionCount=corners.Length;
   for(int i=0;i<corners.Length;i++) pathLine.SetPosition(i,corners[i]+Vector3.up*.08f);
  } else pathLine.positionCount=0;
 }
 void SetPatrolDestination() {
  if(!NavMesh.SamplePosition(patrolPoints[PatrolIndex],out var hit,.3f,enemy.areaMask) || !enemy.SetDestination(hit.position)) {Fail("Cannot set patrol destination "+PatrolIndex); return;}
  patrolDestination=hit.position; pathStarted=Time.time; Debug.Log("UNIT1_PATROL_TARGET "+PatrolIndex+" "+hit.position);
 }
 void OnApplicationFocus(bool hasFocus) {
  focused=hasFocus; verticalSpeed=0;
  if(move!=null) {move.Disable(); if(hasFocus && ready) move.Enable();}
 }
 void OnDisable() {
  ready=false; if(actions!=null) {actions.Disable(); Destroy(actions);}
  if(pathLine!=null) pathLine.enabled=false;
 }
 public void Fail(string message) {
  if(Failure.Length>0) return;
  Failure=message; ready=false;
  if(pathLine!=null)pathLine.enabled=false;
  if(enemy!=null && enemy.isOnNavMesh) enemy.isStopped=true;
  if(move!=null) move.Disable();
  Debug.LogError("UNIT1_ERROR: "+message);
 }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
 System.Collections.IEnumerator DevelopmentSmoke() {
  var dir=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Unit1Evidence"));
  System.IO.Directory.CreateDirectory(dir);
  yield return new WaitForSecondsRealtime(2f);
  ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"Runtime.png"));
  DebugVisible=true; // Diagnostic overlay capture, not proof of a physical F1 press.
  yield return new WaitForSecondsRealtime(28f);
  ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"RuntimeDebug.png"));
  yield return new WaitForSecondsRealtime(1f);
  bool success=Failure.Length==0 && arrivals==15;
  Debug.Log("UNIT1_DEVELOPMENT_SMOKE_"+(success?"PASS":"FAIL")+" arrivalsMask="+arrivals+" failure="+Failure);
  Application.Quit(success?0:1);
 }
#endif
 void OnGUI() {
  var style=new GUIStyle(GUI.skin.box){fontSize=18,alignment=TextAnchor.UpperLeft,wordWrap=true};
  GUI.Box(new Rect(12,12,560,56),"WAREHOUSE / UNIT 1     WASD: move     F1: debug",style);
  if(Failure.Length>0) {GUI.color=new Color(1,.35f,.3f);GUI.Box(new Rect(12,80,700,120),"INITIALIZATION / NAVIGATION ERROR\n"+Failure,style); GUI.color=Color.white;}
  else if(DebugVisible) {
   string path=enemy!=null && enemy.isOnNavMesh ? (enemy.pathPending?"Pending":enemy.pathStatus.ToString()) : "Off NavMesh";
   GUI.Box(new Rect(12,80,560,150),"State: Patrol\nTarget: "+(PatrolIndex+1)+" / 4   Path: "+path+"\nFocus: "+focused+"   Player: "+player.transform.position.ToString("F2")+"\nOnly movement / patrol are active. No battery or exit gameplay.",style);
  }
 }
}
