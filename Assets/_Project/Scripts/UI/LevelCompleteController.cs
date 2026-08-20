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
            StartCoroutine(CelebrationSequence());
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
                yield return new WaitForSecondsRealtime(PreUnlockDelaySeconds);
                Sprite badge = levelSelectController.GetWorldSignSprite(justUnlockedWorld.Value);
                // The just-unlocked world's own gameplay backdrop, shown faded behind the badge —
                // see NewWorldUnlockScreen's doc comment. MazeType's enum order matches world index
                // directly (CornField=0, VegPatch=1, Orchard=2, Wheat=3), same convention every
                // other world<->MazeType lookup in this project relies on.
                var tileMapRenderer = FindFirstObjectByType<TileMapRenderer>();
                Sprite backdrop = tileMapRenderer != null
                    ? tileMapRenderer.GetOrAddArtSet((MazeType)justUnlockedWorld.Value).backdropSprite
                    : null;
                worldUnlockScreen.Show(badge, backdrop, () => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
            }
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
        /// chain.</summary>
        private void Play()
        {
            int nextLevelIndex = GameManager.Instance.CurrentLevel != null
                ? GameManager.Instance.CurrentLevel.levelNumber + 1
                : 0;
            levelSelectController.OpenLevelSelectForLevel(nextLevelIndex);
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
