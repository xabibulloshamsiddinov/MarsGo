using System;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Rocket flight physics (port of rocket.gd). No sound, no particles, no rules here:
    /// VFX/SFX listen to the events; FuelTank and LandingJudge are sibling components.
    /// Rotation sign: positive = counter-clockwise as seen by the camera (nose tilts left).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Rocket : MonoBehaviour
    {
        public event Action ThrustStarted;
        public event Action ThrustStopped;
        /// <summary>0..1, whenever the smoothed throttle changes.</summary>
        public event Action<float> ThrustChanged;
        /// <summary>-1..1, positive = counter-clockwise. When the input changes.</summary>
        public event Action<float> RotationChanged;

        [Header("Thrust")]
        [Tooltip("Acceleration at full throttle (m/s²), multiplied by mass. 18 vs gravity 9.8 ≈ thrust/weight 1.8.")]
        public float thrustAcceleration = 18f;
        [Tooltip("0 → 1 throttle time (s).")]
        public float throttleUpTime = 0.15f;
        [Tooltip("1 → 0 throttle time (s).")]
        public float throttleDownTime = 0.22f;
        [Tooltip("Hard speed cap (m/s).")]
        public float maxSpeed = 14f;

        [Header("Rotation")]
        [Tooltip("rad/s. 2.6 ≈ 150°/s.")]
        public float maxAngularSpeed = 2.6f;
        public float angularAcceleration = 12f;
        public float angularDeceleration = 16f;

        const float MinRampTime = 0.001f;

        public float Throttle { get; private set; }
        /// <summary>-1 mirrors rotate input (for cameras looking from the far side).</summary>
        public float rotationInputSign = 1f;
        public bool ControlsEnabled { get; private set; } = true;
        public bool ThrustAvailable { get; private set; } = true;
        public bool IsThrusting { get; private set; }
        public Rigidbody Body { get; private set; }

        float _rotationInput;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool canThrust = ControlsEnabled && ThrustAvailable;
            UpdateThrottle(canThrust ? GameInput.ThrustValue : 0f, dt);
            SetRotationInput(ControlsEnabled ? GameInput.RotateValue * rotationInputSign : 0f);

            // Thrust along the rocket's own up.
            if (Throttle > 0f)
                Body.AddForce(transform.up * (thrustAcceleration * Body.mass * Throttle), ForceMode.Force);

            // Smooth turning: steer angular velocity toward the target, never snap it.
            if (ControlsEnabled)
            {
                float target = _rotationInput * maxAngularSpeed;
                float spin = Body.angularVelocity.z;
                float rate = Mathf.Abs(target) > Mathf.Abs(spin) ? angularAcceleration : angularDeceleration;
                spin = Mathf.MoveTowards(spin, target, rate * dt);
                Body.angularVelocity = new Vector3(0f, 0f, spin);
            }

            var v = Body.linearVelocity;
            if (v.magnitude > maxSpeed) Body.linearVelocity = v.normalized * maxSpeed;
        }

        public void DisableControls()
        {
            ControlsEnabled = false;
            SetRotationInput(0f);
            SetThrusting(false);   // throttle then winds down naturally in FixedUpdate
        }

        public void EnableControls() => ControlsEnabled = true;

        public void SetThrustAvailable(bool available) => ThrustAvailable = available;

        void UpdateThrottle(float target, float dt)
        {
            SetThrusting(target > 0f);
            float ramp = target > Throttle ? throttleUpTime : throttleDownTime;
            float next = Mathf.MoveTowards(Throttle, target, dt / Mathf.Max(ramp, MinRampTime));
            if (!Mathf.Approximately(next, Throttle))
            {
                Throttle = next;
                ThrustChanged?.Invoke(Throttle);
            }
        }

        void SetThrusting(bool value)
        {
            if (value == IsThrusting) return;
            IsThrusting = value;
            if (value) ThrustStarted?.Invoke();
            else ThrustStopped?.Invoke();
        }

        void SetRotationInput(float value)
        {
            if (Mathf.Approximately(value, _rotationInput)) return;
            _rotationInput = value;
            RotationChanged?.Invoke(value);
        }
    }
}
