using System;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Foundation for the Daily Challenge feature. Today's challenge type is deterministic from
    /// the UTC date (same seed for every player on a given day, matching the "everyone gets the
    /// same daily" convention). "Modified maze layout" per the GDD is content-authoring scope, not
    /// engineering scope — this reuses LevelData index DailyChallengeLevelIndex (LevelData_01) and
    /// overlays a rule/objective rather than generating a distinct maze; swap in a real daily-maze
    /// LevelData later without changing this class's shape.
    /// </summary>
    public class DailyChallengeManager : Singleton<DailyChallengeManager>
    {
        public const int DailyChallengeLevelIndex = 0;
        private const int ScoreThresholdTarget = 2000;
        private const float SpeedRunTargetSeconds = 60f;
        private const int BonusCoins = 25;

        public ChallengeType TodayChallenge { get; private set; }
        public string TodayDateKey { get; private set; }

        public bool IsCompletedToday =>
            SaveManager.Instance != null && SaveManager.Instance.GetDailyChallengeCompletedDate() == TodayDateKey;

        protected override void Awake()
        {
            base.Awake();
            DetermineTodayChallenge();
        }

        private void DetermineTodayChallenge()
        {
            TodayDateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var values = (ChallengeType[])Enum.GetValues(typeof(ChallengeType));
            var rng = new System.Random(TodayDateKey.GetHashCode());
            TodayChallenge = values[rng.Next(values.Length)];
        }

        public string GetObjectiveDescription()
        {
            return TodayChallenge switch
            {
                ChallengeType.SpeedRun => $"Complete the level in under {SpeedRunTargetSeconds:F0} seconds.",
                ChallengeType.NoPower => "Complete the level without touching a power pellet.",
                ChallengeType.CharacterLocked => "Complete the level using only your starting character.",
                ChallengeType.ComboHunt => "Trigger at least one character-swap combo this run.",
                ChallengeType.ScoreThreshold => $"Score at least {ScoreThresholdTarget} points.",
                _ => string.Empty
            };
        }

        /// <summary>Called by GameManager.EndLevel(true). Only actually awards/marks completion
        /// when playing DailyChallengeLevelIndex and the objective is met — a normal level
        /// completion elsewhere is a no-op.</summary>
        public void CheckCompletionOnLevelEnd(float elapsedSeconds)
        {
            if (IsCompletedToday || GameManager.Instance == null || GameManager.Instance.CurrentLevel == null)
            {
                return;
            }
            if (GameManager.Instance.CurrentLevel.levelNumber != DailyChallengeLevelIndex)
            {
                return;
            }
            if (!EvaluateObjective(elapsedSeconds))
            {
                return;
            }

            SaveManager.Instance.SetDailyChallengeCompletedDate(TodayDateKey);
            SaveManager.Instance.AddCoins(BonusCoins);
            SaveManager.Instance.SaveProgress();
            Debug.Log($"[DailyChallengeManager] Daily challenge ({TodayChallenge}) completed — +{BonusCoins} coins.");
        }

        private bool EvaluateObjective(float elapsedSeconds)
        {
            return TodayChallenge switch
            {
                ChallengeType.SpeedRun => elapsedSeconds < SpeedRunTargetSeconds,
                ChallengeType.NoPower => PowerPelletManager.Instance != null && !PowerPelletManager.Instance.WasActivatedThisMaze,
                ChallengeType.CharacterLocked => ComboSystem.Instance != null && ComboSystem.Instance.DistinctCharactersUsedCount <= 1,
                ChallengeType.ComboHunt => ComboSystem.Instance != null && ComboSystem.Instance.AnyComboTriggeredThisMaze,
                ChallengeType.ScoreThreshold => ScoreManager.Instance != null && ScoreManager.Instance.CurrentMazeScore >= ScoreThresholdTarget,
                _ => false
            };
        }
    }
}
