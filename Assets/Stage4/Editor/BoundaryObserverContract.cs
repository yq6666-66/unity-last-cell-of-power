#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
public static class BoundaryObserverContract {
 public static void Build(){Run();Stage4Builder.Build();}
 public static void Run(){
  GameObject go=null;InputActionAsset original=null,clone=null;
  try {
   original=ScriptableObject.CreateInstance<InputActionAsset>();original.name="ContractOriginal";
   var map=new InputActionMap("Player");map.AddAction("Move",InputActionType.Value,"<Keyboard>/d");original.AddActionMap(map);
   clone=UnityEngine.Object.Instantiate(original);var expected=clone.FindAction("Player/Move",true);
   original.Enable();clone.Enable();go=new GameObject("BoundaryContractRuntime");var runtime=go.AddComponent<Stage4Runtime>();
   typeof(Stage4Runtime).GetField("move",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(runtime,expected);
   var observed=Unit1BoundaryProbe.ResolveRuntimeMove(runtime);
   if(!ReferenceEquals(observed,expected))throw new Exception("Observer must follow exact runtime action with duplicate names enabled");
   if(ReferenceEquals(observed,original.FindAction("Player/Move",true)))throw new Exception("Observer selected project source instead of runtime clone");
   expected.Disable();if(!ReferenceEquals(Unit1BoundaryProbe.ResolveRuntimeMove(runtime),expected))throw new Exception("Observer must retain identity when action disabled for focus loss");
   if(Unit1BoundaryProbe.ResolveRuntimeMove(null)!=null)throw new Exception("Missing runtime must not resolve");
   Debug.Log("BOUNDARY_OBSERVER_CONTRACT_PASS: duplicate assets, disabled runtime action, missing runtime");
  } finally {if(go!=null)UnityEngine.Object.DestroyImmediate(go);if(clone!=null){clone.Disable();UnityEngine.Object.DestroyImmediate(clone);}if(original!=null){original.Disable();UnityEngine.Object.DestroyImmediate(original);}}
 }
}
#endif
