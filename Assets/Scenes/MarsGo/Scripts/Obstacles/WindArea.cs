using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Box of wind that pushes the rocket (port of wind_zone.gd; renamed because Unity already has
    /// a WindZone). Force = direction * strength * gust * mass, so strength is an acceleration in
    /// m/s² (gravity 9.8, full thrust 18). Optional on/off cycle with a warning. Never crashes.
    /// Visuals: dust streaks flowing in the wind direction + a faint dust haze.
    /// </summary>
    public class WindArea : ObstacleBase
    {
        public enum State { Off, Warning, On }

        const float MinTime = 0.01f;

        [Header("Wind")]
        public Vector3 size = new Vector3(8f, 10f, 4f);
        public Vector3 direction = Vector3.right;
        public float strength = 7f;
        [Range(0f, 1f)] public float gustAmount = 0.3f;
        public float gustFrequency = 0.6f;

        [Header("Cycle")]
        public bool useCycle;
        public float onTime = 3f;
        public float offTime = 2f;
        public float warningTime = 0.7f;
        public float rampTime = 0.4f;
        public float startOffset;

        [Header("Look")]
        public int streakCount = 26;
        public float streakBaseSpeed = 3f;
        public float streakSpeedFactor = 0.8f;
        [Range(0f, 1f)] public float warningStreakRatio = 0.2f;
        public float streakDepth = 1f;

        public override string AudioKind => "wind";

        public State CurrentState { get; private set; } = State.On;
        /// <summary>Current push in m/s².</summary>
        public float CurrentAcceleration => strength * _power * _gust;

        readonly HashSet<Collider> _inside = new HashSet<Collider>();
        Rigidbody _rocketBody;
        float _time, _power, _gust = 1f, _seed;
        Vector3 _dir = Vector3.right;
        Transform[] _streaks;
        float[] _streakPhase;
        Material _streakMat;

        public void Build()
        {
            _dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;
            _seed = Random.value * 100f;

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var haze = MeshFactory.Part(transform, "Haze", MeshFactory.Cube, MarsMaterials.Get("WindFill"),
                new Vector3(0f, 0f, 1.5f), Quaternion.identity, new Vector3(size.x, size.y, 0.01f));
            haze.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _streakMat = MarsMaterials.Instance("WindStreak");
            _streaks = new Transform[streakCount];
            _streakPhase = new float[streakCount];
            var rot = Quaternion.FromToRotation(Vector3.right, _dir);
            for (int i = 0; i < streakCount; i++)
            {
                var s = MeshFactory.Part(transform, "Streak", MeshFactory.Cube, _streakMat, Vector3.zero, rot, Vector3.one);
                s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _streaks[i] = s.transform;
                _streakPhase[i] = Random.value;
                PlaceStreak(i, true);
            }

            _time = startOffset;
            CurrentState = StateAt(_time);
            _power = CurrentState == State.On ? 1f : 0f;
        }

        void Start()
        {
            // After the audio router has subscribed (Phase 3).
            if (CurrentState == State.On) Emit(SignalActivated);
        }

        void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || body.GetComponent<Rocket>() == null) return;
            _inside.Add(other);
            _rocketBody = body;
        }

        void OnTriggerExit(Collider other)
        {
            _inside.Remove(other);
            if (_inside.Count == 0) _rocketBody = null;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _time += dt;
            var next = StateAt(_time);
            if (next != CurrentState)
            {
                CurrentState = next;
                Emit(next == State.On ? SignalActivated : next == State.Warning ? SignalWarning : SignalDeactivated);
            }
            float target = CurrentState == State.On ? 1f : 0f;
            _power = Mathf.MoveTowards(_power, target, dt / Mathf.Max(rampTime, MinTime));
            _gust = 1f + gustAmount * (Mathf.PerlinNoise(_seed, _time * gustFrequency) * 2f - 1f);

            _inside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (_inside.Count == 0) _rocketBody = null;
            float accel = CurrentAcceleration;
            if (accel > 0f && _rocketBody != null)
                _rocketBody.AddForce(transform.rotation * _dir * (accel * _rocketBody.mass), ForceMode.Force);
        }

        void Update()
        {
            if (_streaks == null) return;
            float ratio = CurrentState == State.Warning ? Mathf.Max(_power, warningStreakRatio) : _power;
            float speed = (streakBaseSpeed + strength * streakSpeedFactor) * Mathf.Max(_gust, 0.1f);
            float travel = Mathf.Abs(_dir.x) * size.x + Mathf.Abs(_dir.y) * size.y;
            float dt = Time.deltaTime;
            for (int i = 0; i < _streaks.Length; i++)
            {
                _streakPhase[i] += dt * speed / Mathf.Max(travel, 0.1f);
                if (_streakPhase[i] >= 1f)
                {
                    _streakPhase[i] -= 1f;
                    PlaceStreak(i, false);
                }
                bool visible = (i + 0.5f) / _streaks.Length <= ratio;
                float p = _streakPhase[i];
                float fade = Mathf.Sin(p * Mathf.PI);
                float len = Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(strength / 12f)) * fade;
                _streaks[i].localScale = visible ? new Vector3(len, 0.04f, 0.04f) : Vector3.zero;
                var basePos = _streaks[i].localPosition;
                var along = _dir * ((p - 0.5f) * travel);
                var perp = new Vector3(-_dir.y, _dir.x, 0f);
                float lane = Vector3.Dot(basePos, perp);
                _streaks[i].localPosition = perp * lane + along + new Vector3(0f, 0f, basePos.z);
            }
        }

        /// <summary>Random lane across the wind and random depth around the flight plane.</summary>
        void PlaceStreak(int i, bool randomPhase)
        {
            var perp = new Vector3(-_dir.y, _dir.x, 0f);
            float across = Mathf.Abs(perp.x) * size.x + Mathf.Abs(perp.y) * size.y;
            float lane = (Random.value - 0.5f) * across * 0.95f;
            float z = (Random.value - 0.5f) * 2f * streakDepth;
            _streaks[i].localPosition = perp * lane + new Vector3(0f, 0f, z);
            if (randomPhase) _streakPhase[i] = Random.value;
        }

        State StateAt(float t)
        {
            if (!useCycle) return State.On;
            float period = Mathf.Max(onTime + offTime, MinTime);
            float local = Mathf.Repeat(t, period);
            if (local < offTime - warningTime) return State.Off;
            if (local < offTime) return State.Warning;
            return State.On;
        }
    }
}
