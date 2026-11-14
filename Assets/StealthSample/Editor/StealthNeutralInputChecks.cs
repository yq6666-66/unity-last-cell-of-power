using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LastCellOfPower.StealthSample;

// Synthetic-device regression only. Does not prove physical focus recovery.
public static class StealthNeutralInputChecks
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("STEALTH_NEUTRAL_FAIL: " + message);
    }

    public static void Run()
    {
        Require(EditorApplication.isPlaying, "Run inside PlayMode");
        var session = UnityEngine.Object.FindFirstObjectByType<StealthSession>();
        Require(!session || session.Paused, "Pause existing gameplay before synthetic test");
        var originalKeyboard = Keyboard.current;
        Keyboard keyboard = null;
        GameObject root = null;
        InputActionAsset asset = null;
        try
        {
            keyboard = InputSystem.AddDevice<Keyboard>("StealthNeutralSyntheticKeyboard");
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            var move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            root = new GameObject("StealthNeutralSyntheticPlayer");
            root.transform.position = new Vector3(30, 2, 30);
            var player = root.AddComponent<StealthPlayerController>();
            player.controller = root.AddComponent<CharacterController>();
            var cameraObject = new GameObject("SyntheticCamera");
            cameraObject.transform.SetParent(root.transform);
            player.viewCamera = cameraObject.AddComponent<Camera>();
            player.viewCamera.enabled = false;
            player.inputAsset = asset;
            player.Initialize();
            player.MoveAction.actionMap.devices = new InputDevice[] { keyboard };

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            player.SetActive(true);
            var initial = player.controller.transform.position;
            player.Tick(.02f);
            Require(player.WaitingForNeutral && player.InputValue == Vector2.zero, "Same-frame zero must not open gate");
            Require(Mathf.Abs(player.controller.transform.position.z - initial.z) < .00001f, "Same-frame no movement");
            // BeforeRender/Editor filtering is source-reviewed only: this package
            // does not expose the typed Update overload to application code.
            InputSystem.Update();
            Require(player.MoveAction.ReadValue<Vector2>().y > .9f, "Initial-state check detects held W");
            player.Tick(.02f);
            Require(player.WaitingForNeutral && player.InputValue == Vector2.zero, "Held W remains blocked after update");
            Require(Mathf.Abs(player.controller.transform.position.z - initial.z) < .00001f, "Held W does not move");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update(); player.Tick(.02f);
            Require(!player.WaitingForNeutral && player.InputValue == Vector2.zero, "Release opens neutral gate");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update(); player.Tick(.02f);
            Require(player.InputValue.y > .9f && player.controller.transform.position.z > initial.z + .04f,
                "Fresh W after release moves player");

            player.SetActive(false);
            player.SetActive(true);
            player.Tick(.02f);
            Require(player.WaitingForNeutral, "Second resume re-arms gate before input update");
            InputSystem.Update(); player.Tick(.02f);
            Require(player.WaitingForNeutral && player.InputValue == Vector2.zero, "Second resume blocks still-held W");
            Debug.Log("STEALTH_NEUTRAL_INPUT_PASS: same-frame, deferred initial-state, held key, release, fresh press, repeated resume; SYNTHETIC INPUT ONLY");
        }
        finally
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            if (asset) UnityEngine.Object.DestroyImmediate(asset);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
        }
    }
}
