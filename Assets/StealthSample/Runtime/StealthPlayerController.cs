using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastCellOfPower.StealthSample
{
    public sealed class StealthPlayerController : MonoBehaviour
    {
        public CharacterController controller;
        public Camera viewCamera;
        public InputActionAsset inputAsset;
        public float speed = 3f, sensitivity = .12f;
        public Vector2 InputValue { get; private set; }
        public bool WaitingForNeutral { get; private set; }
        public float Pitch { get; private set; }
        public bool HorizontalMovementLocked { get; set; }
        public float HorizontalDistanceLastTick { get; private set; }
        public InputAction MoveAction => move;
        InputActionAsset ownedActions;
        InputAction move;
        float verticalSpeed;
        bool active;
        string lastInput;
        ulong inputUpdateGeneration, enabledAtGeneration;
        bool subscribed;

        public void Initialize()
        {
            if (!controller || !controller.enabled || !viewCamera || !inputAsset)
                throw new InvalidOperationException("Player controller/camera/input reference missing");
            ownedActions = Instantiate(inputAsset);
            ownedActions.Disable();
            move = ownedActions.FindAction("Player/Move", true);
            InputSystem.onAfterUpdate += AfterInputUpdate;
            subscribed = true;
            viewCamera.transform.SetParent(controller.transform, false);
            viewCamera.transform.localPosition = new Vector3(0, .9f, 0);
            viewCamera.transform.localRotation = Quaternion.identity;
            Pitch = 0;
            SetActive(false);
        }

        public void SetActive(bool value)
        {
            active = value;
            verticalSpeed = 0;
            InputValue = Vector2.zero;
            HorizontalDistanceLastTick = 0;
            WaitingForNeutral = value;
            if (move == null) return;
            move.Disable();
            if (value)
            {
                enabledAtGeneration = inputUpdateGeneration;
                move.Enable();
            }
        }

        void AfterInputUpdate()
        {
            // Match InputActionState.OnBeforeInitialUpdate: Editor/BeforeRender
            // updates do not run the deferred initial-state check.
            var type = InputState.currentUpdateType;
            if (type == InputUpdateType.Dynamic || type == InputUpdateType.Fixed || type == InputUpdateType.Manual)
                inputUpdateGeneration++;
        }

        public void Tick(float dt)
        {
            HorizontalDistanceLastTick = 0;
            if (!active) return;
            if (!controller || !controller.enabled) throw new InvalidOperationException("Player controller disabled");
            if (!viewCamera) throw new InvalidOperationException("Player camera was destroyed");
            var raw = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            var k = Keyboard.current;
            string sample = $"value={raw} control={move.activeControl?.path} device={move.activeControl?.device.deviceId} WASD={k?.wKey.isPressed},{k?.aKey.isPressed},{k?.sKey.isPressed},{k?.dKey.isPressed} arrows={k?.upArrowKey.isPressed},{k?.leftArrowKey.isPressed},{k?.downArrowKey.isPressed},{k?.rightArrowKey.isPressed}";
            if (sample != lastInput) { Debug.Log("STEALTH_INPUT " + sample); lastInput = sample; }
            // Enable's initial-state check is deferred until an input update. The
            // same-frame default zero is not evidence that held controls were released.
            if (WaitingForNeutral && inputUpdateGeneration != enabledAtGeneration && raw.sqrMagnitude < .0001f)
                WaitingForNeutral = false;
            InputValue = WaitingForNeutral ? Vector2.zero : raw;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue() * sensitivity;
                controller.transform.Rotate(0, delta.x, 0);
                Pitch = Mathf.Clamp(Pitch - delta.y, -80, 80);
                viewCamera.transform.localRotation = Quaternion.Euler(Pitch, 0, 0);
            }
            verticalSpeed = controller.isGrounded ? -2 : verticalSpeed + Physics.gravity.y * dt;
            var direction = HorizontalMovementLocked ? Vector3.zero : controller.transform.right * InputValue.x + controller.transform.forward * InputValue.y;
            var before = controller.transform.position;
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * dt);
            var displacement = controller.transform.position - before; displacement.y = 0;
            HorizontalDistanceLastTick = displacement.magnitude;
        }

        void OnDestroy()
        {
            if (subscribed) InputSystem.onAfterUpdate -= AfterInputUpdate;
            if (ownedActions) { ownedActions.Disable(); Destroy(ownedActions); }
        }
    }
}
