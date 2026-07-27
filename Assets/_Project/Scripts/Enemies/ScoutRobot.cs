using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Predictive interception — targets 4 tiles ahead of the player's facing direction.
    /// Pinky equivalent.</summary>
    public class ScoutRobot : RobotBase
    {
        private const int TilesAhead = 4;

        protected override Vector2Int GetTargetPosition()
        {
            if (playerMovement == null)
            {
                return CurrentGridPosition;
            }

            Vector2Int facing = DirectionUtils.ToVector(playerMovement.CurrentDirection);
            return playerMovement.CurrentGridPosition + facing * TilesAhead;
        }
    }
}
