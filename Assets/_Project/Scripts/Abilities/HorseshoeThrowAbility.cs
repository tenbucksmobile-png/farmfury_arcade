using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Horace's ability, reworked 2026-09-16 from RearKickAbility ("find the nearest robot
    /// within 3 tiles and knock it back") into a launched projectile — the class/file were renamed
    /// (RearKickAbility.cs, guid-preserving rename) rather than kept under the old name, since
    /// nothing about "kicking" applies any more. Horace launches a horseshoe ThrowTilesBase tiles in
    /// his current facing direction (doubled with the Crossfire combo), spinning as it flies via
    /// ThrownProjectileEffect, instantly defeating any robot it touches along the way (ForceDefeat,
    /// same "deployed hazard kills outright" convention as EggHazard/BounceRollAbility) — same shape
    /// as Percy's Bounce Roll/Billy's Headbutt Through, just externalized into a thrown object
    /// instead of Horace's own body, so it never needs Horace to have a target already nearby.
    ///
    /// Two real problems with the old version motivated this, both found via direct playtesting:
    /// (1) requiring an enemy already within 3 tiles meant the ability could silently do nothing at
    /// all — reported as "nothing dropped" when tested with no robot nearby; (2) the landing-impact
    /// "buck" pose (the old HoraceBuckEffect) needed a distinct sprite per knockback direction that
    /// Kling AI was never able to generate — it kept returning near-duplicates of Horace's own walk
    /// cycle (see CLAUDE.md's own history of this under "Art status"). A launched, spinning prop
    /// sidesteps both: it always fires regardless of what's nearby, and needs no Horace-specific art
    /// at all, just one symmetric object sprite reused for every direction via runtime rotation.
    ///
    /// Machine Cosmetics: with the Hay Baler skin equipped, launches a hay bale instead of a
    /// horseshoe (hayBaleProjectilePrefab, a themed clone MachineWiringBuilder builds from
    /// Horseshoe.prefab) — same distance/speed/defeat rule, purely a themed swap of which prefab is
    /// thrown, same convention every other machine's hazard reskin uses (EggDropAbility/
    /// GroundSlamAbility) — so the paid skin never grants extra power, only a different look.</summary>
    public class HorseshoeThrowAbility : AbilityBase
    {
        // Distance/speed re-tuned 2026-09-16, per direct feedback: the throw was both too far (5
        // tiles, hard to track) and way too fast (0.1s/tile — nearly instant) to actually read as
        // "a horseshoe/hay bale is being thrown." Cut to 2 tiles (4 with Crossfire, still double)
        // and slowed to 0.4s/tile (a 2-tile throw now takes 0.8s total, clearly visible) — applies
        // to both the horseshoe and the Hay Baler skin's hay bale, since they share this same
        // Execute()/Launch() call. Same session, second pass: the spawn point moved from Horace's
        // own tile to one tile ahead of him — see Execute()'s own doc comment on `oneAhead` below.
        private const int ThrowTilesBase = 2;
        private const int ThrowTilesBuffed = 4;
        private const float SecondsPerTile = 0.4f;

        [SerializeField] private GameObject horseshoePrefab;
        [SerializeField] private GameObject hayBaleProjectilePrefab;

        protected override void Execute()
        {
            GameObject prefabToThrow = IsHayBalerSkinEquipped() && hayBaleProjectilePrefab != null
                ? hayBaleProjectilePrefab
                : horseshoePrefab;
            if (prefabToThrow == null)
            {
                return;
            }

            AudioManager.Instance?.PlayHoraceKickSfx();

            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleThrowDistance();
            int tiles = doubled ? ThrowTilesBuffed : ThrowTilesBase;

            // LastFacingDirection (not CurrentDirection, which resets to None the instant no
            // direction is held) so the throw fires correctly in whichever of the 4 directions
            // Horace actually last faced, including while completely stationary — same reasoning
            // BounceRollAbility's own roll uses.
            Direction facing = Movement.LastFacingDirection;
            Vector2Int dirVector = DirectionUtils.ToVector(facing);
            Vector2Int horaceCell = Movement.CurrentGridPosition;

            // Spawns one tile AHEAD of Horace instead of at his own feet (2026-09-16, per direct
            // feedback: it used to spawn on his own tile and animate its first hop moving out from
            // under him, which read as "left behind, rolling forward" rather than a clean throw, and
            // made it harder to actually line up with a nearby robot). `tiles` (2 base / 4 buffed)
            // is still the total reach in front of him — the projectile just appears instantly at
            // the first of those tiles rather than animating its way there, then visibly travels the
            // remaining tiles from there. Falls back to spawning at Horace's own tile only if a wall
            // sits immediately in front of him (matching TravelRoutine's own "stop early at a wall"
            // rule, so a wall-blocked throw still degrades gracefully instead of spawning inside it).
            Vector2Int oneAhead = horaceCell + dirVector;
            bool oneAheadWalkable = TileMap.IsWalkable(oneAhead);
            Vector2Int spawnCell = oneAheadWalkable ? oneAhead : horaceCell;
            int hopsFromSpawn = oneAheadWalkable ? tiles - 1 : tiles;

            var projectile = Instantiate(prefabToThrow, TileMap.GridToWorld(spawnCell), Quaternion.identity);
            projectile.GetComponent<ThrownProjectileEffect>()?.Launch(TileMap, spawnCell, dirVector, hopsFromSpawn, SecondsPerTile);
        }

        private static bool IsHayBalerSkinEquipped()
        {
            return SaveManager.Instance != null &&
                SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Skin, CharacterType.Horace) == IAPManager.MachineHayHoraceProductId;
        }
    }
}
