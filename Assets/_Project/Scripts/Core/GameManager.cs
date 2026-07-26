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
        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public LevelData CurrentLevel { get; private set; }
        public CharacterData CurrentCharacter { get; private set; }

        private int _cropsRemaining;
        private SceneController _sceneController;
        private GameState _stateBeforePause;

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
            CurrentState = GameState.Playing;

            _sceneController.LoadLevelContent(level);
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
            CurrentState = success ? GameState.LevelComplete : GameState.LevelFailed;

            if (success && CurrentLevel != null && SaveManager.Instance != null)
            {
                SaveManager.Instance.SetHighestLevelReached(CurrentLevel.levelNumber);
                SaveManager.Instance.SaveProgress();
            }
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
