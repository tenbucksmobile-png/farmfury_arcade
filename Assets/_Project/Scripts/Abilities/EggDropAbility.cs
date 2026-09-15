using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Cluck's ability. Drops a single egg at her current position (previously dropped 3
    /// eggs trailing behind her at 0/2/4 tiles — simplified to just the one, right where she is
    /// the moment the ability activates).
    ///
    /// Machine Cosmetics (2026-09-15): when Cluck's Tractor skin is equipped, this drops an oil
    /// spill instead of an egg — a themed reskin of the exact same EggHazard mechanic (same
    /// defeat-on-contact rule, same timing), not a new/stronger power, so a paid cosmetic never
    /// grants a gameplay edge a free character doesn't already have. oilHazardPrefab is a separate
    /// prefab (MachineWiringBuilder.WireMachineHazards) rather than swapping sprites on the shared
    /// Egg prefab at runtime, so the un-skinned egg is never affected.</summary>
    public class EggDropAbility : AbilityBase
    {
        [SerializeField] private GameObject eggPrefab;
        [SerializeField] private GameObject oilHazardPrefab;

        private GameObject _activeHazardInstance;
        private Vector2Int _activeHazardOrigin;

        /// <summary>Real bug found and fixed (2026-09-15): Cluck could die from the exact robot her
        /// own freshly-dropped egg/oil spill was about to defeat, if that robot arrived at her tile
        /// the same frame she dropped it — the egg spawns at her own current position, so a robot
        /// touching that tile fires two independent, unordered OnTriggerEnter2D events (PlayerHealth
        /// on Cluck's own body, EggHazard on the spawned egg); if Cluck's own death check happened
        /// to run first, she died before the egg got a chance to catch the contact.
        ///
        /// Unlike Percy/Billy/Gerald/Bessie's abilities (which live directly on the character's own
        /// GameObject and are protected via PlayerHealth.IsProtectedByActiveAbility), the egg is a
        /// separate spawned object — so protection here is keyed on "is Cluck still standing on the
        /// exact tile of an unresolved hazard she just dropped," true only while she hasn't moved off
        /// it (Unity's fake-null operator makes _activeHazardInstance != null become false the
        /// instant the hazard is destroyed, whether by a robot triggering it or its own timeout, so
        /// this needs no manual cleanup).</summary>
        public bool IsProtectingCurrentTile =>
            _activeHazardInstance != null && Movement.CurrentGridPosition == _activeHazardOrigin;

        protected override void Execute()
        {
            if (TileMap == null)
            {
                return;
            }

            Vector2Int origin = Movement.CurrentGridPosition;
            if (!TileMap.IsWalkable(origin))
            {
                return;
            }

            bool tractorSkinEquipped = SaveManager.Instance != null &&
                SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Skin, CharacterType.Cluck) == IAPManager.MachineTractorCluckyProductId;
            GameObject prefabToSpawn = tractorSkinEquipped && oilHazardPrefab != null ? oilHazardPrefab : eggPrefab;
            if (prefabToSpawn == null)
            {
                return;
            }

            _activeHazardInstance = Instantiate(prefabToSpawn, TileMap.GridToWorld(origin), Quaternion.identity);
            _activeHazardOrigin = origin;
            AudioManager.Instance?.PlayEggDropSfx();
        }
    }
}
