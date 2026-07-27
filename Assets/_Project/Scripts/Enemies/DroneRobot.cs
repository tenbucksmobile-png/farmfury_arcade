using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Wall-ignoring straight-line pursuit at half speed. Unlike every other robot, Drone
    /// deliberately does not use RobotAI (which is always wall-respecting) — it picks whichever of
    /// the 4 grid directions is closest to its target using only maze-bounds checks, so it drifts
    /// straight through walls toward the player. Visual hover/transparency is left to
    /// RobotVisual/art, not movement code.</summary>
    public class DroneRobot : RobotBase
    {
        private static readonly Direction[] AllDirections =
        {
            Direction.Up, Direction.Down, Direction.Left, Direction.Right
        };

        protected override float SpeedMultiplier => 0.5f;

        protected override Vector2Int GetTargetPosition()
        {
            return playerMovement != null ? playerMovement.CurrentGridPosition : CurrentGridPosition;
        }

        protected override bool IsWalkableForThisRobot(Vector2Int cell) => tileMap.IsInBounds(cell);

        protected override Direction ComputeDesiredDirection(Vector2Int cell)
        {
            Vector2Int target = ResolveTarget();
            Direction best = CurrentDirection;
            int bestDistSqr = int.MaxValue;

            foreach (var dir in AllDirections)
            {
                Vector2Int next = cell + DirectionUtils.ToVector(dir);
                if (!tileMap.IsInBounds(next))
                {
                    continue;
                }

                Vector2Int delta = next - target;
                int distSqr = delta.x * delta.x + delta.y * delta.y;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = dir;
                }
            }

            return best;
        }
    }
}
