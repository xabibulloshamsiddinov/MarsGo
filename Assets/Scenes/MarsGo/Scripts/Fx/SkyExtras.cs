using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Ambient life around the camera (ports of ambient_dust.gd and meteor_spawner.gd):
    /// slow drifting dust motes in front of the level, and an occasional meteor streaking far
    /// behind it (with a quiet whoosh). Purely visual; lives on the main camera.
    /// </summary>
    public class SkyExtras : MonoBehaviour
    {
        [Header("Dust motes")]
        public int dustCount = 70;
        public Vector3 dustBox = new Vector3(60f, 34f, 22f);
        public float dustDrift = 0.6f;

        [Header("Meteors")]
        public Vector2 meteorInterval = new Vector2(10f, 28f);
        public Vector2 firstMeteorDelay = new Vector2(3f, 10f);
        public float meteorPlaneZ = 180f;
        public Vector2 meteorHeight = new Vector2(55f, 95f);
        public Vector2 meteorSpeed = new Vector2(70f, 120f);
        public Vector2 meteorDive = new Vector2(12f, 35f);
        public float meteorFlightTime = 1.1f;

        ParticleSystem _dust;
        Transform _dustRoot;
        float _meteorTimer;

        void Start()
        {
            _dustRoot = new GameObject("AmbientDust").transform;
            _dust = FxAssets.System(_dustRoot, "Motes", Vector3.zero, FxAssets.Alpha);
            var m = _dust.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            m.startSpeed = 0f;
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            m.maxParticles = dustCount * 2;
            m.startColor = new Color(0.95f, 0.78f, 0.62f, 0.55f);
            var sh = _dust.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = dustBox;
            var e = _dust.emission;
            e.rateOverTime = dustCount / 9f;
            var vel = _dust.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(dustDrift * 0.5f, dustDrift * 1.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var noise = _dust.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.25f;
            FxAssets.ColorOverLife(_dust, FxAssets.Grad((0f, new Color(1f, 1f, 1f, 0f)), (0.2f, Color.white), (0.8f, Color.white), (1f, new Color(1f, 1f, 1f, 0f))));
            _dust.Simulate(9f, true, true);   // start full, not empty
            _dust.Play();

            _meteorTimer = Random.Range(firstMeteorDelay.x, firstMeteorDelay.y);
        }

        void LateUpdate()
        {
            // The dust box follows the camera's view of the flight plane.
            var p = transform.position;
            _dustRoot.position = new Vector3(p.x, p.y, 4f);

            _meteorTimer -= Time.deltaTime;
            if (_meteorTimer > 0f) return;
            _meteorTimer = Random.Range(meteorInterval.x, meteorInterval.y);
            LaunchMeteor();
        }

        void LaunchMeteor()
        {
            var cam = transform.position;
            float side = Random.value < 0.5f ? -1f : 1f;
            float dive = Random.Range(meteorDive.x, meteorDive.y) * Mathf.Deg2Rad;
            var dir = new Vector3(-side * Mathf.Cos(dive), -Mathf.Sin(dive), 0f);
            float speed = Random.Range(meteorSpeed.x, meteorSpeed.y);
            var start = new Vector3(cam.x + side * Random.Range(30f, 90f), cam.y + Random.Range(meteorHeight.x, meteorHeight.y), meteorPlaneZ);

            var go = new GameObject("Meteor");
            go.transform.position = start;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FxAssets.Additive;
            trail.time = 0.35f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0f));
            trail.colorGradient = FxAssets.Grad((0f, new Color(1f, 0.95f, 0.85f, 1f)), (0.3f, new Color(1f, 0.6f, 0.3f, 0.8f)), (1f, new Color(1f, 0.3f, 0.1f, 0f)));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeteorMotion>().Init(dir * speed, meteorFlightTime);
            if (AudioManager.Instance != null) AudioManager.Instance.Play("meteor_whoosh", start, 0.1f, -8f);
        }
    }

    public class MeteorMotion : MonoBehaviour
    {
        Vector3 _velocity;
        float _life, _t;
        TrailRenderer _trail;

        public void Init(Vector3 velocity, float life)
        {
            _velocity = velocity;
            _life = life;
            _trail = GetComponent<TrailRenderer>();
        }

        void Update()
        {
            _t += Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            // Burn out: the head shrinks over the last third.
            float k = Mathf.Clamp01((_life - _t) / (_life * 0.35f));
            _trail.widthMultiplier = k;
            if (_t > _life + _trail.time) Destroy(gameObject);
        }
    }
}
