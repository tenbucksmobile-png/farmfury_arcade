using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Gerald's ability: Feather Burst. Fires a spinning burst of feathers FeatherBurstTiles
    /// in his facing direction (a ThrownProjectileEffect, same distance/speed/one-target-defeat rule
    /// as Horace's horseshoe). Iron Stampede combo (Bessie -> Gerald) makes the next burst fire in
    /// all four directions at once.
    ///
    /// Was "Puff Up" until 2026-10-19: he pulsed to 2x size for 3s at half speed, defeating any robot
    /// he touched, couldn't use warp tunnels, and fired the feathers as a side effect. The puff was
    /// removed per direct request ("remove the puff ability from Gerald and only keep the feathers").
    /// The class name, AbilityType.PuffUp and the PlayGeraldPuffSfx/geraldPuffClip names are kept so
    /// the Gerald prefab's script reference and serialized fields stay intact.</summary>
    public class PuffUpAbility : AbilityBase
    {
        private const int FeatherBurstTiles = 3;
        private const float FeatherBurstSecondsPerTile = 0.4f;

        private static readonly Direction[] AllDirections = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        [SerializeField] private GameObject featherBurstPrefab;

        protected override void Execute()
        {
            AudioManager.Instance?.PlayGeraldPuffSfx();

            bool allDirections = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeFeatherBurstAllDirections();
            if (allDirections)
            {
                foreach (var dir in AllDirections)
                {
                    Fire(dir);
                }
            }
            else
            {
                Fire(Movement.LastFacingDirection);
            }
        }

        private void Fire(Direction direction)
        {
            ThrownProjectileEffect.SpawnAhead(featherBurstPrefab, TileMap, Movement.CurrentGridPosition,
                direction, FeatherBurstTiles, FeatherBurstSecondsPerTile);
        }
    }
}
