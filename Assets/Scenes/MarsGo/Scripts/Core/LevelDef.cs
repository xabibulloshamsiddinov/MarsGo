using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    public enum BlockKind { Ground, Wall, Ceiling, Rock, Tower, HazardFloor, Scenery }

    public enum ObstacleKind { MovingBlock, FuelCapsule, Wind, Laser, Blade }

    /// <summary>A static box in the level. Centre/size in metres; the flight plane is z = 0.</summary>
    public struct BlockDef
    {
        public BlockKind Kind;
        public Vector3 Center;
        public Vector3 Size;

        public BlockDef(BlockKind kind, Vector3 center, Vector3 size)
        {
            Kind = kind;
            Center = center;
            Size = size;
        }
    }

    /// <summary>A moving / interactive obstacle (built from Phase 2 on).</summary>
    public class ObstacleDef
    {
        public ObstacleKind Kind;
        public Vector3 Position;
        public float RotationZ;
        public Vector3 Travel;
        public float MoveTime = 1.5f;
        public float PauseTime = 1.2f;
        public float StartDelay;
        public float FuelAmount = 25f;
        public Vector3 Size = new Vector3(6f, 6f, 4f);
        public float Strength = 4f;
        public float GustAmount = 0.2f;
        public float BeamLength = 6f;
        public float SpeedDegrees = 90f;
        public float BladeLength = 2.5f;
    }

    /// <summary>Everything a level needs (the Godot LevelData + level .tscn layout in one object).</summary>
    public class LevelDef
    {
        public string Name = "Level";
        public string Description = "";
        public float FuelCapacity = 100f;
        public float ParTime = 20f;
        /// <summary>Fuel left (0..1) needed for the 2nd and 3rd star.</summary>
        public Vector2 StarFuel = new Vector2(0.2f, 0.4f);

        public Vector3 Spawn = new Vector3(0f, 2.4f, 0f);
        public Vector2 BoundsMin = new Vector2(-15f, -3f);
        public Vector2 BoundsMax = new Vector2(45f, 25f);
        public float DeathZoneY = -14f;

        public Vector3 LaunchPad = new Vector3(0f, 0.3f, 0f);
        public Vector3 LaunchPadSize = new Vector3(5f, 0.6f, 4f);
        public Vector3 LandingPad = new Vector3(25f, 0.3f, 0f);
        public Vector3 LandingPadSize = new Vector3(6f, 0.6f, 4f);

        public readonly List<BlockDef> Blocks = new List<BlockDef>();
        public readonly List<ObstacleDef> Obstacles = new List<ObstacleDef>();

        /// <summary>1 star = any landing; 2 = enough fuel; 3 = more fuel AND under par time.</summary>
        public int CalculateStars(float time, float fuelRatio)
        {
            int stars = 1;
            if (fuelRatio >= StarFuel.x)
            {
                stars++;
                if (fuelRatio >= StarFuel.y && time <= ParTime) stars++;
            }
            return Mathf.Min(stars, 3);
        }

        public LevelDef Block(BlockKind kind, float x, float y, float sx, float sy, float sz, float z = 0f)
        {
            Blocks.Add(new BlockDef(kind, new Vector3(x, y, z), new Vector3(sx, sy, sz)));
            return this;
        }

        public LevelDef Obstacle(ObstacleDef def)
        {
            Obstacles.Add(def);
            return this;
        }
    }
}
