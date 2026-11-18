using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Single owner of Time.timeScale: pause > hit-stop > slow motion. Everybody asks this class
    /// instead of writing timeScale, so effects never fight (and never leave the game frozen).
    /// </summary>
    public static class TimeControl
    {
        public const float HitStopScale = 0.05f;

        public static bool Paused;
        /// <summary>1 = normal speed. LandingCelebration lowers it briefly.</summary>
        public static float SlowMo = 1f;

        static float _hitStopUntil;

        public static bool HitStopActive => Time.unscaledTime < _hitStopUntil;

        public static void HitStop(float seconds)
        {
            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds);
            Apply();
        }

        /// <summary>Called every frame by the GameManager.</summary>
        public static void Apply()
        {
            Time.timeScale = Paused ? 0f : HitStopActive ? HitStopScale : Mathf.Clamp(SlowMo, 0.05f, 1f);
        }

        public static void Reset()
        {
            Paused = false;
            SlowMo = 1f;
            _hitStopUntil = 0f;
            Apply();
        }
    }
}
