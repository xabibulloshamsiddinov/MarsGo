using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Launch / landing pad (port of pad_prop.gd): metal slab with blinking edge lights.
    /// Landing pads get an "H" marking and a sign, and blink faster as the rocket approaches.
    /// </summary>
    public class Pad : MonoBehaviour
    {
        public enum PadKind { Launch, Landing }

        public PadKind kind = PadKind.Landing;
        public Vector3 size = new Vector3(6f, 0.6f, 4f);

        [Header("Lights")]
        public int lightsPerEdge = 4;
        public float lightSize = 0.18f;
        public float lightInset = 0.35f;

        [Header("Marking / sign")]
        public float markingSize = 1.6f;
        public float signHeight = 2.4f;
        public float signPoleWidth = 0.12f;

        [Header("Blink")]
        public float blinkHzIdle = 0.8f;
        public float blinkHzNear = 4.5f;
        public float approachRadius = 18f;
        public float blinkEnergyOff = 0.3f;
        public float blinkEnergyOn = 4f;

        [Header("Success")]
        public float successEnergy = 8f;
        public float successPulseHz = 1.2f;

        public Transform rocket;

        readonly List<Material> _lightMats = new List<Material>();
        Color _lightColor;
        float _phase;
        bool _success;
        float _successTime;

        public float TopY => transform.position.y + size.y * 0.5f;

        public void Build()
        {
            bool landing = kind == PadKind.Landing;
            _lightColor = landing ? Palette.Safe : Palette.Accent;

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(transform, false);
            slab.transform.localScale = size;
            slab.GetComponent<MeshRenderer>().sharedMaterial = MarsMaterials.Get("PadMetal");
            var tag = slab.AddComponent<SurfaceTag>();
            tag.kind = landing ? SurfaceKind.Finish : SurfaceKind.Friendly;

            // Edge lights along the front and back top edges, both ends of the sides.
            float top = size.y * 0.5f + lightSize * 0.25f;
            float hx = size.x * 0.5f - lightInset;
            float hz = size.z * 0.5f - lightInset;
            for (int i = 0; i < lightsPerEdge; i++)
            {
                float t = lightsPerEdge == 1 ? 0.5f : i / (float)(lightsPerEdge - 1);
                float x = Mathf.Lerp(-hx, hx, t);
                AddLight(new Vector3(x, top, -hz), i);
                AddLight(new Vector3(x, top, hz), i);
            }

            if (landing)
            {
                var marking = MarsMaterials.Get("PadMarking");
                float m = markingSize;
                float y = size.y * 0.5f + 0.005f;
                // "H" on the top surface.
                Bar(marking, new Vector3(-m * 0.3f, y, 0f), new Vector3(m * 0.14f, 0.01f, m));
                Bar(marking, new Vector3(m * 0.3f, y, 0f), new Vector3(m * 0.14f, 0.01f, m));
                Bar(marking, new Vector3(0f, y, 0f), new Vector3(m * 0.6f, 0.01f, m * 0.14f));

                // Sign behind the pad (+Z = away from the camera), facing the camera.
                var metal = MarsMaterials.Get("PadMetal");
                float signZ = size.z * 0.5f - 0.2f;
                float poleX = size.x * 0.5f - 0.4f;
                Bar(metal, new Vector3(poleX, size.y * 0.5f + signHeight * 0.5f, signZ), new Vector3(signPoleWidth, signHeight, signPoleWidth));
                var boardCenter = new Vector3(poleX, size.y * 0.5f + signHeight + 0.4f, signZ);
                Bar(metal, boardCenter, new Vector3(1.0f, 0.9f, 0.08f));
                float s = 0.55f;
                var front = new Vector3(0f, 0f, -0.05f);
                Bar(marking, boardCenter + front + new Vector3(-s * 0.3f, 0f, 0f), new Vector3(s * 0.14f, s, 0.02f));
                Bar(marking, boardCenter + front + new Vector3(s * 0.3f, 0f, 0f), new Vector3(s * 0.14f, s, 0.02f));
                Bar(marking, boardCenter + front, new Vector3(s * 0.6f, s * 0.14f, 0.02f));
            }
        }

        void AddLight(Vector3 localPos, int index)
        {
            var mat = MarsMaterials.Instance(kind == PadKind.Landing ? "PadLightSafe" : "PadLightAccent");
            _lightMats.Add(mat);
            MeshFactory.Part(transform, "Light" + index, MeshFactory.Cube, mat, localPos, Quaternion.identity,
                new Vector3(lightSize, lightSize * 0.5f, lightSize));
        }

        void Bar(Material mat, Vector3 localPos, Vector3 scale)
        {
            MeshFactory.Part(transform, "Mark", MeshFactory.Cube, mat, localPos, Quaternion.identity, scale);
        }

        /// <summary>Landing celebration: steady bright glow with a soft pulse.</summary>
        public void PlaySuccess()
        {
            _success = true;
            _successTime = 0f;
        }

        void Update()
        {
            if (_lightMats.Count == 0) return;
            if (_success)
            {
                _successTime += Time.unscaledDeltaTime;
                float pulse = 1f + 0.25f * Mathf.Sin(_successTime * successPulseHz * Mathf.PI * 2f);
                foreach (var m in _lightMats) MarsMaterials.SetEmission(m, Palette.Safe * (successEnergy * pulse));
                return;
            }

            float hz = blinkHzIdle;
            if (kind == PadKind.Landing && rocket != null)
            {
                float d = Vector3.Distance(rocket.position, transform.position);
                hz = Mathf.Lerp(blinkHzNear, blinkHzIdle, Mathf.Clamp01(d / approachRadius));
            }
            _phase = Mathf.Repeat(_phase + hz * Time.deltaTime, 1f);
            for (int i = 0; i < _lightMats.Count; i++)
            {
                // Chase pattern: each light is offset along the edge.
                float p = Mathf.Repeat(_phase - (i / 2) / (float)lightsPerEdge, 1f);
                float e = p < 0.35f ? blinkEnergyOn : blinkEnergyOff;
                MarsMaterials.SetEmission(_lightMats[i], _lightColor * e);
            }
        }
    }
}
