using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Adaptive music (port of music_director.gd):
    ///  * each level has an ambient + a tension layer started on the same DSP tick (never drift);
    ///  * tension = max(danger from the nearest hazard, low fuel) and drives the tension layer;
    ///  * crash → low-pass + duck; landing → fade out + victory jingle;
    ///  * ambience loops (wind / rumble) and random distant booms.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        class LevelMusic
        {
            public string Ambient, Tension;
            public string[] Loops;
            public bool Booms;
        }

        static readonly LevelMusic[] Playlist =
        {
            new LevelMusic { Ambient = "level_dusk_ambient", Tension = "level_dusk_tension", Loops = new[] { "wind_loop" } },
            new LevelMusic { Ambient = "level_canyon_ambient", Tension = "level_canyon_tension", Loops = new[] { "wind_loop" }, Booms = true },
            new LevelMusic { Ambient = "level_storm_ambient", Tension = "level_storm_tension", Loops = new[] { "wind_loop", "rumble_loop" }, Booms = true },
            new LevelMusic { Ambient = "level_canyon_ambient", Tension = "level_canyon_tension", Loops = new[] { "rumble_loop" }, Booms = true },
            new LevelMusic { Ambient = "level_storm_ambient", Tension = "level_storm_tension", Loops = new[] { "wind_loop", "rumble_loop" }, Booms = true },
        };

        [Header("Crossfade")]
        public float crossfadeTime = 1.5f;

        [Header("Tension")]
        [Tooltip("Distance (m, to the hazard surface) where the tension layer is at full volume.")]
        public float dangerNear = 2.5f;
        public float dangerFar = 10f;
        [Range(0f, 1f)] public float lowFuelRatio = 0.25f;
        [Range(0f, 1f)] public float lowFuelMinTension = 0.6f;
        public float riseSpeed = 1.5f;
        public float fallSpeed = 0.35f;
        public float scanInterval = 0.1f;

        [Header("Crash")]
        public float crashFadeTime = 0.3f;
        public float crashLowpassHz = 450f;
        public float crashDuckDb = -9f;

        [Header("Victory")]
        public float victoryFadeTime = 1.2f;
        public float jingleVolumeDb = -2f;

        [Header("Ambience")]
        public float ambienceVolumeDb = -14f;
        public float boomIntervalMin = 9f;
        public float boomIntervalMax = 22f;
        public float boomVolumeDb = -8f;

        public float Tension { get; private set; }

        class Deck
        {
            public AudioSource Ambient, Tension;
            public AudioLowPassFilter LowpassA, LowpassT;
            public float Gain, TargetGain, FadeSpeed;
            public string Name;
        }

        readonly Deck[] _decks = new Deck[2];
        int _active;
        AudioSource _jingle;
        readonly List<AudioSource> _ambience = new List<AudioSource>();
        float _ambienceGain, _ambienceTarget;
        float _targetTension, _scanLeft, _boomLeft = -1f;
        float _lowpass = 22000f, _lowpassTarget = 22000f;
        bool _booms;
        readonly Collider[] _overlap = new Collider[32];

        void Start()
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("MusicDeck" + i);
                go.transform.SetParent(transform, false);
                var d = new Deck
                {
                    Ambient = go.AddComponent<AudioSource>(),
                    Tension = new GameObject("Tension").AddComponent<AudioSource>(),
                };
                d.Tension.transform.SetParent(go.transform, false);
                d.LowpassA = go.AddComponent<AudioLowPassFilter>();
                d.LowpassT = d.Tension.gameObject.AddComponent<AudioLowPassFilter>();
                foreach (var s in new[] { d.Ambient, d.Tension })
                {
                    s.loop = true;
                    s.playOnAwake = false;
                    s.ignoreListenerPause = true;
                    s.volume = 0f;
                }
                _decks[i] = d;
            }
            _jingle = gameObject.AddComponent<AudioSource>();
            _jingle.playOnAwake = false;

            var gm = GameManager.Instance;
            gm.LevelStarted += OnLevelStarted;
            gm.PlayerCrashed += _ => OnCrashed();
            gm.LevelCompleted += _ => OnLanded();
            if (gm.Current != null) OnLevelStarted(gm.LevelIndex, gm.Level, gm.Current);
        }

        void OnLevelStarted(int index, LevelDef def, LevelInstance inst)
        {
            _lowpassTarget = 22000f;
            var music = Playlist[index % Playlist.Length];
            var deck = _decks[_active];
            if (deck.Name == music.Ambient && deck.Ambient.isPlaying)
            {
                deck.TargetGain = 1f;   // restart: keep the groove going
                deck.FadeSpeed = 1f / 0.6f;
            }
            else
            {
                Tension = 0f;
                _targetTension = 0f;
                var old = deck;
                old.TargetGain = 0f;
                old.FadeSpeed = 1f / crossfadeTime;
                _active = 1 - _active;
                deck = _decks[_active];
                deck.Name = music.Ambient;
                deck.Ambient.clip = AudioManager.Instance.Clip("Music/" + music.Ambient);
                deck.Tension.clip = AudioManager.Instance.Clip("Music/" + music.Tension);
                double start = AudioSettings.dspTime + 0.1;
                deck.Ambient.PlayScheduled(start);
                deck.Tension.PlayScheduled(start);
                deck.Gain = 0f;
                deck.TargetGain = 1f;
                deck.FadeSpeed = 1f / crossfadeTime;
            }
            SetAmbience(music.Loops);
            _booms = music.Booms;
            _boomLeft = Random.Range(boomIntervalMin, boomIntervalMax);
        }

        void SetAmbience(string[] loops)
        {
            foreach (var s in _ambience) Destroy(s);
            _ambience.Clear();
            foreach (var name in loops)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.clip = AudioManager.Instance.Clip("Ambience/" + name);
                s.loop = true;
                s.volume = 0f;
                s.Play();
                _ambience.Add(s);
            }
            _ambienceGain = 0f;
            _ambienceTarget = 1f;
        }

        void OnCrashed()
        {
            _targetTension = 0f;
            _lowpassTarget = crashLowpassHz;
            var d = _decks[_active];
            d.TargetGain = AudioManager.DbToLinear(crashDuckDb);
            d.FadeSpeed = 1f / crashFadeTime;
        }

        void OnLanded()
        {
            _targetTension = 0f;
            var d = _decks[_active];
            d.TargetGain = 0f;
            d.FadeSpeed = 1f / victoryFadeTime;
            d.Name = null;   // next level start restarts the music
            _jingle.clip = AudioManager.Instance.Clip("Music/victory_jingle");
            _jingle.volume = AudioManager.DbToLinear(jingleVolumeDb) * AudioManager.Instance.BusVolume(AudioBus.Music);
            _jingle.Play();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var gm = GameManager.Instance;
            bool paused = gm != null && gm.State == GameState.Paused;

            if (!paused)
            {
                _scanLeft -= dt;
                if (_scanLeft <= 0f)
                {
                    _scanLeft = scanInterval;
                    _targetTension = gm != null && gm.State == GameState.Playing ? ComputeTension(gm) : 0f;
                }
                float speed = _targetTension > Tension ? riseSpeed : fallSpeed;
                Tension = Mathf.MoveTowards(Tension, _targetTension, speed * dt);
                UpdateBooms(dt);
            }

            float musicBus = AudioManager.Instance.BusVolume(AudioBus.Music) * (paused ? 0.5f : 1f);
            _lowpass = Mathf.Lerp(_lowpass, _lowpassTarget, 1f - Mathf.Exp(-8f * dt));
            foreach (var d in _decks)
            {
                if (d == null) continue;
                d.Gain = Mathf.MoveTowards(d.Gain, d.TargetGain, d.FadeSpeed * dt);
                d.Ambient.volume = d.Gain * musicBus;
                // Equal-power-ish blend: tension layer rises, ambient dips slightly.
                d.Tension.volume = d.Gain * musicBus * Tension;
                d.LowpassA.cutoffFrequency = _lowpass;
                d.LowpassT.cutoffFrequency = _lowpass;
                if (d.Gain <= 0f && d.TargetGain <= 0f && d.Ambient.isPlaying && d != _decks[_active])
                {
                    d.Ambient.Stop();
                    d.Tension.Stop();
                }
            }

            _ambienceGain = Mathf.MoveTowards(_ambienceGain, _ambienceTarget, dt / 2f);
            float amb = AudioManager.DbToLinear(ambienceVolumeDb) * _ambienceGain * AudioManager.Instance.BusVolume(AudioBus.Ambience);
            foreach (var s in _ambience) s.volume = amb;
        }

        float ComputeTension(GameManager gm)
        {
            var rocket = gm.Current.Rocket;
            if (rocket == null) return 0f;
            var p = rocket.transform.position;
            float danger = 0f;
            int n = Physics.OverlapSphereNonAlloc(p, dangerFar + 3f, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c.attachedRigidbody == rocket.Body) continue;
                bool hazard = false;
                var tag = c.GetComponentInParent<SurfaceTag>();
                if (tag != null && tag.kind == SurfaceKind.Hazard) hazard = true;
                var laser = c.GetComponentInParent<LaserGate>();
                if (laser != null && c.isTrigger) hazard = laser.IsOn;
                if (!hazard) continue;
                float d = Vector3.Distance(p, c.ClosestPoint(p));
                danger = Mathf.Max(danger, 1f - Mathf.InverseLerp(dangerNear, dangerFar, d));
            }
            var tank = gm.Current.Tank;
            if (tank.Ratio < lowFuelRatio)
                danger = Mathf.Max(danger, Mathf.Lerp(1f, lowFuelMinTension, tank.Ratio / Mathf.Max(lowFuelRatio, 0.001f)));
            return danger;
        }

        void UpdateBooms(float dt)
        {
            if (!_booms || _boomLeft < 0f) return;
            _boomLeft -= dt;
            if (_boomLeft > 0f) return;
            _boomLeft = Random.Range(boomIntervalMin, boomIntervalMax);
            var src = AudioManager.Instance.Play("distant_boom", null, 0.15f, boomVolumeDb);
            if (src != null) src.panStereo = Random.Range(-0.7f, 0.7f);
        }
    }
}
