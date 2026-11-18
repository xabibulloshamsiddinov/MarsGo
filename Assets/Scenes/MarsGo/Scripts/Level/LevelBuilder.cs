using UnityEngine;

namespace MarsGo
{
    /// <summary>Handles to the pieces of a built level.</summary>
    public class LevelInstance
    {
        public GameObject Root;
        public Rocket Rocket;
        public FuelTank Tank;
        public LandingJudge Judge;
        public Pad LaunchPad;
        public Pad LandingPad;
    }

    /// <summary>
    /// Builds a whole level (geometry, pads, death zone, Mars backdrop, rocket) from a LevelDef.
    /// The flight plane is z = 0; the camera looks down +Z, so "behind the level" is +Z.
    /// </summary>
    public static class LevelBuilder
    {
        // Mars backdrop
        const float PlainTopY = -0.35f;
        const float PlainStartZ = 4f;
        const float PlainDepth = 400f;
        const int MesaCount = 28;
        const int ScatterPerTenMetres = 6;

        public static LevelInstance Build(LevelDef def, int seed)
        {
            var inst = new LevelInstance { Root = new GameObject("Level: " + def.Name) };
            var root = inst.Root.transform;
            var rng = new System.Random(seed);

            var geometry = new GameObject("Geometry").transform;
            geometry.SetParent(root, false);
            foreach (var b in def.Blocks) BuildBlock(geometry, b, rng);

            BuildBackdrop(root, def, rng);

            inst.LaunchPad = BuildPad(root, "LaunchPad", Pad.PadKind.Launch, def.LaunchPad, def.LaunchPadSize);
            inst.LandingPad = BuildPad(root, "LandingPad", Pad.PadKind.Landing, def.LandingPad, def.LandingPadSize);

            var death = new GameObject("DeathZone");
            death.transform.SetParent(root, false);
            death.transform.position = new Vector3(0f, def.DeathZoneY, 0f);
            var trigger = death.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(600f, 4f, 60f);
            death.AddComponent<DeathZone>();

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(root, false);
            foreach (var o in def.Obstacles) ObstacleFactory.Build(obstacles, o);

            BuildRocket(inst, def);
            inst.LandingPad.rocket = inst.Rocket.transform;
            return inst;
        }

        static void BuildBlock(Transform parent, BlockDef b, System.Random rng)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = b.Kind.ToString();
            go.transform.SetParent(parent, false);
            go.transform.position = b.Center;
            go.transform.localScale = b.Size;
            var mr = go.GetComponent<MeshRenderer>();

            switch (b.Kind)
            {
                case BlockKind.Ground:
                    mr.sharedMaterial = MarsMaterials.Get("Ground");
                    Tag(go, SurfaceKind.Ground);
                    Scatter(parent, b, rng);
                    break;
                case BlockKind.Rock:
                    mr.sharedMaterial = MarsMaterials.Get("Rock");
                    Tag(go, SurfaceKind.Obstacle);
                    break;
                case BlockKind.Wall:
                case BlockKind.Ceiling:
                    mr.sharedMaterial = MarsMaterials.Get("Basalt");
                    Tag(go, SurfaceKind.Obstacle);
                    break;
                case BlockKind.HazardFloor:
                    mr.sharedMaterial = MarsMaterials.Get("HazardFloor");
                    Tag(go, SurfaceKind.Hazard);
                    break;
                case BlockKind.Tower:
                    mr.sharedMaterial = MarsMaterials.Get("TowerBody");
                    Tag(go, SurfaceKind.Hazard);
                    var beacon = MeshFactory.Part(parent, "Beacon", MeshFactory.Sphere, MarsMaterials.Get("Beacon"),
                        b.Center + Vector3.up * (b.Size.y * 0.5f + 0.25f), Quaternion.identity, Vector3.one * 0.6f);
                    beacon.AddComponent<SphereCollider>();
                    Tag(beacon, SurfaceKind.Hazard);
                    break;
                case BlockKind.Scenery:
                    mr.sharedMaterial = MarsMaterials.Get("Rock");
                    Object.Destroy(go.GetComponent<Collider>());
                    break;
            }
        }

        static void Tag(GameObject go, SurfaceKind kind) => go.AddComponent<SurfaceTag>().kind = kind;

