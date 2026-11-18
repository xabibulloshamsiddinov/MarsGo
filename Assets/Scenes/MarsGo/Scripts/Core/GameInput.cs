using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsGo
{
    /// <summary>
    /// All game input in one place (keyboard, gamepad, and on-screen touch buttons).
    /// Mirrors the Godot input map: thrust, rotate_left/right, pause, restart.
    /// Touch controls write the Virtual* fields; gameplay only reads the combined values.
    /// </summary>
    public static class GameInput
    {
        public static readonly InputAction Thrust = new InputAction("Thrust", InputActionType.Value, expectedControlType: "Axis");
        /// <summary>+1 = rotate left (counter-clockwise), -1 = rotate right.</summary>
        public static readonly InputAction Rotate = new InputAction("Rotate", InputActionType.Value, expectedControlType: "Axis");
        public static readonly InputAction Pause = new InputAction("Pause", InputActionType.Button);
        public static readonly InputAction Restart = new InputAction("Restart", InputActionType.Button);
        public static readonly InputAction Confirm = new InputAction("Confirm", InputActionType.Button);

        /// <summary>0..1 from the on-screen thrust button.</summary>
        public static float VirtualThrust;
        /// <summary>-1..1 from the on-screen rotate buttons (+1 = left).</summary>
        public static float VirtualRotate;

        static bool _enabled;

        static GameInput()
        {
            Thrust.AddBinding("<Keyboard>/space");
            Thrust.AddBinding("<Keyboard>/w");
            Thrust.AddBinding("<Keyboard>/upArrow");
            Thrust.AddBinding("<Gamepad>/buttonSouth");
            Thrust.AddBinding("<Gamepad>/rightTrigger");

            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/d").With("Positive", "<Keyboard>/a");
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/rightArrow").With("Positive", "<Keyboard>/leftArrow");
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/dpad/right").With("Positive", "<Gamepad>/dpad/left");
            Rotate.AddBinding("<Gamepad>/leftStick/x").WithProcessors("axisDeadzone(min=0.2),invert");

            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");

            Restart.AddBinding("<Keyboard>/r");
            Restart.AddBinding("<Gamepad>/buttonNorth");

            Confirm.AddBinding("<Keyboard>/enter");
            Confirm.AddBinding("<Keyboard>/numpadEnter");
            Confirm.AddBinding("<Keyboard>/space");
            Confirm.AddBinding("<Gamepad>/buttonSouth");
        }

        public static void Enable()
        {
            if (_enabled) return;
            _enabled = true;
            Thrust.Enable();
            Rotate.Enable();
            Pause.Enable();
            Restart.Enable();
            Confirm.Enable();
        }

        public static float ThrustValue => Mathf.Clamp01(Mathf.Max(Thrust.ReadValue<float>(), VirtualThrust));

        public static float RotateValue => Mathf.Clamp(Rotate.ReadValue<float>() + VirtualRotate, -1f, 1f);
    }
}
