using UnityEngine;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Flanking coordination — mirrors the player through HarvesterRobot's position, so it
    /// tends to approach from the opposite side. Inky equivalent. Falls back to direct pursuit if
    /// no HarvesterRobot exists in the scene (e.g. a level with Patrol but no Harvester).</summary>
    public class PatrolRobot : RobotBase
    {
        private HarvesterRobot _harvester;

        protected override void Start()
        {
            base.Start();
            _harvester = FindFirstObjectByType<HarvesterRobot>();
        }

        protected override Vector2Int GetTargetPosition()
        {
            if (playerMovement == null)
            {
                return CurrentGridPosition;
            }

            Vector2Int playerPos = playerMovement.CurrentGridPosition;
            if (_harvester == null)
            {
                return playerPos;
            }

            Vector2Int vector = playerPos - _harvester.CurrentGridPosition;
            return playerPos + vector;
        }
    }
}
