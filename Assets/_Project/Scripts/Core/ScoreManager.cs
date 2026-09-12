using System;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>Tracks per-maze and lifetime score. ComboMultiplier is wired up but always 1
    /// until Phase 4's combo system sets it.</summary>
    public class ScoreManager : Singleton<ScoreManager>
    {
        public event Action<int> OnScoreChanged;

        public int CurrentMazeScore { get; private set; }
        public int TotalLifetimeScore { get; private set; }
        public int ComboMultiplier { get; private set; } = 1;

        /// <summary>Category breakdown for LevelCompleteController's score breakdown display —
        /// tracked alongside CurrentMazeScore, not instead of it. Reset with it every maze.</summary>
        public int CropPoints { get; private set; }
        public int RobotPoints { get; private set; }

        /// <summary>Loaded here, not Awake — Start is the one lifecycle method Unity guarantees
        /// runs only after every object's Awake has already completed, so SaveManager.Instance is
        /// guaranteed assigned by now regardless of which GameManagers child happens to Awake first
        /// (same class of ordering fix as ComboHypeScreen's own OnEnable->Start move; see its doc
        /// comment). Without this, TotalLifetimeScore always started at 0 every session — see
        /// SaveManager.GetTotalLifetimeScore's own doc comment for the full bug writeup.</summary>
        private void Start()
        {
            TotalLifetimeScore = SaveManager.Instance != null ? SaveManager.Instance.GetTotalLifetimeScore() : 0;
        }

        public void AddPoints(int amount)
        {
            int applied = amount * ComboMultiplier;
            CurrentMazeScore += applied;
            TotalLifetimeScore += applied;
            SaveManager.Instance?.SetTotalLifetimeScore(TotalLifetimeScore);
            OnScoreChanged?.Invoke(CurrentMazeScore);
        }

        /// <summary>CropCollector calls this for crop/vegetable/power-pellet pickups.</summary>
        public void AddCropPoints(int amount)
        {
            CropPoints += amount;
            AddPoints(amount);
        }

        /// <summary>ChaseScoreManager calls this for the chain-scoring robot defeats.</summary>
        public void AddRobotPoints(int amount)
        {
            RobotPoints += amount;
            AddPoints(amount);
        }

        public void ResetMazeScore()
        {
            CurrentMazeScore = 0;
            CropPoints = 0;
            RobotPoints = 0;
            ComboMultiplier = 1;
            OnScoreChanged?.Invoke(CurrentMazeScore);
        }
    }
}
