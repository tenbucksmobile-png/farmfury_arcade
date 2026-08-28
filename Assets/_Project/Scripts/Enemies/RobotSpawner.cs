using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Enemies
{
    /// <summary>
    /// Reads LevelData.robotSpawns and instantiates each robot at its spawn position after its
    /// spawnDelay, wired to the matching RobotData asset. Each spawned RobotBase then owns its own
    /// full state-machine lifecycle (Chase/Scatter/Vulnerable/Defeated/Returning/respawn) — this
    /// class only handles bringing robots into the maze and, on player death, resetting the ones
    /// already spawned back to their factory cell.
    /// </summary>
    public class RobotSpawner : MonoBehaviour
    {
        [SerializeField] private Transform robotParent;
        [SerializeField] private TileMapRenderer tileMap;
        [SerializeField] private GameObject harvesterPrefab;
        [SerializeField] private GameObject scoutPrefab;
        [SerializeField] private GameObject patrolPrefab;
        [SerializeField] private GameObject drifterPrefab;
        [SerializeField] private GameObject heavyPrefab;
        [SerializeField] private GameObject dronePrefab;

        /// <summary>Applied to every robot this spawner creates from here on (SpawnRobot reads it
        /// at spawn time, not just once) — GameManager.LoadLevel sets this right before calling
        /// SceneController.LoadLevelContent, so it's already correct by the time SpawnLevelRobots
        /// actually instantiates anything. 1f for a normal level; DailyChallengeManager.
        /// RobotDifficultySpeedMultiplier for a Daily Challenge run. Left at whatever the previous
        /// LoadLevel call set until the next one changes it — GameManager always sets it explicitly
        /// on every LoadLevel, normal or otherwise, so it never goes stale.</summary>
        public float DifficultyMultiplier = 1f;

        private readonly List<RobotBase> _activeRobots = new List<RobotBase>();
        private LevelData _level;

        public IReadOnlyList<RobotBase> ActiveRobots => _activeRobots;

        public void SpawnLevelRobots(LevelData level)
        {
            ClearRobots();
            _level = level;

            // Audit finding C7.6: ChaseScoreManager's "all robots defeated on one pellet" +5,000
            // bonus used to be hardcoded to fire at exactly 4 chain-defeats — but the difficulty
            // curve spawns as few as 2 robots on a level's earliest band, where the bonus could
            // never mathematically fire, and as many as 5 on the hardest band, where it fired one
            // robot early. Set from level.robotSpawns.Length directly (one spawn definition per
            // robot, known immediately — well before any of the staggered spawn coroutines below
            // could possibly result in a defeat) rather than from _activeRobots.Count, which is
            // still 0 at this point since spawning itself is delayed.
            ChaseScoreManager.Instance?.SetTotalRobotsThisMaze(level.robotSpawns?.Length ?? 0);

            if (level.robotSpawns == null)
            {
                return;
            }

            foreach (var spawn in level.robotSpawns)
            {
                StartCoroutine(SpawnAfterDelay(spawn));
            }
        }

        public void ClearRobots()
        {
            StopAllCoroutines();
            foreach (var robot in _activeRobots)
            {
                if (robot != null)
                {
                    Destroy(robot.gameObject);
                }
            }
            _activeRobots.Clear();
        }

        /// <summary>Called by PlayerHealth's death sequence — score is kept, but every robot
        /// already in the maze snaps back to its spawn cell in Chase state.</summary>
        public void ResetAllRobotsToFactory()
        {
            foreach (var robot in _activeRobots)
            {
                if (robot != null)
                {
                    robot.ResetToFactory();
                }
            }
        }

        private IEnumerator SpawnAfterDelay(RobotSpawnData spawn)
        {
            if (spawn.spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawn.spawnDelay);
            }
            SpawnRobot(spawn);
        }

        private void SpawnRobot(RobotSpawnData spawn)
        {
            GameObject prefab = GetPrefabFor(spawn.robotType);
            if (prefab == null)
            {
                Debug.LogWarning($"[RobotSpawner] No prefab assigned for {spawn.robotType}; skipping spawn.");
                return;
            }

            Vector3 worldPos = tileMap.GridToWorld(spawn.spawnPosition);
            var go = Instantiate(prefab, worldPos, Quaternion.identity, robotParent);
            var robot = go.GetComponent<RobotBase>();
            if (robot == null)
            {
                Debug.LogError($"[RobotSpawner] Prefab for {spawn.robotType} has no RobotBase component.");
                Destroy(go);
                return;
            }

            var data = DataManager.Instance.GetRobotData(spawn.robotType);
            if (data == null)
            {
                // Audit finding C8.1 — RobotBase.Initialize already degrades gracefully to
                // fallback health/speed on a null RobotData, so this isn't a crash risk; without
                // this log it was a silent degrade with zero signal to notice in a playtest.
                Debug.LogWarning($"[RobotSpawner] No RobotData found for {spawn.robotType} — spawning with fallback stats.");
            }
            robot.Initialize(data, tileMap, spawn.spawnPosition, GetScatterCorner(spawn.robotType));
            robot.SetDifficultyMultiplier(DifficultyMultiplier);
            _activeRobots.Add(robot);
            // PlayRobotRespawnSfx used to fire only from a defeated robot's mid-level walk-back to
            // the factory (RobotBase.ArriveAtFactory) — that flow no longer exists (see RobotBase's
            // Disappear()/IsPermanentlyDefeated doc comments), so this is now the only spawn event
            // left to play "RobotSpawn.mp3" against, level-start spawns included.
            AudioManager.Instance?.PlayRobotRespawnSfx();
        }

        private GameObject GetPrefabFor(RobotType type)
        {
            return type switch
            {
                RobotType.Harvester => harvesterPrefab,
                RobotType.Scout => scoutPrefab,
                RobotType.Patrol => patrolPrefab,
                RobotType.Drifter => drifterPrefab,
                RobotType.Heavy => heavyPrefab,
                RobotType.Drone => dronePrefab,
                _ => null
            };
        }

        /// <summary>Classic four-corner scatter targets, inset by 1 tile from the border walls.</summary>
        private Vector2Int GetScatterCorner(RobotType type)
        {
            int w = _level.mazeWidth;
            int h = _level.mazeHeight;

            return type switch
            {
                RobotType.Harvester => new Vector2Int(w - 2, h - 2), // top-right
                RobotType.Scout => new Vector2Int(1, h - 2),          // top-left
                RobotType.Patrol => new Vector2Int(w - 2, 1),         // bottom-right
                RobotType.Drifter => new Vector2Int(1, 1),            // bottom-left
                _ => new Vector2Int(w / 2, h - 2)
            };
        }
    }
}
