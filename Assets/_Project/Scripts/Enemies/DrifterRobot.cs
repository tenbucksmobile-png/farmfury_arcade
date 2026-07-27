using UnityEngine;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Distance-based mode switching — chases directly when far from the player, retreats
    /// to its own scatter corner when within retreatDistanceTiles. Dangerous only from far away.
    /// Clyde equivalent.</summary>
    public class DrifterRobot : RobotBase
    {
        [SerializeField] private float retreatDistanceTiles = 8f;

        protected override Vector2Int GetTargetPosition()
        {
            if (playerMovement == null)
            {
                return CurrentGridPosition;
            }

            float distance = Vector2Int.Distance(CurrentGridPosition, playerMovement.CurrentGridPosition);
            return distance > retreatDistanceTiles ? playerMovement.CurrentGridPosition : scatterCornerPosition;
        }
    }
}
