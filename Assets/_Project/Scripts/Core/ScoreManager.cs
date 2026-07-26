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

        public void AddPoints(int amount)
        {
            int applied = amount * ComboMultiplier;
            CurrentMazeScore += applied;
            TotalLifetimeScore += applied;
            OnScoreChanged?.Invoke(CurrentMazeScore);
        }

        public void ResetMazeScore()
        {
            CurrentMazeScore = 0;
            ComboMultiplier = 1;
            OnScoreChanged?.Invoke(CurrentMazeScore);
        }
    }
}
