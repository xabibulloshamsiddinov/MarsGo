using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Engine loop following the throttle (port of engine_audio.gd): pitch 0.9 → 1.15, volume on a
    /// curve, ignition / shutdown / sputter one-shots. kill() fades it out and ignores throttle.
    /// </summary>
    public class EngineAudio : MonoBehaviour
    {
        public float minPitch = 0.9f;
        public float maxPitch = 1.15f;
        public float maxVolumeDb = -12f;
        public float volumeCurve = 1.3f;
        public float riseSpeed = 10f;
        public float fallSpeed = 5f;
        public float stopLevel = 0.005f;
        public float ignitionCooldown = 0.25f;
        public float minBurnForShutdown = 0.15f;

        public Transform target;

        AudioSource _loop;
        float _targetLevel, _level, _burnTime, _sinceIgnition = 1000f;
        bool _burning, _dead;

        void Start()
        {
            _loop = AudioManager.Instance.CreateLoop("engine_loop", gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _sinceIgnition += dt;
            if (_burning) _burnTime += dt;
            if (_loop == null) return;

            float speed = _targetLevel > _level ? riseSpeed : fallSpeed;
            _level = Mathf.Lerp(_level, _targetLevel, 1f - Mathf.Exp(-speed * dt));
            if (_level < stopLevel && _targetLevel <= 0f)
            {
                if (_loop.isPlaying) _loop.Stop();
                return;
            }
            if (!_loop.isPlaying) _loop.Play();
            _loop.pitch = Mathf.Lerp(minPitch, maxPitch, _level);
            _loop.volume = Mathf.Pow(_level, volumeCurve) * AudioManager.DbToLinear(maxVolumeDb)
                           * AudioManager.Instance.BusVolume(AudioBus.Sfx);
            if (target != null) _loop.panStereo = AudioManager.Instance.Pan(target.position) * 0.35f;
        }

        public void SetThrust(float amount)
        {
            if (_dead) return;
            _targetLevel = Mathf.Clamp01(amount);
        }

        public void Ignite()
        {
            if (_dead) return;
            _burning = true;
            _burnTime = 0f;
            if (_sinceIgnition < ignitionCooldown) return;
            _sinceIgnition = 0f;
            AudioManager.Instance.Play("engine_ignition", Pos, 0.08f);
        }

        public void ShutDown()
        {
            if (_dead || !_burning) return;
            _burning = false;
            if (_burnTime >= minBurnForShutdown) AudioManager.Instance.Play("engine_shutdown", Pos, 0.08f);
        }

        /// <summary>Out of fuel: cough, then silence.</summary>
        public void Sputter()
        {
            if (!_dead) AudioManager.Instance.Play("engine_sputter", Pos, 0.1f);
        }

        /// <summary>Crash / landing: the loop dies quickly and ignores further throttle.</summary>
        public void Kill(float fadeSpeed = 25f)
        {
            _dead = true;
            _burning = false;
            _targetLevel = 0f;
            fallSpeed = fadeSpeed;
        }

        Vector3? Pos => target != null ? target.position : (Vector3?)null;
    }
}
