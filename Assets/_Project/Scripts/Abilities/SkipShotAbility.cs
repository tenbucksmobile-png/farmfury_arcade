using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Ducky's ability. If she's adjacent to (or standing on) an unused water tile,
    /// teleports her to its paired tile and marks the pair used for the rest of this maze — "one
    /// use per water tile pair per maze" per spec, tracked on WaterTile itself rather than a time
    /// cooldown (totalCooldown is a short button-debounce, not the real gate). No-ops with a log
    /// if no adjacent unused pair exists. Skip Shatter combo (Ducky -> Woolly) spawns 2 wool clones
    /// at the destination on the next use.</summary>
    public class SkipShotAbility : AbilityBase
    {
        [SerializeField] private GameObject woolClonePrefab;
        [SerializeField] private GameObject splashEffectPrefab;

        protected override void Execute()
        {
            Vector2Int pos = Movement.CurrentGridPosition;
            WaterTile source = FindAdjacentUnusedWater(pos);
            if (source == null || source.PairedWater == null)
            {
                Debug.Log("[SkipShotAbility] No adjacent unused water tile pair to teleport across.");
                return;
            }

            Vector3 departurePosition = transform.position;
            bool movingRight = source.PairedWater.transform.position.x >= departurePosition.x;

            transform.position = source.PairedWater.transform.position;
            source.MarkUsed();
            SpawnSplashEffect(departurePosition, movingRight);

            if (ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleWoolClones())
            {
                SpawnWoolCloneCombo();
            }
        }

        private void SpawnSplashEffect(Vector3 position, bool movingRight)
        {
            if (splashEffectPrefab == null)
            {
                return;
            }

            var go = Instantiate(splashEffectPrefab, position, Quaternion.identity);
            go.GetComponent<DuckySplashEffect>()?.PlayForDirection(movingRight);
        }

        private WaterTile FindAdjacentUnusedWater(Vector2Int pos)
        {
            foreach (var water in FindObjectsByType<WaterTile>(FindObjectsSortMode.None))
            {
                if (water.Used)
                {
                    continue;
                }

                Vector2Int waterCell = TileMap.WorldToGrid(water.transform.position);
                if (Vector2Int.Distance(waterCell, pos) <= 1f)
                {
                    return water;
                }
            }
            return null;
        }

        private void SpawnWoolCloneCombo()
        {
            if (woolClonePrefab == null || TileMap == null)
            {
                return;
            }

            Vector2Int here = Movement.CurrentGridPosition;
            for (int i = 0; i < 2; i++)
            {
                var go = Instantiate(woolClonePrefab, transform.position, Quaternion.identity);
                var clone = go.GetComponent<WoollyClone>();
                clone.Initialize(TileMap, here, null);
            }
        }
    }
}
