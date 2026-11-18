using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Turns gameplay events into sounds (port of level_audio_router.gd). Gameplay code never
    /// plays audio itself: this router listens to the rocket, judge, tank, obstacles and the
    /// landing celebration. One router lives on the rocket of each level.
    /// </summary>
    public class LevelAudioRouter : MonoBehaviour
    {
        [Header("Landing")]
        public float hardLandingSpeed = 1.8f;
        public float touchdownCooldown = 0.3f;

        [Header("Crash")]
        public float loudCrashSpeed = 8f;
        public float quietCrashDb = -6f;

        [Header("RCS")]
        public float rcsRepeatInterval = 0.32f;
        public float rcsRepeatDb = -5f;

        [Header("Fuel")]
        public float beepIntervalSlow = 1.1f;
        public float beepIntervalFast = 0.4f;

        static readonly Dictionary<string, string> EmitterSounds = new Dictionary<string, string>
        {
            { "laser:warning", "laser_charge" },
            { "laser:activated", "laser_zap" },
            { "laser:deactivated", "laser_off" },
            { "block:warning", "hazard_warning" },
            { "block:deactivated", "block_slam" },
            { "blade:activated", "blade_whoosh" },
            { "blade:warning", "hazard_warning" },
            { "wind:warning", "hazard_warning" },
            { "wind:activated", "wind_gust" },
            { "fuel:collected", "fuel_pickup" },
        };

        static readonly Dictionary<string, string> EmitterLoops = new Dictionary<string, string>
        {
            { "laser", "laser_hum" },
            { "wind", "wind_zone_loop" },
        };

        Rocket _rocket;
        LandingJudge _judge;
        FuelTank _tank;
        EngineAudio _engine;
        readonly Dictionary<ObstacleBase, AudioSource> _loops = new Dictionary<ObstacleBase, AudioSource>();
        float _rcsDirection, _rcsTimer, _sinceTouchdown = 10f, _beepTimer = -1f;
        bool _resolved;

        void Awake()
        {
            _rocket = GetComponent<Rocket>();
            _judge = GetComponent<LandingJudge>();
            _tank = GetComponent<FuelTank>();

            var engineGo = new GameObject("EngineAudio");
            engineGo.transform.SetParent(transform, false);
            _engine = engineGo.AddComponent<EngineAudio>();
            _engine.target = transform;

            _rocket.ThrustChanged += a => _engine.SetThrust(a);
            _rocket.ThrustStarted += () => _engine.Ignite();
            _rocket.ThrustStopped += () => _engine.ShutDown();
            _rocket.RotationChanged += OnRotationChanged;
            _judge.TouchdownStarted += OnTouchdown;
            _judge.LandedSafely += _ => OnResolved(6f);
            _judge.Crashed += OnCrashed;
            _tank.FuelLow += () => { if (!_resolved) Beep(); };
            _tank.FuelAdded += _ =>
            {
                if (_tank.Ratio >= _tank.lowFuelRatio) _beepTimer = -1f;
            };
            _tank.FuelEmpty += () =>
            {
                _beepTimer = -1f;
                if (!_resolved) _engine.Sputter();
            };
            ObstacleBase.AnySignal += OnObstacleSignal;
            LandingCelebration.StarShown += OnStarShown;
        }

        void OnDestroy()
        {
            ObstacleBase.AnySignal -= OnObstacleSignal;
            LandingCelebration.StarShown -= OnStarShown;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _sinceTouchdown += dt;

            if (_rcsDirection != 0f && !_resolved)
            {
                _rcsTimer -= dt;
                if (_rcsTimer <= 0f)
                {
                    _rcsTimer = rcsRepeatInterval;
                    AudioManager.Instance.Play("rcs_puff", transform.position, 0.12f, rcsRepeatDb);
                }
            }

            if (_beepTimer >= 0f)
            {
                _beepTimer -= dt;
                if (_beepTimer < 0f) Beep();
            }

            // Obstacle loops (laser hum, wind) get louder as the rocket gets closer.
            foreach (var kv in _loops)
            {
                if (kv.Key == null || kv.Value == null) continue;
                float d = Vector3.Distance(kv.Key.transform.position, transform.position);
                float near = Mathf.Clamp01(1f - (d - 4f) / 22f);
                var e = SfxLibrary.Get(EmitterLoops[kv.Key.AudioKind]);
                kv.Value.volume = AudioManager.DbToLinear(e.VolumeDb) * near * AudioManager.Instance.BusVolume(AudioBus.Sfx);
                kv.Value.panStereo = AudioManager.Instance.Pan(kv.Key.transform.position);
            }
        }

        void OnRotationChanged(float direction)
        {
            bool wasIdle = _rcsDirection == 0f;
            _rcsDirection = direction;
            if (direction != 0f && wasIdle && !_resolved)
            {
                _rcsTimer = rcsRepeatInterval;
                AudioManager.Instance.Play("rcs_puff", transform.position, 0.12f);
            }
        }

        void OnTouchdown()
        {
            if (_sinceTouchdown < touchdownCooldown) return;
            _sinceTouchdown = 0f;
            float speed = _rocket.Body.linearVelocity.magnitude;
            AudioManager.Instance.Play(speed >= hardLandingSpeed ? "land_hard" : "land_soft", transform.position, 0.06f);
        }

        void OnResolved(float fade)
        {
            _resolved = true;
            _beepTimer = -1f;
            _engine.Kill(fade);
        }

        void OnCrashed(CrashInfo info)
        {
            OnResolved(25f);
            float weight = Mathf.Clamp01(info.ImpactSpeed / Mathf.Max(loudCrashSpeed, 0.01f));
            float db = Mathf.Lerp(quietCrashDb, 0f, weight);
            var where = transform.position;
            var am = AudioManager.Instance;
            am.Play("explosion_impact", where, 0.08f, db);
            StartCoroutine(Delayed(0.02f, () => am.Play("explosion_rumble", where, 0.08f, db)));
            StartCoroutine(Delayed(0.1f, () => am.Play("explosion_debris", where, 0.08f, db)));
        }

        static System.Collections.IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        void Beep()
        {
            if (_resolved || _tank.IsEmpty || _tank.Ratio >= _tank.lowFuelRatio)
            {
                _beepTimer = -1f;
                return;
            }
            AudioManager.Instance.Play("fuel_low_beep");
            float t = _tank.Ratio / Mathf.Max(_tank.lowFuelRatio, 0.001f);
            _beepTimer = Mathf.Lerp(beepIntervalFast, beepIntervalSlow, t);
        }

        void OnObstacleSignal(ObstacleBase obstacle, string signal)
        {
            if (obstacle == null || !obstacle.gameObject.scene.IsValid()) return;
            if (EmitterSounds.TryGetValue(obstacle.AudioKind + ":" + signal, out var id))
            {
                // Distant obstacles are quieter (they are on the far side of the level).
                float d = Vector3.Distance(obstacle.transform.position, transform.position);
                float db = Mathf.Lerp(0f, -14f, Mathf.Clamp01((d - 6f) / 30f));
                AudioManager.Instance.Play(id, obstacle.transform.position, 0.1f, db);
            }
            if (EmitterLoops.TryGetValue(obstacle.AudioKind, out var loopId))
            {
                if (signal == ObstacleBase.SignalActivated && !_loops.ContainsKey(obstacle))
                {
                    var src = AudioManager.Instance.CreateLoop(loopId, gameObject);
                    src.Play();
                    _loops[obstacle] = src;
                }
                else if (signal == ObstacleBase.SignalDeactivated && _loops.TryGetValue(obstacle, out var src))
                {
                    Destroy(src);
                    _loops.Remove(obstacle);
                }
            }
        }

        void OnStarShown(int index)
        {
            AudioManager.Instance.Play("star_pop_" + Mathf.Clamp(index + 1, 1, 3));
        }
    }
}
