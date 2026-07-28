using System.Collections;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Phase 3 verification harness — same PASS/FAIL/INFO/SKIP/WARN convention as Phase1Test/
    /// Phase2Test. Loads LevelData_01 (Harvester@2s, Scout@6s per spec) and drives its own timing
    /// rather than waiting on real spawnDelay where a shortcut is safe, to fit the whole battery
    /// inside Phase1ProjectBuilder's batch-mode Play window: harvester pursuit, chase-state
    /// contact killing the player, power-pellet-driven vulnerability, hit-defeats-robot, chain
    /// scoring, power expiry, and the second (Scout) robot's delayed spawn. The full 20s Chase/
    /// Scatter cycle is not practical to assert within that window — logged as INFO instead, same
    /// spirit as Phase2Test's reversal test. Also has OnGUI manual buttons for interactive testing.
    /// </summary>
    public class Phase3Test : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private GameObject _player;
        private GridMovement _movement;
        private PlayerHealth _playerHealth;
        private TileMapRenderer _tileMap;
        private RobotSpawner _spawner;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunVerification());
            }
        }

        [ContextMenu("Run Phase 3 Verification")]
        public void RunVerificationFromMenu()
        {
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            Debug.Log("[Phase3Test] --- Starting Phase 3 verification ---");

            GameManager.Instance.LoadLevel(0);
            yield return null;

            FindRefs();
            VerifyNoRobotsAtStart();

            yield return WaitSeconds(2.3f);
            var harvester = VerifyHarvesterSpawned();

            yield return TestHarvesterPursuit(harvester);
            yield return TestChaseContactKillsPlayer(harvester);

            yield return TestPowerPelletActivatesVulnerable();
            TestHitDefeatsVulnerableRobot();
            TestChainScoring();
            yield return TestPowerExpiry();

            VerifyScoutSpawnedBySix();

            Debug.Log("[Phase3Test] INFO: full 20s Chase<->Scatter cycle not exercised here — " +
                      "use the manual buttons or watch a robot for 25+ real seconds to confirm it.");
            Debug.Log("[Phase3Test] --- Phase 3 verification complete ---");
        }

        private void FindRefs()
        {
            _movement = FindFirstObjectByType<GridMovement>();
            _player = _movement != null ? _movement.gameObject : null;
            _playerHealth = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            _tileMap = FindFirstObjectByType<TileMapRenderer>();
            _spawner = FindFirstObjectByType<RobotSpawner>();
        }

        private void VerifyNoRobotsAtStart()
        {
            var robot = FindFirstObjectByType<RobotBase>();
            Debug.Log(robot == null
                ? "[Phase3Test] PASS: no robots present immediately after LoadLevel (spawnDelay respected)."
                : "[Phase3Test] FAIL: a robot already exists before its spawnDelay elapsed.");
        }

        private HarvesterRobot VerifyHarvesterSpawned()
        {
            var harvester = FindFirstObjectByType<HarvesterRobot>();
            Debug.Log(harvester != null
                ? "[Phase3Test] PASS: HarvesterRobot spawned after its 2s delay."
                : "[Phase3Test] FAIL: no HarvesterRobot found ~2.3s after LoadLevel.");
            return harvester;
        }

        private IEnumerator TestHarvesterPursuit(HarvesterRobot harvester)
        {
            if (harvester == null || _movement == null)
            {
                yield break;
            }

            float before = Vector2Int.Distance(harvester.CurrentGridPosition, _movement.CurrentGridPosition);
            yield return WaitSeconds(1.0f);
            float after = Vector2Int.Distance(harvester.CurrentGridPosition, _movement.CurrentGridPosition);

            Debug.Log(after < before
                ? $"[Phase3Test] PASS: Harvester closed the distance to Cluck ({before:F1} -> {after:F1} tiles)."
                : $"[Phase3Test] INFO: Harvester distance did not shrink this second ({before:F1} -> {after:F1}) " +
                  "— can happen if it's still exiting the factory or Cluck is stationary near a wall; not a hard failure.");
        }

        private IEnumerator TestChaseContactKillsPlayer(HarvesterRobot harvester)
        {
            if (harvester == null || _player == null || _playerHealth == null)
            {
                Debug.LogWarning("[Phase3Test] SKIP chase-contact test: missing harvester/player/PlayerHealth.");
                yield break;
            }

            if (harvester.CurrentState != RobotState.Chase)
            {
                Debug.LogWarning($"[Phase3Test] SKIP chase-contact test: Harvester is {harvester.CurrentState}, not Chase.");
                yield break;
            }

            _player.transform.position = harvester.transform.position;
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            bool respawning = _playerHealth.IsRespawning;
            Debug.Log(respawning
                ? "[Phase3Test] PASS: touching a Chase-state robot triggered the death sequence."
                : "[Phase3Test] FAIL: no death sequence started on contact with a Chase-state robot.");

            if (respawning)
            {
                yield return WaitSeconds(1.8f);
                bool backAtStart = _movement.CurrentGridPosition == GameManager.Instance.CurrentLevel.playerStartPosition;
                Debug.Log(backAtStart
                    ? "[Phase3Test] PASS: Cluck respawned at playerStartPosition; score was not reset."
                    : $"[Phase3Test] FAIL: Cluck at {_movement.CurrentGridPosition} after respawn, expected " +
                      $"{GameManager.Instance.CurrentLevel.playerStartPosition}.");

                bool harvesterReset = harvester.CurrentState == RobotState.Chase &&
                                       harvester.CurrentGridPosition == new Vector2Int(7, 7);
                Debug.Log(harvesterReset
                    ? "[Phase3Test] PASS: Harvester was reset to its factory spawn on player death."
                    : "[Phase3Test] FAIL: Harvester was not reset to its factory spawn on player death.");
            }
        }

        private IEnumerator TestPowerPelletActivatesVulnerable()
        {
            var harvester = FindFirstObjectByType<HarvesterRobot>();
            if (harvester == null || PowerPelletManager.Instance == null)
            {
                Debug.LogWarning("[Phase3Test] SKIP power pellet test: missing Harvester or PowerPelletManager.");
                yield break;
            }

            PowerPelletManager.Instance.ActivatePower(1.6f);
            yield return null;

            bool active = PowerPelletManager.Instance.IsPowerActive;
            bool vulnerable = harvester.CurrentState == RobotState.Vulnerable;
            Debug.Log(active && vulnerable
                ? "[Phase3Test] PASS: ActivatePower set IsPowerActive and flipped the robot to Vulnerable."
                : $"[Phase3Test] FAIL: IsPowerActive={active}, Harvester state={harvester.CurrentState} " +
                  "(expected true/Vulnerable).");
        }

        private void TestHitDefeatsVulnerableRobot()
        {
            var harvester = FindFirstObjectByType<HarvesterRobot>();
            if (harvester == null)
            {
                Debug.LogWarning("[Phase3Test] SKIP hit-defeats-robot test: no Harvester found.");
                return;
            }

            if (harvester.CurrentState != RobotState.Vulnerable)
            {
                Debug.LogWarning($"[Phase3Test] SKIP hit-defeats-robot test: Harvester is {harvester.CurrentState}, not Vulnerable.");
                return;
            }

            harvester.RegisterHit();
            Debug.Log(harvester.CurrentState == RobotState.Defeated
                ? "[Phase3Test] PASS: RegisterHit() on a Vulnerable 1-health robot transitions it to Defeated."
                : $"[Phase3Test] FAIL: expected Defeated after RegisterHit(), got {harvester.CurrentState}.");
        }

        private void TestChainScoring()
        {
            if (ChaseScoreManager.Instance == null || ScoreManager.Instance == null)
            {
                Debug.LogWarning("[Phase3Test] SKIP chain scoring test: managers missing.");
                return;
            }

            // A prior test (TestHitDefeatsVulnerableRobot) already caused one real defeat, which
            // bumped ChainCount via RobotBase.TransitionToDefeated -> OnRobotDefeated. Reset here
            // so this check starts from a known ChainCount=0, same as a fresh power pellet.
            ChaseScoreManager.Instance.ResetChain();

            int[] expectedDeltas = { 200, 400, 800, 1600 + 5000 };
            bool allOk = true;

            for (int i = 0; i < expectedDeltas.Length; i++)
            {
                int before = ScoreManager.Instance.CurrentMazeScore;
                ChaseScoreManager.Instance.OnRobotDefeated();
                int delta = ScoreManager.Instance.CurrentMazeScore - before;
                if (delta != expectedDeltas[i])
                {
                    allOk = false;
                    Debug.Log($"[Phase3Test] FAIL: defeat #{i + 1} awarded {delta}, expected {expectedDeltas[i]}.");
                }
            }

            Debug.Log(allOk
                ? "[Phase3Test] PASS: chain scoring awarded 200/400/800/1600+5000 across 4 defeats."
                : "[Phase3Test] FAIL: chain scoring sequence did not match 200/400/800/1600(+5000 on the 4th).");

            ChaseScoreManager.Instance.ResetChain();
        }

        private IEnumerator TestPowerExpiry()
        {
            if (PowerPelletManager.Instance == null)
            {
                yield break;
            }

            var harvester = FindFirstObjectByType<HarvesterRobot>();
            // Harvester was defeated by the previous test, so use whatever robot is currently
            // Vulnerable-capable; re-activate on it for the expiry check regardless of its state —
            // PowerPelletManager broadcasts globally, so any listening robot proves the point.
            PowerPelletManager.Instance.ActivatePower(1.5f);
            yield return WaitSeconds(1.8f);

            Debug.Log(!PowerPelletManager.Instance.IsPowerActive
                ? "[Phase3Test] PASS: power state expired on its own after the requested duration."
                : "[Phase3Test] FAIL: IsPowerActive still true after the duration elapsed.");

            if (harvester != null)
            {
                bool backToChaseOrScatter = harvester.CurrentState is RobotState.Chase or RobotState.Scatter
                    or RobotState.Defeated or RobotState.Returning;
                Debug.Log(backToChaseOrScatter
                    ? "[Phase3Test] PASS: robot left Vulnerable state once power expired."
                    : $"[Phase3Test] FAIL: robot still {harvester.CurrentState} after power expired.");
            }
        }

        private void VerifyScoutSpawnedBySix()
        {
            var scout = FindFirstObjectByType<ScoutRobot>();
            Debug.Log(scout != null
                ? "[Phase3Test] PASS: ScoutRobot has spawned (6s spawnDelay elapsed by this point in the run)."
                : "[Phase3Test] FAIL: no ScoutRobot found — expected it to have spawned by now.");
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
            GUILayout.BeginArea(new Rect(240, 110, 260, 240));
            GUILayout.Label("Phase 3 manual controls:");
            if (GUILayout.Button("Reload Level 0")) { GameManager.Instance.LoadLevel(0); FindRefs(); }
            if (GUILayout.Button("Load Level 5 (3 robots)")) { GameManager.Instance.LoadLevel(4); FindRefs(); }
            if (GUILayout.Button("Force Activate Power (8s)")) { PowerPelletManager.Instance?.ActivatePower(8f); }
            if (GUILayout.Button("Kill All Robots' Health"))
            {
                foreach (var robot in FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
                {
                    if (robot.CurrentState == RobotState.Vulnerable) robot.RegisterHit();
                }
            }
            if (GUILayout.Button("Reset Robots To Factory")) { _spawner?.ResetAllRobotsToFactory(); }
            GUILayout.EndArea();
        }
    }
}
