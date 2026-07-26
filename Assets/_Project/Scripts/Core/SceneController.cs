using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Instantiates/tears down level content for the single Game scene. Maze tiles, crops, and
    /// power pellets are delegated to TileMapRenderer (same GameObject); this class spawns the
    /// player character and (placeholder, Phase 3 will replace) robot markers.
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        [SerializeField] private Transform characterParent;
        [SerializeField] private Transform robotParent;
        [SerializeField] private GameObject cluckPrefab;

        private TileMapRenderer _tileMapRenderer;
        private GameObject _spawnedPlayer;
        private GameObject _spawnedRobotMarkers;

        private void Awake()
        {
            _tileMapRenderer = GetComponent<TileMapRenderer>();
        }

        public void LoadLevelContent(LevelData level)
        {
            ClearLevelContent();

            if (level == null)
            {
                Debug.LogWarning("[SceneController] LoadLevelContent called with null LevelData.");
                return;
            }

            _tileMapRenderer.RenderMaze(level);
            SpawnPlayer(level);
            SpawnRobotPlaceholders(level);
        }

        public void ClearLevelContent()
        {
            _tileMapRenderer.ClearMaze();

            if (_spawnedPlayer != null)
            {
                Destroy(_spawnedPlayer);
                _spawnedPlayer = null;
            }

            if (_spawnedRobotMarkers != null)
            {
                Destroy(_spawnedRobotMarkers);
                _spawnedRobotMarkers = null;
            }
        }

        private void SpawnPlayer(LevelData level)
        {
            if (cluckPrefab == null)
            {
                Debug.LogWarning("[SceneController] No Cluck prefab assigned; skipping player spawn.");
                return;
            }

            Vector3 worldPos = _tileMapRenderer.GridToWorld(level.playerStartPosition);
            _spawnedPlayer = Instantiate(cluckPrefab, worldPos, Quaternion.identity, characterParent);

            var characterData = DataManager.Instance.GetCharacterData(CharacterType.Cluck);
            if (characterData == null)
            {
                return;
            }

            _spawnedPlayer.GetComponent<CharacterAnimator>()?.SetCharacterData(characterData);
            var movement = _spawnedPlayer.GetComponent<GridMovement>();
            if (movement != null)
            {
                movement.SetSpeed(characterData.movementSpeed);
            }
        }

        /// <summary>Phase 1/2 placeholder only — Phase 3 replaces this with real robot prefabs
        /// and AI. Currently a no-op if LevelData.robotSpawns is empty (Phase 2's LevelData_01
        /// intentionally has none, per spec).</summary>
        private void SpawnRobotPlaceholders(LevelData level)
        {
            if (level.robotSpawns == null || level.robotSpawns.Length == 0)
            {
                return;
            }

            _spawnedRobotMarkers = new GameObject("RobotPlaceholders");
            _spawnedRobotMarkers.transform.SetParent(robotParent, false);

            var robotSprite = PlaceholderSprite.Get(Color.red);
            foreach (var spawn in level.robotSpawns)
            {
                var marker = new GameObject($"Robot_{spawn.robotType}");
                marker.transform.SetParent(_spawnedRobotMarkers.transform, false);
                marker.transform.localPosition = _tileMapRenderer.GridToWorld(spawn.spawnPosition);
                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = robotSprite;
            }
        }
    }
}
