using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Every material the game uses, defined once. The editor setup saves them as assets under
    /// Resources/MarsGo/Materials (so builds include the shader variants); at runtime they are
    /// loaded from there, or created on the fly if missing.
    /// </summary>
    public static class MarsMaterials
    {
        public struct Spec
        {
            public string Name;
            public Color Color;
            public float Metallic;
            public float Smoothness;
            public Color Emission;   // HDR (already multiplied by intensity); black = none
            /// <summary>Alpha-blended (Color.a is the opacity).</summary>
            public bool Transparent;

            public Spec(string name, Color color, float metallic, float smoothness, Color? emission = null, bool transparent = false)
            {
                Name = name;
                Color = color;
                Metallic = metallic;
                Smoothness = smoothness;
                Emission = emission ?? Color.black;
                Transparent = transparent;
            }
        }

        public const string ResourceFolder = "MarsGo/Materials";
        public const string LitShader = "Universal Render Pipeline/Lit";

        public static readonly Spec[] Specs =
        {
            new Spec("Ground", Palette.Ground, 0f, 0.12f),
            new Spec("Rock", Palette.Rock, 0f, 0.15f),
            new Spec("Basalt", Palette.Hex("#5E3428"), 0.05f, 0.2f),
            new Spec("Scenery", Palette.Dust, 0f, 0.08f),
            new Spec("HazardFloor", Palette.Hex("#5A1010"), 0f, 0.4f, Palette.Hazard * 1.6f),
            new Spec("TowerBody", Palette.Hex("#3A2228"), 0.4f, 0.4f),
            new Spec("Beacon", Palette.Hex("#801C2B"), 0f, 0.5f, Palette.Hazard * 4f),
            new Spec("PadMetal", Palette.Hex("#8C8F99"), 0.7f, 0.55f),
            new Spec("PadLightSafe", Palette.Safe, 0f, 0.5f, Palette.Safe * 4f),
            new Spec("PadLightAccent", Palette.Accent, 0f, 0.5f, Palette.Accent * 4f),
            new Spec("PadMarking", Palette.Hex("#F2F2F2"), 0f, 0.3f, Palette.Safe * 1.2f),
            new Spec("Hull", new Color(0.86f, 0.87f, 0.9f), 0.6f, 0.6f),
            new Spec("Accent", Palette.Hazard, 0.3f, 0.5f),
            new Spec("Trim", new Color(0.22f, 0.22f, 0.27f), 0.8f, 0.65f),
            new Spec("Window", Palette.Accent, 0f, 0.9f, Palette.Accent * 2.5f),
            new Spec("Nozzle", new Color(0.25f, 0.08f, 0.05f), 0.5f, 0.5f, new Color(0.01f, 0.01f, 0.01f)),
            new Spec("NavLeft", Palette.Hazard, 0f, 0.5f, Palette.Hazard * 5f),
            new Spec("NavRight", Palette.Safe, 0f, 0.5f, Palette.Safe * 5f),

            // Obstacles (Phase 2)
            new Spec("HazardBlock", Palette.Hex("#5C1218"), 0.3f, 0.45f, Palette.Hazard * 0.35f),
            new Spec("HazardStripe", Palette.Hex("#FFC94D"), 0f, 0.4f, Palette.Hex("#FFC94D") * 0.6f),
            new Spec("LaneFill", new Color(1f, 0.2f, 0.33f, 0.10f), 0f, 0f, Palette.Hazard * 0.25f, true),
            new Spec("LaneBorder", Palette.Hazard, 0f, 0.3f, Palette.Hazard * 1.5f),
            new Spec("FuelGlass", Palette.Safe, 0.1f, 0.85f, Palette.Safe * 2.2f),
            new Spec("FuelCap", Palette.Hex("#C8CCD6"), 0.85f, 0.7f),
            new Spec("FuelHalo", new Color(0.22f, 0.95f, 0.63f, 0.16f), 0f, 0f, Palette.Safe * 0.8f, true),
            new Spec("LaserHousing", Palette.Hex("#2E2A30"), 0.7f, 0.5f),
            new Spec("LaserLens", Palette.Hex("#661018"), 0f, 0.8f, Palette.Hazard * 0.3f),
            new Spec("LaserBeam", Palette.Hazard, 0f, 0.5f, Palette.Hazard * 8f),
            new Spec("LaserHalo", new Color(1f, 0.2f, 0.33f, 0.18f), 0f, 0f, Palette.Hazard * 2f, true),
            new Spec("BladeMetal", Palette.Hex("#B8BCC6"), 0.9f, 0.75f),
            new Spec("BladeEdge", Palette.Hazard, 0.2f, 0.5f, Palette.Hazard * 1.2f),
            new Spec("Hub", Palette.Hex("#3A3640"), 0.8f, 0.6f),
            new Spec("ReachRing", Palette.Hazard, 0f, 0.3f, Palette.Hazard * 0.6f),
            new Spec("WindStreak", new Color(1f, 0.95f, 0.9f, 0.5f), 0f, 0f, new Color(0.9f, 0.85f, 0.8f) * 0.8f, true),
            new Spec("StarGold", Palette.Hex("#FFD95A"), 0.6f, 0.7f, Palette.Hex("#FFC531") * 3f),
            new Spec("StarHalo", new Color(1f, 0.85f, 0.35f, 0.25f), 0f, 0f, Palette.Hex("#FFC531") * 1.5f, true),
            new Spec("WindFill", new Color(0.95f, 0.85f, 0.75f, 0.06f), 0f, 0f, null, true),
        };

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Get(string name)
        {
            if (Cache.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>($"{ResourceFolder}/{name}");
            if (m == null)
            {
                foreach (var s in Specs)
                    if (s.Name == name) { m = Create(s); break; }
            }
            if (m == null)
            {
                Debug.LogError($"MarsMaterials: unknown material '{name}'");
                m = Create(new Spec(name, Color.magenta, 0f, 0.5f));
            }
            Cache[name] = m;
            return m;
        }

        /// <summary>Own copy, for materials animated per object (blinking lights, nozzle glow).</summary>
        public static Material Instance(string name) => new Material(Get(name));

        public static Material Create(Spec s)
        {
            var m = new Material(Shader.Find(LitShader)) { name = s.Name };
            Apply(m, s);
            return m;
        }

        public static void Apply(Material m, Spec s)
        {
            m.SetColor("_BaseColor", s.Color);
            m.SetFloat("_Metallic", s.Metallic);
            m.SetFloat("_Smoothness", s.Smoothness);
            if (s.Transparent) MakeTransparent(m);
            if (s.Emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", s.Emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        public static void SetEmission(Material m, Color hdr) => m.SetColor("_EmissionColor", hdr);

        public static void SetAlpha(Material m, float alpha)
        {
            var c = m.GetColor("_BaseColor");
            c.a = alpha;
            m.SetColor("_BaseColor", c);
        }

        /// <summary>URP Lit: switch to alpha-blended transparent surface.</summary>
        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_ReceiveShadows", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
