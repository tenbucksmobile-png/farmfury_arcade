using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Tags a power pellet GameObject with its type/value so CropCollector can read it on
    /// overlap and activate PowerPelletManager for the duration matching pelletType.</summary>
    public class PowerPelletPickup : MonoBehaviour
    {
        public PowerPelletType pelletType;
        public int points = 500;

        [SerializeField] private GameObject collectEffectPrefab;

        /// <summary>Editor-time wiring hook (Phase2ProjectBuilder) — avoids needing a
        /// SerializedObject round-trip just to set one reference before the GameObject becomes a
        /// prefab asset.</summary>
        public void SetCollectEffectPrefab(GameObject prefab) => collectEffectPrefab = prefab;

        /// <summary>Called by CropCollector just before destroying this pellet. GoldenWheat/Rainbow
        /// tiers ("rare" pellets) get a procedural collect burst — see PelletCollectBurst for why
        /// it's procedural rather than dedicated VFX art. Sunflower (the common tier) stays plain,
        /// matching its "nothing special" rarity.</summary>
        public void SpawnCollectEffectIfRare()
        {
            if (pelletType == PowerPelletType.Sunflower || collectEffectPrefab == null)
            {
                return;
            }

            var effect = Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            effect.GetComponent<PelletCollectBurst>()?.Configure(pelletType);
        }
    }
}
