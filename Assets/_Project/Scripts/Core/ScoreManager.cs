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

        public void AddPoints(int amount)
        {
            int applied = amount * ComboMultiplier;
            CurrentMazeScore += applied;
            TotalLifetimeScore += applied;
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
