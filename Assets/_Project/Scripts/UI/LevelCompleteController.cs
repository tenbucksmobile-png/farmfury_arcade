using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Star/score celebration, shown by GameplayHUD when GameManager.CurrentState becomes
    /// LevelComplete. Reads GameManager.LastLevelResult (computed in GameManager.EndLevel) rather
    /// than recomputing anything. If UnlockManager unlocked a character this level, shows
    /// NewCharacterUnlockScreen as an overlay once the celebration sequence finishes.
    ///
    /// If this completion also crossed a world's 2-star gate for the first time
    /// (GameManager.JustUnlockedWorldIndex), NewWorldUnlockScreen bursts that world's badge in
    /// afterward (waiting for any character-unlock card to finish first, so the two never overlap)
    /// and, once its own reveal/pulse/hold finishes, automatically shows Level Select in its
    /// world-select state — unlike the character-unlock celebration, which just returns silently to
    /// this screen, this one navigates on its own since the whole point is to show the freshly
    /// unlocked badge without requiring a tap.
    ///
    /// Rebuilt to a Canva mockup (2026-07-31): LevelComplete.png's panel only has room for the
    /// "LEVEL COMPLETE!" banner (baked into the art), 3 stars, and a score readout on its wooden
    /// shelf — the previous crop/robot/time/perfect-bonus breakdown, combo-achievements line, and
    /// "new best" badge are gone.
    ///
    /// A single Skip button used to be the only way off this screen (returning to Level Select),
    /// then a Play/Home/Settings row. Home and Settings were removed (2026-08-20, per a screenshot
    /// review) — Play jumps straight into Level Select's tile grid for the world containing the
    /// level that was just unlocked (via LevelSelectController.OpenLevelSelectForLevel, so the
    /// player immediately sees the newly unlocked tile rather than having to navigate there
    /// manually — this is what actually exercises the unlock chain end to end) and is now the only
    /// real navigation off this screen; DoubleCoins took over the bottom-right corner Home/Settings
    /// used to share.
    /// </summary>
    public class LevelCompleteController : MonoBehaviour
    {
        [SerializeField] private StarDisplay starDisplay;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button doubleCoinsButton;
        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private LevelSelectController levelSelectController;
        [SerializeField] private NewCharacterUnlockScreen unlockScreen;
        [SerializeField] private NewWorldUnlockScreen worldUnlockScreen;

        private const float StarStepSeconds = 0.35f;
        private const float PreStarDelaySeconds = 0.3f;
        private const float PreUnlockDelaySeconds = 0.3f;
        // Deliberately longer than PreUnlockDelaySeconds — the world-unlock celebration is a much
        // bigger beat (full-screen badge burst) than the character-unlock hand-off, so it gets its
        // own more generous pause after the score finishes counting up, so it doesn't read as
        // cutting the star/score reveal off before the player has had a moment to actually see it.
        private const float PreWorldUnlockDelaySeconds = 0.8f;

        private Coroutine _celebrationRoutine;

        private void Awake()
        {
            playButton.onClick.AddListener(Play);
            if (doubleCoinsButton != null)
            {
                doubleCoinsButton.onClick.AddListener(HandleDoubleCoins);
            }
        }

        private void OnEnable()
        {
            RefreshDoubleCoinsButton();
            // Defensive — guarantees only one celebration sequence ever runs at a time even if
            // OnEnable somehow fires twice in a row (e.g. a future ShowOnly call re-activating an
            // already-active screen) instead of two overlapping coroutines racing each other and
            // both trying to show their own unlock overlay independently.
            if (_celebrationRoutine != null)
            {
                StopCoroutine(_celebrationRoutine);
            }
            _celebrationRoutine = StartCoroutine(CelebrationSequence());
        }

        private IEnumerator CelebrationSequence()
        {
            var result = GameManager.Instance.LastLevelResult;
            int? justUnlockedWorld = GameManager.Instance.JustUnlockedWorldIndex;

            starDisplay.SetStars(0);
            scoreText.text = "0";

            yield return new WaitForSecondsRealtime(PreStarDelaySeconds);
            for (int i = 1; i <= result.stars; i++)
            {
                starDisplay.SetStars(i);
                yield return new WaitForSecondsRealtime(StarStepSeconds);
            }

            yield return CountUpScore(result.totalScore);

            if (UnlockManager.Instance != null && UnlockManager.Instance.LastUnlockedBatch.Count > 0)
            {
                yield return new WaitForSecondsRealtime(PreUnlockDelaySeconds);

                // Only block on the character card's own auto-dismiss when a world-unlock
                // celebration also needs to run right after it — otherwise this stays fire-and-
                // forget, same as before, so the plain "unlocked a character" case is unaffected.
                bool characterCelebrationDone = false;
                unlockScreen.Show(UnlockManager.Instance.LastUnlockedBatch[0], () => characterCelebrationDone = true);

                if (justUnlockedWorld.HasValue)
                {
                    yield return new WaitUntil(() => characterCelebrationDone);
                }
            }

            if (justUnlockedWorld.HasValue && worldUnlockScreen != null && levelSelectController != null)
            {
                yield return new WaitForSecondsRealtime(PreWorldUnlockDelaySeconds);
                Sprite badge = levelSelectController.GetWorldSignSprite(justUnlockedWorld.Value);
                // The just-unlocked world's own gameplay backdrop, shown faded behind the badge —
                // see NewWorldUnlockScreen's doc comment. MazeType's enum order matches world index
                // directly (CornField=0, VegPatch=1, Orchard=2, Wheat=3), same convention every
                // other world<->MazeType lookup in this project relies on.
                var tileMapRenderer = FindFirstObjectByType<TileMapRenderer>();
                Sprite backdrop = tileMapRenderer != null
                    ? tileMapRenderer.GetOrAddArtSet((MazeType)justUnlockedWorld.Value).backdropSprite
                    : null;
                if (backdrop == null)
                {
                    // Diagnostic for a real report ("world unlock page shows plain black, not the
                    // world's own backdrop") — this makes the two possible causes (no TileMapRenderer
                    // found at all vs. a genuinely-unwired MazeArtSet.backdropSprite for this world)
                    // distinguishable from the Console instead of only from a screenshot.
                    Debug.LogWarning($"[LevelCompleteController] No backdrop resolved for world index " +
                        $"{justUnlockedWorld.Value} (tileMapRenderer {(tileMapRenderer == null ? "NOT FOUND" : "found")}) " +
                        "— NewWorldUnlockScreen will show a plain black background instead of the world's own scenery.");
                }
                worldUnlockScreen.Show(badge, backdrop, () => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
            }

            _celebrationRoutine = null;
        }

        private IEnumerator CountUpScore(int target)
        {
            int shown = 0;
            while (shown < target)
            {
                shown = Mathf.Min(target, shown + Mathf.Max(20, target / 30));
                scoreText.text = shown.ToString("N0");
                yield return null;
            }
            scoreText.text = target.ToString("N0");
        }

        /// <summary>Targets the level right after the one just completed — the one whose unlock
        /// this celebration is actually about, per UnlockProgression's "predecessor needs 1+ star"
        /// chain.
        ///
        /// Real bug found and fixed (2026-09-12): a Daily Challenge completion always fell through
        /// to this same "jump into the next sequential level's world" logic — but a Daily Challenge
        /// level is picked from whichever unlocked world DailyChallengeManager.GetTodayLevelIndex()
        /// happened to land on (see its own doc comment), not a step in that world's own normal
        /// progression, so "levelNumber + 1" has no real relationship to what the player was just
        /// doing. In practice this most often opened Corn Field's tile grid (world 0 is always
        /// unlocked, so the daily pick — and thus levelNumber+1 — landed there disproportionately
        /// often), reported as "concluding the daily challenge goes directly into Cornfield." Fixed
        /// by checking DailyChallengeManager.IsPlayingDailyChallenge first: a daily-challenge
        /// completion now just shows Level Select with no pending target queued, landing on World
        /// Select the same way Level Failed's Home button already does — the only sensible "back"
        /// destination when the level just played isn't part of any single world's own sequence.</summary>
        private void Play()
        {
            bool wasDailyChallenge = DailyChallengeManager.Instance != null &&
                DailyChallengeManager.Instance.IsPlayingDailyChallenge;
            if (!wasDailyChallenge)
            {
                int nextLevelIndex = GameManager.Instance.CurrentLevel != null
                    ? GameManager.Instance.CurrentLevel.levelNumber + 1
                    : 0;
                levelSelectController.OpenLevelSelectForLevel(nextLevelIndex);
            }
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }

        /// <summary>Rewarded-ad placement #2 (Monetisation Build Plan Phase 2): tops up this
        /// completion's coin payout with an equal second copy — see GameManager.
        /// ClaimDoubleCoinsViaAd's own doc comment for why that's an additive top-up rather than a
        /// retroactive change to LastLevelResult.coinsEarned. Hidden entirely — never shown as a
        /// dead button — whenever there's nothing to claim: no ad ready, no coins earned this
        /// completion, or already claimed. Icon-only now (DoubleCoins.png, no text label — see
        /// Phase5ProjectBuilder.BuildLevelComplete for why the old text overlay was removed);
        /// "claimed" feedback is just disabling the button, same as every other icon-only button in
        /// this project that has no dedicated "used" art variant.</summary>
        private void RefreshDoubleCoinsButton()
        {
            if (doubleCoinsButton == null)
            {
                return;
            }

            bool claimed = GameManager.Instance != null && GameManager.Instance.DoubleCoinsClaimed;
            bool hasCoinsToDouble = GameManager.Instance != null && GameManager.Instance.LastLevelResult.coinsEarned > 0;
            bool adReady = Core.AdManager.Instance != null && Core.AdManager.Instance.IsRewardedAdReady;

            doubleCoinsButton.gameObject.SetActive(!claimed && hasCoinsToDouble && adReady);
        }

        private void HandleDoubleCoins()
        {
            if (Core.AdManager.Instance == null)
            {
                return;
            }

            doubleCoinsButton.interactable = false;
            Core.AdManager.Instance.ShowRewardedAd("double_coins_level_complete", rewarded =>
            {
                if (rewarded && GameManager.Instance != null && GameManager.Instance.ClaimDoubleCoinsViaAd())
                {
                    doubleCoinsButton.interactable = false;
                }
                else
                {
                    // Ad closed early/failed, or nothing left to claim — re-show as tappable rather
                    // than leaving it stuck disabled, same re-check convention RevivePromptController
                    // uses after a failed Watch Ad attempt.
                    doubleCoinsButton.interactable = true;
                    RefreshDoubleCoinsButton();
                }
            });
        }
    }
}
