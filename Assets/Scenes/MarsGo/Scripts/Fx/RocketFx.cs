using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// The smaller rocket effects in one component (ports of rcs_puffs.gd, smoke_trail.gd,
    /// landing_dust.gd, hard_landing_fx.gd and fuel_sputter_fx.gd). Listens to Rocket / Judge /
    /// FuelTank events; never touches gameplay.
    /// </summary>
    public class RocketFx : MonoBehaviour
    {
        [Header("RCS puffs")]
        public Vector3 rcsOffset = new Vector3(0.48f, 0.85f, 0f);
        public int rcsPopAmount = 10;
        public float rcsHoldRate = 18f;

        [Header("Smoke trail")]
        public Vector3 trailOffset = new Vector3(0f, -1.4f, 0f);
        public float trailWidthStart = 0.12f;
        public float trailWidthEnd = 0.7f;
        public float trailMaxAlpha = 0.35f;

        [Header("Landing dust")]
        public float dustRange = 7f;
        public float dustRate = 160f;
        public Color dustColor = new Color(0.86f, 0.58f, 0.42f, 0.55f);

        [Header("Hard landing")]
        public float hardLandingSpeed = 1.8f;
        public float hardLandingTrauma = 0.3f;
        public Vector3 feetOffset = new Vector3(0f, -1.7f, 0f);

        [Header("Fuel sputter")]
        public Vector2 sputterInterval = new Vector2(0.35f, 1.2f);
        public Vector2Int sputterFrames = new Vector2Int(2, 3);

        Rocket _rocket;
        LandingJudge _judge;
        FuelTank _tank;
        EngineFlame _flame;
        ParticleSystem _rcsLeft, _rcsRight, _dust, _ring, _sputterSmoke;
        TrailRenderer _trail;
        float _rotation, _sputterTimer;
        bool _resolved;

        void Start()
        {
            _rocket = GetComponent<Rocket>();
            _judge = GetComponent<LandingJudge>();
            _tank = GetComponent<FuelTank>();
            _flame = GetComponent<EngineFlame>();

            _rcsRight = Puffs("RcsRight", new Vector3(rcsOffset.x, rcsOffset.y, 0f), 0f);
            _rcsLeft = Puffs("RcsLeft", new Vector3(-rcsOffset.x, rcsOffset.y, 0f), 180f);
            BuildTrail();
            BuildDust();
            BuildRing();
            BuildSputterSmoke();

            _rocket.RotationChanged += OnRotation;
            _judge.TouchdownStarted += OnTouchdown;
            _judge.Crashed += _ => Resolve();
            _judge.LandedSafely += _ => Resolve();
        }

        void Resolve()
        {
            _resolved = true;
            _rotation = 0f;
            UpdatePuffRates();
            _trail.emitting = false;
        }

        // ---------------------------------------------------------------- RCS

        ParticleSystem Puffs(string name, Vector3 pos, float yaw)
        {
            var ps = FxAssets.System(transform, name, pos, FxAssets.Alpha);
            ps.transform.localRotation = Quaternion.Euler(0f, 90f + yaw, 0f);   // emit sideways (±X)
            var m = ps.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.maxParticles = 60;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 20f;
            sh.radius = 0.04f;
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 0.5f;
            lv.dampen = 0.3f;
            FxAssets.ColorOverLife(ps, FxAssets.Grad((0f, new Color(1f, 1f, 1f, 0.8f)), (1f, new Color(0.95f, 0.9f, 0.85f, 0f))));
            FxAssets.SizeOverLife(ps, new Keyframe(0f, 0.5f), new Keyframe(1f, 2f));
            ps.Play();
            return ps;
        }

        /// <summary>+1 (counter-clockwise) fires the right-hand nose thruster, -1 the left.</summary>
        void OnRotation(float direction)
        {
            bool wasIdle = _rotation == 0f;
            _rotation = _resolved ? 0f : direction;
            if (_rotation != 0f && wasIdle) (_rotation > 0f ? _rcsRight : _rcsLeft).Emit(rcsPopAmount);
            UpdatePuffRates();
        }

        void UpdatePuffRates()
        {
            var e = _rcsRight.emission;
            e.rateOverTime = _rotation > 0f ? rcsHoldRate : 0f;
            e = _rcsLeft.emission;
            e.rateOverTime = _rotation < 0f ? rcsHoldRate : 0f;
        }

        // ---------------------------------------------------------------- trail

        void BuildTrail()
        {
            var go = new GameObject("SmokeTrail");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = trailOffset;
            _trail = go.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = FxAssets.Alpha;
            _trail.time = 1.2f;
            _trail.minVertexDistance = 0.25f;
            _trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidthStart), new Keyframe(1f, trailWidthEnd));
            _trail.colorGradient = FxAssets.Grad((0f, new Color(0.9f, 0.82f, 0.78f, trailMaxAlpha)), (1f, new Color(0.85f, 0.7f, 0.62f, 0f)));
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.emitting = false;
        }

        // ---------------------------------------------------------------- landing dust

        void BuildDust()
        {
            _dust = FxAssets.System(null, "LandingDust", Vector3.zero, FxAssets.Alpha);
            _dust.transform.SetParent(transform.parent, true);
            _dust.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);   // circle shape lies on the ground
            var m = _dust.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.04f;
            m.maxParticles = 400;
            var sh = _dust.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.6f;
            sh.radiusThickness = 0.2f;
            var lv = _dust.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 1f;
            lv.dampen = 0.12f;
            FxAssets.ColorOverLife(_dust, FxAssets.Grad((0f, new Color(dustColor.r, dustColor.g, dustColor.b, 0f)),
                (0.12f, dustColor), (1f, new Color(dustColor.r, dustColor.g, dustColor.b, 0f))));
            FxAssets.SizeOverLife(_dust, new Keyframe(0f, 0.6f), new Keyframe(1f, 2.6f));
            _dust.Play();
        }

        void BuildRing()
        {
            _ring = FxAssets.System(null, "HardLandingRing", Vector3.zero, FxAssets.Alpha);
            _ring.transform.SetParent(transform.parent, true);
            _ring.transform.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
            var m = _ring.main;
            m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            m.maxParticles = 60;
            var sh = _ring.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.9f;
            sh.radiusThickness = 0f;
            var lv = _ring.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 0.5f;
            lv.dampen = 0.2f;
            FxAssets.ColorOverLife(_ring, FxAssets.Grad((0f, dustColor), (1f, new Color(dustColor.r, dustColor.g, dustColor.b, 0f))));
            FxAssets.SizeOverLife(_ring, new Keyframe(0f, 0.8f), new Keyframe(1f, 2.4f));
        }

        void OnTouchdown()
        {
            float speed = _rocket.Body.linearVelocity.magnitude;
            if (speed < hardLandingSpeed) return;
            _ring.transform.position = transform.TransformPoint(feetOffset);
            _ring.Emit(Mathf.RoundToInt(Mathf.Lerp(16, 36, Mathf.InverseLerp(hardLandingSpeed, 3f, speed))));
            var cam = Camera.main != null ? Camera.main.GetComponent<GameCamera>() : null;
            if (cam != null) cam.AddTrauma(hardLandingTrauma);
        }

        // ---------------------------------------------------------------- sputter

        void BuildSputterSmoke()
        {
            _sputterSmoke = FxAssets.System(transform, "SputterSmoke", new Vector3(0f, -1.4f, 0f), FxAssets.Alpha);
            _sputterSmoke.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var m = _sputterSmoke.main;
            m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            m.maxParticles = 50;
            var sh = _sputterSmoke.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 25f;
            FxAssets.ColorOverLife(_sputterSmoke, FxAssets.Grad((0f, new Color(0.12f, 0.1f, 0.1f, 0.7f)), (1f, new Color(0.2f, 0.18f, 0.17f, 0f))));
            FxAssets.SizeOverLife(_sputterSmoke, new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f));
        }

        void Update()
        {
            if (_dust == null) return;
            float throttle = _resolved ? 0f : _rocket.Throttle;
            var speed = _rocket.Body.linearVelocity.magnitude;

            _trail.emitting = !_resolved && (throttle > 0.05f || speed > 2.5f);
            _trail.time = Mathf.Lerp(0.3f, 1.4f, Mathf.Clamp01(speed / 14f));

            // Dust where the exhaust hits the ground.
            float rate = 0f;
            var nozzle = transform.TransformPoint(new Vector3(0f, -1.4f, 0f));
            var exhaust = -transform.up;
            if (throttle > 0.02f && Physics.Raycast(nozzle, exhaust, out var hit, dustRange, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody != _rocket.Body)
            {
                float near = 1f - hit.distance / dustRange;
                rate = dustRate * throttle * near * near;
                _dust.transform.position = hit.point + hit.normal * 0.05f;
                _dust.transform.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
            }
            var e = _dust.emission;
            e.rateOverTime = rate;

            UpdateSputter();
        }

        /// <summary>Low or empty tank: the engine coughs (flame blanks for a few frames + black puff).</summary>
        void UpdateSputter()
        {
            if (_resolved || _tank == null) return;
            bool low = _tank.Ratio < _tank.lowFuelRatio;
            bool wantsThrust = GameInput.ThrustValue > 0.1f && _rocket.ControlsEnabled;
            if (!low || !wantsThrust)
            {
                _sputterTimer = Random.Range(sputterInterval.x, sputterInterval.y);
                return;
            }
            _sputterTimer -= Time.deltaTime;
            if (_sputterTimer > 0f) return;
            _sputterTimer = _tank.IsEmpty ? Random.Range(0.25f, 0.6f) : Random.Range(sputterInterval.x, sputterInterval.y);
            if (_flame != null) _flame.Cough(Random.Range(sputterFrames.x, sputterFrames.y + 1));
            _sputterSmoke.Emit(_tank.IsEmpty ? 10 : 6);
        }
    }
}
