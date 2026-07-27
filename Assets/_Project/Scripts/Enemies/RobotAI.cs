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

        /// <summary>Greedy directional choice: among the walkable, non-reversing directions from
        /// currentPos, pick the one whose neighbour cell is closest (straight-line) to targetPos.
        /// Falls back to reversing only when currentPos is a dead end.</summary>
        public static Direction GetNextDirection(Vector2Int currentPos, Vector2Int targetPos, Direction currentDir, TileMapRenderer maze)
        {
            Direction[] valid = GetValidDirections(currentPos, currentDir, maze);
            if (valid.Length == 0)
            {
                return Direction.None;
            }

            Direction best = valid[0];
            int bestDistSqr = int.MaxValue;
            foreach (var dir in valid)
            {
                Vector2Int next = currentPos + DirectionUtils.ToVector(dir);
                Vector2Int delta = next - targetPos;
                int distSqr = delta.x * delta.x + delta.y * delta.y;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = dir;
                }
            }

            return best;
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
