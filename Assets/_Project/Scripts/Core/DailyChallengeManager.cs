using System;
using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Foundation for the Daily Challenge feature. Today's challenge type AND today's level are
    /// both deterministic from the UTC date (same seed for every player on a given day, matching
    /// the "everyone gets the same daily" convention) — though since the level pool is restricted
    /// to worlds this player has actually unlocked (see GetTodayLevelIndex), two players can still
    /// land on different levels the same day if their progress differs. Rather than a distinct
    /// "modified maze layout" (content-authoring scope per the GDD, not built), the challenge picks
    /// a real, already-authored level and layers a harder robot-speed multiplier on top
    /// (RobotDifficultySpeedMultiplier) plus the existing rule/objective overlay — see
    /// LevelSelectController.PlayDailyChallenge for how a run is actually launched, and
    /// GameManager.LoadLevel's isDailyChallenge parameter for how both are threaded through.
    /// </summary>
    public class DailyChallengeManager : Singleton<DailyChallengeManager>
    {
        private const int ScoreThresholdTarget = 2000;
        private const float SpeedRunTargetSeconds = 60f;
        private const int BonusCoins = 25;

        /// <summary>Robot movement-speed multiplier applied for the duration of a daily-challenge
        /// run (see RobotBase.SetDifficultyMultiplier/RobotSpawner.DifficultyMultiplier) — the
        /// "relatively difficult" factor that distinguishes a daily-challenge playthrough of a level
        /// from a normal one, on top of whichever robots that level already spawns.</summary>
        public const float RobotDifficultySpeedMultiplier = 1.25f;

        public ChallengeType TodayChallenge { get; private set; }
        public string TodayDateKey { get; private set; }

        /// <summary>True only for the duration of a run launched via the Daily Challenge shield
        /// (LevelSelectController.PlayDailyChallenge) — set by GameManager.LoadLevel's
        /// isDailyChallenge parameter on every level load, normal or otherwise, so it can never go
        /// stale: a normal tile tap (isDailyChallenge: false, the default) always clears it, and a
        /// Restart/Retry that explicitly re-passes the current value keeps it set across a retry of
        /// the same challenge attempt. CheckCompletionOnLevelEnd and the robot-difficulty bump both
        /// key off this rather than the played level's index, since today's level is now a real,
        /// normally-reachable level in its own right (unlike the old fixed LevelData_01 approach),
        /// so index alone can no longer tell a challenge run apart from an ordinary one.</summary>
        public bool IsPlayingDailyChallenge { get; private set; }

        public bool IsCompletedToday =>
            SaveManager.Instance != null && SaveManager.Instance.GetDailyChallengeCompletedDate() == TodayDateKey;

        private int? _todayLevelIndex;

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

        public void SetPlayingDailyChallenge(bool playing)
        {
            IsPlayingDailyChallenge = playing;
        }

        /// <summary>Today's level index — a real, already-authored level from whichever worlds are
        /// currently unlocked for THIS player (UnlockProgression.IsWorldUnlocked), so the challenge
        /// never points at a world the player can't otherwise reach yet. Deterministic per UTC date
        /// via its own RNG (seeded separately from TodayChallenge's, so changing the number of
        /// ChallengeType values can never shift which level gets picked) and computed lazily on
        /// first call rather than in Awake, since SaveManager's Awake-order relative to this
        /// singleton isn't guaranteed — by the time Level Select is actually opened and this is
        /// first called, every manager on GameManagers has long since finished initialising.
        /// Cached for the lifetime of the app session, same "doesn't re-check for a date rollover
        /// mid-session" limitation TodayChallenge already has.</summary>
        public int GetTodayLevelIndex()
        {
            if (!_todayLevelIndex.HasValue)
            {
                _todayLevelIndex = DetermineTodayLevelIndex();
            }
            return _todayLevelIndex.Value;
        }

        private int DetermineTodayLevelIndex()
        {
            int worldCount = Mathf.CeilToInt((float)UnlockProgression.TotalLevels / UnlockProgression.LevelsPerWorld);
            var unlockedWorlds = new List<int>();
            for (int world = 0; world < worldCount; world++)
            {
                if (UnlockProgression.IsWorldUnlocked(world))
                {
                    unlockedWorlds.Add(world);
                }
            }
            if (unlockedWorlds.Count == 0)
            {
                unlockedWorlds.Add(0); // World 0 is always unlocked — defensive only.
            }

            var rng = new System.Random((TodayDateKey + "-level").GetHashCode());
            int chosenWorld = unlockedWorlds[rng.Next(unlockedWorlds.Count)];
            int levelInWorld = rng.Next(UnlockProgression.LevelsPerWorld);
            return chosenWorld * UnlockProgression.LevelsPerWorld + levelInWorld;
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
        /// when this run was launched via the Daily Challenge shield (IsPlayingDailyChallenge) and
        /// the objective is met — an ordinary level completion elsewhere is a no-op, including a
        /// normal replay of today's own challenge level tapped from its regular tile.</summary>
        public void CheckCompletionOnLevelEnd(float elapsedSeconds)
        {
            if (!IsPlayingDailyChallenge)
            {
                return;
            }
            if (IsCompletedToday || GameManager.Instance == null || GameManager.Instance.CurrentLevel == null)
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
