using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Stylised rocket built from procedural meshes (port of rocket_visuals.gd).
    /// Listens to Rocket events; never touches physics.
    /// Azimuth convention: 0° = toward the camera (-Z in Unity), 90° = right (+X).
    /// </summary>
    public class RocketVisuals : MonoBehaviour
    {
        static readonly float[] FinAzimuths = { 60f, 180f, 300f };
        static readonly float[] LegAzimuths = { 0f, 120f, 240f };
        const float RightFinAzimuth = 60f;
        const float LeftFinAzimuth = 300f;
        const float NavBlinkDuty = 0.25f;

        [Header("Nozzle ring")]
        public Color nozzleColdColor = new Color(0.25f, 0.08f, 0.05f);
        public Color nozzleHotColor = Palette.Accent;
        public float nozzleMaxEnergy = 6f;
        public float nozzleResponse = 8f;

        [Header("Nav lights")]
        public float navBlinkHz = 1f;
        public float navEnergyOn = 5f;
        public float navEnergyOff = 0.2f;

        [Header("Engine light")]
        public Color engineLightCold = Palette.Accent;
        public Color engineLightHot = new Color(1f, 0.92f, 0.8f);
        public float engineLightMaxIntensity = 6f;
        public float engineLightRange = 6f;
        public Vector3 engineLightOffset = new Vector3(0f, -2f, 0f);
        public float engineFlicker = 0.15f;
        public float engineFlickerSpeed = 25f;

        [Header("Dimensions")]
        public float bodyRadius = 0.45f;
        public float bodyHeight = 2f;
        public float bodyCenterY = 0.1f;
        public float noseHeight = 0.85f;
        public float nozzleHeight = 0.45f;
        public float nozzleTopRadius = 0.28f;
        public float nozzleBottomRadius = 0.36f;
        public Vector3 finSize = new Vector3(0.5f, 0.8f, 0.06f);
        public float finCenterY = -0.5f;
        public float legLength = 1.27f;
        public float legThickness = 0.09f;
        public float legTiltDegrees = 24.7f;
        public float legTopY = -0.5f;
        public float footRadius = 0.14f;
        public float footHeight = 0.08f;
        public float windowY = 0.55f;
        public float windowRadius = 0.17f;
        public float stripeHeight = 0.1f;
        public float[] stripeY = { 0.95f, -0.75f };

        Rocket _rocket;
        Transform _parts;
        Material _nozzle, _navLeft, _navRight;
        Light _engineLight;
        float _thrustTarget, _thrust, _navPhase, _time;

        void Start()
        {
            Build();
            _rocket = GetComponentInParent<Rocket>();
            if (_rocket != null) _rocket.ThrustChanged += a => _thrustTarget = a;
        }

        void Update()
        {
            if (_nozzle == null) return;
            float dt = Time.deltaTime;
            _time += dt;
            _thrust = Mathf.Lerp(_thrust, _thrustTarget, 1f - Mathf.Exp(-nozzleResponse * dt));

            MarsMaterials.SetEmission(_nozzle, Color.Lerp(nozzleColdColor, nozzleHotColor, _thrust) * (nozzleMaxEnergy * _thrust));

            float flicker = 1f + engineFlicker * (Mathf.PerlinNoise(_time * engineFlickerSpeed, 0.37f) * 2f - 1f);
            _engineLight.intensity = engineLightMaxIntensity * _thrust * flicker;
            _engineLight.color = Color.Lerp(engineLightCold, engineLightHot, _thrust);
            _engineLight.enabled = _thrust > 0.01f;

            _navPhase = Mathf.Repeat(_navPhase + navBlinkHz * dt, 1f);
            bool on = _navPhase < NavBlinkDuty;
            MarsMaterials.SetEmission(_navLeft, Palette.Hazard * (on ? navEnergyOn : navEnergyOff));
            MarsMaterials.SetEmission(_navRight, Palette.Safe * (on ? navEnergyOff : navEnergyOn));
        }

        /// <summary>Hull / accent / trim, so crash debris looks like pieces of this rocket.</summary>
        public Material[] DebrisMaterials => new[] { MarsMaterials.Get("Hull"), MarsMaterials.Get("Accent"), MarsMaterials.Get("Trim") };

        Vector3 Radial(float azimuthDeg, float radius, float y)
        {
            float a = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, y, -Mathf.Cos(a) * radius);
        }

        void Build()
        {
            if (_parts != null) Destroy(_parts.gameObject);
            _parts = new GameObject("GeneratedParts").transform;
            _parts.SetParent(transform, false);

            var hull = MarsMaterials.Get("Hull");
            var accent = MarsMaterials.Get("Accent");
            var trim = MarsMaterials.Get("Trim");
            var glass = MarsMaterials.Get("Window");
            _nozzle = MarsMaterials.Instance("Nozzle");
            _navLeft = MarsMaterials.Instance("NavLeft");
            _navRight = MarsMaterials.Instance("NavRight");

            float bodyTop = bodyCenterY + bodyHeight * 0.5f;
            float bodyBottom = bodyCenterY - bodyHeight * 0.5f;
            var id = Quaternion.identity;

            MeshFactory.Part(_parts, "Body", MeshFactory.Frustum(bodyRadius, bodyRadius, bodyHeight, 32), hull, new Vector3(0f, bodyCenterY, 0f), id);
            MeshFactory.Part(_parts, "Nose", MeshFactory.Frustum(0f, bodyRadius, noseHeight, 32), hull, new Vector3(0f, bodyTop + noseHeight * 0.5f, 0f), id);
            MeshFactory.Part(_parts, "Nozzle", MeshFactory.Frustum(nozzleTopRadius, nozzleBottomRadius, nozzleHeight), trim,
                new Vector3(0f, bodyBottom - nozzleHeight * 0.5f, 0f), id);
            MeshFactory.Part(_parts, "NozzleRing", MeshFactory.Torus(nozzleTopRadius * 0.75f, nozzleBottomRadius * 0.85f), _nozzle,
                new Vector3(0f, bodyBottom - nozzleHeight + footHeight, 0f), id);

            foreach (float y in stripeY)
                MeshFactory.Part(_parts, "Stripe", MeshFactory.Frustum(bodyRadius * 1.02f, bodyRadius * 1.02f, stripeHeight, 32), accent, new Vector3(0f, y, 0f), id);

            // Porthole facing the camera (-Z).
            var faceCamera = Quaternion.Euler(-90f, 0f, 0f);
            MeshFactory.Part(_parts, "WindowFrame", MeshFactory.Torus(windowRadius, windowRadius * 1.3f), trim, new Vector3(0f, windowY, -(bodyRadius - 0.02f)), faceCamera);
            MeshFactory.Part(_parts, "Window", MeshFactory.Frustum(windowRadius, windowRadius, 0.04f), glass, new Vector3(0f, windowY, -(bodyRadius - 0.01f)), faceCamera);

            // 3 swept fins: vertical edge on the hull, slope outward.
            foreach (float az in FinAzimuths)
            {
                var outward = Radial(az, 1f, 0f);
                MeshFactory.Part(_parts, "Fin", MeshFactory.Prism(finSize), accent,
                    Radial(az, bodyRadius + finSize.x * 0.5f, finCenterY), Quaternion.FromToRotation(Vector3.right, outward));
            }

            // Nav lights on the left/right fin tips.
            float tipR = bodyRadius + finSize.x + footRadius * 0.3f;
            float tipY = finCenterY - finSize.y * 0.5f + footRadius;
            var navScale = Vector3.one * (footRadius * 1.2f);
            MeshFactory.Part(_parts, "NavRight", MeshFactory.Sphere, _navRight, Radial(RightFinAzimuth, tipR, tipY), id, navScale);
            MeshFactory.Part(_parts, "NavLeft", MeshFactory.Sphere, _navLeft, Radial(LeftFinAzimuth, tipR, tipY), id, navScale);

            // 3 landing legs with foot pads.
            float tilt = legTiltDegrees * Mathf.Deg2Rad;
            float reach = legLength * Mathf.Sin(tilt);
            float drop = legLength * Mathf.Cos(tilt);
            foreach (float az in LegAzimuths)
            {
                var top = Radial(az, bodyRadius * 0.95f, legTopY);
                var foot = Radial(az, bodyRadius * 0.95f + reach, legTopY - drop);
                MeshFactory.Part(_parts, "Leg", MeshFactory.Cube, trim, (top + foot) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, (foot - top).normalized), new Vector3(legThickness, legLength, legThickness));
                MeshFactory.Part(_parts, "Foot", MeshFactory.Frustum(footRadius, footRadius, footHeight), trim, foot - new Vector3(0f, footHeight * 0.5f, 0f), id);
            }

            var lightGo = new GameObject("EngineLight");
            lightGo.transform.SetParent(_parts, false);
            lightGo.transform.localPosition = engineLightOffset;
            _engineLight = lightGo.AddComponent<Light>();
            _engineLight.type = LightType.Point;
            _engineLight.range = engineLightRange;
            _engineLight.intensity = 0f;
            _engineLight.shadows = LightShadows.None;
            _engineLight.enabled = false;
        }
    }
}
