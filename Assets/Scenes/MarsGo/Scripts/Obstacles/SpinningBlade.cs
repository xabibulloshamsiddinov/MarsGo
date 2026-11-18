using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Spinning rotor in the X/Y plane, rotating around Z (port of spinning_blade.gd).
    /// Deadly on touch (crash reason "blade"). Telegraph: a faint red reach ring at the tip
    /// radius that brightens when the rocket comes within warnDistance of the tips.
    /// </summary>
    public class SpinningBlade : ObstacleBase
    {
        const float MinTime = 0.01f;
        const float HubDepthRatio = 1.2f;
        const float RingDepthGap = 0.05f;

        [Header("Spin")]
        public float speedDegrees = 120f;
        public bool clockwise;
        public float spinUpTime = 0.8f;
        public float startDelay;
        public bool startSpinning = true;
        public float startAngleDegrees;

        [Header("Blades")]
        [Range(1, 8)] public int bladeCount = 3;
        public float bladeLength = 3f;
        public float bladeWidth = 0.45f;
        public float bladeDepth = 1f;
        public float hubRadius = 0.6f;

        [Header("Telegraph")]
        public float ringMargin = 0.2f;
        public float warnDistance = 3f;
        public float warnHysteresis = 1f;
        public float alertResponse = 6f;

        public override string AudioKind => "blade";

        Rigidbody _rotor;
        Material _ring;
        float _speed, _targetSpeed, _delay, _angle, _alert;
        bool _activatedSent, _alerted;

        public void Build()
        {
            var rotorGo = new GameObject("Rotor");
            rotorGo.transform.SetParent(transform, false);
            _rotor = rotorGo.AddComponent<Rigidbody>();
            _rotor.isKinematic = true;
            _rotor.interpolation = RigidbodyInterpolation.Interpolate;
            var tag = rotorGo.AddComponent<SurfaceTag>();
            tag.kind = SurfaceKind.Hazard;
            tag.crashReason = CrashReasons.Blade;

            var faceCamera = Quaternion.Euler(90f, 0f, 0f);
            MeshFactory.Part(rotorGo.transform, "Hub", MeshFactory.Frustum(hubRadius, hubRadius, bladeDepth * HubDepthRatio, 24),
                MarsMaterials.Get("Hub"), Vector3.zero, faceCamera);
            var hubCol = rotorGo.AddComponent<CapsuleCollider>();
            hubCol.direction = 2;
            hubCol.radius = hubRadius;
            hubCol.height = bladeDepth;

            var metal = MarsMaterials.Get("BladeMetal");
            var edge = MarsMaterials.Get("BladeEdge");
            for (int i = 0; i < bladeCount; i++)
            {
                float a = 360f * i / bladeCount;
                var rot = Quaternion.Euler(0f, 0f, a);
                var dir = rot * Vector3.right;
                var blade = new GameObject("Blade" + i).transform;
                blade.SetParent(rotorGo.transform, false);
                blade.localPosition = dir * (bladeLength * 0.5f);
                blade.localRotation = rot;
                var size = new Vector3(bladeLength, bladeWidth, bladeDepth);
                MeshFactory.Part(blade, "Mesh", MeshFactory.Cube, metal, Vector3.zero, Quaternion.identity, size);
                // Red leading edge + tip so the deadly reach reads at a glance.
                MeshFactory.Part(blade, "Edge", MeshFactory.Cube, edge, new Vector3(0f, bladeWidth * 0.5f, 0f), Quaternion.identity,
                    new Vector3(bladeLength * 0.85f, 0.06f, bladeDepth * 1.02f));
                MeshFactory.Part(blade, "Tip", MeshFactory.Cube, edge, new Vector3(bladeLength * 0.5f - 0.12f, 0f, 0f), Quaternion.identity,
                    new Vector3(0.24f, bladeWidth * 1.02f, bladeDepth * 1.02f));
                var col = blade.gameObject.AddComponent<BoxCollider>();
                col.size = size;
            }

            _ring = MarsMaterials.Instance("ReachRing");
            float ringRadius = bladeLength + ringMargin;
            var ring = MeshFactory.Part(transform, "ReachRing", MeshFactory.Torus(ringRadius - 0.05f, ringRadius + 0.05f, 64, 6), _ring,
                new Vector3(0f, 0f, bladeDepth * 0.5f + RingDepthGap), faceCamera);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _angle = startAngleDegrees;
            _rotor.rotation = transform.rotation * Quaternion.Euler(0f, 0f, _angle);
            rotorGo.transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
            _delay = startDelay;
            if (startSpinning) _targetSpeed = speedDegrees;
        }

        /// <summary>Spins up to speedDegrees (emits "activated" at full speed).</summary>
        public void StartSpin()
        {
            _targetSpeed = speedDegrees;
            _activatedSent = false;
        }

        /// <summary>Spins down to a stop (emits "deactivated").</summary>
        public void StopSpin()
        {
            _targetSpeed = 0f;
            Emit(SignalDeactivated);
        }

        void FixedUpdate()
        {
            if (_rotor == null) return;
            float dt = Time.fixedDeltaTime;
            if (_delay > 0f)
            {
                _delay -= dt;
                return;
            }
            float accel = speedDegrees / Mathf.Max(spinUpTime, MinTime);
            _speed = Mathf.MoveTowards(_speed, _targetSpeed, accel * dt);
            if (!_activatedSent && _targetSpeed > 0f && Mathf.Approximately(_speed, _targetSpeed))
            {
                _activatedSent = true;
                Emit(SignalActivated);
            }
            _angle = Mathf.Repeat(_angle + _speed * (clockwise ? -1f : 1f) * dt, 360f);
            _rotor.MoveRotation(transform.rotation * Quaternion.Euler(0f, 0f, _angle));
        }

        void Update()
        {
            if (_ring == null) return;
            var rocket = FindRocket();
            float target = 0f;
            if (rocket != null)
            {
                var offset = rocket.transform.position - transform.position;
                float gap = new Vector2(offset.x, offset.y).magnitude - bladeLength;
                if (!_alerted && gap < warnDistance)
                {
                    _alerted = true;
                    Emit(SignalWarning);
                }
                else if (_alerted && gap > warnDistance + warnHysteresis)
                {
                    _alerted = false;
                }
                target = _alerted ? 1f : 0f;
            }
            _alert = Mathf.Lerp(_alert, target, 1f - Mathf.Exp(-alertResponse * Time.deltaTime));
            float pulse = 1f + 0.3f * _alert * Mathf.Sin(Time.time * 12f);
            MarsMaterials.SetEmission(_ring, Palette.Hazard * Mathf.Lerp(0.6f, 5f, _alert) * pulse);
        }
    }
}
