using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Cluck's ability. Drops a single egg at her current position (previously dropped 3
    /// eggs trailing behind her at 0/2/4 tiles — simplified to just the one, right where she is
    /// the moment the ability activates).</summary>
    public class EggDropAbility : AbilityBase
    {
        [SerializeField] private GameObject eggPrefab;

        protected override void Execute()
        {
            if (eggPrefab == null || TileMap == null)
            {
                return;
            }

            Vector2Int origin = Movement.CurrentGridPosition;
            if (!TileMap.IsWalkable(origin))
            {
                return;
            }

            Instantiate(eggPrefab, TileMap.GridToWorld(origin), Quaternion.identity);
        }
    }
}
