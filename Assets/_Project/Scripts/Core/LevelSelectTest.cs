using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.UI;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Level Select verification harness — same PASS/FAIL/INFO/SKIP/WARN convention and
    /// runOnStart/ContextMenu shape as Phase1-5Test. Only this harness's runOnStart is left enabled
    /// (Phase5ProjectBuilder disables Phase5Test's when it wires this in — see that file's own
    /// comment on why only one test drives GameManager.LoadLevel at a time).
    ///
    /// Drives the screen via each controller's public entry points (ShowOnly, Button.onClick.Invoke)
    /// rather than simulating real taps, matching Phase5Test's own approach. Covers the world-select
    /// -> reveal-tiles flow (LevelSelectController.ShowWorldSelect/RevealWorld) on a fresh/low-
    /// progress save, where only the Corn Field (world 0) shield is expected to be available.
    /// </summary>
    public class LevelSelectTest : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunVerification());
            }
        }

        [ContextMenu("Run Level Select Verification")]
        public void RunVerificationFromMenu()
        {
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            Debug.Log("[LevelSelectTest] --- Starting Level Select verification ---");

            yield return TestNavigationToLevelSelect();
            TestWorldSelectShowsOnlyAvailableWorlds();
            yield return TestSelectingWorldRevealsTiles();
            TestLockedAndUnlockedVisualState();
            yield return TestTappingUnlockedTileLoadsLevel();
            yield return TestTappingLockedTileShowsHint();
            yield return TestCurrentWorldIndicatorReturnsToWorldSelect();
            yield return TestBackButtonReturnsToMainMenu();

            Debug.Log("[LevelSelectTest] --- Level Select verification complete ---");
        }

        private static Transform _canvasTransform;

        /// <summary>Transform.Find works on inactive children (unlike GameObject.Find) — same
        /// reasoning as Phase5Test's own Find helper, since most screens are inactive most of the
        /// time by design.</summary>
        private static GameObject Find(string name)
        {
            if (_canvasTransform == null)
            {
                _canvasTransform = GameObject.Find("Canvas")?.transform;
            }
            return _canvasTransform != null ? _canvasTransform.Find(name)?.gameObject : null;
        }

        private IEnumerator TestNavigationToLevelSelect()
        {
            var mainMenu = Find("MainMenuScreen");
            var levelSelect = Find("LevelSelectScreen");
            if (mainMenu == null || levelSelect == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: MainMenuScreen/LevelSelectScreen not all found.");
                yield break;
            }

            SceneTransitionManager.Instance.ShowOnly(mainMenu);
            yield return WaitForTransition();

            var playButton = mainMenu.transform.Find("PlayButton")?.GetComponent<Button>();
            if (playButton == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: Main Menu PlayButton not found.");
                yield break;
            }
            playButton.onClick.Invoke();
            yield return WaitForTransition();

            bool shown = levelSelect.activeSelf && !mainMenu.activeSelf;
            Debug.Log(shown
                ? "[LevelSelectTest] PASS: Main Menu's Play button opens Level Select."
                : "[LevelSelectTest] FAIL: Level Select did not become the active screen after tapping Play.");
        }

        /// <summary>Level Select should open into world-select, not straight into a tile grid — the
        /// grid (ScrollView) should be hidden, WorldShieldContainer active, and all 4 world shields
        /// present (locked worlds still get a badge, just greyed out/non-interactable — see
        /// LevelSelectController.ShowWorldSelect). On a fresh/low-progress save, exactly 1 of those
        /// 4 should be interactable (Corn Field), since only it is available until world 0's gate
        /// level reaches 2+ stars.</summary>
        private void TestWorldSelectShowsOnlyAvailableWorlds()
        {
            var levelSelect = Find("LevelSelectScreen");
            if (levelSelect == null || !levelSelect.activeSelf)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP world-select test: Level Select not active.");
                return;
            }

            var scrollView = levelSelect.transform.Find("ScrollView");
            var shieldContainer = levelSelect.transform.Find("WorldShieldContainer");
            if (scrollView == null || shieldContainer == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: ScrollView or WorldShieldContainer not found.");
                return;
            }

            bool opensIntoWorldSelect = !scrollView.gameObject.activeSelf && shieldContainer.gameObject.activeSelf;
            Debug.Log(opensIntoWorldSelect
                ? "[LevelSelectTest] PASS: Level Select opens into world-select (tile grid hidden, shields shown)."
                : $"[LevelSelectTest] FAIL: expected grid hidden/shields shown, got gridActive={scrollView.gameObject.activeSelf}, shieldsActive={shieldContainer.gameObject.activeSelf}.");

            int worldCount = Mathf.CeilToInt((float)UnlockProgression.TotalLevels / UnlockProgression.LevelsPerWorld);
            int shieldCount = shieldContainer.childCount;
            Debug.Log(shieldCount == worldCount
                ? $"[LevelSelectTest] PASS: all {worldCount} world shields are shown (locked ones greyed out, not omitted)."
                : $"[LevelSelectTest] FAIL: expected {worldCount} world shields shown, found {shieldCount}.");

            int interactableCount = 0;
            foreach (Transform child in shieldContainer)
            {
                var button = child.GetComponent<Button>();
                if (button != null && button.interactable)
                {
                    interactableCount++;
                }
            }

            bool onlyCornFieldAvailable = UnlockProgression.GetStarsForLevel(UnlockProgression.LevelsPerWorld - 1) < 2;
            if (onlyCornFieldAvailable)
            {
                Debug.Log(interactableCount == 1
                    ? "[LevelSelectTest] PASS: exactly 1 world shield is interactable/coloured (Corn Field only, fresh/low-progress save)."
                    : $"[LevelSelectTest] FAIL: expected 1 interactable world shield on a fresh save, found {interactableCount}.");
            }
            else
            {
                Debug.Log($"[LevelSelectTest] INFO: {interactableCount} world shield(s) interactable — save has progress past Corn Field's gate, more than 1 is expected here.");
            }
        }

        /// <summary>Taps the first available world's shield and waits for the shrink/fade reveal
        /// animation, then checks the grid shows exactly that world's UnlockProgression.
        /// LevelsPerWorld (25) tiles with contiguous indices.</summary>
        private IEnumerator TestSelectingWorldRevealsTiles()
        {
            var levelSelect = Find("LevelSelectScreen");
            var shieldContainer = levelSelect != null ? levelSelect.transform.Find("WorldShieldContainer") : null;
            if (shieldContainer == null || shieldContainer.childCount == 0)
            {
                Debug.LogError("[LevelSelectTest] FAIL: no world shield available to tap.");
                yield break;
            }

            var shieldButton = shieldContainer.GetChild(0).GetComponent<Button>();
            if (shieldButton == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: first world shield has no Button.");
                yield break;
            }

            shieldButton.onClick.Invoke();

            // Reveal is a real-time coroutine (ShieldRevealSeconds), not a SceneTransitionManager
            // fade — poll for the grid becoming active instead of guessing a fixed wait.
            const float timeoutSeconds = 5f;
            float elapsed = 0f;
            var scrollView = levelSelect.transform.Find("ScrollView");
            while (!scrollView.gameObject.activeSelf && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            var tiles = levelSelect.GetComponentsInChildren<LevelTileController>(true);
            bool countOk = tiles.Length == UnlockProgression.LevelsPerWorld;
            Debug.Log(countOk
                ? $"[LevelSelectTest] PASS: revealed grid contains exactly {UnlockProgression.LevelsPerWorld} tiles (one world)."
                : $"[LevelSelectTest] FAIL: expected {UnlockProgression.LevelsPerWorld} tiles after revealing a world, found {tiles.Length}.");

            bool indicesOk = tiles.Select(t => t.LevelIndex).OrderBy(i => i)
                .SequenceEqual(Enumerable.Range(0, UnlockProgression.LevelsPerWorld));
            Debug.Log(indicesOk
                ? "[LevelSelectTest] PASS: revealed tile indices are exactly 0..LevelsPerWorld-1 with no gaps/duplicates."
                : "[LevelSelectTest] FAIL: revealed tile indices are missing entries or contain duplicates.");

            var indicator = levelSelect.transform.Find("CurrentWorldIndicator");
            bool indicatorShown = indicator != null && indicator.gameObject.activeSelf;
            bool shieldsHidden = !shieldContainer.gameObject.activeSelf;
            Debug.Log(indicatorShown && shieldsHidden
                ? "[LevelSelectTest] PASS: CurrentWorldIndicator appears and WorldShieldContainer hides after selecting a world."
                : $"[LevelSelectTest] FAIL: indicatorShown={indicatorShown}, shieldsHidden={shieldsHidden}.");
        }

        private void TestLockedAndUnlockedVisualState()
        {
            var levelSelect = Find("LevelSelectScreen");
            if (levelSelect == null || !levelSelect.activeSelf)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP locked/unlocked visual test: Level Select not active.");
                return;
            }

            var tiles = levelSelect.GetComponentsInChildren<LevelTileController>(true);
            var firstTile = tiles.FirstOrDefault(t => t.LevelIndex == 0);
            var lastInWorldTile = tiles.FirstOrDefault(t => t.LevelIndex == UnlockProgression.LevelsPerWorld - 1);
            if (firstTile == null || lastInWorldTile == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: could not find Level 1 and the last tile of the revealed world.");
                return;
            }

            bool firstUnlocked = UnlockProgression.IsLevelUnlocked(0);
            bool lastLocked = !UnlockProgression.IsLevelUnlocked(UnlockProgression.LevelsPerWorld - 1);
            Debug.Log(firstUnlocked && lastLocked
                ? "[LevelSelectTest] PASS: Level 1 is unlocked, the world's last level is locked (fresh/low-progress save)."
                : $"[LevelSelectTest] INFO: Level 1 unlocked={firstUnlocked}, last-in-world locked={lastLocked} (depends on existing save progress — not necessarily a failure).");

            // Diagnostic: dump the actual runtime sprite/colour/active-state of both tiles, to
            // check what's really being rendered rather than just what the data says should be.
            // Image lives on the "TileBackground" child, not the tile root. (This is what caught
            // the "black tiles" bug — see LevelTileController's UpdateVisualState doc comment.)
            var lastImage = lastInWorldTile.transform.Find("TileBackground")?.GetComponent<Image>();
            Debug.Log(lastImage == null
                ? "[LevelSelectTest] INFO: locked tile diagnostic — TileBackground child/Image not found."
                : $"[LevelSelectTest] INFO: locked tile diagnostic — sprite={(lastImage.sprite != null ? lastImage.sprite.name : "NULL")}, color={lastImage.color}, type={lastImage.type}, rectSize={((RectTransform)lastImage.transform).rect.size}.");

            var firstImage = firstTile.transform.Find("TileBackground")?.GetComponent<Image>();
            Debug.Log(firstImage == null
                ? "[LevelSelectTest] INFO: unlocked tile diagnostic — TileBackground child/Image not found."
                : $"[LevelSelectTest] INFO: unlocked tile diagnostic — sprite={(firstImage.sprite != null ? firstImage.sprite.name : "NULL")}, color={firstImage.color}, type={firstImage.type}.");
        }

        private IEnumerator TestTappingUnlockedTileLoadsLevel()
        {
            var levelSelect = Find("LevelSelectScreen");
            var gameplayScreen = Find("GameplayScreen");
            if (levelSelect == null || !levelSelect.activeSelf || gameplayScreen == null)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP unlocked-tile tap test: Level Select not active.");
                yield break;
            }

            var tiles = levelSelect.GetComponentsInChildren<LevelTileController>(true);
            var tile0 = tiles.FirstOrDefault(t => t.LevelIndex == 0);
            var button = tile0 != null ? tile0.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: Level 1 tile/Button not found.");
                yield break;
            }

            button.onClick.Invoke();
            yield return WaitForTransition();

            bool loaded = gameplayScreen.activeSelf
                && GameManager.Instance.CurrentState == GameState.Playing
                && GameManager.Instance.CurrentLevel != null
                && GameManager.Instance.CurrentLevel.levelNumber == 0;
            Debug.Log(loaded
                ? "[LevelSelectTest] PASS: tapping an unlocked tile loads that level and shows Gameplay directly (no Matchup screen)."
                : $"[LevelSelectTest] FAIL: gameplayShown={gameplayScreen.activeSelf}, state={GameManager.Instance.CurrentState}.");

            // Return to Level Select for the remaining tests — this re-enters world-select
            // (OnEnable -> OpenLevelSelect), so re-select the world to get back to its tile grid.
            SceneTransitionManager.Instance.ShowOnly(levelSelect);
            yield return WaitForTransition();

            var shieldContainer = levelSelect.transform.Find("WorldShieldContainer");
            var shieldButton = shieldContainer != null && shieldContainer.childCount > 0 ? shieldContainer.GetChild(0).GetComponent<Button>() : null;
            if (shieldButton != null)
            {
                shieldButton.onClick.Invoke();
                float elapsed = 0f;
                var scrollView = levelSelect.transform.Find("ScrollView");
                while (!scrollView.gameObject.activeSelf && elapsed < 5f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private IEnumerator TestTappingLockedTileShowsHint()
        {
            var levelSelect = Find("LevelSelectScreen");
            if (levelSelect == null || !levelSelect.activeSelf)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP locked-tile tap test: Level Select not active.");
                yield break;
            }

            var tiles = levelSelect.GetComponentsInChildren<LevelTileController>(true);
            var lockedTile = tiles.FirstOrDefault(t => !UnlockProgression.IsLevelUnlocked(t.LevelIndex));
            if (lockedTile == null)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP locked-tile tap test: every level in this world currently unlocked (nothing locked to tap).");
                yield break;
            }

            var hintPanel = levelSelect.transform.Find("LockedHintPanel")?.gameObject;
            var button = lockedTile.GetComponent<Button>();
            if (hintPanel == null || button == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: LockedHintPanel or locked tile Button not found.");
                yield break;
            }

            hintPanel.SetActive(false);
            button.onClick.Invoke();
            yield return null;

            Debug.Log(hintPanel.activeSelf
                ? "[LevelSelectTest] PASS: tapping a locked tile shows the LockedHintPanel."
                : "[LevelSelectTest] FAIL: LockedHintPanel did not appear after tapping a locked tile.");
        }

        /// <summary>Tapping the small CurrentWorldIndicator (top-left of the header, only visible
        /// once a world's tiles are showing) should hide the grid and bring world-select back.</summary>
        private IEnumerator TestCurrentWorldIndicatorReturnsToWorldSelect()
        {
            var levelSelect = Find("LevelSelectScreen");
            var indicatorButton = levelSelect != null ? levelSelect.transform.Find("CurrentWorldIndicator")?.GetComponent<Button>() : null;
            if (indicatorButton == null || !indicatorButton.gameObject.activeSelf)
            {
                Debug.LogWarning("[LevelSelectTest] SKIP current-world-indicator test: indicator not active (a world tile grid isn't currently showing).");
                yield break;
            }

            indicatorButton.onClick.Invoke();
            yield return null;

            var scrollView = levelSelect.transform.Find("ScrollView");
            var shieldContainer = levelSelect.transform.Find("WorldShieldContainer");
            bool backToWorldSelect = !scrollView.gameObject.activeSelf && shieldContainer.gameObject.activeSelf && !indicatorButton.gameObject.activeSelf;
            Debug.Log(backToWorldSelect
                ? "[LevelSelectTest] PASS: tapping CurrentWorldIndicator returns to world-select."
                : $"[LevelSelectTest] FAIL: expected grid hidden/shields shown/indicator hidden, got gridActive={scrollView.gameObject.activeSelf}, shieldsActive={shieldContainer.gameObject.activeSelf}, indicatorActive={indicatorButton.gameObject.activeSelf}.");
        }

        private IEnumerator TestBackButtonReturnsToMainMenu()
        {
            var levelSelect = Find("LevelSelectScreen");
            var mainMenu = Find("MainMenuScreen");
            // BackButton is a direct child of the screen root (bottom-left, safe-area inset), not
            // nested under Header — see Phase5ProjectBuilder.BuildLevelSelect.
            var backButton = levelSelect != null ? levelSelect.transform.Find("BackButton")?.GetComponent<Button>() : null;
            if (backButton == null || mainMenu == null)
            {
                Debug.LogError("[LevelSelectTest] FAIL: Back button or MainMenuScreen not found.");
                yield break;
            }

            backButton.onClick.Invoke();
            yield return WaitForTransition();

            bool backToMenu = mainMenu.activeSelf && !levelSelect.activeSelf;
            Debug.Log(backToMenu
                ? "[LevelSelectTest] PASS: Back button returns to Main Menu."
                : "[LevelSelectTest] FAIL: Back button did not return to Main Menu.");
        }

        /// <summary>Same batch-mode timing gotcha Phase5Test's own WaitForTransition documents —
        /// polls SceneTransitionManager.IsTransitioning to completion instead of a fixed delay.</summary>
        private static IEnumerator WaitForTransition()
        {
            const float timeoutSeconds = 15f;
            float elapsed = 0f;

            yield return null;
            while (SceneTransitionManager.Instance.IsTransitioning && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
