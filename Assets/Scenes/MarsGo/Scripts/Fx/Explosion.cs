using System.Collections;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Cinematic explosion (port of explosion.gd + crash_fx.gd): flash light, fireball, ground
    /// shockwave ring, bouncing sparks, black smoke column and rocket debris, plus camera trauma
    /// and a short hit-stop. CrashFx (on the rocket) hides the rocket and calls Explode().
    /// Everything cleans itself up.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        public float cameraTrauma = 0.8f;
        public float hitStopDuration = 0.08f;
        public float flashIntensity = 14f;
        public float flashTime = 0.12f;
        public float shockwaveRadius = 7f;
        public float shockwaveTime = 0.45f;
        public int fireballCount = 18;
        public int sparkCount = 50;
        public int smokeCount = 26;
        public int debrisCount = 8;
        public float lifetime = 6f;

        public static Explosion Spawn(Transform parent, Vector3 position, Vector3 velocity, Material[] debrisMaterials)
        {
            var go = new GameObject("Explosion");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var e = go.AddComponent<Explosion>();
            e.StartCoroutine(e.Run(velocity, debrisMaterials));
            return e;
        }

        IEnumerator Run(Vector3 velocity, Material[] debrisMaterials)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<GameCamera>() : null;
            if (cam != null) cam.AddTrauma(cameraTrauma);
            TimeControl.HitStop(hitStopDuration);

            var flash = new GameObject("Flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.85f, 0.6f);
            flash.range = 25f;
            flash.intensity = flashIntensity;

            Fireball();
            Sparks(velocity);
            Smoke();
            Debris(velocity, debrisMaterials);
            var ring = Shockwave();
            var ringMat = ring.GetComponent<MeshRenderer>().sharedMaterial;

            float t = 0f;
            while (t < Mathf.Max(flashTime, shockwaveTime))
            {
                t += Time.unscaledDeltaTime;
                flash.intensity = flashIntensity * Mathf.Clamp01(1f - t / flashTime);
                float s = Mathf.Clamp01(t / shockwaveTime);
                float eased = 1f - (1f - s) * (1f - s) * (1f - s);
                ring.localScale = Vector3.one * Mathf.Lerp(0.3f, shockwaveRadius, eased);
                ringMat.SetColor("_BaseColor", new Color(1f, 0.75f, 0.5f, 1f - s));
                yield return null;
            }
            Destroy(flash.gameObject);
            Destroy(ring.gameObject);
            Destroy(gameObject, lifetime);
        }

        void Fireball()
        {
            var ps = FxAssets.System(transform, "Fireball", Vector3.zero, FxAssets.Additive);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.15f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.6f;
            FxAssets.Burst(ps, fireballCount);
            FxAssets.ColorOverLife(ps, FxAssets.Grad((0f, new Color(1f, 1f, 0.9f, 1f)), (0.2f, new Color(1f, 0.7f, 0.25f, 1f)),
                (0.6f, new Color(0.9f, 0.25f, 0.05f, 0.7f)), (1f, new Color(0.2f, 0.05f, 0.02f, 0f))));
            FxAssets.SizeOverLife(ps, new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.7f));
            ps.Play();
        }

        void Sparks(Vector3 velocity)
        {
            var ps = FxAssets.System(transform, "Sparks", Vector3.zero, FxAssets.Additive);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.55f, 0.2f));
            m.gravityModifier = 1f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.3f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var inherit = ps.inheritVelocity;
            inherit.enabled = false;
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.4f;
            col.dampen = 0.3f;
            col.lifetimeLoss = 0.15f;
            col.quality = ParticleSystemCollisionQuality.Medium;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1f;
            FxAssets.Burst(ps, sparkCount);
            FxAssets.ColorOverLife(ps, FxAssets.Grad((0f, Color.white), (0.7f, new Color(1f, 0.6f, 0.2f, 1f)), (1f, new Color(1f, 0.3f, 0.1f, 0f))));
            ps.Play();
        }

        void Smoke()
        {
            var ps = FxAssets.System(transform, "Smoke", Vector3.zero, FxAssets.Alpha);
            var m = ps.main;
            m.startDelay = 0.08f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.12f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.8f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 1.2f;
            lv.dampen = 0.1f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            FxAssets.Burst(ps, smokeCount);
            FxAssets.ColorOverLife(ps, FxAssets.Grad((0f, new Color(0.3f, 0.2f, 0.15f, 0f)), (0.1f, new Color(0.16f, 0.12f, 0.1f, 0.75f)),
                (1f, new Color(0.35f, 0.28f, 0.24f, 0f))));
            FxAssets.SizeOverLife(ps, new Keyframe(0f, 0.6f), new Keyframe(1f, 2.6f));
            ps.Play();
        }

        Transform Shockwave()
        {
            var mat = FxAssets.AdditiveInstance();
            mat.SetTexture("_BaseMap", null);
            var go = MeshFactory.Part(transform, "Shockwave", MeshFactory.Torus(0.92f, 1f, 48, 6), mat, Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Sit on the ground under the blast if there is ground close by.
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                go.transform.position = hit.point + Vector3.up * 0.1f;
            return go.transform;
        }

        void Debris(Vector3 velocity, Material[] mats)
        {
            for (int i = 0; i < debrisCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Debris" + i;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Random.insideUnitSphere * 0.5f;
                go.transform.localRotation = Random.rotation;
                float s = Random.Range(0.15f, 0.45f);
                go.transform.localScale = new Vector3(s * Random.Range(0.6f, 1.6f), s * Random.Range(0.3f, 1f), s);
                if (mats != null && mats.Length > 0) go.GetComponent<MeshRenderer>().sharedMaterial = mats[i % mats.Length];
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.linearDamping = 0.3f;
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.4f;
                dir.z *= 0.35f;
                rb.linearVelocity = velocity * 0.3f + dir.normalized * Random.Range(5f, 11f);
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                go.AddComponent<DebrisFade>().delay = Random.Range(3f, 4.5f);
            }
        }
    }

    /// <summary>Shrinks a debris chunk away after a delay, then removes it.</summary>
    public class DebrisFade : MonoBehaviour
    {
        public float delay = 4f;
        public float shrinkTime = 0.6f;
        float _t;
        Vector3 _scale;

        void Start() => _scale = transform.localScale;

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < delay) return;
            float k = 1f - (_t - delay) / shrinkTime;
            if (k <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = _scale * k;
        }
    }

    /// <summary>On the rocket: crash → hide the rocket, stop it, and blow up (port of crash_fx.gd).</summary>
    public class CrashFx : MonoBehaviour
    {
        void Start()
        {
            GetComponent<LandingJudge>().Crashed += OnCrashed;
        }

        void OnCrashed(CrashInfo info)
        {
            var rocket = GetComponent<Rocket>();
            var velocity = rocket.Body.linearVelocity;
            var visuals = GetComponentInChildren<RocketVisuals>();
            var mats = visuals != null ? visuals.DebrisMaterials : null;
            if (visuals != null) visuals.gameObject.SetActive(false);
            var flame = GetComponent<EngineFlame>();
            if (flame != null) flame.Kill();
            rocket.Body.isKinematic = true;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            Explosion.Spawn(transform.parent, transform.position, velocity, mats);
        }
    }
}
