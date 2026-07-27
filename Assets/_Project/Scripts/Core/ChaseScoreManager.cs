using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>Tracks consecutive robot defeats within one power pellet activation: 200/400/800/
    /// 1600 points, plus a 5000 bonus for clearing all 4 in one go. RobotBase.TransitionToDefeated
    /// calls OnRobotDefeated(); PowerPelletManager calls ResetChain() when the power state ends.</summary>
    public class ChaseScoreManager : Singleton<ChaseScoreManager>
    {
        private static readonly int[] ChainPoints = { 200, 400, 800, 1600 };
        private const int FullChainBonus = 5000;

        public int ChainCount { get; private set; }

        public void OnRobotDefeated()
        {
            int index = Mathf.Min(ChainCount, ChainPoints.Length - 1);
            ScoreManager.Instance.AddRobotPoints(ChainPoints[index]);
            ChainCount++;

            if (ChainCount == ChainPoints.Length)
            {
                ScoreManager.Instance.AddRobotPoints(FullChainBonus);
            }
        }

        public void ResetChain()
        {
            ChainCount = 0;
        }
    }
}
