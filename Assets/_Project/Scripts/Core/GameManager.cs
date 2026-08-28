using System;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Top-level game state: current level/character, score, coins, and save coordination.
    /// Delegates actual scene content instantiation to <see cref="SceneController"/>, which
    /// lives on the same GameManagers GameObject.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        /// <summary>Snapshot of one level-complete result, computed by EndLevel(true) and read by
        /// LevelCompleteController. Deliberately a plain data holder, not a ScriptableObject —
        /// it's per-run, never persisted as an asset (SaveManager/LeaderboardManager persist the
        /// bits that matter across sessions).</summary>
        public struct LevelResult
        {
            public int cropScore;
            public int robotScore;
            public int timeBonus;
            public int perfectBonus;
            public int totalScore;
            public int stars;
            public int coinsEarned;
            public bool isNewBestScore;
            public float elapsedSeconds;
        }

        private const int TimeBonusCap = 500;
        private const int PerfectBonusCap = 500;
        private const float TimeBonusDecaySeconds = 120f;
        private const int BaseCoinsPerLevel = 10;
        private const int CoinsPerStar = 5;

        /// <summary>A maze isn't endless — the player gets 3 respawns (a 4th death ends the run) and
        /// a 2-minute clock; either exhausting triggers EndLevel(false), which GameplayHUD's
        /// state-watcher reacts to by showing LevelFailedScreen ("Try Again").</summary>
        public const int MaxRespawns = 3;
        public const float LevelTimeLimitSeconds = 120f;

        /// <summary>Monetisation: coin cost of RequestRevivePrompt's "one more life" offer — see
        /// AcceptRevive.</summary>
        public const int ReviveCoinsCost = 5;

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public LevelData CurrentLevel { get; private set; }
        public CharacterData CurrentCharacter { get; private set; }
        public int DeathCountThisMaze { get; private set; }
        public LevelResult LastLevelResult { get; private set; }

        /// <summary>Set by EndLevel(true) the instant a world's gate level crosses from &lt;2 to
        /// 2+ stars for the first time this call — the world index that just became available, or
        /// null if this completion didn't unlock a new world. Recomputed (and reset to null) on
        /// every EndLevel(true) call, same "reflects only the most recent result" convention as
        /// LastLevelResult — read it once from LevelCompleteController's celebration sequence, same
        /// frame it's set, before the next level's completion overwrites it.</summary>
        public int? JustUnlockedWorldIndex { get; private set; }

        /// <summary>Monetisation: whether this level-complete's "double coins" rewarded-ad offer
        /// has already been claimed. Reset to false on every EndLevel(true) call, same one-shot-
        /// per-completion convention as JustUnlockedWorldIndex — LevelCompleteController reads it
        /// to hide the Double Coins button once claimed, and ClaimDoubleCoinsViaAd refuses to pay
        /// out twice for the same completion even if called again.</summary>
        public bool DoubleCoinsClaimed { get; private set; }

        /// <summary>True from the moment a death would exceed MaxRespawns (RequestRevivePrompt)
        /// until AcceptRevive/DeclineRevive resolves it. PlayerHealth.DeathSequence polls this via
        /// WaitUntil instead of ending the run immediately, giving the player a chance to spend
        /// ReviveCoinsCost for one more life. Time.timeScale is frozen for the duration (same
        /// freeze PauseGame uses) so robots don't keep roaming/chasing while the prompt is up.</summary>
        public bool ReviveDecisionPending { get; private set; }

        /// <summary>Fired by RequestRevivePrompt so a UI owner (GameplayHUD) can show the actual
        /// prompt — GameManager itself has no UI knowledge, same "manager raises event, screen
        /// reacts" convention used throughout this project (see LevelCompleteController/
        /// LevelFailedScreen reacting to CurrentState changes). If nothing is subscribed when a
        /// revive is offered, RequestRevivePrompt auto-declines rather than leaving
        /// ReviveDecisionPending stuck true forever with nothing able to resolve it.</summary>
        public event Action OnReviveOffered;

        private bool _wasRevived;
        private int _cropsRemaining;
        private SceneController _sceneController;
        private GameState _stateBeforePause;
        private float _levelStartTime;

        protected override void Awake()
        {
            base.Awake();
            _sceneController = GetComponent<SceneController>();
        }

        private void Update()
        {
            if (CurrentState == GameState.Playing && GetElapsedSeconds() >= LevelTimeLimitSeconds)
            {
                EndLevel(false);
            }
        }

        /// <summary>isDailyChallenge defaults to false for every normal navigation path (a level
        /// tile tap, a Level Complete "Play", a Retry) — only LevelSelectController.
        /// PlayDailyChallenge (and a Restart/RestartLevel call that explicitly re-passes the
        /// current DailyChallengeManager.IsPlayingDailyChallenge value, so a retry stays a challenge
        /// attempt) ever pass true. Sets DailyChallengeManager's flag on every call, true or false,
        /// so it can never linger stale from an earlier challenge run into a later normal one — see
        /// DailyChallengeManager.IsPlayingDailyChallenge's own doc comment. Also applies (or clears)
        /// the challenge's robot-speed difficulty bump via the scene's RobotSpawner before content
        /// spawns, so every robot the level would normally spawn comes in already boosted.</summary>
        public void LoadLevel(int levelIndex, bool isDailyChallenge = false)
        {
            var level = DataManager.Instance.GetLevelData(levelIndex);
            if (level == null)
            {
                Debug.LogError($"[GameManager] No LevelData found for level index {levelIndex}.");
                return;
            }

            CurrentLevel = level;
            _cropsRemaining = level.totalCropsRequired;
            ScoreManager.Instance.ResetMazeScore();
            DeathCountThisMaze = 0;
            _levelStartTime = Time.time;
            CurrentState = GameState.Playing;
            // Hands off from the "Theme" landing track to this level's own world music the moment
            // the level actually begins (see AudioManager.PlayWorldMusic's own doc comment).
            AudioManager.Instance?.PlayWorldMusic(level.mazeType);

            DailyChallengeManager.Instance?.SetPlayingDailyChallenge(isDailyChallenge);
            if (_sceneController != null && _sceneController.RobotSpawner != null)
            {
                _sceneController.RobotSpawner.DifficultyMultiplier =
                    isDailyChallenge ? DailyChallengeManager.RobotDifficultySpeedMultiplier : 1f;
            }

            _sceneController.LoadLevelContent(level);

            // Between-levels interstitial trigger — deliberately called here (never mid-Playing,
            // since LoadLevel only ever runs at a level transition) rather than from wherever the
            // player tapped a level tile, so every LoadLevel call site gets this for free. Time is
            // frozen (same convention PauseGame/RequestRevivePrompt use) for whatever gap
            // NotifyLevelLoaded takes to resolve, so a due interstitial genuinely gates the start of
            // play instead of showing as an overlay while the player/robots/timer keep running
            // behind it — _levelStartTime is already stamped above, and Time.time (what
            // GetElapsedSeconds reads) doesn't advance while frozen, so no time is lost either.
            // AudioListener.pause is set alongside the timeScale freeze — Time.timeScale alone only
            // stops movement/animation (anything driven by scaled Time.deltaTime); AudioSource
            // playback is real-time and completely unaffected by timeScale, so the world music
            // PlayWorldMusic already started above (and any 0-delay robot-spawn SFX fired
            // synchronously by LoadLevelContent just before this block) kept playing audibly under
            // the interstitial even though gameplay itself was correctly frozen — reported via a
            // Daily Challenge playtest ("I can hear it playing while the ad is running") but not
            // actually specific to that flow; every interstitial-gated LoadLevel call had this same
            // gap. AudioListener.pause silences every AudioSource in the scene regardless of
            // timeScale, which is exactly what's needed here. When no ad is due/ready,
            // NotifyLevelLoaded's callback fires back-to-back on the same frame and both the freeze
            // and the mute are imperceptible.
            if (AdManager.Instance != null)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                AdManager.Instance.NotifyLevelLoaded(() =>
                {
                    Time.timeScale = 1f;
                    AudioListener.pause = false;
                });
            }
        }

        /// <summary>Seconds since LoadLevel while Playing/Paused; frozen at the final value once
        /// the level ends (LastLevelResult.elapsedSeconds). Used by GameplayHUD's timer and by
        /// DailyChallengeManager's speed-run check.</summary>
        public float GetElapsedSeconds()
        {
            return CurrentState is GameState.LevelComplete or GameState.LevelFailed
                ? LastLevelResult.elapsedSeconds
                : Time.time - _levelStartTime;
        }

        /// <summary>Called by PlayerHealth every time the death sequence starts — tracked for the
        /// LevelComplete "perfect bonus" (no deaths this run) and for the respawn cap. Returns
        /// whether the player still has a respawn left; once MaxRespawns is exceeded this no longer
        /// ends the run immediately — it raises a revive-for-coins offer instead (RequestRevivePrompt)
        /// and still returns false, so PlayerHealth knows this wasn't a normal respawn and needs to
        /// wait (ReviveDecisionPending) for the outcome before deciding whether to respawn or stay
        /// faded out.</summary>
        public bool NotifyPlayerDeath()
        {
            DeathCountThisMaze++;
            if (DeathCountThisMaze > MaxRespawns)
            {
                RequestRevivePrompt();
                return false;
            }
            return true;
        }

        /// <summary>The 4th (or later) death this maze — offers the player a chance to spend
        /// ReviveCoinsCost for one more life instead of ending the run outright. Freezes time for
        /// the duration of the decision (mirrors PauseGame's freeze) so nothing moves underneath the
        /// faded-out character while the prompt is up.</summary>
        private void RequestRevivePrompt()
        {
            ReviveDecisionPending = true;
            _wasRevived = false;

            if (OnReviveOffered == null)
            {
                // No UI is listening (e.g. a test harness with no GameplayHUD) — auto-decline
                // rather than leaving ReviveDecisionPending stuck true with nothing able to
                // resolve it, which would hang PlayerHealth's WaitUntil forever.
                DeclineRevive();
                return;
            }

            Time.timeScale = 0f;
            OnReviveOffered.Invoke();
        }

        /// <summary>Called by the revive prompt's "No Thanks" button (or RequestRevivePrompt's own
        /// no-listener safety net). Ends the run exactly as an unrevived 4th death always did.</summary>
        public void DeclineRevive()
        {
            if (!ReviveDecisionPending)
            {
                return;
            }

            ReviveDecisionPending = false;
            Time.timeScale = 1f;
            EndLevel(false);
        }

        /// <summary>Called by the revive prompt's "Revive" button. Spends ReviveCoinsCost coins and,
        /// if that succeeds, grants exactly one more life — resetting DeathCountThisMaze back to
        /// MaxRespawns (not below it) so the very next death offers the prompt again rather than
        /// silently handing out a free extra respawn cushion beyond the one just paid for. Returns
        /// whether the revive actually happened, so the caller (RevivePromptController) can leave
        /// the prompt open on an insufficient-funds tap rather than dismissing it — though the
        /// button should already be disabled in that case (see RevivePromptController.Show).</summary>
        public bool AcceptRevive()
        {
            if (!ReviveDecisionPending)
            {
                return false;
            }
            if (SaveManager.Instance == null || !SaveManager.Instance.SpendCoins(ReviveCoinsCost))
            {
                return false;
            }

            GrantRevive();
            return true;
        }

        /// <summary>Called by the revive prompt's "Watch Ad" button once AdManager confirms the
        /// reward was actually granted (never on a merely-closed/skipped ad — see
        /// AdManager.ShowRewardedAd's own doc comment on that distinction). Same effect as
        /// AcceptRevive, minus the coin spend — watching the ad IS the payment.</summary>
        public bool AcceptReviveViaAd()
        {
            if (!ReviveDecisionPending)
            {
                return false;
            }

            GrantRevive();
            return true;
        }

        /// <summary>Shared by AcceptRevive/AcceptReviveViaAd — resets DeathCountThisMaze back to
        /// MaxRespawns (not below it) so the next death offers the prompt again rather than
        /// silently handing out a free extra respawn cushion, and unfreezes time.</summary>
        private void GrantRevive()
        {
            DeathCountThisMaze = MaxRespawns;
            _wasRevived = true;
            ReviveDecisionPending = false;
            Time.timeScale = 1f;
        }

        /// <summary>Called by LevelCompleteController's "Double Coins" button once AdManager
        /// confirms the reward was actually granted (never on a merely-closed/skipped ad — same
        /// distinction AcceptReviveViaAd relies on). Pays out a second copy of this completion's
        /// LastLevelResult.coinsEarned — "double" meaning the ad grants an equal top-up on top of
        /// what EndLevel(true) already paid, not a retroactive change to LastLevelResult.coinsEarned
        /// itself (that field stays a record of the base payout). Refuses to pay out twice for the
        /// same completion (DoubleCoinsClaimed) or when there's nothing to double.</summary>
        public bool ClaimDoubleCoinsViaAd()
        {
            if (DoubleCoinsClaimed || LastLevelResult.coinsEarned <= 0 || SaveManager.Instance == null)
            {
                return false;
            }

            SaveManager.Instance.AddCoins(LastLevelResult.coinsEarned);
            SaveManager.Instance.SaveProgress();
            DoubleCoinsClaimed = true;
            return true;
        }

        /// <summary>Consumed exactly once by PlayerHealth.DeathSequence right after its WaitUntil on
        /// ReviveDecisionPending unblocks — tells it whether to fall through to the normal respawn
        /// path or stay faded out (DeclineRevive already ended the run in that case).</summary>
        public bool ConsumeRevived()
        {
            bool result = _wasRevived;
            _wasRevived = false;
            return result;
        }

        /// <summary>Called by CropCollector for every crop or power pellet collected. Both count
        /// toward level completion, matching the original arcade convention that everything on
        /// the board must be cleared.</summary>
        public void NotifyCropCollected()
        {
            _cropsRemaining = Mathf.Max(0, _cropsRemaining - 1);
            if (_cropsRemaining <= 0 && CurrentState == GameState.Playing)
            {
                Debug.Log("[GameManager] Level Complete! All crops collected.");
                EndLevel(true);
            }
        }

        public void SelectCharacter(CharacterType type)
        {
            CurrentCharacter = DataManager.Instance.GetCharacterData(type);
        }

        public void PauseGame()
        {
            if (CurrentState == GameState.Paused)
            {
                return;
            }

            _stateBeforePause = CurrentState;
            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused)
            {
                return;
            }

            CurrentState = _stateBeforePause;
            Time.timeScale = 1f;
        }

        /// <summary>Abandons the current run without recording a level-failed result — used by
        /// PauseMenuController's Quit button, which returns to Level Select (not Main Menu — one
        /// step back to where the player picked this level from, not all the way out). Deliberately
        /// distinct from EndLevel(false): that path sets GameState.LevelFailed, which GameplayHUD's
        /// state-watcher reacts to by showing the Level Failed ("Try Again") screen — not what a
        /// deliberate quit should do.</summary>
        public void QuitToLevelSelect()
        {
            CurrentState = GameState.LevelSelect;
            Time.timeScale = 1f;
            // Landing/menu track resumes instead of cutting to silence — matches EndLevel's own
            // fix below (see its comment) so leaving gameplay by any path always leaves music
            // playing, never stopped outright.
            AudioManager.Instance?.PlayLandingMusic();
        }

        public void EndLevel(bool success)
        {
            float elapsed = Time.time - _levelStartTime;
            CurrentState = success ? GameState.LevelComplete : GameState.LevelFailed;
            // Reverts to the landing/menu track rather than stopping music outright — per
            // feedback that Main Menu's music should "play all the way through," only ever
            // swapped out (never silenced) while a level is actually running.
            AudioManager.Instance?.PlayLandingMusic();

            JustUnlockedWorldIndex = null;
            DoubleCoinsClaimed = false;

            if (success && CurrentLevel != null && SaveManager.Instance != null)
            {
                LastLevelResult = ComputeLevelResult(elapsed);

                int levelNumber = CurrentLevel.levelNumber;

                SaveManager.Instance.AddCoins(LastLevelResult.coinsEarned);
                SaveManager.Instance.SetLevelStars(levelNumber, LastLevelResult.stars);
                SaveManager.Instance.SetLevelBestScore(levelNumber, LastLevelResult.totalScore);
                SaveManager.Instance.SetLevelBestTime(levelNumber, elapsed);
                SaveManager.Instance.SetHighestLevelReached(levelNumber);
                SaveManager.Instance.SaveProgress();

                JustUnlockedWorldIndex = ComputeJustUnlockedWorld(levelNumber, LastLevelResult.stars);
                if (JustUnlockedWorldIndex.HasValue)
                {
                    SaveManager.Instance.SetWorldUnlockSeen(JustUnlockedWorldIndex.Value);
                    SaveManager.Instance.SaveProgress();
                }

                UnlockManager.Instance?.CheckUnlocksOnLevelComplete(SaveManager.Instance.HighestLevelReached);
                LeaderboardManager.Instance?.RecordLevelResult(CurrentLevel.levelNumber, LastLevelResult.totalScore, elapsed, LastLevelResult.stars);
                DailyChallengeManager.Instance?.CheckCompletionOnLevelEnd(elapsed);
            }
            else
            {
                LastLevelResult = new LevelResult { elapsedSeconds = elapsed };
            }
        }

        /// <summary>The level just completed unlocks a new world only if it's the last level of a
        /// world (its own index is a world's gate level), its stars now meet the 2-star gate
        /// threshold, and that world's unlock celebration hasn't already been shown
        /// (SaveManager.HasSeenWorldUnlock) — matches UnlockProgression.IsWorldUnlocked's own gate
        /// for the star check, but deliberately does NOT compare against
        /// this level's stars-before-this-call: an earlier version did, which meant the celebration
        /// silently never fired for anyone who reached 2+ stars on a gate level any way other than
        /// this exact EndLevel transition (e.g. SceneCleanupBuilder's "Set 3 Stars on all levels"
        /// debug tool, or simply having already 2-starred the level in an earlier session/before
        /// this feature existed) — the world was genuinely unlocked, but the player had still never
        /// actually seen the celebration for it. The persisted HasSeenWorldUnlock flag is the
        /// correct one-shot gate instead (same convention as IsCharacterUnlocked), and it's set
        /// immediately once this returns non-null so replaying the gate level again never re-fires
        /// it. Also requires the next world to actually have authored LevelData — World 3/4
        /// currently have art but no levels, and celebrating an empty, unplayable world would be
        /// confusing.</summary>
        private static int? ComputeJustUnlockedWorld(int levelNumber, int starsAfter)
        {
            const int WorldGateStarRequirement = 2;

            bool isWorldGateLevel = (levelNumber + 1) % UnlockProgression.LevelsPerWorld == 0;
            if (!isWorldGateLevel || starsAfter < WorldGateStarRequirement)
            {
                return null;
            }

            int nextWorld = (levelNumber + 1) / UnlockProgression.LevelsPerWorld;
            // Purchase-gated worlds (e.g. FrostbiteGarden) aren't unlocked by star progress at
            // all — finishing Wheat with 2+ stars should never trigger this celebration for a
            // world the player hasn't bought. Its own "just became available" moment is the
            // purchase itself, not a level-complete transition.
            if (UnlockProgression.IsPurchaseGatedWorld(nextWorld))
            {
                return null;
            }
            int nextWorldFirstLevel = nextWorld * UnlockProgression.LevelsPerWorld;
            if (nextWorldFirstLevel >= UnlockProgression.TotalLevels ||
                DataManager.Instance == null || DataManager.Instance.GetLevelData(nextWorldFirstLevel) == null)
            {
                return null;
            }

            if (SaveManager.Instance.HasSeenWorldUnlock(nextWorld))
            {
                return null;
            }

            return nextWorld;
        }

        /// <summary>Time and perfect-run bonuses are folded into ScoreManager.CurrentMazeScore
        /// here (so the running score display and the final total agree) — crop/robot points were
        /// already added incrementally during play via ScoreManager.AddCropPoints/AddRobotPoints.
        /// Star thresholds are LevelData.ComputeMaxPossibleScoreEstimate()-relative: 1 star for
        /// completing at all, 2 at 75% of that estimate, 3 at 95%.</summary>
        private LevelResult ComputeLevelResult(float elapsedSeconds)
        {
            int cropScore = ScoreManager.Instance.CropPoints;
            int robotScore = ScoreManager.Instance.RobotPoints;

            int timeBonus = Mathf.RoundToInt(Mathf.Clamp01(1f - elapsedSeconds / TimeBonusDecaySeconds) * TimeBonusCap);
            int perfectBonus = DeathCountThisMaze == 0 ? PerfectBonusCap : 0;
            if (timeBonus > 0 || perfectBonus > 0)
            {
                ScoreManager.Instance.AddPoints(timeBonus + perfectBonus);
            }

            int totalScore = ScoreManager.Instance.CurrentMazeScore;
            int maxPossible = CurrentLevel.ComputeMaxPossibleScoreEstimate();
            int stars = ComputeStars(totalScore, maxPossible);
            int coinsEarned = BaseCoinsPerLevel + stars * CoinsPerStar;
            bool isNewBest = totalScore > SaveManager.Instance.GetLevelBestScore(CurrentLevel.levelNumber);

            return new LevelResult
            {
                cropScore = cropScore,
                robotScore = robotScore,
                timeBonus = timeBonus,
                perfectBonus = perfectBonus,
                totalScore = totalScore,
                stars = stars,
                coinsEarned = coinsEarned,
                isNewBestScore = isNewBest,
                elapsedSeconds = elapsedSeconds
            };
        }

        private static int ComputeStars(int score, int maxPossibleScore)
        {
            if (maxPossibleScore <= 0)
            {
                return score > 0 ? 1 : 0;
            }

            float pct = (float)score / maxPossibleScore;
            if (pct >= 0.95f) return 3;
            if (pct >= 0.75f) return 2;
            return 1; // per spec: 1 star just for completing the level
        }

        public int GetCurrentScore()
        {
            return ScoreManager.Instance.CurrentMazeScore;
        }

        public void AddScore(int amount)
        {
            ScoreManager.Instance.AddPoints(amount);
        }
    }
}
