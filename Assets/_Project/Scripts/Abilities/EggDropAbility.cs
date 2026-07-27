using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Cluck's ability. Drops 3 eggs in her current lane: at her position, 2 tiles
    /// behind, and 4 tiles behind ("behind" = opposite her current facing, defaulting to Down
    /// when idle, same fallback CharacterAnimator already uses). Skips any tile that isn't
    /// currently walkable so an egg never spawns inside a wall.</summary>
    public class EggDropAbility : AbilityBase
    {
        private static readonly int[] TileOffsetsBehind = { 0, 2, 4 };

        [SerializeField] private GameObject eggPrefab;

        protected override void Execute()
        {
            if (eggPrefab == null || TileMap == null)
            {
                return;
            }

            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;
            Vector2Int behindStep = -DirectionUtils.ToVector(facing);
            Vector2Int origin = Movement.CurrentGridPosition;

            foreach (int offset in TileOffsetsBehind)
            {
                Vector2Int cell = origin + behindStep * offset;
                if (!TileMap.IsWalkable(cell))
                {
                    continue;
                }
                Instantiate(eggPrefab, TileMap.GridToWorld(cell), Quaternion.identity);
            }
        }
    }
}
