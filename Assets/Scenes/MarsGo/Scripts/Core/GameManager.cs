using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MarsGo
{
    public enum GameState { Boot, Playing, Paused, Crashed, Landed }

    public struct LevelResult
    {
        public int Stars;
        public float Time;
        public float FuelRatio;
        public float LandingSpeed;
        public bool IsRecord;
        public bool IsLastLevel;
    }

    /// <summary>
    /// Game flow (port of game_manager.gd): loads levels, tracks time, handles crash / landing,
    /// records progress. Knows nothing about VFX, audio or UI — they subscribe to its events.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int, LevelDef, LevelInstance> LevelStarted;
        public event Action<CrashInfo> PlayerCrashed;
        public event Action<LevelResult> LevelCompleted;

        [Tooltip("Physics ticks per second (Godot project used 60).")]
        public int physicsTicksPerSecond = 60;
        [Tooltip("Seconds after a crash / landing before Confirm is accepted.")]
        public float confirmDelay = 1.3f;

        public GameState State { get; private set; } = GameState.Boot;
        public int LevelIndex { get; private set; }
        public LevelDef Level { get; private set; }
        public LevelInstance Current { get; private set; }
        public float LevelTime { get; private set; }
        public CrashInfo LastCrash { get; private set; }
        public LevelResult LastResult { get; private set; }
        /// <summary>Unscaled seconds since the current state started.</summary>
        public float StateTime { get; private set; }

        GameCamera _camera;

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 1f / physicsTicksPerSecond;
            Application.targetFrameRate = 60;
            GameInput.Enable();
            if (GetComponent<AudioManager>() == null) gameObject.AddComponent<AudioManager>();
            if (GetComponent<MusicDirector>() == null) gameObject.AddComponent<MusicDirector>();
        }

        void Start()
        {
            var cam = Camera.main;
            _camera = cam.GetComponent<GameCamera>();
            if (_camera == null) _camera = cam.gameObject.AddComponent<GameCamera>();
            if (cam.GetComponent<SkyExtras>() == null) cam.gameObject.AddComponent<SkyExtras>();
            int unlocked = Mathf.Min(SaveData.UnlockedCount, LevelLibrary.Count);
            LoadLevel(Mathf.Clamp(SaveData.LastLevel, 0, unlocked - 1));
        }

        void Update()
        {
            TimeControl.Apply();
            StateTime += Time.unscaledDeltaTime;
            if (State == GameState.Playing) LevelTime += Time.deltaTime;

            if (GameInput.Pause.WasPressedThisFrame()) TogglePause();
            if (GameInput.Restart.WasPressedThisFrame() &&
                (State == GameState.Playing || State == GameState.Paused || State == GameState.Crashed))
            {
                RestartLevel();
                return;
            }

            float delay = State == GameState.Landed ? LandingCelebration.Duration : confirmDelay;
            if (StateTime >= delay && GameInput.Confirm.WasPressedThisFrame())
            {
                if (State == GameState.Crashed) RestartLevel();
                else if (State == GameState.Landed) NextLevel();
            }

            // Quick level jump (number keys) — replaced by the level select menu in Phase 4.
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < LevelLibrary.Count && i < 9; i++)
                {
                    var key = kb[(Key)((int)Key.Digit1 + i)];
                    if (key.wasPressedThisFrame && SaveData.IsUnlocked(i)) LoadLevel(i);
                }
            }
        }

        public void LoadLevel(int index)
        {
            TimeControl.Reset();
            AudioListener.pause = false;
            if (Current != null)
            {
                Current.Root.SetActive(false);   // stop old physics/events this frame
                Destroy(Current.Root);
            }
            LevelIndex = Mathf.Clamp(index, 0, LevelLibrary.Count - 1);
            Level = LevelLibrary.Levels[LevelIndex];
            SaveData.LastLevel = LevelIndex;

            Current = LevelBuilder.Build(Level, 1000 + LevelIndex);
            Current.Tank.Refill(Level.FuelCapacity);
            _camera.SetBounds(Level.BoundsMin, Level.BoundsMax);
            _camera.SetTarget(Current.Rocket);
            _camera.SnapToTarget();

            LevelTime = 0f;
            ChangeState(GameState.Playing);
            LevelStarted?.Invoke(LevelIndex, Level, Current);
        }

        public void RestartLevel() => LoadLevel(LevelIndex);

        public void NextLevel() => LoadLevel(IsLastLevel ? 0 : LevelIndex + 1);

        public bool IsLastLevel => LevelIndex >= LevelLibrary.Count - 1;

        public void RegisterCrash(CrashInfo info)
        {
            if (State != GameState.Playing) return;
            LastCrash = info;
            ChangeState(GameState.Crashed);
            PlayerCrashed?.Invoke(info);
        }

        public void RegisterLanding(LandingStats stats)
        {
            if (State != GameState.Playing) return;
            int stars = Level.CalculateStars(LevelTime, stats.FuelRatio);
            bool record = SaveData.RecordResult(LevelIndex, LevelTime, stars);
            LastResult = new LevelResult
            {
                Stars = stars,
                Time = LevelTime,
                FuelRatio = stats.FuelRatio,
                LandingSpeed = stats.LandingSpeed,
                IsRecord = record,
                IsLastLevel = IsLastLevel,
            };
            Current.LandingPad.PlaySuccess();
            LandingCelebration.Play(Current.Root.transform, Current.LandingPad, Current.Rocket, stars);
            ChangeState(GameState.Landed);
            LevelCompleted?.Invoke(LastResult);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetPaused(true);
            else if (State == GameState.Paused) SetPaused(false);
        }

        void SetPaused(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            if (!paused && State != GameState.Paused) return;
            TimeControl.Paused = paused;
            TimeControl.Apply();
            AudioListener.pause = paused;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(paused ? "ui_pause" : "ui_click");
            ChangeState(paused ? GameState.Paused : GameState.Playing);
        }

        void ChangeState(GameState next)
        {
            if (State == next) return;
            State = next;
            StateTime = 0f;
            StateChanged?.Invoke(next);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TimeControl.Reset();
        }
    }
}
