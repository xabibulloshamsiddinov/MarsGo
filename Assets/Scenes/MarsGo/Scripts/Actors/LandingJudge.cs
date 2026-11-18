using System;
using UnityEngine;

namespace MarsGo
{
    public static class CrashReasons
    {
        public const string Ground = "ground";
        public const string Obstacle = "obstacle";
        public const string Hazard = "hazard";
        public const string TooFast = "too_fast";
        public const string BadAngle = "bad_angle";
        public const string OutOfFuel = "out_of_fuel";
        public const string Laser = "laser";
        public const string Blade = "blade";
        public const string Crushed = "crushed";
    }

    /// <summary>Crash details, kept so the crash screen can explain exactly what went wrong.</summary>
    public struct CrashInfo
    {
        public string Reason;
        public float ImpactSpeed;
        public float TiltDegrees;
        public float SafeSpeed;
        public float SafeAngle;
    }

    public struct LandingStats
    {
        public float LandingSpeed;
        public float LandingAngle;
        public float FuelLeft;
        public float FuelRatio;
        public float FlightTime;
    }

    /// <summary>
    /// Game rules for the rocket: safe landing, crash and reason (port of landing_judge.gd).
    /// Collisions are gathered in OnCollisionEnter/Stay and judged in the next FixedUpdate.
    ///  * touching a pad with the side/edge  -> crash "obstacle"
    ///  * bouncing off the pad               -> settle timer resets, next touch is judged again
    ///  * tipping over while settling        -> crash "bad_angle"
    ///  * sliding on the pad                 -> must be (almost) still for settleTime to win
    ///  * out of fuel, stuck off the pad     -> crash "out_of_fuel" after strandedTime
    /// </summary>
    [RequireComponent(typeof(Rocket))]
    public class LandingJudge : MonoBehaviour
    {
        public event Action<LandingStats> LandedSafely;
        public event Action<CrashInfo> Crashed;
        public event Action TouchdownStarted;
        public event Action TouchdownCancelled;
        /// <summary>speed, vertical speed (m/s, &lt;0 = falling), tilt (degrees from upright).</summary>
        public event Action<float, float, float> TelemetryChanged;

        [Header("Safe landing")]
        public float safeLandingSpeed = 3f;
        public float safeLandingAngle = 15f;
        public float settleTime = 0.5f;
        public float settleMaxSpeed = 0.6f;
        [Tooltip("A pad contact lower than (pad top - this) is a side hit.")]
        public float padTopTolerance = 0.15f;

        [Header("Stranded (no fuel)")]
        public float strandedTime = 2.5f;
        public float strandedMaxSpeed = 0.3f;

        public bool IsResolved { get; private set; }
        public CrashInfo LastCrash { get; private set; }
        public Rocket Rocket { get; private set; }

        FuelTank _tank;
        Vector3 _prevVelocity;
        bool _touchingPad;
        bool _padContactThisStep;
        float _padImpactThisStep;
        float _touchdownSpeed;
        float _settleTimer;
        float _strandedTimer;
        float _flightTime;

        void Awake()
        {
            Rocket = GetComponent<Rocket>();
            _tank = GetComponent<FuelTank>();
            GetComponent<Rigidbody>().sleepThreshold = 0f;   // keep OnCollisionStay coming while it sits on the pad
        }

        public float TiltDegrees => Vector3.Angle(transform.up, Vector3.up);

