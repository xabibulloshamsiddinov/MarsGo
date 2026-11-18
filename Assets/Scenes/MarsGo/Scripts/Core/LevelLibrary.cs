using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// The 5 campaign levels, converted 1:1 from the Godot scenes (level_01..05.tscn + data/*.tres).
    /// Godot's camera looked down -Z, Unity's looks down +Z, so background scenery z is mirrored.
    /// </summary>
    public static class LevelLibrary
    {
        static List<LevelDef> _levels;

        public static IReadOnlyList<LevelDef> Levels => _levels ??= Build();

        public static int Count => Levels.Count;

        static List<LevelDef> Build()
        {
            var list = new List<LevelDef>();

            // 1 — First Flight
            var l1 = new LevelDef
            {
                Name = "First Flight",
                Description = "Lift off, drift east and set down gently on the green pad.",
                FuelCapacity = 1000f, ParTime = 12f, StarFuel = new Vector2(0.3f, 0.55f),
                BoundsMin = new Vector2(-15f, -3f), BoundsMax = new Vector2(50f, 22f),
                LandingPad = new Vector3(28f, 0.3f, 0f), LandingPadSize = new Vector3(7f, 0.6f, 4f),
            };
            l1.Block(BlockKind.Ground, 20f, -1f, 80f, 2f, 8f)
              .Block(BlockKind.Scenery, 14f, 1.5f, 8f, 5f, 6f, 6f)
              .Block(BlockKind.Scenery, 40f, 1f, 4f, 3f, 4f, 5f);
            list.Add(l1);

            // 2 — Narrow Corridor
            var l2 = new LevelDef
            {
                Name = "Narrow Corridor",
                Description = "Thread the gap between floor and ceiling without touching the walls.",
                FuelCapacity = 100f, ParTime = 15f, StarFuel = new Vector2(0.25f, 0.5f),
                BoundsMax = new Vector2(60f, 26f),
                LandingPad = new Vector3(46f, 0.3f, 0f),
            };
            l2.Block(BlockKind.Ground, 25f, -1f, 90f, 2f, 8f)
              .Block(BlockKind.Wall, 23f, 2f, 18f, 4f, 6f)
              .Block(BlockKind.Ceiling, 23f, 18.169556f, 18f, 16f, 6f)
              .Block(BlockKind.Ceiling, 25f, 26f, 90f, 2f, 8f);
            list.Add(l2);

            // 3 — Moving Barrier
            var l3 = new LevelDef
            {
                Name = "Moving Barrier",
                Description = "The lane flashes before the block moves: wait for the warning and grab the fuel capsule.",
                FuelCapacity = 100f, ParTime = 18f, StarFuel = new Vector2(0.2f, 0.45f),
                BoundsMin = new Vector2(-15f, -3f), BoundsMax = new Vector2(58f, 27f),
                LandingPad = new Vector3(42f, 3.3f, 0f),
            };
            l3.Block(BlockKind.Ground, 25f, -1f, 90f, 2f, 8f)
              .Block(BlockKind.Ceiling, 20f, 19f, 3f, 14f, 6f)
              .Block(BlockKind.Ceiling, 25f, 27f, 90f, 2f, 8f)
              .Block(BlockKind.Tower, 31f, 5f, 1.5f, 10f, 1.5f)
              .Block(BlockKind.Rock, 42f, 1.5f, 10f, 3f, 6f)
              .Obstacle(new ObstacleDef
              {
                  Kind = ObstacleKind.MovingBlock, Position = new Vector3(20f, 2.5f, 0f),
                  Travel = new Vector3(0f, 7f, 0f), MoveTime = 1.4f, PauseTime = 1.6f, StartDelay = 1f,
              })
              .Obstacle(new ObstacleDef { Kind = ObstacleKind.FuelCapsule, Position = new Vector3(25.5f, 8f, 0f), FuelAmount = 20f });
            list.Add(l3);

            // 4 — Fuel Saver
            var l4 = new LevelDef
            {
                Name = "Fuel Saver",
                Description = "Fuel is scarce: ride the tailwind over the pit and grab the capsule above the pillar.",
                FuelCapacity = 50f, ParTime = 9f, StarFuel = new Vector2(0.2f, 0.35f),
                BoundsMin = new Vector2(-12f, -8f), BoundsMax = new Vector2(70f, 24f),
                LandingPad = new Vector3(54f, 0.3f, 0f),
            };
            l4.Block(BlockKind.Ground, 6f, -1f, 28f, 2f, 8f)
              .Block(BlockKind.Ground, 52f, -1f, 28f, 2f, 8f)
              .Block(BlockKind.HazardFloor, 29f, -4f, 18f, 2f, 8f)
              .Block(BlockKind.Rock, 29f, 1f, 3f, 8f, 6f)
              .Obstacle(new ObstacleDef
              {
                  Kind = ObstacleKind.Wind, Position = new Vector3(29f, 11f, 0f),
                  Size = new Vector3(16f, 8f, 4f), Strength = 5f, GustAmount = 0.25f,
              })
              .Obstacle(new ObstacleDef { Kind = ObstacleKind.FuelCapsule, Position = new Vector3(29f, 7.2f, 0f), FuelAmount = 15f });
            list.Add(l4);

            // 5 — All Together
            var l5 = new LevelDef
            {
                Name = "All Together",
                Description = "Corridor, moving barrier, hazard tower and a high pad — all in one flight.",
                FuelCapacity = 70f, ParTime = 30f, StarFuel = new Vector2(0.15f, 0.3f),
                BoundsMax = new Vector2(75f, 30f),
                LandingPad = new Vector3(58f, 6.3f, 0f),
            };
            l5.Block(BlockKind.Ground, 35f, -1f, 110f, 2f, 8f)
              .Block(BlockKind.Ceiling, 35f, 30f, 110f, 2f, 8f)
              .Block(BlockKind.Wall, 16f, 2f, 14f, 4f, 6f)
              .Block(BlockKind.Ceiling, 16f, 19f, 14f, 20f, 6f)
              .Block(BlockKind.Ceiling, 32f, 20.5f, 3f, 17f, 6f)
              .Block(BlockKind.Tower, 42f, 6f, 1.5f, 12f, 1.5f)
              .Block(BlockKind.Rock, 58f, 3f, 10f, 6f, 6f)
              .Block(BlockKind.Ceiling, 58f, 17f, 16f, 4f, 6f)
              .Obstacle(new ObstacleDef
              {
                  Kind = ObstacleKind.MovingBlock, Position = new Vector3(32f, 2.5f, 0f),
                  Travel = new Vector3(0f, 6.5f, 0f), MoveTime = 1.2f,
              })
              .Obstacle(new ObstacleDef { Kind = ObstacleKind.Laser, Position = new Vector3(8.6f, 6.5f, 0f), RotationZ = 90f, BeamLength = 5f })
              .Obstacle(new ObstacleDef { Kind = ObstacleKind.Blade, Position = new Vector3(37.5f, 19.5f, 0f), SpeedDegrees = 110f, BladeLength = 2.6f });
            list.Add(l5);

            return list;
        }
    }
}
