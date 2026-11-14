using System.Linq;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
public static class Stage4Contract {
 public static void Run() {
  EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
  if(!File.Exists(Path.GetFullPath("../Stage4Evidence/Before.png"))) Capture("Before.png");
  const string scene="Assets/Stage4/Scenes/WarehouseUnit1.unity";
  if(!File.Exists(scene)) throw new Exception("CONTRACT FAIL: WarehouseUnit1 scene is not implemented");
  EditorSceneManager.OpenScene(scene);
  if(GameObject.Find("Player") == null) throw new Exception("CONTRACT FAIL: missing Player");
  if(GameObject.Find("Player").GetComponent<CharacterController>() == null) throw new Exception("CONTRACT FAIL: missing player controller");
  if(GameObject.Find("Player").GetComponentsInChildren<Renderer>().Length==0) throw new Exception("CONTRACT FAIL: player visual is not attached to movement root");
  foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) foreach(var m in r.sharedMaterials) if(!AssetDatabase.GetAssetPath(m).StartsWith("Assets/Stage4/Materials/")) throw new Exception("CONTRACT FAIL: material remap not persisted: "+r.name);
  var enemy=GameObject.Find("Enemy");
  if(enemy==null || enemy.GetComponent<NavMeshAgent>()==null) throw new Exception("CONTRACT FAIL: missing navigation agent");
  foreach(var v in new[]{new Vector3(-4,0,3),new Vector3(4,0,3),new Vector3(4,0,4.5f),new Vector3(-4,0,4.5f),new Vector3(0,0,-4),new Vector3(0,0,4)}) {
   if(!NavMesh.SamplePosition(v,out var hit,.3f,enemy.GetComponent<NavMeshAgent>().areaMask)) throw new Exception("CONTRACT FAIL: unreachable position "+v);
  }
  if(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length<40) throw new Exception("CONTRACT FAIL: room visual missing");
  if(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.CompareTag("MainCamera"))!=1) throw new Exception("CONTRACT FAIL: expected exactly one main camera");
  if(Camera.main==null) throw new Exception("CONTRACT FAIL: missing main camera");
  Capture("After.png");
  Debug.Log("STAGE4_CONTRACT_PASS: scene, controller, agent, all six positions and room renderers");
 }
 public static void Capture(string name) {
  var camera=Camera.main;
  if(camera==null) throw new Exception("Screenshot requires MainCamera");
  var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
  var rt=new RenderTexture(1280,800,24);Texture2D image=null;
  try {
   camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   image=new Texture2D(1280,800,TextureFormat.RGB24,false);
   image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
   var dir=Path.GetFullPath("../Stage4Evidence");Directory.CreateDirectory(dir);
   File.WriteAllBytes(Path.Combine(dir,name),image.EncodeToPNG());
  } finally {
   camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
   if(image!=null)UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
  }
 }
}
