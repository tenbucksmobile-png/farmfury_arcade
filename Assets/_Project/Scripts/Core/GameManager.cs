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

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public LevelData CurrentLevel { get; private set; }
        public CharacterData CurrentCharacter { get; private set; }
        public int DeathCountThisMaze { get; private set; }
        public LevelResult LastLevelResult { get; private set; }

        private int _cropsRemaining;
        private SceneController _sceneController;
        private GameState _stateBeforePause;
        private float _levelStartTime;

        protected override void Awake()
        {
            base.Awake();
            _sceneController = GetComponent<SceneController>();
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
        /// LevelComplete "perfect bonus" (no deaths this run).</summary>
        public void NotifyPlayerDeath()
        {
            DeathCountThisMaze++;
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

        public void EndLevel(bool success)
        {
            float elapsed = Time.time - _levelStartTime;
            CurrentState = success ? GameState.LevelComplete : GameState.LevelFailed;

            if (success && CurrentLevel != null && SaveManager.Instance != null)
            {
                LastLevelResult = ComputeLevelResult(elapsed);

                SaveManager.Instance.AddCoins(LastLevelResult.coinsEarned);
                SaveManager.Instance.SetLevelStars(CurrentLevel.levelNumber, LastLevelResult.stars);
                SaveManager.Instance.SetLevelBestScore(CurrentLevel.levelNumber, LastLevelResult.totalScore);
                SaveManager.Instance.SetLevelBestTime(CurrentLevel.levelNumber, elapsed);
                SaveManager.Instance.SetHighestLevelReached(CurrentLevel.levelNumber);
                SaveManager.Instance.SaveProgress();

                UnlockManager.Instance?.CheckUnlocksOnLevelComplete(SaveManager.Instance.HighestLevelReached);
                LeaderboardManager.Instance?.RecordLevelResult(CurrentLevel.levelNumber, LastLevelResult.totalScore, elapsed, LastLevelResult.stars);
                DailyChallengeManager.Instance?.CheckCompletionOnLevelEnd(elapsed);
            }
            else
            {
                LastLevelResult = new LevelResult { elapsedSeconds = elapsed };
            }
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