        /// <summary>Visual-only pebbles and boulders on a ground block, kept off the flight lane.</summary>
        static void Scatter(Transform parent, BlockDef b, System.Random rng)
        {
            var rock = MarsMaterials.Get("Rock");
            var basalt = MarsMaterials.Get("Basalt");
            float top = b.Center.y + b.Size.y * 0.5f;
            int count = Mathf.RoundToInt(b.Size.x / 10f * ScatterPerTenMetres);
            float halfDepth = b.Size.z * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float x = b.Center.x + (float)(rng.NextDouble() - 0.5) * (b.Size.x - 1f);
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float z = side * Mathf.Lerp(1.6f, halfDepth - 0.3f, (float)rng.NextDouble());
                float s = Mathf.Lerp(0.15f, 0.8f, Mathf.Pow((float)rng.NextDouble(), 2f));
                var scale = new Vector3(s * Mathf.Lerp(0.8f, 1.6f, (float)rng.NextDouble()), s * 0.6f, s);
                var rot = Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f);
                MeshFactory.Part(parent, "Pebble", MeshFactory.Sphere, rng.NextDouble() < 0.7 ? rock : basalt,
                    new Vector3(x, top + scale.y * 0.2f, z), rot, scale);
            }
        }

        static void BuildBackdrop(Transform root, LevelDef def, System.Random rng)
        {
            var back = new GameObject("Backdrop").transform;
            back.SetParent(root, false);
            float midX = (def.BoundsMin.x + def.BoundsMax.x) * 0.5f;

            var plain = MeshFactory.Part(back, "Plain", MeshFactory.Cube, MarsMaterials.Get("Ground"),
                new Vector3(midX, PlainTopY - 0.5f, PlainStartZ + PlainDepth * 0.5f), Quaternion.identity,
                new Vector3(1200f, 1f, PlainDepth));
            plain.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var scenery = MarsMaterials.Get("Scenery");
            var rockMat = MarsMaterials.Get("Rock");
            for (int i = 0; i < MesaCount; i++)
            {
                float z = Mathf.Lerp(90f, 340f, (float)rng.NextDouble());
                float x = midX + (float)(rng.NextDouble() - 0.5) * (200f + z * 1.4f);
                float w = Mathf.Lerp(25f, 80f, (float)rng.NextDouble());
                float h = Mathf.Lerp(4f, 16f, (float)rng.NextDouble()) * (0.5f + z / 250f);
                float d = Mathf.Lerp(10f, 40f, (float)rng.NextDouble());
                var mat = rng.NextDouble() < 0.6 ? scenery : rockMat;
                // Mesa: flat-topped frustum, slightly tilted.
                var mesh = MeshFactory.Frustum(0.75f, 1f, 1f, 7);
                var go = MeshFactory.Part(back, "Mesa", mesh, mat, new Vector3(x, PlainTopY + h * 0.5f - 0.5f, z),
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(w, h, d));
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Pad BuildPad(Transform root, string name, Pad.PadKind kind, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var pad = go.AddComponent<Pad>();
            pad.kind = kind;
            pad.size = size;
            pad.Build();
            return pad;
        }

        static void BuildRocket(LevelInstance inst, LevelDef def)
        {
            var go = new GameObject("Rocket");
            go.transform.SetParent(inst.Root.transform, false);
            go.transform.position = def.Spawn;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

            var pm = new PhysicsMaterial("Rocket")
            {
                dynamicFriction = 0.8f,
                staticFriction = 0.8f,
                bounciness = 0.05f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            var body = go.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.3f, 0f);
            body.radius = 0.45f;
            body.height = 3.3f;
            body.sharedMaterial = pm;
            var feet = go.AddComponent<BoxCollider>();
            feet.center = new Vector3(0f, -1.61f, 0f);
            feet.size = new Vector3(1.9f, 0.25f, 1.9f);
            feet.sharedMaterial = pm;
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);

            inst.Rocket = go.AddComponent<Rocket>();
            inst.Tank = go.AddComponent<FuelTank>();
            inst.Judge = go.AddComponent<LandingJudge>();

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(go.transform, false);
            visuals.AddComponent<RocketVisuals>();

            // Effects + audio listen to the rocket's events (gameplay never calls them).
            go.AddComponent<EngineFlame>();
            go.AddComponent<RocketFx>();
            go.AddComponent<CrashFx>();
            go.AddComponent<LevelAudioRouter>();
        }
    }
}
