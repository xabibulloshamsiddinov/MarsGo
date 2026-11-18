using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// 2.5D follow camera (port of game_camera.gd): critically damped spring, dead-zone,
    /// look-ahead, speed zoom, level bounds, trauma shake and a victory zoom-in.
    /// The camera sits at -Z looking at the z = 0 flight plane.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        [Header("Follow")]
        public Vector2 followOffset = new Vector2(0f, 1.5f);
        public float followSmoothTime = 0.3f;
        public Vector2 deadZone = new Vector2(1.2f, 0.8f);
        [Tooltip("Slight downward look so pad tops read better.")]
        public float pitchDegrees = 4f;

        [Header("Look-ahead")]
        public float lookAheadTime = 0.4f;
        public float lookAheadMax = 4f;
        public float lookAheadSmoothTime = 0.6f;

        [Header("Speed zoom")]
        public float baseDistance = 22f;
        public float extraDistanceAtMaxSpeed = 6f;
        public float baseFov = 50f;
        public float extraFovAtMaxSpeed = 4f;
        public float speedForMaxZoom = 14f;
        public float zoomSmoothTime = 0.8f;

        [Header("Bounds")]
        public bool useBounds = true;
        public Vector2 boundsMin = new Vector2(-50f, -10f);
        public Vector2 boundsMax = new Vector2(50f, 40f);

        [Header("Shake")]
        public float traumaDecay = 1.3f;
        public Vector2 maxShakeOffset = new Vector2(0.6f, 0.45f);
        public float maxShakeRollDegrees = 2.5f;
        public float maxShakePitchYawDegrees = 0.6f;
        public float shakeFrequency = 18f;
        [Tooltip("Constant trauma while the engine burns, scaled by throttle.")]
        public float engineTrauma = 0.05f;
        [Range(0f, 1f)] public float shakeStrength = 1f;

        [Header("Victory")]
        public float victoryDistanceFactor = 0.55f;
        public float victorySmoothTime = 1.2f;

        public float Trauma { get; private set; }

        Camera _cam;
        Rocket _rocket;
        Transform _target;
        float _constantTrauma, _noiseTime;
        Vector2 _deadCenter, _focus, _focusVel, _look, _lookVel;
        float _distance, _distanceVel, _fovVel;
        bool _victory;
        float _seed;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _distance = baseDistance;
            _cam.fieldOfView = baseFov;
            _seed = Random.value * 100f;
        }

        public void SetTarget(Rocket rocket)
        {
            if (_rocket != null) _rocket.ThrustChanged -= OnThrustChanged;
            _rocket = rocket;
            _target = rocket != null ? rocket.transform : null;
            _victory = false;
            _constantTrauma = 0f;
            Trauma = 0f;
            if (_rocket == null) return;
            _rocket.ThrustChanged += OnThrustChanged;
            var judge = _rocket.GetComponent<LandingJudge>();
            if (judge != null)
            {
                judge.Crashed += _ => _constantTrauma = 0f;
                judge.LandedSafely += _ => { _constantTrauma = 0f; _victory = true; };
            }
        }

        public void SnapToTarget()
        {
            if (_target == null) return;
            var p = TargetPoint();
            _deadCenter = p;
            _focus = p;
            _focusVel = Vector2.zero;
            _look = Vector2.zero;
            _lookVel = Vector2.zero;
            _distance = baseDistance;
            _cam.fieldOfView = baseFov;
            ApplyTransform(ClampToBounds(_focus, _distance), 0f);
        }

        public void SetBounds(Vector2 min, Vector2 max)
        {
            boundsMin = Vector2.Min(min, max);
            boundsMax = Vector2.Max(min, max);
            useBounds = true;
        }

        /// <summary>0..1, stacks, capped at 1.</summary>
        public void AddTrauma(float amount) => Trauma = Mathf.Clamp01(Trauma + amount);

        void OnThrustChanged(float amount) => _constantTrauma = engineTrauma * amount;

        void LateUpdate()
        {
            if (_target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var p = TargetPoint();
            var zone = _victory ? Vector2.zero : deadZone;
            var diff = p - _deadCenter;
            if (Mathf.Abs(diff.x) > zone.x) _deadCenter.x = p.x - Mathf.Sign(diff.x) * zone.x;
            if (Mathf.Abs(diff.y) > zone.y) _deadCenter.y = p.y - Mathf.Sign(diff.y) * zone.y;

            var velocity = TargetVelocity();
            var ahead = _victory ? Vector2.zero : Vector2.ClampMagnitude(velocity * lookAheadTime, lookAheadMax);
            _look = Vector2.SmoothDamp(_look, ahead, ref _lookVel, lookAheadSmoothTime, Mathf.Infinity, dt);
            _focus = Vector2.SmoothDamp(_focus, _deadCenter + _look, ref _focusVel, followSmoothTime, Mathf.Infinity, dt);

            float speedRatio = Mathf.Clamp01(velocity.magnitude / speedForMaxZoom);
            float targetDistance = baseDistance + extraDistanceAtMaxSpeed * speedRatio;
            float targetFov = baseFov + extraFovAtMaxSpeed * speedRatio;
            float zoomTime = zoomSmoothTime;
            if (_victory)
            {
                targetDistance = baseDistance * victoryDistanceFactor;
                targetFov = baseFov;
                zoomTime = victorySmoothTime;
            }
            _distance = Mathf.SmoothDamp(_distance, targetDistance, ref _distanceVel, zoomTime, Mathf.Infinity, dt);
            _cam.fieldOfView = Mathf.SmoothDamp(_cam.fieldOfView, targetFov, ref _fovVel, zoomTime, Mathf.Infinity, dt);

            ApplyTransform(ClampToBounds(_focus, _distance), dt);
        }

        void ApplyTransform(Vector2 focus, float dt)
        {
            Trauma = Mathf.Max(Trauma - traumaDecay * dt, 0f);
            float shake = Mathf.Max(Trauma, _constantTrauma) * shakeStrength;
            shake *= shake;
            _noiseTime += dt * shakeFrequency;

            float Noise(float row) => Mathf.PerlinNoise(_seed + row, _noiseTime) * 2f - 1f;
            var rot = Quaternion.Euler(
                pitchDegrees + maxShakePitchYawDegrees * shake * Noise(3f),
                maxShakePitchYawDegrees * shake * Noise(4f),
                maxShakeRollDegrees * shake * Noise(2f));
            transform.rotation = rot;
            var offset = rot * new Vector3(maxShakeOffset.x * shake * Noise(0f), maxShakeOffset.y * shake * Noise(1f), 0f);
            transform.position = new Vector3(focus.x, focus.y, -_distance) + offset;
        }

        /// <summary>Keeps the visible rectangle (at z = 0) inside the bounds.</summary>
        Vector2 ClampToBounds(Vector2 focus, float distance)
        {
            if (!useBounds) return focus;
            float halfH = distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;
            var o = focus;
            o.x = boundsMax.x - boundsMin.x < 2f * halfW
                ? (boundsMin.x + boundsMax.x) * 0.5f
                : Mathf.Clamp(focus.x, boundsMin.x + halfW, boundsMax.x - halfW);
            o.y = boundsMax.y - boundsMin.y < 2f * halfH
                ? (boundsMin.y + boundsMax.y) * 0.5f
                : Mathf.Clamp(focus.y, boundsMin.y + halfH, boundsMax.y - halfH);
            return o;
        }

        Vector2 TargetPoint()
        {
            var o = _target.position;
            return new Vector2(o.x, o.y) + followOffset;
        }

        Vector2 TargetVelocity()
        {
            if (_rocket == null) return Vector2.zero;
            var v = _rocket.Body.linearVelocity;
            return new Vector2(v.x, v.y);
        }
    }
}
