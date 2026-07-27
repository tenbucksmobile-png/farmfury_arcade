using UnityEngine;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Marks a water tile (id 8) and its pair, wired by TileMapRenderer the same way as
    /// WarpTunnel. Unlike a warp tunnel, walking onto water does nothing automatically — it just
    /// blocks movement for anyone without CharacterData.canCrossWater (see GridMovement/
    /// TileMapRenderer.IsWalkable). SkipShotAbility is what actually teleports Ducky between a
    /// pair, and only once per pair per maze (tracked here via Used/MarkUsed).</summary>
    public class WaterTile : MonoBehaviour
    {
        public WaterTile PairedWater { get; set; }
        public bool Used { get; private set; }

        public void MarkUsed()
        {
            Used = true;
            if (PairedWater != null)
            {
                PairedWater.Used = true;
            }
        }
    }
}
