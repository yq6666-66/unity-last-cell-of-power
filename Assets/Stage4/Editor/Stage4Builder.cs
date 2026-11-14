using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;
using UnityEngine.Rendering;

public static class Stage4Builder {
 const string Root="Assets/Stage4";
 public static void Create() {
  if(File.Exists(Root+"/Scenes/WarehouseUnit1.unity")) throw new Exception("Scene exists; use Build or an explicitly scoped repair instead of recreating it");
  foreach(var dir in new[]{"Scenes","Materials","Navigation"}) Directory.CreateDirectory(Root+"/"+dir);
  AssetDatabase.Refresh();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/SCN_Warehouse_12m.fbx");
  if(model==null) throw new Exception("Refined FBX failed to import");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var room=(GameObject)PrefabUtility.InstantiatePrefab(model);
  room.name="RefinedWarehouse";
  PrefabUtility.UnpackPrefabInstance(room,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  var all=room.GetComponentsInChildren<Transform>(true);
  Transform Find(string name) {var t=all.FirstOrDefault(x=>x.name==name);if(t==null) throw new Exception("Model node missing: "+name);return t;}
  var playerVisual=Find("CHR_ServiceBot__player"); var enemyVisual=Find("CHR_PatrolBot__enemy");
  Debug.Log("UNIT1_IMPORTED_PLAYER: "+playerVisual.position+" ENEMY: "+enemyVisual.position+" ROOT_SCALE: "+room.transform.lossyScale);
  if(Vector3.Distance(playerVisual.position,new Vector3(0,0,-4))>.05f || Vector3.Distance(enemyVisual.position,new Vector3(0,0,4))>.05f)
   throw new Exception("FBX axis/scale mismatch; do not rotate the room by guesswork");
  var renderers=room.GetComponentsInChildren<Renderer>();
  if(renderers.Length==0) throw new Exception("Imported room has no renderers");
  if(room.GetComponentsInChildren<Collider>().Length!=0) throw new Exception("Unexpected imported visual colliders");
  var bounds=new Bounds(renderers[0].bounds.center,Vector3.zero);
  foreach(var r in room.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
  if(bounds.size.x<12.5f || bounds.size.x>12.8f || bounds.size.y<3.3f || bounds.size.y>3.6f || bounds.size.z<12.5f || bounds.size.z>12.9f) throw new Exception("FBX room scale mismatch: "+bounds);
  Debug.Log("UNIT1_ROOM_BOUNDS: "+bounds);
  ApplyMaterials(room);
  var player=new GameObject("Player"); player.transform.position=new Vector3(0,0,-4);
  playerVisual.SetParent(player.transform,true);
  var controller=player.AddComponent<CharacterController>(); controller.height=1.1f;controller.radius=.42f;controller.center=new Vector3(0,.55f,0);controller.stepOffset=.15f;controller.skinWidth=.03f;
  var enemy=new GameObject("Enemy"); enemy.transform.SetPositionAndRotation(new Vector3(0,0,4),Quaternion.Euler(0,180,0));
  enemyVisual.SetParent(enemy.transform,true);
  var agent=enemy.AddComponent<NavMeshAgent>(); agent.radius=.4f;agent.height=1.1f;agent.speed=1.6f;agent.angularSpeed=180;agent.acceleration=8;agent.stoppingDistance=.12f;
  var collision=new GameObject("CollisionWorld");
  Box(collision,"Floor",new Vector3(0,-.06f,0),new Vector3(12,.12f,12));
  Box(collision,"NorthWallWithClosedDoor",new Vector3(0,1.2f,6.16f),new Vector3(12.64f,2.4f,.32f));
  Box(collision,"SouthWall",new Vector3(0,1.2f,-6.16f),new Vector3(12.64f,2.4f,.32f));
  Box(collision,"EastWall",new Vector3(6.16f,1.2f,0),new Vector3(.32f,2.4f,12));
  Box(collision,"WestWall",new Vector3(-6.16f,1.2f,0),new Vector3(.32f,2.4f,12));
  Box(collision,"CentralOccluder",new Vector3(.0061f,1.656f,-.0058f),new Vector3(1.973449f,3.312f,4.602325f));
  foreach(var t in all.Where(t=>t.name.StartsWith("ENV_Pillar__") || t.name.StartsWith("PROP_SafetyBollard__") || t.name.StartsWith("PROP_Charging") || t.name.StartsWith("PROP_Battery") || t.name.StartsWith("PROP_Terminal"))) {
   var rs=t.GetComponentsInChildren<Renderer>(); if(rs.Length==0) continue;
   var b=rs[0].bounds;foreach(var r in rs) b.Encapsulate(r.bounds); Box(collision,"Collision_"+t.name,b.center,b.size);
  }
  var surface=collision.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
  surface.BuildNavMesh(); if(surface.navMeshData==null) throw new Exception("NavMesh baking failed");
  AssetDatabase.CreateAsset(surface.navMeshData,Root+"/Navigation/WarehouseUnit1.asset");EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();
  var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
  camera.transform.position=new Vector3(0,16,-11);camera.transform.LookAt(new Vector3(0,0,0));camera.orthographic=true;camera.orthographicSize=8.4f;camera.nearClipPlane=.1f;camera.farClipPlane=70;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.07f);
  camera.gameObject.AddComponent<AudioListener>();
  var light=new GameObject("Warehouse Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(55,-25,0);light.shadows=LightShadows.Soft;
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.5f,.55f);
  var line=new GameObject("PatrolPathDebug").AddComponent<LineRenderer>();line.useWorldSpace=true;line.widthMultiplier=.05f;line.positionCount=0;line.enabled=false;
  var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null) throw new Exception("URP Unlit missing");
  var debugMat=new Material(shader);debugMat.SetColor("_BaseColor",new Color(.2f,1,.6f));AssetDatabase.CreateAsset(debugMat,Root+"/Materials/DebugPath.mat");line.sharedMaterial=debugMat;
  var runtime=new GameObject("Unit1Session").AddComponent<Stage4Runtime>();runtime.player=controller;runtime.enemy=agent;runtime.pathLine=line;runtime.inputAsset=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
  if(runtime.inputAsset==null) throw new Exception("Existing input asset missing");
  foreach(var point in runtime.patrolPoints) if(!NavMesh.SamplePosition(point,out var hit,.3f,agent.areaMask))throw new Exception("Patrol NavMesh sample failed: "+point);
  EditorSceneManager.SaveScene(scene,Root+"/Scenes/WarehouseUnit1.unity");
  var existing=EditorBuildSettings.scenes.Where(s=>s.path!=Root+"/Scenes/WarehouseUnit1.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/WarehouseUnit1.unity",true)}.Concat(existing).ToArray();
  AssetDatabase.SaveAssets();Debug.Log("UNIT1_SCENE_CREATED");
  Stage4Contract.Run();
 }
 static void Box(GameObject parent,string name,Vector3 center,Vector3 size) {var go=new GameObject(name);go.transform.SetParent(parent.transform);go.transform.position=center;go.AddComponent<BoxCollider>().size=size;}
 static void ApplyMaterials(GameObject room) {
  var colors=new Dictionary<string,string>{{"MAT_Graphite","#202D36"},{"MAT_Steel","#536774"},{"MAT_Mist","#B7C7CB"},{"MAT_ServiceYellow","#E7B951"},{"MAT_PowerMint","#69E0B0"},{"MAT_AlarmCoral","#F16C61"},{"MAT_Concrete","#78878A"},{"MAT_Rubber","#111A22"}};
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new Exception("URP Lit shader unavailable");
  var cache=new Dictionary<string,Material>();
  foreach(var r in room.GetComponentsInChildren<Renderer>()) {
   var mats=r.sharedMaterials;
   for(int i=0;i<mats.Length;i++) {
    var name=mats[i]!=null?mats[i].name:"";var key=colors.Keys.FirstOrDefault(k=>name.StartsWith(k));if(key==null)throw new Exception("Unknown FBX material: "+name);
    if(!cache.TryGetValue(key,out var m)) {
     m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+key+".mat");
     bool fresh=m==null; if(fresh) m=new Material(shader){name=key};ColorUtility.TryParseHtmlString(colors[key],out var c);m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",key=="MAT_Rubber" ? .15f : (key=="MAT_PowerMint" || key=="MAT_AlarmCoral" ? .55f : .35f));m.SetFloat("_Metallic",key=="MAT_Graphite" || key=="MAT_Steel" || key=="MAT_Mist" ? .15f : 0);
     float emission=key=="MAT_PowerMint"?1.3417f:(key=="MAT_AlarmCoral"?1.5833f:0);m.SetColor("_EmissionColor",c.linear*emission);if(emission>0)m.EnableKeyword("_EMISSION");else m.DisableKeyword("_EMISSION");
     if(fresh) AssetDatabase.CreateAsset(m,Root+"/Materials/"+key+".mat");else EditorUtility.SetDirty(m);cache.Add(key,m);
    }
    mats[i]=m;
   }
   r.sharedMaterials=mats;
  }
 }
 public static void RepairImportedScene() {
  var scene=EditorSceneManager.OpenScene(Root+"/Scenes/WarehouseUnit1.unity");
  var room=GameObject.Find("RefinedWarehouse");
  if(PrefabUtility.IsPartOfPrefabInstance(room)) PrefabUtility.UnpackPrefabInstance(room,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  if(room==null)throw new Exception("Repair requires RefinedWarehouse scene root");
  var transforms=room.GetComponentsInChildren<Transform>(true);
  var p=transforms.FirstOrDefault(t=>t.name=="CHR_ServiceBot__player");if(p!=null)p.SetParent(GameObject.Find("Player").transform,true);
  var e=transforms.FirstOrDefault(t=>t.name=="CHR_PatrolBot__enemy");if(e!=null)e.SetParent(GameObject.Find("Enemy").transform,true);
  ApplyMaterials(room); ApplyMaterials(GameObject.Find("Player")); ApplyMaterials(GameObject.Find("Enemy"));
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Stage4Contract.Run();
 }
 public static void Build() {
  Stage4Contract.Run();
  var output=Path.GetFullPath("../Builds/WarehouseUnit1/LastCellOfPower.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/WarehouseUnit1.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  if(result.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed: "+result.summary.result);
  Debug.Log("UNIT1_WINDOWS_BUILD_PASS: "+output);
 }
}
