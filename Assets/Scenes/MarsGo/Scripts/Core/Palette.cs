using UnityEngine;

namespace MarsGo
{
    /// <summary>Mars colour palette (the Godot palette re-themed for the red planet).</summary>
    public static class Palette
    {
        public static readonly Color SkyTop = Hex("#C9785A");
        public static readonly Color SkyHorizon = Hex("#E8B08A");
        public static readonly Color Ground = Hex("#A0472A");
        public static readonly Color Rock = Hex("#7A3522");
        public static readonly Color Basalt = Hex("#4A2A22");
        public static readonly Color Dust = Hex("#D9946A");
        public static readonly Color Hazard = Hex("#FF3355");
        public static readonly Color Safe = Hex("#38F2A0");
        public static readonly Color Accent = Hex("#FF9F43");
        public static readonly Color UI = Hex("#7FDBFF");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
