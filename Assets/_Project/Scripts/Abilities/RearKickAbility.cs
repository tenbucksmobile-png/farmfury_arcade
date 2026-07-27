using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Horace's ability. Finds the nearest robot within 3-tile Manhattan distance and
    /// knocks it back 4 tiles (8 with the Crossfire combo, Billy -> Horace) in Horace's current
    /// facing direction, stunning it 2 seconds on landing (RobotBase.KnockBack handles the
    /// slide-then-stun and stops early at a wall).</summary>
    public class RearKickAbility : AbilityBase
    {
        private const int SearchRadiusTiles = 3;
        private const int KnockbackTiles = 4;
        private const float LandingStunDuration = 2f;

        protected override void Execute()
        {
            RobotBase target = FindNearestRobotWithinManhattan(Movement.CurrentGridPosition, SearchRadiusTiles);
            if (target == null)
            {
                return;
            }

            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleKnockback();
            int distance = doubled ? KnockbackTiles * 2 : KnockbackTiles;

            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;
            Vector2Int knockDirection = DirectionUtils.ToVector(facing);

            target.KnockBack(knockDirection, distance, LandingStunDuration);
        }

        private static RobotBase FindNearestRobotWithinManhattan(Vector2Int origin, int maxDistance)
        {
            RobotBase nearest = null;
            int bestDistance = int.MaxValue;

            foreach (var robot in FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
            {
                int distance = Mathf.Abs(robot.CurrentGridPosition.x - origin.x) +
                               Mathf.Abs(robot.CurrentGridPosition.y - origin.y);
                if (distance <= maxDistance && distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = robot;
                }
            }

            return nearest;
        }
    }
}
