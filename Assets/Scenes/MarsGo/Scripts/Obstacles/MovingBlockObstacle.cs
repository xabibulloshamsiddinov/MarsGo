using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Crusher block moving A ↔ B (port of moving_block_obstacle.gd). Cycle: PAUSE → WARN (the
    /// danger lane flashes and the block shudders) → MOVE → PAUSE at the other end.
    /// Deadly on touch: crash reason "crushed". The danger lane behind it covers the swept area.
    /// </summary>
    public class MovingBlockObstacle : ObstacleBase
    {
        enum Phase { Pause, Warn, Move }

        const float ShakeYRatio = 1.37f;
        const float MinTime = 0.01f;

        [Header("Path")]
        [Tooltip("Point B relative to point A (this object's start). Keep z = 0.")]
        public Vector3 travel = new Vector3(0f, 6f, 0f);
        public float moveTime = 1.4f;
        public float pauseTime = 1.2f;
        public float startDelay;
        public bool startAtB;

        [Header("Block")]
        public Vector3 blockSize = new Vector3(3f, 5f, 6f);

        [Header("Telegraph")]
        public float warningTime = 0.5f;
        public float shakeAmplitude = 0.06f;
        public float shakeFrequency = 38f;
        public float warningBlinkRate = 6f;
        [Range(0f, 1f)] public float movingPulse = 0.35f;

        [Header("Danger lane")]
        public float laneMargin = 0.3f;
        public float laneBorderWidth = 0.35f;
        public float laneDepthOffset = 0.05f;

        public override string AudioKind => "block";

        Rigidbody _block;
        Vector3 _origin;
        Phase _phase = Phase.Pause;
        float _timer;
        bool _atB;
        Material _laneFill, _laneBorder;

        public void Build()
        {
            _origin = transform.position;

            var blockGo = new GameObject("Block");
            blockGo.transform.SetParent(transform, false);
            _block = blockGo.AddComponent<Rigidbody>();
            _block.isKinematic = true;
            _block.interpolation = RigidbodyInterpolation.Interpolate;
            var col = blockGo.AddComponent<BoxCollider>();
            col.size = blockSize;
            var tag = blockGo.AddComponent<SurfaceTag>();
            tag.kind = SurfaceKind.Hazard;
            tag.crashReason = CrashReasons.Crushed;

            MeshFactory.Part(blockGo.transform, "Body", MeshFactory.Cube, MarsMaterials.Get("HazardBlock"), Vector3.zero, Quaternion.identity, blockSize);
            // Yellow/black warning stripes on the camera face (-Z).
            var stripe = MarsMaterials.Get("HazardStripe");
            float faceZ = -blockSize.z * 0.5f - 0.01f;
            for (int i = -1; i <= 1; i++)
            {
                MeshFactory.Part(blockGo.transform, "Stripe", MeshFactory.Cube, stripe,
                    new Vector3(0f, i * blockSize.y * 0.25f, faceZ), Quaternion.Euler(0f, 0f, 35f),
                    new Vector3(blockSize.x * 0.9f, 0.22f, 0.02f));
            }

            BuildLane();

            _atB = startAtB;
            blockGo.transform.localPosition = EndPosition(_atB);
            _timer = -startDelay;
        }

        void BuildLane()
        {
            var lane = new GameObject("DangerLane").transform;
            lane.SetParent(transform, false);
            var size = new Vector2(Mathf.Abs(travel.x) + blockSize.x + laneMargin * 2f,
                                   Mathf.Abs(travel.y) + blockSize.y + laneMargin * 2f);
            // Behind the block = +Z (away from the camera).
            lane.localPosition = new Vector3(travel.x * 0.5f, travel.y * 0.5f, blockSize.z * 0.5f + laneDepthOffset);

            _laneFill = MarsMaterials.Instance("LaneFill");
            _laneBorder = MarsMaterials.Instance("LaneBorder");
            var fill = MeshFactory.Part(lane, "Fill", MeshFactory.Cube, _laneFill, Vector3.zero, Quaternion.identity, new Vector3(size.x, size.y, 0.01f));
            fill.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float b = laneBorderWidth;
            Border(lane, new Vector3(0f, size.y * 0.5f - b * 0.5f, 0f), new Vector3(size.x, b, 0.02f));
            Border(lane, new Vector3(0f, -size.y * 0.5f + b * 0.5f, 0f), new Vector3(size.x, b, 0.02f));
            Border(lane, new Vector3(size.x * 0.5f - b * 0.5f, 0f, 0f), new Vector3(b, size.y, 0.02f));
            Border(lane, new Vector3(-size.x * 0.5f + b * 0.5f, 0f, 0f), new Vector3(b, size.y, 0.02f));
            SetPulse(0f);
        }

        void Border(Transform lane, Vector3 pos, Vector3 scale)
        {
            var go = MeshFactory.Part(lane, "Border", MeshFactory.Cube, _laneBorder, pos, Quaternion.identity, scale);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void FixedUpdate()
        {
            if (_block == null) return;
            float dt = Time.fixedDeltaTime;
            _timer += dt;
            float warn = Mathf.Clamp(warningTime, 0f, pauseTime);
            switch (_phase)
            {
                case Phase.Pause:
                    SetPulse(0f);
                    if (_timer >= pauseTime - warn)
                    {
                        Enter(Phase.Warn);
                        Emit(SignalWarning);
                    }
                    break;
                case Phase.Warn:
                    var shake = new Vector3(Mathf.Sin(_timer * shakeFrequency * Mathf.PI * 2f),
                                            Mathf.Sin(_timer * shakeFrequency * ShakeYRatio * Mathf.PI * 2f), 0f) * shakeAmplitude;
                    MoveTo(EndPosition(_atB) + shake);
                    SetPulse(0.5f + 0.5f * Mathf.Cos(_timer * warningBlinkRate * Mathf.PI * 2f));
                    if (_timer >= warn)
                    {
                        MoveTo(EndPosition(_atB));
                        Enter(Phase.Move);
                        Emit(SignalActivated);
                    }
                    break;
                case Phase.Move:
                    float t = Mathf.Clamp01(_timer / Mathf.Max(moveTime, MinTime));
                    MoveTo(Vector3.Lerp(EndPosition(_atB), EndPosition(!_atB), Ease(t)));
                    SetPulse(movingPulse);
                    if (t >= 1f)
                    {
                        _atB = !_atB;
                        Enter(Phase.Pause);
                        Emit(SignalDeactivated);
                    }
                    break;
            }
        }

        void MoveTo(Vector3 local) => _block.MovePosition(_origin + transform.rotation * local);

        void Enter(Phase p)
        {
            _phase = p;
            _timer = 0f;
        }

        Vector3 EndPosition(bool atB) => atB ? travel : Vector3.zero;

        /// <summary>
        /// Same shape as the Godot easing curve: (0,0) → (0.5,0.5, slope 1.8) → (1,1), flat ends.
        /// Heavier than smoothstep in the middle.
        /// </summary>
        static float Ease(float t)
        {
            if (t > 0.5f) return 1f - Ease(1f - t);
            float u = t / 0.5f;
            float h01 = -2f * u * u * u + 3f * u * u;
            float h11 = u * u * u - u * u;
            return h01 * 0.5f + h11 * 0.5f * 1.8f;
        }

        void SetPulse(float v)
        {
            if (_laneBorder == null) return;
            MarsMaterials.SetEmission(_laneBorder, Palette.Hazard * Mathf.Lerp(0.4f, 4f, v));
            MarsMaterials.SetAlpha(_laneFill, Mathf.Lerp(0.06f, 0.3f, v));
        }
    }
}