        void FixedUpdate()
        {
            if (IsResolved) return;
            float dt = Time.fixedDeltaTime;
            _flightTime += dt;
            var velocity = Rocket.Body.linearVelocity;

            bool onPad = _padContactThisStep;
            float impact = _padImpactThisStep > 0f ? _padImpactThisStep : _prevVelocity.magnitude;
            _padContactThisStep = false;
            _padImpactThisStep = 0f;

            if (onPad && !_touchingPad)
            {
                // New touchdown: the first one, or the next one after a bounce.
                if (impact > safeLandingSpeed) { Crash(CrashReasons.TooFast, impact); return; }
                if (TiltDegrees > safeLandingAngle) { Crash(CrashReasons.BadAngle, impact); return; }
                _touchdownSpeed = impact;
                _settleTimer = 0f;
                TouchdownStarted?.Invoke();
            }
            else if (!onPad && _touchingPad)
            {
                _settleTimer = 0f;
                TouchdownCancelled?.Invoke();
            }
            _touchingPad = onPad;

            if (_touchingPad)
            {
                if (TiltDegrees > safeLandingAngle) { Crash(CrashReasons.BadAngle, velocity.magnitude); return; }
                _settleTimer = velocity.magnitude <= settleMaxSpeed ? _settleTimer + dt : 0f;
                if (_settleTimer >= settleTime) { Land(); return; }
            }

            bool stuck = _tank != null && _tank.IsEmpty && !_touchingPad && velocity.magnitude <= strandedMaxSpeed;
            _strandedTimer = stuck ? _strandedTimer + dt : 0f;
            if (_strandedTimer >= strandedTime) { Crash(CrashReasons.OutOfFuel, 0f); return; }

            _prevVelocity = velocity;
            TelemetryChanged?.Invoke(velocity.magnitude, velocity.y, TiltDegrees);
        }

        void OnCollisionEnter(Collision c) => HandleContact(c, true);

        void OnCollisionStay(Collision c) => HandleContact(c, false);

        void HandleContact(Collision c, bool isNew)
        {
            if (IsResolved) return;
            var tag = c.collider.GetComponentInParent<SurfaceTag>();
            var kind = tag != null ? tag.kind : SurfaceKind.Obstacle;
            if (kind == SurfaceKind.Friendly) return;

            float impact = isNew ? c.relativeVelocity.magnitude : 0f;
            if (kind == SurfaceKind.Finish)
            {
                float top = tag.TopY;
                for (int i = 0; i < c.contactCount; i++)
                {
                    if (c.GetContact(i).point.y < top - padTopTolerance)
                    {
                        Crash(CrashReasons.Obstacle, Mathf.Max(impact, _prevVelocity.magnitude));
                        return;
                    }
                }
                _padContactThisStep = true;
                _padImpactThisStep = Mathf.Max(_padImpactThisStep, impact);
                return;
            }
            Crash(ReasonFor(tag, kind), Mathf.Max(impact, _prevVelocity.magnitude));
        }

        static string ReasonFor(SurfaceTag tag, SurfaceKind kind)
        {
            if (tag != null && !string.IsNullOrEmpty(tag.crashReason)) return tag.crashReason;
            switch (kind)
            {
                case SurfaceKind.Hazard: return CrashReasons.Hazard;
                case SurfaceKind.Ground: return CrashReasons.Ground;
                default: return CrashReasons.Obstacle;
            }
        }

        /// <summary>Playground: judge the next flight from scratch.</summary>
        public void Rearm()
        {
            IsResolved = false;
            LastCrash = default;
            _prevVelocity = Vector3.zero;
            _touchingPad = false;
            _touchdownSpeed = 0f;
            _settleTimer = 0f;
            _strandedTimer = 0f;
            _flightTime = 0f;
            Rocket.EnableControls();
        }

        /// <summary>Called by DeathZone, lasers, blades and other external killers.</summary>
        public void ForceCrash(string reason, float impactSpeed = 0f) => Crash(reason, impactSpeed);

        void Crash(string reason, float impactSpeed)
        {
            if (IsResolved) return;
            IsResolved = true;
            Rocket.DisableControls();
            LastCrash = new CrashInfo
            {
                Reason = reason,
                ImpactSpeed = impactSpeed,
                TiltDegrees = TiltDegrees,
                SafeSpeed = safeLandingSpeed,
                SafeAngle = safeLandingAngle,
            };
            Crashed?.Invoke(LastCrash);
            if (GameManager.Instance != null) GameManager.Instance.RegisterCrash(LastCrash);
        }

        void Land()
        {
            if (IsResolved) return;
            IsResolved = true;
            Rocket.DisableControls();
            var stats = new LandingStats
            {
                LandingSpeed = _touchdownSpeed,
                LandingAngle = TiltDegrees,
                FuelLeft = _tank != null ? _tank.Fuel : 0f,
                FuelRatio = _tank != null ? _tank.Ratio : 0f,
                FlightTime = _flightTime,
            };
            LandedSafely?.Invoke(stats);
            if (GameManager.Instance != null) GameManager.Instance.RegisterLanding(stats);
        }
    }
}
