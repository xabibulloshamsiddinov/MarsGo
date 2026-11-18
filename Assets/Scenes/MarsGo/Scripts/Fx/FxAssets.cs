using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsGo
{
    /// <summary>
    /// Particle materials + helpers to build ParticleSystems from code. The editor setup saves
    /// ParticleAdd / ParticleAlpha (URP Particles/Unlit + soft-dot texture) under Resources so builds
    /// include the shader; at runtime they are loaded from there or created on the fly.
    /// </summary>
    public static class FxAssets
    {
        public const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        public const string AdditiveName = "ParticleAdd";
        public const string AlphaName = "ParticleAlpha";
        public const string SoftDotName = "SoftDot";

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Texture2D _softDot;

        public static Material Additive => Get(AdditiveName, true);
        public static Material Alpha => Get(AlphaName, false);

        /// <summary>Own copy, for effects that animate their material colour.</summary>
        public static Material AdditiveInstance() => new Material(Additive);
        public static Material AlphaInstance() => new Material(Alpha);

        static Material Get(string name, bool additive)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>($"{MarsMaterials.ResourceFolder}/{name}");
            if (m == null)
            {
                m = new Material(Shader.Find(ParticleShader)) { name = name };
                Configure(m, additive, SoftDot);
            }
            Cache[name] = m;
            return m;
        }

        public static void Configure(Material m, bool additive, Texture texture)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            if (texture != null) m.SetTexture("_BaseMap", texture);
        }

        /// <summary>64×64 white dot with a soft radial alpha falloff.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                _softDot = MakeSoftDot(64);
                return _softDot;
            }
        }

        public static Texture2D MakeSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = SoftDotName };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ----------------------------------------------------------------- builders

        public static ParticleSystem System(Transform parent, string name, Vector3 localPos, Material mat, bool worldSpace = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        public static Gradient Grad(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
                ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        public static void ColorOverLife(ParticleSystem ps, Gradient g)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static void SizeOverLife(ParticleSystem ps, params Keyframe[] keys)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        public static void Burst(ParticleSystem ps, int count)
        {
            var e = ps.emission;
            e.rateOverTime = 0f;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        /// <summary>Runs a one-shot system and destroys its GameObject when finished.</summary>
        public static void PlayOnce(ParticleSystem ps, float lifetime)
        {
            var main = ps.main;
            main.loop = false;
            ps.Play();
            Object.Destroy(ps.gameObject, lifetime);
        }

        /// <summary>Cone mesh pointing down -Y from the origin, vertex colours fading to the tip.</summary>
        public static Mesh FlameMesh(float radius, float length, Color baseColor, Color tipColor, int segments = 16)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            int rings = 6;
            for (int r = 0; r <= rings; r++)
            {
                float f = r / (float)rings;
                float rad = radius * (1f - f * f) * (1f - 0.15f * f);
                var col = Color.Lerp(baseColor, tipColor, f);
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    v.Add(new Vector3(Mathf.Sin(a) * rad, -f * length, Mathf.Cos(a) * rad));
                    c.Add(col);
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s, b = a + stride;
                    // Double-sided so it reads from any angle.
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh { name = "Flame" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
