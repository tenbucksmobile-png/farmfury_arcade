using UnityEngine;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Direct pursuit — always targets the player's current grid cell. Blinky equivalent.</summary>
    public class HarvesterRobot : RobotBase
    {
        protected override Vector2Int GetTargetPosition()
        {
            return playerMovement != null ? playerMovement.CurrentGridPosition : CurrentGridPosition;
        }
    }
}
