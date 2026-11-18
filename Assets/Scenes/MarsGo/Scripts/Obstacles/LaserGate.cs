using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Timed laser barrier between two emitters along local X (rotate 90° for a vertical beam).
    /// Port of laser_gate.gd. Cycle: OFF → WARNING (blink) → ON → OFF...
    /// While ON, a rocket inside the hitbox crashes with reason "laser" — also one that was
    /// already inside when the beam switched on.
    /// </summary>
    public class LaserGate : ObstacleBase
    {
        public enum State { Off, Warning, On }

        const float BlinkDuty = 0.5f;
        const float MinTime = 0.01f;

        [Header("Timing")]
        public float onTime = 2f;
        public float offTime = 1.5f;
        public float warningTime = 0.5f;
        public float startOffset;
        public bool startOn;
        public bool gateEnabled = true;

        [Header("Shape")]
        public float beamLength = 8f;
        [Tooltip("Deadly thickness — a bit thinner than it looks: fair hits only.")]
        public float hitThickness = 0.35f;
        public float visualWidth = 1.2f;
        public float hitDepth = 2f;
        public Vector3 emitterSize = new Vector3(0.8f, 0.9f, 1.4f);

        [Header("Look")]
        public float blinkRate = 8f;
        public float fadeSpeed = 30f;
        public float lensOffEnergy = 0.3f;
        public float lensOnEnergy = 6f;
        public float lensWarningEnergy = 4f;

        public override string AudioKind => "laser";

        public State CurrentState { get; private set; } = State.Off;
        public bool IsOn => CurrentState == State.On;

        float _time, _intensity;
        Material _lens, _core, _halo;
        Transform _coreT, _haloT, _dashes;
        BoxCollider _hitbox;

        public void Build()
        {
            float half = beamLength * 0.5f;
            var housing = MarsMaterials.Get("LaserHousing");
            _lens = MarsMaterials.Instance("LaserLens");
            for (int side = -1; side <= 1; side += 2)
            {
                var em = GameObject.CreatePrimitive(PrimitiveType.Cube);
                em.name = side < 0 ? "EmitterA" : "EmitterB";
                em.transform.SetParent(transform, false);
                em.transform.localPosition = new Vector3(side * (half + emitterSize.x * 0.5f), 0f, 0f);
                em.transform.localScale = emitterSize;
                em.GetComponent<MeshRenderer>().sharedMaterial = housing;
                em.AddComponent<SurfaceTag>().kind = SurfaceKind.Obstacle;
                MeshFactory.Part(transform, "Lens", MeshFactory.Sphere, _lens,
                    new Vector3(side * half, 0f, 0f), Quaternion.identity, Vector3.one * 0.44f);
            }

            _core = MarsMaterials.Instance("LaserBeam");
            _halo = MarsMaterials.Instance("LaserHalo");
            var core = MeshFactory.Part(transform, "BeamCore", MeshFactory.Cube, _core, Vector3.zero, Quaternion.identity,
                new Vector3(beamLength, 0.12f, 0.12f));
            var halo = MeshFactory.Part(transform, "BeamHalo", MeshFactory.Cube, _halo, Vector3.zero, Quaternion.identity,
                new Vector3(beamLength, visualWidth * 0.5f, 0.3f));
            core.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coreT = core.transform;
            _haloT = halo.transform;

            // Dashed line shown while warning.
            _dashes = new GameObject("WarningDashes").transform;
            _dashes.SetParent(transform, false);
            int dashCount = Mathf.Max(2, Mathf.RoundToInt(beamLength / 0.6f));
            float step = beamLength / dashCount;
            for (int i = 0; i < dashCount; i++)
            {
                var d = MeshFactory.Part(_dashes, "Dash", MeshFactory.Cube, _core,
                    new Vector3(-half + step * (i + 0.5f), 0f, 0f), Quaternion.identity, new Vector3(step * 0.5f, 0.05f, 0.05f));
                d.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var hit = new GameObject("Hitbox");
            hit.transform.SetParent(transform, false);
            _hitbox = hit.AddComponent<BoxCollider>();
            _hitbox.isTrigger = true;
            _hitbox.size = new Vector3(beamLength, hitThickness, hitDepth);
            hit.AddComponent<LaserHitbox>().gate = this;

            _time = startOffset + (startOn ? offTime : 0f);
            CurrentState = StateAt(_time);
            UpdateVisuals(0f);
        }

        void FixedUpdate()
        {
            _time += Time.fixedDeltaTime;
            var next = StateAt(_time);
            if (next == CurrentState) return;
            CurrentState = next;
            Emit(next == State.On ? SignalActivated : next == State.Warning ? SignalWarning : SignalDeactivated);
        }

        void Update() => UpdateVisuals(Time.deltaTime);

        void UpdateVisuals(float dt)
        {
            if (_core == null) return;
            float target = CurrentState == State.On ? 1f : 0f;
            _intensity = dt <= 0f ? target : Mathf.Lerp(_intensity, target, 1f - Mathf.Exp(-fadeSpeed * dt));
            bool blink = CurrentState == State.Warning && Mathf.Repeat(_time * blinkRate, 1f) < BlinkDuty;

            float wobble = 1f + 0.08f * Mathf.Sin(Time.time * 47f) + 0.05f * Mathf.Sin(Time.time * 83f);
            _coreT.localScale = new Vector3(beamLength, 0.12f * _intensity * wobble, 0.12f * _intensity);
            _haloT.localScale = new Vector3(beamLength, visualWidth * 0.5f * _intensity * wobble, 0.3f * _intensity);
            _coreT.gameObject.SetActive(_intensity > 0.01f);
            _haloT.gameObject.SetActive(_intensity > 0.01f);
            _dashes.gameObject.SetActive(blink);
            MarsMaterials.SetEmission(_core, Palette.Hazard * (blink && _intensity < 0.5f ? 3f : 8f));

            float lens = lensOffEnergy;
            if (CurrentState == State.On) lens = lensOnEnergy;
            else if (CurrentState == State.Warning) lens = blink ? lensWarningEnergy : lensOffEnergy;
            MarsMaterials.SetEmission(_lens, Palette.Hazard * lens);
        }

        State StateAt(float t)
        {
            if (!gateEnabled) return State.Off;
            float period = Mathf.Max(onTime + offTime, MinTime);
            float local = Mathf.Repeat(t, period);
            if (local < offTime - warningTime) return State.Off;
            if (local < offTime) return State.Warning;
            return State.On;
        }

        /// <summary>Called by the hitbox for every rocket collider inside it, every physics step.</summary>
        public void TryKill(Collider other)
        {
            if (!IsOn) return;
            var judge = FlyingJudge(other);
            if (judge != null) judge.ForceCrash(CrashReasons.Laser, judge.Rocket.Body.linearVelocity.magnitude);
        }
    }

    /// <summary>Trigger child of a LaserGate (OnTriggerStay also catches a rocket already inside).</summary>
    public class LaserHitbox : MonoBehaviour
    {
        public LaserGate gate;

        void OnTriggerEnter(Collider other) => gate.TryKill(other);

        void OnTriggerStay(Collider other) => gate.TryKill(other);
    }
}
