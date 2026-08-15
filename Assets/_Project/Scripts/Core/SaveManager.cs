using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Persists player progress to PlayerPrefs: highest level reached, character unlocks,
    /// coin balance, per-level star ratings, and settings preferences.
    /// </summary>
    public class SaveManager : Singleton<SaveManager>
    {
        private const string HighestLevelKey = "FFA_HighestLevel";
        private const string CoinBalanceKey = "FFA_CoinBalance";
        private const string LevelStarsKeyPrefix = "FFA_LevelStars_";
        private const string CharacterUnlockedKeyPrefix = "FFA_CharacterUnlocked_";
        private const string WorldUnlockSeenKeyPrefix = "FFA_WorldUnlockSeen_";
        private const string LevelBestScoreKeyPrefix = "FFA_LevelBestScore_";
        private const string LevelBestTimeKeyPrefix = "FFA_LevelBestTime_";
        private const string MusicOnKey = "FFA_MusicOn";
        private const string SfxOnKey = "FFA_SfxOn";
        private const string MusicVolumeKey = "FFA_MusicVolume";
        private const string SfxVolumeKey = "FFA_SfxVolume";
        private const string VibrationOnKey = "FFA_VibrationOn";
        private const string LanguageKey = "FFA_Language";
        private const string LeftHandedKey = "FFA_LeftHanded";
        private const string DailyChallengeCompletedDateKey = "FFA_DailyChallengeCompletedDate";
        private const string TotalCombosTriggeredKey = "FFA_TotalCombosTriggered";

        /// <summary>Bound used only by ResetAllProgress's per-level key sweep — matches the GDD's
        /// 100-level World 1-6 scope even though only a handful of LevelData assets exist so far;
        /// deleting a PlayerPrefs key that was never set is a harmless no-op.</summary>
        private const int MaxLevelsForReset = 100;

        public int HighestLevelReached { get; private set; }
        public int CoinBalance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            LoadProgress();
        }

        public void SaveProgress()
        {
            PlayerPrefs.SetInt(HighestLevelKey, HighestLevelReached);
            PlayerPrefs.SetInt(CoinBalanceKey, CoinBalance);
            PlayerPrefs.Save();
        }

        public void LoadProgress()
        {
            HighestLevelReached = PlayerPrefs.GetInt(HighestLevelKey, 0);
            CoinBalance = PlayerPrefs.GetInt(CoinBalanceKey, 0);

            // Starter characters are unlocked by default.
            if (!PlayerPrefs.HasKey(CharacterUnlockedKeyPrefix + CharacterType.Cluck))
            {
                UnlockCharacter(CharacterType.Cluck);
            }
            if (!PlayerPrefs.HasKey(CharacterUnlockedKeyPrefix + CharacterType.Bessie))
            {
                UnlockCharacter(CharacterType.Bessie);
            }
        }

        public void SetHighestLevelReached(int levelIndex)
        {
            if (levelIndex > HighestLevelReached)
            {
                HighestLevelReached = levelIndex;
            }
        }

        public void AddCoins(int amount)
        {
            CoinBalance += amount;
        }

        public bool SpendCoins(int amount)
        {
            if (CoinBalance < amount)
            {
                return false;
            }

            CoinBalance -= amount;
            return true;
        }

        public int GetLevelStars(int levelIndex)
        {
            return PlayerPrefs.GetInt(LevelStarsKeyPrefix + levelIndex, 0);
        }

        public void SetLevelStars(int levelIndex, int stars)
        {
            int existing = GetLevelStars(levelIndex);
            if (stars > existing)
            {
                PlayerPrefs.SetInt(LevelStarsKeyPrefix + levelIndex, stars);
            }
        }

        /// <summary>Sum of stars across every level slot up to MaxLevelsForReset — used by the
        /// Level Select star counter. Levels never played simply contribute 0 (GetLevelStars'
        /// default), same convention ResetAllProgress's per-level sweep already relies on.</summary>
        public int GetTotalStars()
        {
            int total = 0;
            for (int i = 0; i < MaxLevelsForReset; i++)
            {
                total += GetLevelStars(i);
            }
            return total;
        }

        public bool IsCharacterUnlocked(CharacterType type)
        {
            return PlayerPrefs.GetInt(CharacterUnlockedKeyPrefix + type, 0) == 1;
        }

        public void UnlockCharacter(CharacterType type)
        {
            PlayerPrefs.SetInt(CharacterUnlockedKeyPrefix + type, 1);
        }

        /// <summary>Whether NewWorldUnlockScreen's celebration has already played for this world
        /// index — deliberately independent of GetLevelStars' own gate-star value: a world can
        /// become star-eligible without ever going through GameManager.EndLevel (e.g. via
        /// SceneCleanupBuilder's "Set 3 Stars on all levels" debug tool, or replaying an
        /// already-2-starred gate level), and in either case the celebration still hasn't actually
        /// been shown to the player yet. Same "persisted one-shot flag" convention as
        /// IsCharacterUnlocked/UnlockCharacter.</summary>
        public bool HasSeenWorldUnlock(int world)
        {
            return PlayerPrefs.GetInt(WorldUnlockSeenKeyPrefix + world, 0) == 1;
        }

        public void SetWorldUnlockSeen(int world)
        {
            PlayerPrefs.SetInt(WorldUnlockSeenKeyPrefix + world, 1);
        }

        // ---- Leaderboard (local, Phase 5 — LeaderboardManager) --------------------------------

        public int GetLevelBestScore(int levelIndex)
        {
            return PlayerPrefs.GetInt(LevelBestScoreKeyPrefix + levelIndex, 0);
        }

        public void SetLevelBestScore(int levelIndex, int score)
        {
            if (score > GetLevelBestScore(levelIndex))
            {
                PlayerPrefs.SetInt(LevelBestScoreKeyPrefix + levelIndex, score);
            }
        }

        /// <summary>0 means "no time recorded yet" — always check GetLevelBestTime(i) <= 0 before
        /// treating a new time as not-a-best.</summary>
        public float GetLevelBestTime(int levelIndex)
        {
            return PlayerPrefs.GetFloat(LevelBestTimeKeyPrefix + levelIndex, 0f);
        }

        public void SetLevelBestTime(int levelIndex, float seconds)
        {
            float existing = GetLevelBestTime(levelIndex);
            if (existing <= 0f || seconds < existing)
            {
                PlayerPrefs.SetFloat(LevelBestTimeKeyPrefix + levelIndex, seconds);
            }
        }

        public int GetTotalCombosTriggered()
        {
            return PlayerPrefs.GetInt(TotalCombosTriggeredKey, 0);
        }

        public void IncrementTotalCombosTriggered()
        {
            PlayerPrefs.SetInt(TotalCombosTriggeredKey, GetTotalCombosTriggered() + 1);
        }

        // ---- Settings (SettingsPanel) ----------------------------------------------------------

        public bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicOnKey, 1) == 1;
            set => PlayerPrefs.SetInt(MusicOnKey, value ? 1 : 0);
        }

        public bool SfxOn
        {
            get => PlayerPrefs.GetInt(SfxOnKey, 1) == 1;
            set => PlayerPrefs.SetInt(SfxOnKey, value ? 1 : 0);
        }

        // Default (before the player ever touches the Settings slider) is deliberately soft/
        // background-level rather than full volume — the background music track is meant to sit
        // behind gameplay, not compete with it.
        public float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.5f);
            set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        }

        public float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public bool VibrationOn
        {
            get => PlayerPrefs.GetInt(VibrationOnKey, 1) == 1;
            set => PlayerPrefs.SetInt(VibrationOnKey, value ? 1 : 0);
        }

        public string Language
        {
            get => PlayerPrefs.GetString(LanguageKey, "English");
            set => PlayerPrefs.SetString(LanguageKey, value);
        }

        public bool LeftHanded
        {
            get => PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;
            set => PlayerPrefs.SetInt(LeftHandedKey, value ? 1 : 0);
        }

        // ---- Daily Challenge --------------------------------------------------------------------

        /// <summary>"" if never completed. Compare against DailyChallengeManager.TodayDateKey.</summary>
        public string GetDailyChallengeCompletedDate()
        {
            return PlayerPrefs.GetString(DailyChallengeCompletedDateKey, string.Empty);
        }

        public void SetDailyChallengeCompletedDate(string dateKey)
        {
            PlayerPrefs.SetString(DailyChallengeCompletedDateKey, dateKey);
        }

        // ---- Reset ------------------------------------------------------------------------------

        /// <summary>SettingsPanel's "Reset Progress" button, after confirmation. Deletes every
        /// known PlayerPrefs key (via ResetAllProgressKeys, static so it's also reachable from
        /// Editor tooling with no live SaveManager instance — see SceneCleanupBuilder's
        /// "Reset All Progress (Testing)" menu item), then re-loads so starter-character unlocks
        /// (Cluck/Bessie) are re-applied immediately rather than waiting for the next app launch.</summary>
        public void ResetAllProgress()
        {
            ResetAllProgressKeys();
            LoadProgress(); // re-applies starter-character unlocks (Cluck/Bessie)
        }

        /// <summary>The actual PlayerPrefs deletion, split out of ResetAllProgress so it can run
        /// with no live SaveManager instance (Singleton&lt;T&gt; only ever assigns Instance from a
        /// real scene Awake() — see Singleton's own doc comment — so an Editor-only tool can't call
        /// the instance method directly without first entering Play mode). PlayerPrefs has no
        /// key-enumeration/prefix-delete API, so every known key is deleted explicitly (per-level
        /// keys swept across MaxLevelsForReset — deleting a key that was never set is a harmless
        /// no-op). Does NOT reset settings (music/sfx/language/etc.) — those aren't "progress".
        /// Callers that only need the on-disk state cleared (e.g. an Edit-mode Editor tool with no
        /// SaveManager instance to update) can call this directly instead of ResetAllProgress.</summary>
        public static void ResetAllProgressKeys()
        {
            PlayerPrefs.DeleteKey(HighestLevelKey);
            PlayerPrefs.DeleteKey(CoinBalanceKey);
            PlayerPrefs.DeleteKey(TotalCombosTriggeredKey);
            PlayerPrefs.DeleteKey(DailyChallengeCompletedDateKey);

            foreach (CharacterType type in System.Enum.GetValues(typeof(CharacterType)))
            {
                PlayerPrefs.DeleteKey(CharacterUnlockedKeyPrefix + type);
            }

            int maxWorldsForReset = Mathf.CeilToInt(MaxLevelsForReset / (float)UnlockProgression.LevelsPerWorld);
            for (int world = 0; world < maxWorldsForReset; world++)
            {
                PlayerPrefs.DeleteKey(WorldUnlockSeenKeyPrefix + world);
            }

            for (int i = 0; i < MaxLevelsForReset; i++)
            {
                PlayerPrefs.DeleteKey(LevelStarsKeyPrefix + i);
                PlayerPrefs.DeleteKey(LevelBestScoreKeyPrefix + i);
                PlayerPrefs.DeleteKey(LevelBestTimeKeyPrefix + i);
            }

            PlayerPrefs.Save();
        }
    }
}
