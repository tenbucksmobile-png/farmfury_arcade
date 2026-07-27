using System;
using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Checks CharacterData.unlockLevel against mazes-completed on level complete and unlocks any
    /// character that's now eligible. SaveManager only tracks HighestLevelReached (a 0-indexed
    /// level number), not a separate "mazes completed" counter, so mazesCompleted is approximated
    /// as HighestLevelReached + 1 — accurate as long as levels are played sequentially without
    /// gaps, which matches how LoadLevel/EndLevel are used everywhere else in this project.
    /// </summary>
    public class UnlockManager : Singleton<UnlockManager>
    {
        public event Action<CharacterType> OnCharacterUnlocked;

        private readonly List<CharacterType> _lastUnlockedBatch = new List<CharacterType>();
        /// <summary>Whatever CheckUnlocksOnLevelComplete's most recent call unlocked (often empty).
        /// LevelCompleteController reads this after its celebration sequence to decide whether to
        /// show NewCharacterUnlockScreen.</summary>
        public IReadOnlyList<CharacterType> LastUnlockedBatch => _lastUnlockedBatch;

        public void CheckUnlocksOnLevelComplete(int highestLevelReached)
        {
            int mazesCompleted = highestLevelReached + 1;
            _lastUnlockedBatch.Clear();

            foreach (var character in DataManager.Instance.GetAllCharacterData())
            {
                if (character.unlockLevel <= 0)
                {
                    continue; // starter character, already unlocked by SaveManager.LoadProgress
                }
                if (SaveManager.Instance.IsCharacterUnlocked(character.characterType))
                {
                    continue;
                }
                if (mazesCompleted < character.unlockLevel)
                {
                    continue;
                }

                SaveManager.Instance.UnlockCharacter(character.characterType);
                _lastUnlockedBatch.Add(character.characterType);
                Debug.Log($"[UnlockManager] Unlocked {character.characterType} after {mazesCompleted} mazes completed.");
                OnCharacterUnlocked?.Invoke(character.characterType);
            }

            if (_lastUnlockedBatch.Count > 0)
            {
                SaveManager.Instance.SaveProgress();
            }
        }
    }
}
