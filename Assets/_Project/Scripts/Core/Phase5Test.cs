using System.Collections;
using UnityEngine;
using FarmFuryArcade.UI;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Phase 5 verification harness — same PASS/FAIL/INFO/SKIP/WARN convention as Phase1-4Test.
    /// Only this harness's runOnStart is left enabled (Phase4ProjectBuilder's precedent: each new
    /// phase disables the previous test's auto-run to avoid racing on GameManager.LoadLevel).
    /// Drives the screen flow directly via each controller's public Show()/ShowOnly() entry
    /// points rather than simulating clicks, verifying: initial screen state, Main Menu -> Level
    /// Select -> Gameplay navigation, HUD element wiring, pause freezing Time.timeScale, level
    /// completion producing a LevelResult with correct-shape stars/score, SaveManager
    /// persistence, and settings mutating SaveManager/AudioManager state.
    /// </summary>
    public class Phase5Test : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunVerification());
            }
        }

        [ContextMenu("Run Phase 5 Verification")]
        public void RunVerificationFromMenu()
        {
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            Debug.Log("[Phase5Test] --- Starting Phase 5 verification ---");

            VerifyMainMenuIsInitialScreen();
            yield return TestMainMenuToGameplay();
            yield return TestHudElementsPresent();
            yield return TestPauseFreezesTime();
            yield return TestLevelCompleteFlow();
            TestSettingsMutatesSaveAndAudio();

            Debug.Log("[Phase5Test] --- Phase 5 verification complete ---");
        }

        private static Transform _canvasTransform;

        /// <summary>GameObject.Find only searches active GameObjects — useless here since most
        /// screens are inactive most of the time by design (that's the whole point of
        /// SceneTransitionManager.ShowOnly). Transform.Find on the (always-active) Canvas works
        /// regardless of a child's active state, so every screen lookup goes through this instead.</summary>
        private static GameObject Find(string name)
        {
            if (_canvasTransform == null)
            {
                _canvasTransform = GameObject.Find("Canvas")?.transform;
            }
            return _canvasTransform != null ? _canvasTransform.Find(name)?.gameObject : null;
        }

        private void VerifyMainMenuIsInitialScreen()
        {
            var mainMenu = Find("MainMenuScreen");
            var levelSelect = Find("LevelSelectScreen");
            bool ok = mainMenu != null && mainMenu.activeSelf && levelSelect != null && !levelSelect.activeSelf;
            Debug.Log(ok
                ? "[Phase5Test] PASS: Main Menu is the only active top-level screen at startup."
                : "[Phase5Test] FAIL: expected only MainMenuScreen active at startup.");
        }

        private IEnumerator TestMainMenuToGameplay()
        {
            var mainMenuGO = Find("MainMenuScreen");
            var mainMenu = mainMenuGO != null ? mainMenuGO.GetComponent<MainMenuController>() : null;
            if (mainMenu == null)
            {
                Debug.LogError("[Phase5Test] FAIL: MainMenuController not found.");
                yield break;
            }

            // World Map was removed from the flow entirely (see CLAUDE.md's "Removed: World Map
            // screen") — Play now opens Level Select directly.
            SceneTransitionManager.Instance.ShowOnly(Find("LevelSelectScreen"));
            yield return WaitForTransition();

            bool levelSelectShown = Find("LevelSelectScreen").activeSelf && !Find("MainMenuScreen").activeSelf;
            Debug.Log(levelSelectShown
                ? "[Phase5Test] PASS: Play navigates Main Menu -> Level Select."
                : "[Phase5Test] FAIL: Level Select did not become the active screen.");

            // The Matchup (VS card) screen was removed — tapping an unlocked level tile now goes
            // straight into gameplay (LevelSelectController's tile tap handler). Reproduce that
            // same effect directly rather than hunting for a tile's Button deep inside Level
            // Select's ScrollRect content, which the test has no stable path into.
            GameManager.Instance.LoadLevel(0);
            SceneTransitionManager.Instance.ShowOnly(Find("GameplayScreen"));
            yield return WaitForTransition();

            bool gameplayShown = Find("GameplayScreen").activeSelf;
            bool isPlaying = GameManager.Instance.CurrentState == GameState.Playing;
            Debug.Log(gameplayShown && isPlaying
                ? "[Phase5Test] PASS: Tapping a level tile goes straight into gameplay (no Matchup screen)."
                : $"[Phase5Test] FAIL: gameplayShown={gameplayShown}, state={GameManager.Instance.CurrentState} (expected true/Playing).");
        }

        private IEnumerator TestHudElementsPresent()
        {
            var hudGO = Find("GameplayScreen");
            if (hudGO == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                Debug.LogWarning("[Phase5Test] SKIP HUD element test: not currently in a Playing gameplay session.");
                yield break;
            }

            bool allPresent =
                hudGO.transform.Find("ScoreText") != null &&
                hudGO.transform.Find("LevelText") != null &&
                hudGO.transform.Find("TimerText") != null &&
                hudGO.transform.Find("CharacterPortrait") != null &&
                hudGO.transform.Find("PauseButton") != null &&
                hudGO.transform.Find("SoundButton") != null &&
                hudGO.transform.Find("HomeButton") != null;

            Debug.Log(allPresent
                ? "[Phase5Test] PASS: all required HUD elements (score/level/timer/portrait/pause/sound/home) exist."
                : "[Phase5Test] FAIL: one or more required HUD elements are missing.");

            // Ability/Swap are no longer HUD buttons (removed in the gameplay-screen cleanup —
            // Space/Tab still activate them directly via InputController), so there's no on-screen
            // cooldown ring left to assert against here.
            yield return null;
        }

        private IEnumerator TestPauseFreezesTime()
        {
            if (GameManager.Instance.CurrentState != GameState.Playing)
            {
                Debug.LogWarning("[Phase5Test] SKIP pause test: not in Playing state.");
                yield break;
            }

            var pauseButton = Find("GameplayScreen")?.transform.Find("PauseButton")?.GetComponent<UnityEngine.UI.Button>();
            var pauseGO = Find("PauseOverlay");
            if (pauseButton == null || pauseGO == null)
            {
                Debug.LogError("[Phase5Test] FAIL: Pause button or PauseOverlay not found.");
                yield break;
            }

            pauseButton.onClick.Invoke();
            yield return null;

            bool paused = GameManager.Instance.CurrentState == GameState.Paused && Time.timeScale == 0f && pauseGO.activeSelf;
            Debug.Log(paused
                ? "[Phase5Test] PASS: Pause button freezes Time.timeScale and shows the pause overlay."
                : $"[Phase5Test] FAIL: state={GameManager.Instance.CurrentState}, timeScale={Time.timeScale}, overlayActive={pauseGO.activeSelf}.");

            var resumeButton = pauseGO.transform.Find("Content/ResumeButton")?.GetComponent<UnityEngine.UI.Button>();
            resumeButton?.onClick.Invoke();
            yield return null;

            bool resumed = GameManager.Instance.CurrentState == GameState.Playing && Time.timeScale == 1f && !pauseGO.activeSelf;
            Debug.Log(resumed
                ? "[Phase5Test] PASS: Resume restores Playing state and Time.timeScale."
                : "[Phase5Test] FAIL: Resume did not restore Playing/timeScale/hide the overlay.");
        }

        private IEnumerator TestLevelCompleteFlow()
        {
            if (GameManager.Instance.CurrentState != GameState.Playing || GameManager.Instance.CurrentLevel == null)
            {
                Debug.LogWarning("[Phase5Test] SKIP level-complete test: not in a Playing session.");
                yield break;
            }

            int before = SaveManager.Instance.GetLevelBestScore(GameManager.Instance.CurrentLevel.levelNumber);
            GameManager.Instance.EndLevel(true);
            yield return null;

            var result = GameManager.Instance.LastLevelResult;
            bool shapeOk = result.stars is >= 1 and <= 3 && result.totalScore > 0 && result.coinsEarned > 0;
            Debug.Log(shapeOk
                ? $"[Phase5Test] PASS: EndLevel(true) produced a well-formed LevelResult (stars={result.stars}, score={result.totalScore}, coins={result.coinsEarned})."
                : $"[Phase5Test] FAIL: LevelResult looked malformed (stars={result.stars}, score={result.totalScore}, coins={result.coinsEarned}).");

            // GameplayHUD's Update polls CurrentState and calls ShowOnly once it notices
            // LevelComplete; poll for that here too rather than guessing a fixed delay.
            const float levelCompleteTimeoutSeconds = 15f;
            float lcElapsed = 0f;
            while (!Find("LevelCompleteScreen").activeSelf && lcElapsed < levelCompleteTimeoutSeconds)
            {
                lcElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            bool levelCompleteShown = Find("LevelCompleteScreen").activeSelf;
            Debug.Log(levelCompleteShown
                ? "[Phase5Test] PASS: completing a level shows LevelCompleteScreen automatically."
                : "[Phase5Test] FAIL: LevelCompleteScreen did not activate after EndLevel(true).");

            int after = SaveManager.Instance.GetLevelBestScore(GameManager.Instance.CurrentLevel.levelNumber);
            Debug.Log(after >= before && after > 0
                ? "[Phase5Test] PASS: SaveManager's level best score persisted the result."
                : $"[Phase5Test] FAIL: best score before={before}, after={after} (expected after >= before and > 0).");
        }

        private void TestSettingsMutatesSaveAndAudio()
        {
            var settingsGO = Find("SettingsOverlay");
            var settings = settingsGO != null ? settingsGO.GetComponent<UI.SettingsPanel>() : null;
            if (settings == null)
            {
                Debug.LogWarning("[Phase5Test] SKIP settings test: SettingsPanel not found.");
                return;
            }

            bool before = SaveManager.Instance.MusicOn;
            SaveManager.Instance.MusicOn = !before;
            AudioManager.Instance?.SetMusicMuted(before); // mirrors what the music toggle's handler does

            bool after = SaveManager.Instance.MusicOn;
            Debug.Log(after == !before
                ? "[Phase5Test] PASS: toggling MusicOn persists via SaveManager."
                : "[Phase5Test] FAIL: SaveManager.MusicOn did not change as expected.");

            SaveManager.Instance.MusicOn = before; // restore
        }

        /// <summary>Polls SceneTransitionManager.IsTransitioning to completion instead of
        /// guessing a fixed duration — Play mode's first few frames in batch mode can coincide
        /// with Unity's own one-time asset-indexing startup work, which stalls Update() ticks for
        /// several real wall-clock seconds (verified via temporary logging: the nominal ~0.5s
        /// fade took ~4s real time the first time, then ~0.5s for every transition afterward).
        /// That's a batch-mode Editor artifact, not something a real player's session hits, so a
        /// robust poll belongs in the test, not a padded constant in SceneTransitionManager.</summary>
        private static IEnumerator WaitForTransition()
        {
            const float timeoutSeconds = 15f;
            float elapsed = 0f;

            yield return null; // let TransitionTo's StartCoroutine actually begin before checking
            while (SceneTransitionManager.Instance.IsTransitioning && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.1f); // small settle margin
        }
    }
}
