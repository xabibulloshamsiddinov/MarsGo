using UnityEngine;

namespace MarsGo
{
    /// <summary>Builds moving / interactive obstacles from their ObstacleDef.</summary>
    public static class ObstacleFactory
    {
        public static ObstacleBase Build(Transform parent, ObstacleDef def)
        {
            var go = new GameObject(def.Kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = def.Position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, def.RotationZ);

            switch (def.Kind)
            {
                case ObstacleKind.MovingBlock:
                {
                    var o = go.AddComponent<MovingBlockObstacle>();
                    o.travel = def.Travel;
                    o.moveTime = def.MoveTime;
                    o.pauseTime = def.PauseTime;
                    o.startDelay = def.StartDelay;
                    o.Build();
                    return o;
                }
                case ObstacleKind.FuelCapsule:
                {
                    var o = go.AddComponent<FuelCapsule>();
                    o.fuelAmount = def.FuelAmount;
                    o.Build();
                    return o;
                }
                case ObstacleKind.Wind:
                {
                    var o = go.AddComponent<WindArea>();
                    o.size = def.Size;
                    o.strength = def.Strength;
                    o.gustAmount = def.GustAmount;
                    o.Build();
                    return o;
                }
                case ObstacleKind.Laser:
                {
                    var o = go.AddComponent<LaserGate>();
                    o.beamLength = def.BeamLength;
                    o.Build();
                    return o;
                }
                case ObstacleKind.Blade:
                {
                    var o = go.AddComponent<SpinningBlade>();
                    o.speedDegrees = def.SpeedDegrees;
                    o.bladeLength = def.BladeLength;
                    o.Build();
                    return o;
                }
            }
            return null;
        }
    }
}
