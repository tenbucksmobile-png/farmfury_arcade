using System.Linq;
using UnityEngine;
using FarmFuryArcade.Data;
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

        // ---- Per-world stats (2026-09-12 Leaderboard redesign) --------------------------------
        // World index -> level range matches UnlockProgression's own convention exactly
        // (world * LevelsPerWorld .. +24) so these never drift out of sync with it.

        private static (int start, int end) LevelRangeForWorld(int world)
        {
            int start = world * UnlockProgression.LevelsPerWorld;
            return (start, start + UnlockProgression.LevelsPerWorld - 1);
        }

        /// <summary>Sum of every level's best score within this world — "HighScore" on the
        /// per-world Leaderboard page is a running TOTAL across the world, not a single level's
        /// peak, per direct instruction.</summary>
        public int GetWorldTotalScore(int world)
        {
            var (start, end) = LevelRangeForWorld(world);
            int total = 0;
            for (int i = start; i <= end; i++)
            {
                total += SaveManager.Instance.GetLevelBestScore(i);
            }
            return total;
        }

        /// <summary>Fastest recorded time among this world's completed levels — 0 means no level in
        /// this world has been completed yet (GetLevelBestTime's own "0 = not recorded" convention),
        /// callers should treat that as "no data" rather than an actual 0-second clear.</summary>
        public float GetWorldFastestTime(int world)
        {
            var (start, end) = LevelRangeForWorld(world);
            float fastest = 0f;
            for (int i = start; i <= end; i++)
            {
                float time = SaveManager.Instance.GetLevelBestTime(i);
                if (time > 0f && (fastest <= 0f || time < fastest))
                {
                    fastest = time;
                }
            }
            return fastest;
        }

        /// <summary>Count of levels in this world currently sitting at exactly 1/2/3 stars — a
        /// snapshot of "where the player's at" within the world, not a cumulative star total.</summary>
        public (int oneStar, int twoStar, int threeStar) GetWorldStarCounts(int world)
        {
            var (start, end) = LevelRangeForWorld(world);
            int one = 0, two = 0, three = 0;
            for (int i = start; i <= end; i++)
            {
                switch (SaveManager.Instance.GetLevelStars(i))
                {
                    case 1: one++; break;
                    case 2: two++; break;
                    case 3: three++; break;
                }
            }
            return (one, two, three);
        }

        /// <summary>"BestFarmFury" — whichever character earned the single HIGHEST individual level
        /// score within this world (independent of GetWorldTotalScore's own summed figure above;
        /// bragging rights go to whoever set the one standout run, not whoever happened to grind the
        /// most total levels). Falls back to Cluck if no level in this world has been completed yet
        /// (never actually shown in that state — callers only display this alongside real stats).</summary>
        public CharacterType GetWorldBestCharacter(int world)
        {
            var (start, end) = LevelRangeForWorld(world);
            int bestScore = -1;
            CharacterType bestCharacter = CharacterType.Cluck;
            for (int i = start; i <= end; i++)
            {
                int score = SaveManager.Instance.GetLevelBestScore(i);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCharacter = SaveManager.Instance.GetLevelBestScoreCharacter(i);
                }
            }
            return bestCharacter;
        }
    }
}
