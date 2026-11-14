using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.StealthSample;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

// Synthetic-device regression, deliberately separate from desktop gameplay evidence.
public static class ExpandedInteractionChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception("EXPANDED_INTERACTION_CHECK: " + message); }
    static void Near(float value, float expected, string message) { Require(Mathf.Abs(value - expected) < .003f, message + ": " + value + " expected " + expected); }
    public static void Run()
    {
        Require(EditorApplication.isPlaying, "PlayMode required");
        foreach (var session in UnityEngine.Object.FindObjectsByType<ExpandedSession>(FindObjectsSortMode.None)) Require(session.Paused, "Pause the live session");
        using (var f = new Fixture(InteractionKind.Charger, 40))
        {
            Near(f.battery.Current, 40, "Profile initial energy");
            f.battery.Tick(10, false, false); Near(f.battery.Current, 38, "New idle drain");
            f.battery.Tick(10, true, false); Near(f.battery.Current, 33, "New total movement drain");
            f.battery.Tick(1, false, true); Near(f.battery.Current, 40.8f, "New net charging rate");
            f.EnableReleased(); f.Keys(Key.E); f.interaction.Prepare();
            Require(f.interaction.Charging && f.player.HorizontalMovementLocked, "E begins charging");
            Vector3 position = f.player.transform.position;
            f.Keys(Key.E, Key.W); f.interaction.Prepare();
            Require(!f.interaction.Current && !f.player.HorizontalMovementLocked && f.interaction.WaitingForRelease, "W cancels in the same Prepare frame");
            f.player.Tick(.1f);
            Require(f.player.transform.position.z > position.z + .2f, "Cancel immediately allows retreat");
            f.Keys(Key.E); f.interaction.Prepare();
            Require(!f.interaction.Current && f.interaction.WaitingForRelease, "Still-held E does not restart");
            f.Keys(); f.interaction.Prepare(); f.Keys(Key.E); f.interaction.Prepare();
            Require(f.interaction.Charging, "Released then fresh E restarts");
        }
        using (var f = new Fixture(InteractionKind.Terminal, 35.9f))
        {
            f.EnableReleased(); f.Keys(Key.E); f.interaction.Prepare();
            Require(!f.interaction.Supplying, "Below 36 is rejected");
        }
        using (var f = new Fixture(InteractionKind.Terminal, 36))
        {
            f.EnableReleased(); f.Keys(Key.E); f.interaction.Prepare();
            Require(f.interaction.Supplying, "36 permits supply");
            f.interaction.Advance(1, false); Near(f.battery.Current, 35.8f, "Supply only drains baseline until completion");
            f.Keys(Key.E, Key.A); f.interaction.Prepare();
            Require(!f.interaction.Current && f.interaction.SupplyElapsed == 0 && !f.interaction.Powered, "Movement cancels partial terminal progress");
            Near(f.battery.Current, 35.8f, "Cancelled supply does not charge fee");
        }
        using (var f = new Fixture(InteractionKind.Terminal, 36))
        {
            f.EnableReleased(); f.Keys(Key.E); f.interaction.Prepare(); f.interaction.Advance(2.4f, false);
            Require(f.interaction.Powered, "Two seconds completes supply"); Near(f.battery.Current, .52f, "One fee plus frame overshoot baseline");
            f.interaction.Advance(.2f, false); Near(f.battery.Current, .48f, "Completed terminal is not charged twice");
            f.door.Tick(1);
            var anchor = f.Child("RotatedExit", new Vector3(10, 0, 10)).transform;
            anchor.rotation = Quaternion.Euler(0, 90, 0); f.door.exitAnchor = anchor;
            Require(f.door.HasEscaped(anchor.TransformPoint(new Vector3(0, 0, .5f))), "Rotated local exit accepts forward point");
            Require(!f.door.HasEscaped(anchor.TransformPoint(new Vector3(0, 0, -.1f))), "Local behind-plane point rejected");
            Require(!f.door.HasEscaped(anchor.TransformPoint(new Vector3(1.3f, 0, .5f))), "Local width enforced");
        }
        string directory = Path.Combine(Application.dataPath, "../Evidence/WarehouseExpanded"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "interaction-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"),
            "PASS new energy profile; move-to-cancel charge and supply; held-E rearm; 35.9/36 threshold; 2s/35 fee and overshoot; rotated local exit.\nSynthetic input/component fixtures only.\n");
        Debug.Log("EXPANDED_INTERACTION_CHECK_PASS");
    }

    sealed class Fixture : IDisposable
    {
        readonly GameObject root;
        readonly InputActionAsset asset;
        readonly ExpandedGameplayProfile profile;
        readonly Keyboard previousKeyboard;
        readonly Mouse previousMouse;
        readonly Keyboard keyboard;
        readonly Mouse mouse;
        public readonly StealthPlayerController player;
        public readonly EscapeBattery battery;
        public readonly EscapeInteraction interaction;
        public readonly EscapeDoor door;
        public Fixture(InteractionKind kind, float initial)
        {
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            keyboard = InputSystem.AddDevice<Keyboard>("ExpandedInteractionKeyboard"); mouse = InputSystem.AddDevice<Mouse>("ExpandedInteractionMouse");
            root = new GameObject("ExpandedInteractionFixture"); root.transform.position = new Vector3(40, 0, 40);
            var rules = root.AddComponent<EscapeRules>(); profile = ScriptableObject.CreateInstance<ExpandedGameplayProfile>(); profile.ApplyTo(rules); rules.initialEnergy = initial;
            battery = root.AddComponent<EscapeBattery>(); battery.rules = rules; battery.Initialize();
            var playerObject = Child("FixturePlayer", Vector3.zero); playerObject.layer = 8;
            player = playerObject.AddComponent<StealthPlayerController>(); player.controller = playerObject.AddComponent<CharacterController>();
            // Match the shipped robot capsule instead of the default 2m capsule,
            // whose center at zero places half of this fixture inside its floor.
            player.controller.enabled = false;
            player.controller.height = 1.1f; player.controller.radius = .42f;
            player.controller.center = new Vector3(0, .55f, 0);
            player.controller.stepOffset = .15f; player.controller.skinWidth = .03f;
            player.controller.enabled = true; player.speed = profile.playerSpeed;
            player.viewCamera = Child("FixtureCamera", new Vector3(0, .9f, 0)).AddComponent<Camera>(); player.viewCamera.enabled = false;
            asset = ScriptableObject.CreateInstance<InputActionAsset>(); var map = asset.AddActionMap("Player");
            var move = map.AddAction("Move", InputActionType.Value); move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            player.inputAsset = asset; player.Initialize(); player.MoveAction.actionMap.devices = new InputDevice[] { keyboard };
            var floor = Child("FixtureFloor", new Vector3(0, -.06f, 0)); floor.AddComponent<BoxCollider>().size = new Vector3(12, .12f, 12);
            door = root.AddComponent<EscapeDoor>(); door.rules = rules;
            door.leftLeaf = Child("DoorLeft", new Vector3(4, 0, 4)).transform; door.rightLeaf = Child("DoorRight", new Vector3(5, 0, 4)).transform;
            var barrier = Child("Barrier", new Vector3(4, 1, 4)); door.doorCollider = barrier.AddComponent<BoxCollider>(); door.doorObstacle = barrier.AddComponent<NavMeshObstacle>(); door.Initialize();
            var primary = Target("Primary", kind, new Vector3(0, .9f, 1.2f));
            var secondary = Target("Secondary", kind == InteractionKind.Charger ? InteractionKind.Terminal : InteractionKind.Charger, new Vector3(-5, .9f, 0));
            interaction = root.AddComponent<EscapeInteraction>(); interaction.player = player; interaction.battery = battery; interaction.rules = rules; interaction.door = door;
            interaction.cancelOnMovement = true; interaction.targets = new[] { primary, secondary }; interaction.Initialize(); interaction.InteractAction.ApplyBindingOverride(keyboard.eKey.path);
            Physics.SyncTransforms();
        }
        public GameObject Child(string name, Vector3 position) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.localPosition = position; return go; }
        EscapeInteractable Target(string name, InteractionKind kind, Vector3 local) { var target = Child(name, local).AddComponent<EscapeInteractable>(); target.anchor = target.transform; target.kind = kind; target.label = name; return target; }
        public void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update(); mouse.MakeCurrent();
        }
        public void EnableReleased() { Keys(); player.SetActive(true); interaction.SetActive(true); Keys(); interaction.Prepare(); player.Tick(0); }
        public void Dispose()
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            if (asset) UnityEngine.Object.DestroyImmediate(asset); if (profile) UnityEngine.Object.DestroyImmediate(profile);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
    }
}
