using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Reward after a safe landing (port of landing_celebration.gd). Timeline (real time):
    ///   0.00  slow motion starts (eases back to normal by ~1.2 s), pad glows green
    ///   0.05  confetti bursts from both sides of the pad, a light ring rises around the rocket
    ///   0.60  stars pop in one after another above the rocket (0.35 s apart)
    ///   ~2.6  sequence finished → results screen
    /// </summary>
    public class LandingCelebration : MonoBehaviour
    {
        /// <summary>A star appeared (0-based index) — audio plays star_pop_N.</summary>
        public static event Action<int> StarShown;
        public static event Action SequenceFinished;

        public float slowMoScale = 0.35f;
        public float slowMoRecover = 1.2f;
        public int confettiPerSide = 80;
        public float ringRiseHeight = 3.5f;
        public float ringTime = 1.1f;
        public float firstStarDelay = 0.6f;
        public float starInterval = 0.35f;
        public float starHeight = 3.6f;
        public float starSpacing = 1.3f;
        public float totalTime = 2.6f;

        public static float Duration = 2.6f;

        static readonly Color[] ConfettiColors =
        {
            Palette.Safe, Palette.Accent, Palette.UI, Palette.Hex("#FFD95A"), Palette.Hex("#FF6BD6"), Color.white,
        };

        public static void Play(Transform parent, Pad pad, Rocket rocket, int stars)
        {
            var go = new GameObject("LandingCelebration");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<LandingCelebration>();
            Duration = c.totalTime;
            c.StartCoroutine(c.Run(pad, rocket, stars));
        }

        IEnumerator Run(Pad pad, Rocket rocket, int stars)
        {
            var top = new Vector3(pad.transform.position.x, pad.TopY, 0f);
            float t = 0f;
            var cam = Camera.main != null ? Camera.main.GetComponent<GameCamera>() : null;

            Confetti(top + new Vector3(-pad.size.x * 0.5f, 0.1f, -0.5f), 1f);
            Confetti(top + new Vector3(pad.size.x * 0.5f, 0.1f, -0.5f), -1f);
            var ringMat = FxAssets.AdditiveInstance();
            ringMat.SetTexture("_BaseMap", null);
            var ring = MeshFactory.Part(transform, "LightRing", MeshFactory.Torus(1.25f, 1.45f, 48, 8), ringMat, top, Quaternion.identity);
            ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var starObjs = new List<Transform>();
            var starPops = new List<float>();
            int shown = 0;
            var starMesh = StarMesh(0.55f, 0.24f, 0.12f);

            while (t < totalTime)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;

                // Slow motion, unless a hit-stop owns time.
                TimeControl.SlowMo = Mathf.Lerp(slowMoScale, 1f, Mathf.SmoothStep(0f, 1f, t / slowMoRecover));

                // Rising light ring.
                float rt = Mathf.Clamp01(t / ringTime);
                ring.transform.position = top + Vector3.up * (ringRiseHeight * (1f - (1f - rt) * (1f - rt)));
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.6f, rt);
                ringMat.SetColor("_BaseColor", new Color(Palette.Safe.r, Palette.Safe.g, Palette.Safe.b, (1f - rt) * 0.9f));

                // Stars.
                var anchor = rocket != null ? rocket.transform.position : top;
                while (shown < stars && t >= firstStarDelay + shown * starInterval)
                {
                    var star = MeshFactory.Part(transform, "Star" + shown, starMesh, MarsMaterials.Get("StarGold"), Vector3.zero, Quaternion.identity).transform;
                    var halo = MeshFactory.Part(star, "Halo", MeshFactory.Sphere, MarsMaterials.Get("StarHalo"), Vector3.zero, Quaternion.identity, Vector3.one * 1.4f);
                    halo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    starObjs.Add(star);
                    starPops.Add(t);
                    StarShown?.Invoke(shown);
                    if (cam != null) cam.AddTrauma(0.08f);
                    shown++;
                }
                for (int i = 0; i < starObjs.Count; i++)
                {
                    float age = t - starPops[i];
                    float x = (i - (stars - 1) * 0.5f) * starSpacing;
                    starObjs[i].position = anchor + new Vector3(x, starHeight + 0.15f * Mathf.Sin(t * 3f + i), -1f);
                    starObjs[i].rotation = Quaternion.Euler(0f, 0f, 0f) * Quaternion.Euler(0f, Mathf.Sin(age * 4f) * 25f, 0f);
                    starObjs[i].localScale = Vector3.one * BackOut(Mathf.Clamp01(age / 0.35f));
                }
                yield return null;
            }
            TimeControl.SlowMo = 1f;
            Destroy(ring);
            SequenceFinished?.Invoke();
        }

        void Confetti(Vector3 pos, float side)
        {
            var ps = FxAssets.System(transform, "Confetti", Vector3.zero, FxAssets.Alpha);
            ps.transform.position = pos;
            ps.transform.rotation = Quaternion.LookRotation(new Vector3(side * 0.55f, 1f, 0f).normalized);
            var m = ps.main;
            m.useUnscaledTime = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0.9f;
            var grad = new Gradient();
            var keys = new GradientColorKey[ConfettiColors.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(ConfettiColors[i], i / (float)(keys.Length - 1));
            grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            grad.mode = GradientMode.Fixed;
            m.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 22f;
            sh.radius = 0.2f;
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 2.5f;
            lv.dampen = 0.08f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            FxAssets.ColorOverLife(ps, FxAssets.Grad((0f, Color.white), (0.8f, Color.white), (1f, new Color(1f, 1f, 1f, 0f))));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ConfettiMaterial;
            FxAssets.Burst(ps, confettiPerSide);
            ps.Play();
        }

        static Material _confetti;

        /// <summary>Alpha particles with a square (not round) texture: paper bits.</summary>
        static Material ConfettiMaterial
        {
            get
            {
                if (_confetti != null) return _confetti;
                _confetti = FxAssets.AlphaInstance();
                _confetti.SetTexture("_BaseMap", null);
                return _confetti;
            }
        }

        /// <summary>Flat 5-point star facing the camera (-Z), with a little thickness.</summary>
        static Mesh StarMesh(float outer, float inner, float depth)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            var ring = new List<Vector3>();
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                ring.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            var front = new Vector3(0f, 0f, -depth * 0.5f);
            var back = new Vector3(0f, 0f, depth * 0.5f);
            int cf = v.Count; v.Add(front * 1.6f);
            foreach (var p in ring) v.Add(p + front);
            int cb = v.Count; v.Add(back * 1.6f);
            foreach (var p in ring) v.Add(p + back);
            for (int i = 0; i < 10; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % 10;
                t.AddRange(new[] { cf, b, a });
                t.AddRange(new[] { cb, cb + a, cb + b });
                t.AddRange(new[] { a, b, cb + b, a, cb + b, cb + a });
            }
            var mesh = new Mesh { name = "Star" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
