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

        /// <summary>Deterministic-greedy directional choice: among the walkable, non-reversing
        /// directions from currentPos, always picks whichever candidate's neighbour cell has the
        /// REAL shortest-path distance to targetPos (a BFS distance, not straight-line — see
        /// ComputeDistances). recentCells only breaks ties among candidates that are EQUALLY close
        /// (preferring a non-recent one over a recently-visited one); genuine ties with no
        /// non-recent option, or ties where recency doesn't distinguish them, are broken by a random
        /// pick among the tied set — never among a worse one.
        ///
        /// This used to be a weighted-random roll (each candidate's distance mapped to a weight via
        /// 1/(1+dist^2), one picked by rolling against the total) rather than a hard pick — which
        /// looked like it should self-correct over time, but a weighted roll still lets RNG choose a
        /// strictly WORSE candidate on any given call. At a two-way intersection with a clear best
        /// option 1 tile away vs. a worse one 3 tiles away, the "worse" pick still had roughly a
        /// 1-in-10 chance every single visit (weight 0.1 vs 0.5, i.e. ~17%) — over a maze with many
        /// intersections in a busy area (e.g. a level's top row, packed with pellets and several
        /// robots crossing it constantly), that adds up to frequent wrong turns, which read exactly
        /// like "stuck looping in the top row" even though the underlying distance math was already
        /// correct. A recentCells weight penalty (tried at both 0.15 and 0.05) narrowed the odds but
        /// could never fully close them, since it only ever discounted a weight rather than removing
        /// the chance of picking it. Committing to the actual best candidate removes that risk
        /// entirely — the only remaining randomness is among genuinely equally-good options, where
        /// any pick is correct by definition.
        ///
        /// Distance weighting itself is unchanged from the original BFS-distance fix: an earlier
        /// straight-line (Euclidean) heuristic biased robots toward "continue straight" even when a
        /// perpendicular branch was the true shorter route, which is what first caused corridor
        /// oscillation before BFS distance replaced it. That fix stays — this change only replaces
        /// how a candidate's distance gets turned into an actual choice.</summary>
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

            var candidateDistances = new int[valid.Length];
            int bestDistance = int.MaxValue;
            for (int i = 0; i < valid.Length; i++)
            {
                Vector2Int next = currentPos + DirectionUtils.ToVector(valid[i]);
                int dist = distances.TryGetValue(next, out int pathDist) ? pathDist : StraightLineDistanceSqr(next, targetPos);
                candidateDistances[i] = dist;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                }
            }

            var best = new List<Direction>(valid.Length);
            var bestNonRecent = new List<Direction>(valid.Length);
            for (int i = 0; i < valid.Length; i++)
            {
                if (candidateDistances[i] != bestDistance)
                {
                    continue;
                }
                best.Add(valid[i]);
                Vector2Int next = currentPos + DirectionUtils.ToVector(valid[i]);
                if (recentCells == null || !recentCells.Contains(next))
                {
                    bestNonRecent.Add(valid[i]);
                }
            }

            var winners = bestNonRecent.Count > 0 ? bestNonRecent : best;
            return winners[Random.Range(0, winners.Count)];
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
