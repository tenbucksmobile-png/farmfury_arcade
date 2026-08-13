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

        public void LoadLevel(int levelIndex)
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
            AudioManager.Instance?.ResumeBackgroundMusic();

            _sceneController.LoadLevelContent(level);
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
        /// whether the player still has a respawn left; once MaxRespawns is exceeded this ends the
        /// run itself (EndLevel(false)) so PlayerHealth knows to skip the respawn and leave the
        /// character faded out instead.</summary>
        public bool NotifyPlayerDeath()
        {
            DeathCountThisMaze++;
            if (DeathCountThisMaze > MaxRespawns)
            {
                EndLevel(false);
                return false;
            }
            return true;
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
            AudioManager.Instance?.StopMusic();
        }

        public void EndLevel(bool success)
        {
            float elapsed = Time.time - _levelStartTime;
            CurrentState = success ? GameState.LevelComplete : GameState.LevelFailed;
            AudioManager.Instance?.StopMusic();

            JustUnlockedWorldIndex = null;

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
        /// (SaveManager.HasSeenWorldUnlock) — matches UnlockProgression/LevelSelectController.
        /// IsWorldAvailable's own gate for the star check, but deliberately does NOT compare against
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
