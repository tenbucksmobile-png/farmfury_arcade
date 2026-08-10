using System.Collections;
using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Phase 2 verification harness — automated checks for spawn position, continuous movement,
    /// mid-corridor reversal blocking, crop/vegetable pickup scoring, warp tunnel teleport, and
    /// level completion, plus manual OnGUI movement buttons for human playtesting of "game feel"
    /// (intersections, wall collision, responsiveness) that isn't practical to fully automate.
    /// Not gameplay or UI — safe to delete once Phase 5 adds a real HUD/input harness.
    /// </summary>
    public class Phase2Test : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private GameObject _player;
        private GridMovement _movement;
        private TileMapRenderer _tileMap;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunVerification());
            }
        }

        [ContextMenu("Run Phase 2 Verification")]
        public void RunVerificationFromMenu()
        {
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            Debug.Log("[Phase2Test] --- Starting Phase 2 verification ---");

            GameManager.Instance.LoadLevel(0);
            yield return null;

            FindPlayer();
            VerifyPlayerSpawnPosition();

            yield return TestBasicMovement();
            yield return TestReversalAllowedImmediately();
            yield return TestCropAndVegetablePickup();
            yield return TestWarpTunnel();
            TestLevelCompletion();

            Debug.Log("[Phase2Test] --- Phase 2 verification complete ---");
        }

        private void FindPlayer()
        {
            _movement = FindFirstObjectByType<GridMovement>();
            _player = _movement != null ? _movement.gameObject : null;
            _tileMap = FindFirstObjectByType<TileMapRenderer>();
        }

        private void VerifyPlayerSpawnPosition()
        {
            if (_player == null)
            {
                Debug.LogError("[Phase2Test] FAIL: no Cluck found in scene after LoadLevel(0).");
                return;
            }

            var expected = GameManager.Instance.CurrentLevel.playerStartPosition;
            bool ok = _movement.CurrentGridPosition == expected;
            Debug.Log(ok
                ? "[Phase2Test] PASS: Cluck spawned at playerStartPosition."
                : $"[Phase2Test] FAIL: Cluck at {_movement.CurrentGridPosition}, expected {expected}.");
        }

        private IEnumerator TestBasicMovement()
        {
            if (_movement == null) yield break;

            Vector2Int start = _movement.CurrentGridPosition;
            _movement.QueueInputDirection(Direction.Up);
            yield return WaitSeconds(1.0f);

            bool moved = _movement.CurrentGridPosition != start;
            Debug.Log(moved
                ? "[Phase2Test] PASS: Cluck moved continuously after a queued direction."
                : "[Phase2Test] FAIL: Cluck did not move after QueueInputDirection(Up).");
        }

        /// <summary>Hold-to-move rules (see GridMovement's own doc comment) allow a full reversal
        /// instantly regardless of corridor shape — this used to assert the opposite (reversal
        /// blocked in a plain 2-neighbour corridor) under the earlier auto-run model, which was
        /// removed per explicit feedback that players need an immediate 180 to escape a robot.</summary>
        private IEnumerator TestReversalAllowedImmediately()
        {
            if (_movement == null || _tileMap == null) yield break;

            Direction before = _movement.CurrentDirection;
            if (before == Direction.None)
            {
                Debug.LogWarning("[Phase2Test] SKIP reversal test: character is currently stopped.");
                yield break;
            }

            _movement.QueueInputDirection(DirectionUtils.Opposite(before));
            yield return WaitSeconds(0.15f);

            bool reversed = _movement.CurrentDirection == DirectionUtils.Opposite(before);
            Debug.Log(reversed
                ? "[Phase2Test] PASS: reversal took effect immediately, regardless of corridor shape."
                : "[Phase2Test] FAIL: reversal did not take effect — expected instant 180 under hold-to-move rules.");
        }


        private IEnumerator TestCropAndVegetablePickup()
        {
            if (_player == null || _tileMap == null) yield break;

            var level = GameManager.Instance.CurrentLevel;
            var layout = level.MazeLayout;

            // GridMovement is disabled for the duration of each isolated teleport check so the
            // character stays parked exactly on the target tile — otherwise it keeps obeying
            // whatever direction was still active from earlier tests and can drift off the tile
            // before the physics engine's next step samples the overlap.
            Vector2Int? kernelCell = FindFirstTile(layout, level.mazeWidth, level.mazeHeight, 2);
            if (kernelCell.HasValue)
            {
                int before = ScoreManager.Instance.CurrentMazeScore;
                _movement.enabled = false;
                _player.transform.position = _tileMap.GridToWorld(kernelCell.Value);
                yield return WaitSeconds(0.1f);
                _movement.enabled = true;
                bool ok = ScoreManager.Instance.CurrentMazeScore == before + 10;
                Debug.Log(ok
                    ? "[Phase2Test] PASS: kernel collected, +10 score."
                    : $"[Phase2Test] FAIL: expected +10 from kernel, score delta was {ScoreManager.Instance.CurrentMazeScore - before}.");
            }
            else
            {
                Debug.LogWarning("[Phase2Test] SKIP kernel test: no kernel (tile id 2) found in maze.");
            }

            Vector2Int? vegetableCell = FindFirstTile(layout, level.mazeWidth, level.mazeHeight, 3);
            if (vegetableCell.HasValue)
            {
                int before = ScoreManager.Instance.CurrentMazeScore;
                _movement.enabled = false;
                _player.transform.position = _tileMap.GridToWorld(vegetableCell.Value);
                yield return WaitSeconds(0.1f);
                _movement.enabled = true;
                bool ok = ScoreManager.Instance.CurrentMazeScore == before + 50;
                Debug.Log(ok
                    ? "[Phase2Test] PASS: vegetable collected, +50 score."
                    : $"[Phase2Test] FAIL: expected +50 from vegetable, score delta was {ScoreManager.Instance.CurrentMazeScore - before}.");
            }
            else
            {
                Debug.LogWarning("[Phase2Test] SKIP vegetable test: no vegetable (tile id 3) found in maze.");
            }
        }

        private IEnumerator TestWarpTunnel()
        {
            if (_player == null || _tileMap == null) yield break;

            var level = GameManager.Instance.CurrentLevel;
            if (level.warpTunnelRows == null || level.warpTunnelRows.Length == 0)
            {
                Debug.LogWarning("[Phase2Test] SKIP warp test: LevelData has no warpTunnelRows.");
                yield break;
            }

            int row = level.warpTunnelRows[0];
            Vector3 leftEdge = _tileMap.GridToWorld(new Vector2Int(0, row));
            Vector3 rightEdge = _tileMap.GridToWorld(new Vector2Int(level.mazeWidth - 1, row));

            _movement.enabled = false;
            _player.transform.position = leftEdge;
            yield return WaitSeconds(0.1f);
            _movement.enabled = true;

            bool warped = Vector3.Distance(_player.transform.position, rightEdge) < 0.1f;
            Debug.Log(warped
                ? "[Phase2Test] PASS: warp tunnel teleported Cluck to the opposite edge."
                : $"[Phase2Test] FAIL: Cluck at {_player.transform.position}, expected near {rightEdge}.");
        }

        private void TestLevelCompletion()
        {
            var level = GameManager.Instance.CurrentLevel;
            for (int i = 0; i < level.totalCropsRequired + 5; i++)
            {
                GameManager.Instance.NotifyCropCollected();
            }

            bool complete = GameManager.Instance.CurrentState == GameState.LevelComplete;
            Debug.Log(complete
                ? "[Phase2Test] PASS: level completion triggers once crops reach zero."
                : "[Phase2Test] FAIL: GameState did not reach LevelComplete.");
        }

        private static Vector2Int? FindFirstTile(int[,] layout, int width, int height, int tileId)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (layout[x, y] == tileId)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
            return null;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 110, 220, 160));
            GUILayout.Label("Phase 2 manual controls:");
            if (GUILayout.Button("Up")) _movement?.QueueInputDirection(Direction.Up);
            if (GUILayout.Button("Down")) _movement?.QueueInputDirection(Direction.Down);
            if (GUILayout.Button("Left")) _movement?.QueueInputDirection(Direction.Left);
            if (GUILayout.Button("Right")) _movement?.QueueInputDirection(Direction.Right);
            if (GUILayout.Button("Reload Level 0"))
            {
                GameManager.Instance.LoadLevel(0);
                FindPlayer();
            }
            GUILayout.EndArea();
        }
    }
}
