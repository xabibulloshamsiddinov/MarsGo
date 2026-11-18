using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Phase 1 HUD drawn with IMGUI: telemetry, level intro, crash and results panels.
    /// Replaced by the real uGUI screens in Phase 4.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        public float introSeconds = 4f;
        public float crashPanelDelay = 1.3f;

        GUIStyle _label, _big, _small, _panel;
        Texture2D _white;

        void EnsureStyles(float scale)
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * scale), richText = true };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(20 * scale) };
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(54 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _panel = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Current == null) return;
            float s = Screen.height / 1080f;
            EnsureStyles(s);

            DrawTelemetry(gm, s);

            switch (gm.State)
            {
                case GameState.Playing:
                    if (gm.StateTime < introSeconds && gm.LevelTime < introSeconds) DrawIntro(gm, s);
                    break;
                case GameState.Paused:
                    DrawPanel(s, "PAUSED", "Esc / Start — resume     R / Y — restart", Palette.UI);
                    break;
                case GameState.Crashed:
                    // Let the explosion play first.
                    if (gm.StateTime >= crashPanelDelay) DrawPanel(s, "CRASHED", CrashText(gm.LastCrash) + "\n\nEnter / A — try again", Palette.Hazard);
                    break;
                case GameState.Landed:
                    if (gm.StateTime >= LandingCelebration.Duration) DrawResults(gm, s);
                    break;
            }

            GUI.Label(new Rect(20 * s, Screen.height - 44 * s, Screen.width, 40 * s),
                "<color=#ffffffaa>Thrust: Space/W/↑/A·RT     Rotate: A/D/←/→/stick     Pause: Esc     Restart: R     Levels: 1–5</color>", _small);
        }

        void DrawTelemetry(GameManager gm, float s)
        {
            var judge = gm.Current.Judge;
            var tank = gm.Current.Tank;
            var v = gm.Current.Rocket.Body.linearVelocity;
            float tilt = judge.TiltDegrees;

            float x = 24 * s, y = 20 * s, w = 340 * s;
            GUI.Label(new Rect(x, y, Screen.width, 40 * s), $"<b>{gm.LevelIndex + 1}. {gm.Level.Name}</b>", _label);
            y += 42 * s;

            // Fuel bar
            var bar = new Rect(x, y + 6 * s, w, 22 * s);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.45f));
            var fuelColor = tank.Ratio < tank.lowFuelRatio ? Palette.Hazard : Palette.Accent;
            DrawRect(new Rect(bar.x, bar.y, bar.width * tank.Ratio, bar.height), fuelColor);
            GUI.Label(new Rect(x + w + 12 * s, y, 200 * s, 36 * s), $"FUEL {tank.Ratio * 100f:0}%", _small);
            y += 40 * s;

            string Col(bool ok) => ok ? "#38F2A0" : "#FF3355";
            bool speedOk = v.magnitude <= judge.safeLandingSpeed;
            bool tiltOk = tilt <= judge.safeLandingAngle;
            GUI.Label(new Rect(x, y, Screen.width, 36 * s), $"SPEED <color={Col(speedOk)}>{v.magnitude:0.0} m/s</color>   V {v.y:+0.0;-0.0} m/s", _small);
            y += 30 * s;
            GUI.Label(new Rect(x, y, Screen.width, 36 * s), $"TILT <color={Col(tiltOk)}>{tilt:0}°</color>", _small);
            y += 30 * s;
            GUI.Label(new Rect(x, y, Screen.width, 36 * s), $"TIME {gm.LevelTime:0.0} s   (par {gm.Level.ParTime:0} s)", _small);
        }

        void DrawIntro(GameManager gm, float s)
        {
            float a = Mathf.Clamp01((introSeconds - gm.LevelTime) / 1f);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80 * s), gm.Level.Name.ToUpperInvariant(), _big);
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.18f + 80 * s, Screen.width * 0.6f, 80 * s), gm.Level.Description, _panel);
            GUI.color = old;
        }

        void DrawResults(GameManager gm, float s)
        {
            var r = gm.LastResult;
            string stars = new string('★', r.Stars) + new string('☆', 3 - r.Stars);
            string body = $"<size={Mathf.RoundToInt(64 * s)}><color=#FFD95A>{stars}</color></size>\n" +
                          $"Time {r.Time:0.0} s   •   Fuel left {r.FuelRatio * 100f:0}%   •   Touchdown {r.LandingSpeed:0.0} m/s" +
                          (r.IsRecord ? "\n<color=#FFD95A>NEW RECORD!</color>" : "") +
                          (r.IsLastLevel ? "\n\nAll levels complete!  Enter / A — back to level 1" : "\n\nEnter / A — next level     R — retry");
            DrawPanel(s, "LANDED!", body, Palette.Safe);
        }

        void DrawPanel(float s, string title, string body, Color accent)
        {
            var rect = new Rect(Screen.width * 0.5f - 480 * s, Screen.height * 0.5f - 200 * s, 960 * s, 400 * s);
            DrawRect(rect, new Color(0.08f, 0.04f, 0.04f, 0.78f));
            DrawRect(new Rect(rect.x, rect.y, rect.width, 6 * s), accent);
            var titleStyle = new GUIStyle(_big);
            titleStyle.normal.textColor = accent;
            GUI.Label(new Rect(rect.x, rect.y + 20 * s, rect.width, 80 * s), title, titleStyle);
            GUI.Label(new Rect(rect.x + 40 * s, rect.y + 110 * s, rect.width - 80 * s, rect.height - 130 * s), body, _panel);
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        public static string CrashText(CrashInfo c)
        {
            switch (c.Reason)
            {
                case CrashReasons.TooFast:
                    return $"Too fast! You touched down at {c.ImpactSpeed:0.0} m/s (safe: {c.SafeSpeed:0.0} m/s or less).";
                case CrashReasons.BadAngle:
                    return $"Bad angle! You were tilted {c.TiltDegrees:0}° (safe: {c.SafeAngle:0}° or less).";
                case CrashReasons.Ground: return "You hit the ground. Land only on the green pad.";
                case CrashReasons.Hazard: return "You touched a hazard.";
                case CrashReasons.OutOfFuel: return "Out of fuel — stranded on Mars.";
                case CrashReasons.Laser: return "Cut by a laser gate.";
                case CrashReasons.Blade: return "Hit by a spinning blade.";
                case CrashReasons.Crushed: return "Crushed by a moving block.";
                default: return "You hit an obstacle.";
            }
        }
    }
}
