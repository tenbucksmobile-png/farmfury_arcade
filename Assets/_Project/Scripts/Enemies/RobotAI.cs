using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>
    /// Stateless pathing helpers shared by every RobotBase subclass. Always wall-respecting
    /// (queries TileMapRenderer.IsWalkable) — DroneRobot deliberately does not use this class
    /// since its whole point is ignoring walls; see DroneRobot.ComputeDesiredDirection.
    /// </summary>
    public static class RobotAI
    {
        private static readonly Direction[] AllDirections =
        {
            Direction.Up, Direction.Down, Direction.Left, Direction.Right
        };

        /// <summary>Weighted-random directional choice: among the walkable, non-reversing
        /// directions from currentPos, each is weighted by how close its neighbour cell would land
        /// to targetPos (inverse-square — the closer option is heavily favoured but not guaranteed),
        /// then one is picked via a weighted roll. Used to be a purely greedy "always pick the
        /// single closest direction" choice, which is fully deterministic given the same relative
        /// position — in open areas (few walkable directions, similar distances) that read as
        /// robots "falling into a loop of going in one line" once they settled into a state, since
        /// the exact same tie/near-tie resolves identically every tick. This keeps movement clearly
        /// seeking the target most of the time while no longer being perfectly predictable. Falls
        /// back to reversing only when currentPos is a dead end.</summary>
        public static Direction GetNextDirection(Vector2Int currentPos, Vector2Int targetPos, Direction currentDir, TileMapRenderer maze)
        {
            Direction[] valid = GetValidDirections(currentPos, currentDir, maze);
            if (valid.Length == 0)
            {
                return Direction.None;
            }
            if (valid.Length == 1)
            {
                return valid[0];
            }

            var weights = new float[valid.Length];
            float totalWeight = 0f;
            for (int i = 0; i < valid.Length; i++)
            {
                Vector2Int next = currentPos + DirectionUtils.ToVector(valid[i]);
                Vector2Int delta = next - targetPos;
                int distSqr = delta.x * delta.x + delta.y * delta.y;
                weights[i] = 1f / (1 + distSqr);
                totalWeight += weights[i];
            }

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < valid.Length; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return valid[i];
                }
            }

            return valid[valid.Length - 1];
        }

        /// <summary>Walkable directions from pos, excluding the reverse of the current direction
        /// (the "cannot reverse mid-corridor" rule) unless reversing is the only walkable option
        /// (dead end).</summary>
        public static Direction[] GetValidDirections(Vector2Int pos, Direction excluding, TileMapRenderer maze)
        {
            Direction reverse = DirectionUtils.Opposite(excluding);
            var result = new List<Direction>(4);

            foreach (var dir in AllDirections)
            {
                if (excluding != Direction.None && dir == reverse)
                {
                    continue;
                }

                if (maze.IsWalkable(pos + DirectionUtils.ToVector(dir)))
                {
                    result.Add(dir);
                }
            }

            if (result.Count == 0 && excluding != Direction.None && maze.IsWalkable(pos + DirectionUtils.ToVector(reverse)))
            {
                result.Add(reverse);
            }

            return result.ToArray();
        }
    }
}
