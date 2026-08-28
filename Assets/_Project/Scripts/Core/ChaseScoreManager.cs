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

        /// <summary>Audit finding C7.6: the "all robots defeated on one pellet" bonus used to check
        /// `ChainCount == ChainPoints.Length` — a hardcoded 4, unreachable on any level spawning
        /// fewer than 4 robots (2 or 3 on 42 of 175 levels per the difficulty curve) and firing one
        /// robot early on the 5-robot band. Set by RobotSpawner.SpawnLevelRobots from that level's
        /// actual robot count on every level load; defaults to the old hardcoded value only if
        /// nothing ever sets it (e.g. a test harness with no RobotSpawner), preserving prior
        /// behaviour in that case rather than silently disabling the bonus.</summary>
        public int TotalRobotsThisMaze { get; private set; } = ChainPoints.Length;

        public void SetTotalRobotsThisMaze(int count)
        {
            TotalRobotsThisMaze = Mathf.Max(count, 0);
        }

        public void OnRobotDefeated()
        {
            int index = Mathf.Min(ChainCount, ChainPoints.Length - 1);
            ScoreManager.Instance.AddRobotPoints(ChainPoints[index]);
            ChainCount++;

            if (ChainCount == TotalRobotsThisMaze)
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
