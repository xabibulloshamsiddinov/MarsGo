using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Engine exhaust (port of engine_flame.gd): glowing cone + fire particles + dusty smoke +
    /// sparks, all scaled by the throttle with a fast rise and a slower fall.
    /// FuelSputterFx can blank it for a few frames (Cough).
    /// </summary>
    public class EngineFlame : MonoBehaviour
    {
        public Vector3 nozzleOffset = new Vector3(0f, -1.33f, 0f);
        public float responseUp = 18f;
        public float responseDown = 5f;
        public float visibleThreshold = 0.01f;
        public float sizeAtIdle = 0.45f;
        public float sizeAtFull = 1f;
        public float flameLength = 2.2f;
        public float fireRate = 140f;
        public float smokeRate = 28f;
        public float sparkRate = 18f;

        public float Level { get; private set; }

        Rocket _rocket;
        Transform _root, _cone, _core;
        ParticleSystem _fire, _smoke, _sparks;
        float _target;
        int _coughFrames;

        void Start()
        {
            _rocket = GetComponent<Rocket>();
            _rocket.ThrustChanged += a => _target = a;

            _root = new GameObject("EngineFlame").transform;
            _root.SetParent(transform, false);
            _root.localPosition = nozzleOffset;

            var coneMesh = FxAssets.FlameMesh(0.32f, 1f, new Color(1f, 0.75f, 0.35f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f));
            var coreMesh = FxAssets.FlameMesh(0.17f, 1f, new Color(1f, 0.97f, 0.9f, 1f), new Color(1f, 0.7f, 0.3f, 0f));
            _cone = MeshFactory.Part(_root, "Cone", coneMesh, FxAssets.Additive, Vector3.zero, Quaternion.identity).transform;
            _core = MeshFactory.Part(_root, "Core", coreMesh, FxAssets.Additive, Vector3.zero, Quaternion.identity).transform;
            foreach (var r in _root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var down = Quaternion.Euler(90f, 0f, 0f);   // particle cones emit along +Z → point it down

            _fire = FxAssets.System(_root, "Fire", Vector3.zero, FxAssets.Additive);
            _fire.transform.localRotation = down;
            var m = _fire.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.maxParticles = 300;
            var sh = _fire.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 7f;
            sh.radius = 0.18f;
            FxAssets.ColorOverLife(_fire, FxAssets.Grad((0f, new Color(1f, 0.95f, 0.8f, 1f)), (0.35f, new Color(1f, 0.55f, 0.15f, 0.9f)), (1f, new Color(0.8f, 0.15f, 0.05f, 0f))));
            FxAssets.SizeOverLife(_fire, new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f));
            _fire.Play();

            _smoke = FxAssets.System(_root, "Smoke", new Vector3(0f, -0.6f, 0f), FxAssets.Alpha);
            _smoke.transform.localRotation = down;
            m = _smoke.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.05f;
            m.maxParticles = 200;
            sh = _smoke.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.25f;
            var lv = _smoke.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 1.2f;
            lv.dampen = 0.15f;
            FxAssets.ColorOverLife(_smoke, FxAssets.Grad((0f, new Color(0.85f, 0.7f, 0.6f, 0f)), (0.15f, new Color(0.8f, 0.62f, 0.5f, 0.35f)), (1f, new Color(0.7f, 0.5f, 0.4f, 0f))));
            FxAssets.SizeOverLife(_smoke, new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f));
            _smoke.Play();

            _sparks = FxAssets.System(_root, "Sparks", Vector3.zero, FxAssets.Additive);
            _sparks.transform.localRotation = down;
            m = _sparks.main;
            m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 15f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            m.startColor = new Color(1f, 0.8f, 0.4f);
            m.gravityModifier = 0.6f;
            m.maxParticles = 80;
            sh = _sparks.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 0.12f;
            var pr = _sparks.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.04f;
            pr.lengthScale = 1.5f;
            _sparks.Play();

            Apply(0f);
        }

        /// <summary>Blank the flame for a few frames (engine cough).</summary>
        public void Cough(int frames) => _coughFrames = Mathf.Max(_coughFrames, frames);

        void Update()
        {
            if (_root == null) return;
            float dt = Time.deltaTime;
            float speed = _target > Level ? responseUp : responseDown;
            Level = Mathf.Lerp(Level, _target, 1f - Mathf.Exp(-speed * dt));
            float shown = Level;
            if (_coughFrames > 0)
            {
                _coughFrames--;
                shown = 0f;
            }
            Apply(shown);
        }

        void Apply(float level)
        {
            bool on = level > visibleThreshold;
            _cone.gameObject.SetActive(on);
            _core.gameObject.SetActive(on);
            float flicker = 1f + 0.12f * (Mathf.PerlinNoise(Time.time * 30f, 0.5f) - 0.5f) * 2f;
            float size = Mathf.Lerp(sizeAtIdle, sizeAtFull, level);
            _cone.localScale = new Vector3(size, flameLength * level * flicker, size);
            _core.localScale = new Vector3(size, flameLength * 0.55f * level * flicker, size);

            var e = _fire.emission;
            e.rateOverTime = on ? fireRate * level : 0f;
            e = _smoke.emission;
            e.rateOverTime = on ? smokeRate * Mathf.Max(level, 0.25f) : 0f;
            e = _sparks.emission;
            e.rateOverTime = on ? sparkRate * level * level : 0f;
        }

        /// <summary>Crash: everything stops at once.</summary>
        public void Kill()
        {
            _target = 0f;
            Level = 0f;
            if (_root == null) return;
            Apply(0f);
            _fire.Clear();
        }
    }
}
