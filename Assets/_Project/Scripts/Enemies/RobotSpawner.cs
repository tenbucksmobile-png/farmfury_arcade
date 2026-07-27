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

        private readonly List<RobotBase> _activeRobots = new List<RobotBase>();
        private LevelData _level;

        public IReadOnlyList<RobotBase> ActiveRobots => _activeRobots;

        public void SpawnLevelRobots(LevelData level)
        {
            ClearRobots();
            _level = level;

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
            robot.Initialize(data, tileMap, spawn.spawnPosition, GetScatterCorner(spawn.robotType));
            _activeRobots.Add(robot);
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
