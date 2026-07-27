using UnityEngine;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Woolly's ability. Spawns 2 AI-controlled WoollyClones at her current position.
    /// Feather Storm combo (Cluck -> Woolly) makes the clones drop eggs as they walk.</summary>
    public class TripleCloneAbility : AbilityBase
    {
        private const int CloneCount = 2;

        [SerializeField] private GameObject clonePrefab;
        [SerializeField] private GameObject eggPrefab;

        protected override void Execute()
        {
            if (clonePrefab == null || TileMap == null)
            {
                return;
            }

            bool eggBuff = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeEggDropClones();
            Vector2Int origin = Movement.CurrentGridPosition;
            Vector3 worldOrigin = TileMap.GridToWorld(origin);

            for (int i = 0; i < CloneCount; i++)
            {
                var go = Instantiate(clonePrefab, worldOrigin, Quaternion.identity);
                var clone = go.GetComponent<WoollyClone>();
                clone.Initialize(TileMap, origin, eggBuff ? eggPrefab : null);
            }
        }
    }
}
