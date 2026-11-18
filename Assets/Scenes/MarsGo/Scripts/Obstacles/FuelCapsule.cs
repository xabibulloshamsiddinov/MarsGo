using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Fuel pickup (port of fuel_capsule.gd). Spins, bobs and glows green; when the rocket
    /// touches it: FuelTank.AddFuel → "collected" → the capsule is sucked into the rocket
    /// (flies to it while shrinking and whirling) → removed (or respawns). Never crashes anything.
    /// </summary>
    public class FuelCapsule : ObstacleBase
    {
        [Header("Fuel")]
        public float fuelAmount = 25f;

        [Header("Idle motion")]
        public float spinSpeedDegrees = 90f;
        public float bobHeight = 0.25f;
        public float bobSpeed = 0.5f;

        [Header("Collect")]
        public float suckTime = 0.3f;
        public float suckSpinBoost = 6f;

        [Header("Respawn")]
        public bool respawn;
        public float respawnTime = 6f;
        public float respawnGrowTime = 0.4f;

        [Header("Size")]
        public float radius = 0.38f;
        public float bodyHeight = 0.9f;
        public float triggerRadius = 0.9f;

        public override string AudioKind => "fuel";

        public bool IsCollected { get; private set; }

        Transform _pivot;
        Transform _halo;
        Light _glow;
        Material _glass;
        SphereCollider _trigger;
        Rocket _rocket;
        float _time, _spin, _suck = -1f, _respawnTimer = -1f, _grow = -1f;
        Vector3 _home, _suckStart;

        public void Build()
        {
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);
            _glass = MarsMaterials.Instance("FuelGlass");
            var cap = MarsMaterials.Get("FuelCap");
            MeshFactory.Part(_pivot, "Body", MeshFactory.Frustum(radius, radius, bodyHeight, 20), _glass, Vector3.zero, Quaternion.identity);
            MeshFactory.Part(_pivot, "CapTop", MeshFactory.Frustum(radius * 0.55f, radius * 1.08f, 0.16f, 20), cap,
                new Vector3(0f, bodyHeight * 0.5f + 0.08f, 0f), Quaternion.identity);
            MeshFactory.Part(_pivot, "CapBottom", MeshFactory.Frustum(radius * 1.08f, radius * 0.55f, 0.16f, 20), cap,
                new Vector3(0f, -bodyHeight * 0.5f - 0.08f, 0f), Quaternion.identity);
            MeshFactory.Part(_pivot, "Ring", MeshFactory.Torus(radius * 1.02f, radius * 1.18f, 24, 8), cap, Vector3.zero, Quaternion.identity);
            MeshFactory.Part(_pivot, "Nozzle", MeshFactory.Frustum(0.07f, 0.09f, 0.12f, 10), cap,
                new Vector3(0f, bodyHeight * 0.5f + 0.22f, 0f), Quaternion.identity);

            var halo = MeshFactory.Part(transform, "Halo", MeshFactory.Sphere, MarsMaterials.Instance("FuelHalo"), Vector3.zero, Quaternion.identity, Vector3.one * 1.9f);
            halo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _halo = halo.transform;

            var lightGo = new GameObject("GlowLight");
            lightGo.transform.SetParent(transform, false);
            _glow = lightGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = Palette.Safe;
            _glow.range = 4f;
            _glow.intensity = 2f;
            _glow.shadows = LightShadows.None;

            _trigger = gameObject.AddComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = triggerRadius;

            _home = _pivot.localPosition;
            _time = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            if (_pivot == null) return;
            float dt = Time.deltaTime;
            _time += dt;

            if (_suck >= 0f)
            {
                UpdateSuck(dt);
                return;
            }
            if (_respawnTimer >= 0f)
            {
                _respawnTimer -= dt;
                if (_respawnTimer < 0f) Respawn();
                return;
            }

            _spin += spinSpeedDegrees * dt;
            _pivot.localPosition = _home + Vector3.up * (Mathf.Sin(_time * bobSpeed * Mathf.PI * 2f) * bobHeight);
            _pivot.localRotation = Quaternion.Euler(0f, _spin, 12f);
            float pulse = 1f + 0.15f * Mathf.Sin(_time * 3f);
            _halo.localScale = Vector3.one * (1.9f * pulse);

            if (_grow >= 0f)
            {
                _grow += dt / Mathf.Max(respawnGrowTime, 0.01f);
                float s = BackOut(Mathf.Clamp01(_grow));
                _pivot.localScale = Vector3.one * Mathf.Max(s, 0.01f);
                if (_grow >= 1f) _grow = -1f;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsCollected) return;
            var judge = FlyingJudge(other);
            if (judge == null) return;
            var tank = judge.GetComponent<FuelTank>();
            if (tank == null) return;

            IsCollected = true;
            _rocket = judge.Rocket;
            tank.AddFuel(fuelAmount);
            _trigger.enabled = false;
            Emit(SignalCollected);

            _halo.gameObject.SetActive(false);
            _glow.enabled = false;
            _suckStart = _pivot.position;
            _suck = 0f;
        }

        /// <summary>Fly into the (moving) rocket, shrink, whirl faster and flash brighter.</summary>
        void UpdateSuck(float dt)
        {
            _suck += dt / Mathf.Max(suckTime, 0.01f);
            float t = Mathf.Clamp01(_suck);
            float eased = t * t;
            if (_rocket != null) _pivot.position = Vector3.Lerp(_suckStart, _rocket.transform.position, eased);
            _spin += spinSpeedDegrees * suckSpinBoost * dt;
            _pivot.localRotation = Quaternion.Euler(0f, _spin, 12f);
            _pivot.localScale = Vector3.one * Mathf.Max(1f - eased, 0.01f);
            MarsMaterials.SetEmission(_glass, Palette.Safe * Mathf.Lerp(2.2f, 10f, t));
            if (t < 1f) return;

            _suck = -1f;
            _pivot.gameObject.SetActive(false);
            if (respawn) _respawnTimer = respawnTime;
            else Destroy(gameObject);
        }

        void Respawn()
        {
            IsCollected = false;
            _rocket = null;
            _pivot.localPosition = _home;
            _pivot.localScale = Vector3.one * 0.01f;
            _pivot.gameObject.SetActive(true);
            MarsMaterials.SetEmission(_glass, Palette.Safe * 2.2f);
            _halo.gameObject.SetActive(true);
            _glow.enabled = true;
            _trigger.enabled = true;
            _grow = 0f;
            Emit(SignalActivated);
        }

        static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
