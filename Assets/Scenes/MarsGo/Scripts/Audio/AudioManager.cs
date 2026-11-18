using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Plays named sounds from the SfxLibrary through a pool of AudioSources (port of
    /// audio_manager.gd). Positions are panned left/right relative to the camera.
    /// Bus volumes (0..1) are the settings knobs (Phase 4 exposes them in the menu).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public const string ResourceRoot = "MarsGo/Audio/";

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 1f;
        [Range(0f, 1f)] public float ambienceVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.8f;
        [Tooltip("Metres from the camera centre for full left/right pan.")]
        public float panWidth = 16f;
        public int poolSize = 24;

        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        int _next;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < poolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool.Add(src);
            }
        }

        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        public float BusVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.UI: return masterVolume * uiVolume;
                case AudioBus.Ambience: return masterVolume * ambienceVolume;
                case AudioBus.Music: return masterVolume * musicVolume;
                default: return masterVolume * sfxVolume;
            }
        }

        public AudioClip Clip(string path)
        {
            if (_clips.TryGetValue(path, out var c)) return c;
            c = Resources.Load<AudioClip>(ResourceRoot + path);
            if (c == null) Debug.LogWarning("AudioManager: missing clip " + path);
            _clips[path] = c;
            return c;
        }

        /// <summary>Random variant, never the same one twice in a row.</summary>
        public AudioClip PickClip(SfxLibrary.Entry e)
        {
            int n = e.Clips.Length;
            int idx = Random.Range(0, n);
            if (n > 1 && _lastVariant.TryGetValue(e.Id, out int last) && idx == last) idx = (idx + 1) % n;
            _lastVariant[e.Id] = idx;
            return Clip(e.Clips[idx]);
        }

        /// <summary>
        /// Plays a library sound once. position null = centred. pitchVariation caps at the entry's.
        /// </summary>
        public AudioSource Play(string id, Vector3? position = null, float pitchVariation = 0f, float extraDb = 0f)
        {
            var e = SfxLibrary.Get(id);
            if (e == null)
            {
                Debug.LogWarning("AudioManager: unknown sfx " + id);
                return null;
            }
            var clip = PickClip(e);
            if (clip == null) return null;
            var src = NextSource();
            float pv = Mathf.Min(pitchVariation, e.PitchVariation);
            src.clip = clip;
            src.loop = false;
            src.pitch = 1f + Random.Range(-pv, pv);
            src.volume = DbToLinear(e.VolumeDb + extraDb) * BusVolume(e.Bus);
            src.panStereo = position.HasValue ? Pan(position.Value) : 0f;
            src.ignoreListenerPause = e.Bus == AudioBus.UI;
            src.Play();
            return src;
        }

        public float Pan(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            return Mathf.Clamp((world.x - cam.transform.position.x) / panWidth, -0.8f, 0.8f);
        }

        /// <summary>A dedicated looping source (engine, laser hum, wind) owned by the caller.</summary>
        public AudioSource CreateLoop(string id, GameObject owner)
        {
            var e = SfxLibrary.Get(id);
            var src = owner.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.clip = e != null ? PickClip(e) : null;
            src.volume = 0f;
            return src;
        }

        AudioSource NextSource()
        {
            // Prefer a free source; otherwise steal the oldest (round robin).
            for (int i = 0; i < _pool.Count; i++)
            {
                var s = _pool[(_next + i) % _pool.Count];
                if (!s.isPlaying)
                {
                    _next = (_next + i + 1) % _pool.Count;
                    return s;
                }
            }
            var steal = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            steal.Stop();
            return steal;
        }

        public void StopAll()
        {
            foreach (var s in _pool) s.Stop();
        }
    }
}
