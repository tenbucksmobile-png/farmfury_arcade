using System.Collections;
using System.Linq;
using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Phase 4 verification harness — same PASS/FAIL/INFO/SKIP/WARN convention as Phase1-3Test.
    /// Only this harness's runOnStart is left enabled by Phase4ProjectBuilder (Phase1-3Test are
    /// disabled) to avoid the concurrent-LoadLevel race documented on Phase3ProjectBuilder.
    /// Drives most checks by calling TryActivate()/SwapCharacter() directly rather than waiting
    /// on real input or full cooldowns, so the battery fits inside Phase1ProjectBuilder's
    /// batch-mode Play window — the only real-time wait is the ~2.3s for Harvester's spawnDelay.
    /// </summary>
    public class Phase4Test : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunVerification());
            }
        }

        [ContextMenu("Run Phase 4 Verification")]
        public void RunVerificationFromMenu()
        {
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            Debug.Log("[Phase4Test] --- Starting Phase 4 verification ---");

            GameManager.Instance.LoadLevel(0);
            yield return null;

            VerifyStartsAsCluck();
            yield return TestEggDropSpawnsThreeEggs();

            yield return WaitSeconds(2.3f); // Harvester's spawnDelay per LevelData_01
            yield return TestEggDefeatsRobot();

            yield return TestSwapToBessie();
            yield return TestGroundSlamDefeatsNearbyRobot();

            TestUnlockManager();

            yield return TestFeatherStormCombo();

            Debug.Log("[Phase4Test] --- Phase 4 verification complete ---");
        }

        private void VerifyStartsAsCluck()
        {
            bool ok = CharacterManager.Instance != null && CharacterManager.Instance.ActiveCharacter == CharacterType.Cluck;
            Debug.Log(ok
                ? "[Phase4Test] PASS: level starts with Cluck as the active character."
                : $"[Phase4Test] FAIL: active character is {CharacterManager.Instance?.ActiveCharacter}, expected Cluck.");
        }

        private IEnumerator TestEggDropSpawnsThreeEggs()
        {
            var ability = GetActiveAbility<EggDropAbility>();
            if (ability == null)
            {
                Debug.LogError("[Phase4Test] FAIL: Cluck has no EggDropAbility component.");
                yield break;
            }

            bool activated = ability.TryActivate();
            yield return null;

            int eggCount = FindObjectsByType<EggHazard>(FindObjectsSortMode.None).Length;
            Debug.Log(activated && eggCount == 3
                ? "[Phase4Test] PASS: Egg Drop activated and spawned 3 eggs."
                : $"[Phase4Test] FAIL: activated={activated}, egg count={eggCount}, expected true/3.");

            Debug.Log(!ability.IsReady && ability.CooldownRemaining > 0f
                ? "[Phase4Test] PASS: ability cooldown started (CooldownRemaining > 0, IsReady false)."
                : "[Phase4Test] FAIL: ability did not enter cooldown after activating.");
        }

        private IEnumerator TestEggDefeatsRobot()
        {
            var harvester = FindFirstObjectByType<HarvesterRobot>();
            var egg = FindFirstObjectByType<EggHazard>();
            if (harvester == null || egg == null)
            {
                Debug.LogWarning("[Phase4Test] SKIP egg-defeat test: missing Harvester or egg.");
                yield break;
            }

            harvester.transform.position = egg.transform.position;
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            Debug.Log(harvester.CurrentState == RobotState.Defeated
                ? "[Phase4Test] PASS: robot walking over an egg is defeated."
                : "[Phase4Test] FAIL: robot did not become defeated after overlapping an egg.");
        }

        private IEnumerator TestSwapToBessie()
        {
            float cluckSpeed = CharacterManager.Instance.ActiveCharacterObject.GetComponent<GridMovement>().Speed;

            bool swapped = CharacterManager.Instance.SwapCharacter(CharacterType.Bessie);
            yield return null;

            bool isBessie = CharacterManager.Instance.ActiveCharacter == CharacterType.Bessie;
            Debug.Log(swapped && isBessie
                ? "[Phase4Test] PASS: SwapCharacter(Bessie) succeeded and ActiveCharacter updated."
                : $"[Phase4Test] FAIL: swapped={swapped}, ActiveCharacter={CharacterManager.Instance.ActiveCharacter}.");

            var bessieBase = CharacterManager.Instance.ActiveCharacterObject.GetComponent<CharacterBase>();
            float bessieSpeed = CharacterManager.Instance.ActiveCharacterObject.GetComponent<GridMovement>().Speed;
            bool differentSpeed = !Mathf.Approximately(cluckSpeed, bessieSpeed);
            Debug.Log(bessieBase != null && bessieBase.CharacterType == CharacterType.Bessie && differentSpeed
                ? $"[Phase4Test] PASS: Bessie has distinct CharacterBase identity and speed ({cluckSpeed:F1} -> {bessieSpeed:F1})."
                : "[Phase4Test] FAIL: Bessie's CharacterBase/speed did not differ from Cluck's as expected.");
        }

        private IEnumerator TestGroundSlamDefeatsNearbyRobot()
        {
            var ability = GetActiveAbility<GroundSlamAbility>();
            var harvester = FindFirstObjectByType<HarvesterRobot>();
            if (ability == null || harvester == null)
            {
                Debug.LogWarning("[Phase4Test] SKIP Ground Slam test: missing ability or Harvester.");
                yield break;
            }

            // Bring the robot adjacent to Bessie so it's within the 2-tile slam radius —
            // otherwise it could be anywhere on the maze depending on how far Chase AI carried it.
            harvester.transform.position = CharacterManager.Instance.ActiveCharacterObject.transform.position;
            yield return null;

            bool activated = ability.TryActivate();
            yield return null;

            Debug.Log(activated && harvester.CurrentState == RobotState.Defeated
                ? "[Phase4Test] PASS: Ground Slam defeated a robot within its radius."
                : $"[Phase4Test] FAIL: activated={activated}, robot state={harvester.CurrentState}.");
        }

        private void TestUnlockManager()
        {
            if (UnlockManager.Instance == null || SaveManager.Instance == null)
            {
                Debug.LogWarning("[Phase4Test] SKIP unlock test: managers missing.");
                return;
            }

            // Not asserting "before == false": SaveManager persists real PlayerPrefs, so a
            // previous verification run (or previous play session) may have already unlocked
            // Percy — same caveat as Phase1Test's coin-balance round-trip. The invariant that
            // actually matters is that the unlock call leaves Percy unlocked.
            bool before = SaveManager.Instance.IsCharacterUnlocked(CharacterType.Percy);
            UnlockManager.Instance.CheckUnlocksOnLevelComplete(4); // 5 mazes completed (0-indexed highest = 4)
            bool after = SaveManager.Instance.IsCharacterUnlocked(CharacterType.Percy);

            Debug.Log(after
                ? $"[Phase4Test] PASS: Percy is unlocked after reaching 5 mazes completed (already unlocked from a prior run: {before})."
                : $"[Phase4Test] FAIL: Percy still locked after CheckUnlocksOnLevelComplete(4) (before={before}).");

            // Also unlock Woolly (10 mazes) here, via the same real pathway, so the Feather Storm
            // test below can legitimately swap to her.
            UnlockManager.Instance.CheckUnlocksOnLevelComplete(9);
        }

        private IEnumerator TestFeatherStormCombo()
        {
            if (ComboSystem.Instance == null)
            {
                Debug.LogWarning("[Phase4Test] SKIP combo test: ComboSystem missing.");
                yield break;
            }

            // Current active character is Bessie (from the swap test) — get back to Cluck first
            // so the very next swap is a genuine Cluck -> Woolly pair.
            CharacterManager.Instance.SwapCharacter(CharacterType.Cluck);
            yield return null;

            bool swappedToWoolly = CharacterManager.Instance.SwapCharacter(CharacterType.Woolly);
            yield return null;

            Debug.Log(swappedToWoolly && ComboSystem.Instance.PendingEggDropClones
                ? "[Phase4Test] PASS: Cluck -> Woolly triggered the Feather Storm combo (pending egg-clone buff)."
                : $"[Phase4Test] FAIL: swappedToWoolly={swappedToWoolly}, PendingEggDropClones={ComboSystem.Instance.PendingEggDropClones}.");

            var cloneAbility = GetActiveAbility<TripleCloneAbility>();
            if (cloneAbility == null)
            {
                Debug.LogWarning("[Phase4Test] SKIP Feather Storm clone check: no TripleCloneAbility found.");
                yield break;
            }

            bool activated = cloneAbility.TryActivate();
            yield return null;

            int cloneCount = FindObjectsByType<WoollyClone>(FindObjectsSortMode.None).Length;
            bool buffConsumed = !ComboSystem.Instance.PendingEggDropClones;
            Debug.Log(activated && cloneCount == 2 && buffConsumed
                ? "[Phase4Test] PASS: Triple Clone spawned 2 clones and consumed the Feather Storm buff."
                : $"[Phase4Test] FAIL: activated={activated}, cloneCount={cloneCount}, buffConsumed={buffConsumed}.");
        }

        private static T GetActiveAbility<T>() where T : AbilityBase
        {
            var obj = CharacterManager.Instance?.ActiveCharacterObject;
            return obj != null ? obj.GetComponent<T>() : null;
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
            GUILayout.BeginArea(new Rect(510, 110, 260, 200));
            GUILayout.Label("Phase 4 manual controls:");
            if (GUILayout.Button("Reload Level 0")) { GameManager.Instance.LoadLevel(0); }
            foreach (CharacterType type in System.Enum.GetValues(typeof(CharacterType)).Cast<CharacterType>())
            {
                if (GUILayout.Button($"Swap to {type}"))
                {
                    CharacterManager.Instance?.SwapCharacter(type);
                }
            }
            if (GUILayout.Button("Activate Active Ability"))
            {
                var obj = CharacterManager.Instance?.ActiveCharacterObject;
                obj?.GetComponent<AbilityBase>()?.TryActivate();
            }
            GUILayout.EndArea();
        }
    }
}
