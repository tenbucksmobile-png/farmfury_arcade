using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Wall-ignoring straight-line pursuit at half speed. Unlike every other robot, Drone
    /// deliberately does not use RobotAI (which is always wall-respecting) — it picks whichever of
    /// the 4 grid directions is closest to its target using only bounds/border checks, so it drifts
    /// straight through INNER walls toward the player. It still can't fly through the maze's
    /// outer border wall ring, though — "flies over walls" means the walls inside the maze, not an
    /// escape route off the playable board. IsBorderCell treats any cell on row/column 0 or
    /// MazeWidth-1/MazeHeight-1 as border regardless of its actual tile id, so this also still
    /// respects a border warp-tunnel opening correctly (those are already walkable via the normal
    /// tileMap.IsWalkable check, which IsWalkableForThisRobot tries first). Visual hover/
    /// transparency is left to RobotVisual/art, not movement code.</summary>
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

        private bool IsBorderCell(Vector2Int cell)
        {
            return cell.x <= 0 || cell.y <= 0 || cell.x >= tileMap.MazeWidth - 1 || cell.y >= tileMap.MazeHeight - 1;
        }

        /// <summary>Normally walkable cells (open floor, or a border warp-tunnel opening) are
        /// allowed as-is; a wall cell is only allowed if it's NOT on the border — that's the actual
        /// "flies over walls" ability, scoped to the maze's interior.</summary>
        protected override bool IsWalkableForThisRobot(Vector2Int cell)
        {
            if (!tileMap.IsInBounds(cell))
            {
                return false;
            }
            if (tileMap.IsWalkable(cell))
            {
                return true;
            }
            return !IsBorderCell(cell);
        }

        protected override Direction ComputeDesiredDirection(Vector2Int cell)
        {
            Vector2Int target = ResolveTarget();
            Direction best = CurrentDirection;
            int bestDistSqr = int.MaxValue;

            foreach (var dir in AllDirections)
            {
                Vector2Int next = cell + DirectionUtils.ToVector(dir);
                if (!IsWalkableForThisRobot(next))
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
