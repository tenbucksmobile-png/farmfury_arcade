using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Instantiates/tears down level content for the single Game scene. Maze tiles, crops, and
    /// power pellets are delegated to TileMapRenderer (same GameObject); player spawning is
    /// delegated to CharacterManager (Phase 4 — it owns character swapping too, so it's the
    /// single place that creates/destroys the player GameObject) and robot spawning to
    /// RobotSpawner (same GameObject).
    /// </summary>
    public class SceneController : MonoBehaviour
    {
        [SerializeField] private Transform robotParent;
        [SerializeField] private RobotSpawner robotSpawner;

        private TileMapRenderer _tileMapRenderer;

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
            ComboSystem.Instance?.ResetForNewMaze();
            PowerPelletManager.Instance?.ResetForNewMaze();
            CharacterManager.Instance?.SpawnInitialCharacter(CharacterType.Cluck, level.playerStartPosition);
            robotSpawner?.SpawnLevelRobots(level);
        }

        public void ClearLevelContent()
        {
            _tileMapRenderer.ClearMaze();
            robotSpawner?.ClearRobots();
            CharacterManager.Instance?.ClearActiveCharacter();
        }
    }
}
