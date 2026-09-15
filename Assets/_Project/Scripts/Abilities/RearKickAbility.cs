using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Horace's ability. Finds the nearest robot within 3-tile Manhattan distance and
    /// knocks it back 4 tiles (8 with the Crossfire combo, Billy -> Horace) in Horace's current
    /// facing direction, defeating it on landing (RobotBase.KnockBack handles the slide-then-
    /// defeat and stops early at a wall). Was a stun; changed per a gameplay rule that a deployed
    /// ability hazard should kill a robot that runs through it, not just incapacitate it.</summary>
    public class RearKickAbility : AbilityBase
    {
        private const int SearchRadiusTiles = 3;
        private const int KnockbackTiles = 4;

        [SerializeField] private GameObject buckEffectPrefab;
        // Machine Cosmetics (2026-09-15) - Hay Baler skin reskins the landing effect to a dropped
        // hay bale instead of the buck-impact pose; same knockback distance/defeat rule, purely a
        // themed swap of which sprite plays (see EggDropAbility's own doc comment for why this is
        // safe as a paid cosmetic). Single non-directional sprite - HayBaleEffect is a HoraceBuckEffect
        // instance with the same sprite wired into both leftSprite/rightSprite slots.
        [SerializeField] private GameObject hayBaleEffectPrefab;

        protected override void Execute()
        {
            RobotBase target = FindNearestRobotWithinManhattan(Movement.CurrentGridPosition, SearchRadiusTiles);
            if (target == null)
            {
                return;
            }

            AudioManager.Instance?.PlayHoraceKickSfx();

            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleKnockback();
            int distance = doubled ? KnockbackTiles * 2 : KnockbackTiles;

            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;
            Vector2Int knockDirection = DirectionUtils.ToVector(facing);

            SpawnBuckEffect(target.transform.position, knockDirection.x >= 0);
            target.KnockBack(knockDirection, distance);
        }

        private void SpawnBuckEffect(Vector3 position, bool movingRight)
        {
            bool hayBalerSkinEquipped = SaveManager.Instance != null &&
                SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Skin, CharacterType.Horace) == IAPManager.MachineHayHoraceProductId;
            GameObject prefabToSpawn = hayBalerSkinEquipped && hayBaleEffectPrefab != null ? hayBaleEffectPrefab : buckEffectPrefab;
            if (prefabToSpawn == null)
            {
                return;
            }

            var go = Instantiate(prefabToSpawn, position, Quaternion.identity);
            go.GetComponent<HoraceBuckEffect>()?.PlayForDirection(movingRight);
        }

        /// <summary>Real bug found and fixed (2026-09-15): "the rear kick doesn't seem to be
        /// effective." A Defeated robot's GameObject is never destroyed — RobotBase.Disappear only
        /// disables its SpriteRenderer/Collider2D, so it stays fully findable via FindObjectsByType
        /// (and invisible/harmless) for the rest of the maze. This search never excluded that state,
        /// so if an already-defeated robot's leftover corpse happened to be nearer to Horace than
        /// any live threat, the WHOLE activation got wasted on it: RobotBase.KnockBack immediately
        /// no-ops for a Defeated robot, so nothing actually happened — no slide, no kill — even
        /// though the buck effect/SFX still played at that empty spot, reading as "kicked at
        /// nothing" while a real robot right next to Horace went untouched. Now skips Defeated
        /// robots entirely so only a genuinely live threat can ever be picked.</summary>
        private static RobotBase FindNearestRobotWithinManhattan(Vector2Int origin, int maxDistance)
        {
            RobotBase nearest = null;
            int bestDistance = int.MaxValue;

            foreach (var robot in FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
            {
                if (robot.CurrentState == RobotState.Defeated)
                {
                    continue;
                }

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
