using System.Linq;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Local leaderboards (per the spec: "cloud sync in Phase 6"). Per-level bests live on
    /// SaveManager (GetLevelBestScore/GetLevelBestTime/GetLevelStars, all already max/min-tracked
    /// there); this class is the read/write façade GameplayHUD/LevelCompleteController/a future
    /// Leaderboards screen go through, plus the overall-stats rollup.
    /// </summary>
    public class LeaderboardManager : Singleton<LeaderboardManager>
    {
        public void RecordLevelResult(int levelIndex, int score, float timeSeconds, int stars)
        {
            SaveManager.Instance.SetLevelBestScore(levelIndex, score);
            SaveManager.Instance.SetLevelBestTime(levelIndex, timeSeconds);
            SaveManager.Instance.SetLevelStars(levelIndex, stars);
        }

        public int GetHighestLevelReached() => SaveManager.Instance.HighestLevelReached;

        public int GetTotalLifetimeScore() => ScoreManager.Instance != null ? ScoreManager.Instance.TotalLifetimeScore : 0;

        public int GetTotalCombosTriggered() => SaveManager.Instance.GetTotalCombosTriggered();

        /// <summary>"Mastered" isn't defined further by the GDD text available to this phase —
        /// approximated as "unlocked" (a stronger mastery metric like per-character win counts can
        /// replace this later without changing the call site).</summary>
        public int GetCharactersMasteredCount()
        {
            return DataManager.Instance.GetAllCharacterData()
                .Count(c => SaveManager.Instance.IsCharacterUnlocked(c.characterType));
        }
    }
}
