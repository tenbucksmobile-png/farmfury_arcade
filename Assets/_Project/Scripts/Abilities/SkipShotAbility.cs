using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Ducky's ability. If he's adjacent to (or standing on) an unused water tile,
    /// teleports him to its paired tile and marks the pair used for the rest of this maze — "one
    /// use per water tile pair per maze" per spec, tracked on WaterTile itself rather than a time
    /// cooldown (totalCooldown is a short button-debounce, not the real gate). No-ops with a log
    /// if no adjacent unused pair exists. Skip Shatter combo (Ducky -> Woolly) spawns 2 wool clones
    /// at the destination on the next use.
    ///
    /// Water Spout (2026-10-15): when there's no adjacent unused water pair to skip across, he
    /// fires a spinning water spout WaterSpoutTiles in his facing direction instead (a
    /// ThrownProjectileEffect, same distance/speed/one-target-defeat rule as Horace's horseshoe), so
    /// the button is never a no-op. Next to water, the skip still takes priority.</summary>
    public class SkipShotAbility : AbilityBase
    {
        private const int WaterSpoutTiles = 3;
        private const float WaterSpoutSecondsPerTile = 0.4f;

        [SerializeField] private GameObject woolClonePrefab;
        [SerializeField] private GameObject splashEffectPrefab;
        [SerializeField] private GameObject waterSpoutPrefab;

        protected override void Execute()
        {
            Vector2Int pos = Movement.CurrentGridPosition;
            WaterTile source = FindAdjacentUnusedWater(pos);
            if (source == null || source.PairedWater == null)
            {
                FireWaterSpout(pos);
                return;
            }

            Vector3 departurePosition = transform.position;
            bool movingRight = source.PairedWater.transform.position.x >= departurePosition.x;

            transform.position = source.PairedWater.transform.position;
            source.MarkUsed();
            AudioManager.Instance?.PlayDuckyTeleportSfx();
            SpawnSplashEffect(departurePosition, movingRight);

            if (ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleWoolClones())
            {
                SpawnWoolCloneCombo();
            }
        }

        private void FireWaterSpout(Vector2Int pos)
        {
            if (waterSpoutPrefab == null)
            {
                Debug.Log("[SkipShotAbility] No adjacent unused water tile pair, and no water spout prefab wired.");
                return;
            }

            AudioManager.Instance?.PlayDuckyTeleportSfx();
            ThrownProjectileEffect.SpawnAhead(waterSpoutPrefab, TileMap, pos, Movement.LastFacingDirection,
                WaterSpoutTiles, WaterSpoutSecondsPerTile);
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
