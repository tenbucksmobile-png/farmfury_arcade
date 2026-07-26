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

        public bool IsCharacterUnlocked(CharacterType type)
        {
            return PlayerPrefs.GetInt(CharacterUnlockedKeyPrefix + type, 0) == 1;
        }

        public void UnlockCharacter(CharacterType type)
        {
            PlayerPrefs.SetInt(CharacterUnlockedKeyPrefix + type, 1);
        }
    }
}
