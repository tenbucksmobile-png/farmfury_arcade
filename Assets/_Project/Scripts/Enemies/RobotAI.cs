using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Weight multiplier applied to a candidate direction whose destination cell
        /// appears in the robot's own recentCells history — not a hard ban (a short dead-end loop
        /// might have no other option, and GetValidDirections' own reversal fallback already
        /// guarantees at least one option always exists), just heavily discouraged so the weighted
        /// roll below prefers genuinely new ground. Tightened from 0.15 — the distance weighting
        /// below is an inverse-square (1/(1+dist^2)), so a very close recent cell could still
        /// out-weight a much farther genuinely-better one even after the 0.15 penalty (e.g. a
        /// recent cell 1 tile away scored 0.5*0.15=0.075, beating a fresh cell 4 tiles away at
        /// 0.0588) — still reported as robots looping in tight sections even after the BFS-distance
        /// fix. 0.05 keeps the same recent cell's score (0.5*0.05=0.025) below that fresh cell's,
        /// so real progress genuinely wins in this case while still not being a hard ban.</summary>
        private const float RecentCellWeightPenalty = 0.05f;

        /// <summary>Weighted-random directional choice: among the walkable, non-reversing
        /// directions from currentPos, each is weighted by its neighbour cell's REAL shortest-path
        /// distance to targetPos (inverse-square of a BFS distance, not straight-line — see
        /// ComputeDistances), then one is picked via a weighted roll.
        ///
        /// This used to weight by straight-line (Euclidean) distance to targetPos instead of the
        /// true path distance. That reads fine in open areas, but a maze has long straight
        /// corridors — whenever the target sat further down the SAME row/column a robot was already
        /// travelling along, continuing straight always scored as "closer" by straight-line distance
        /// even at intersections where turning onto a perpendicular corridor was the actual shorter
        /// route to the target (or the only route at all, if the straight corridor was itself a dead
        /// end further along). The robot would then keep re-choosing "continue straight" every time,
        /// bounce off the corridor's ends (the no-U-turn rule only forces a reversal at an actual
        /// dead end), and permanently oscillate within that one row/column — reported as robots
        /// "getting stuck in a row and looping, never moving away from it." Confirmed level-agnostic:
        /// any maze that recently gained a long peripheral corridor (or already had one) can trigger
        /// it, so this needed a genuine pathing fix in the shared method every robot type but Drone
        /// funnels through, not a per-level workaround.
        ///
        /// Using each candidate's real BFS distance to targetPos instead removes that structural
        /// bias entirely: a perpendicular branch that's genuinely closer via the maze's actual
        /// connectivity now scores as closer, so a robot correctly turns off a long corridor exactly
        /// when doing so shortens its real route — including while fleeing (Vulnerable state), where
        /// targetPos is now always a real, reachable, far-away cell (see RobotBase.GetFleeTarget /
        /// FindFarthestCell) rather than a straight-line projection that could sit outside the maze
        /// entirely and only ever bias the same Euclidean heuristic.
        ///
        /// recentCells (RobotBase's own short rolling history of the last few cells it occupied) is
        /// still applied as an extra weight penalty on top of the distance weighting, as a second
        /// line of defence against short back-and-forth cycles between two similarly-distant
        /// intersections that real BFS distance alone doesn't fully rule out (e.g. two branches
        /// genuinely equidistant from targetPos). Optional/nullable so call sites without a history
        /// (or DroneRobot, which bypasses this class entirely) don't need to change.</summary>
        public static Direction GetNextDirection(Vector2Int currentPos, Vector2Int targetPos, Direction currentDir, TileMapRenderer maze, IReadOnlyCollection<Vector2Int> recentCells = null)
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

            Dictionary<Vector2Int, int> distances = ComputeDistances(targetPos, maze);

            var weights = new float[valid.Length];
            float totalWeight = 0f;
            for (int i = 0; i < valid.Length; i++)
            {
                Vector2Int next = currentPos + DirectionUtils.ToVector(valid[i]);
                int dist = distances.TryGetValue(next, out int pathDist) ? pathDist : StraightLineDistanceSqr(next, targetPos);
                weights[i] = 1f / (1 + dist * dist);
                if (recentCells != null && recentCells.Contains(next))
                {
                    weights[i] *= RecentCellWeightPenalty;
                }
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

        /// <summary>Breadth-first search over walkable cells (same wall-respecting rule
        /// GetValidDirections uses) giving every reachable cell's true shortest-path distance from
        /// start in tile steps. Returns an empty map if start itself isn't walkable — callers fall
        /// back to straight-line distance for any cell missing from the result, which degrades to
        /// the old Euclidean heuristic rather than throwing (targetPos should always be a real
        /// walkable cell in practice — player position, a scatter corner, the factory, or a
        /// BFS-found farthest cell — but per-robot Chase targets like Scout's "N tiles ahead of
        /// facing" projection can land on a wall or off the grid).</summary>
        private static Dictionary<Vector2Int, int> ComputeDistances(Vector2Int start, TileMapRenderer maze)
        {
            var distances = new Dictionary<Vector2Int, int>();
            if (!maze.IsWalkable(start))
            {
                return distances;
            }

            distances[start] = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                int nextDist = distances[cur] + 1;
                foreach (var dir in AllDirections)
                {
                    Vector2Int next = cur + DirectionUtils.ToVector(dir);
                    if (distances.ContainsKey(next) || !maze.IsWalkable(next))
                    {
                        continue;
                    }
                    distances[next] = nextDist;
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        private static int StraightLineDistanceSqr(Vector2Int a, Vector2Int b)
        {
            Vector2Int delta = a - b;
            return delta.x * delta.x + delta.y * delta.y;
        }

        /// <summary>The walkable cell with the greatest true shortest-path distance from `from`
        /// (ties broken by whichever BFS happens to reach last). Used by RobotBase.GetFleeTarget so
        /// a Vulnerable robot's flee target is always a real, reachable cell — genuinely the
        /// farthest point in the maze from the player — instead of a straight-line projection that
        /// could land outside the maze bounds entirely and only ever bias the old Euclidean
        /// weighting in GetNextDirection.</summary>
        public static Vector2Int FindFarthestCell(Vector2Int from, TileMapRenderer maze)
        {
            Dictionary<Vector2Int, int> distances = ComputeDistances(from, maze);
            Vector2Int farthest = from;
            int best = -1;
            foreach (var kvp in distances)
            {
                if (kvp.Value > best)
                {
                    best = kvp.Value;
                    farthest = kvp.Key;
                }
            }
            return farthest;
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
