using UnityEngine;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Bonus pickup scattered by TileMapRenderer.SpawnBonusPickups (currently CornField
    /// only, via MazeArtSet.bonusPickupPrefab) on top of already-rendered tiles — awards
    /// SaveManager coins directly, not maze score. Deliberately not a CropPickup: collecting it
    /// must NOT call GameManager.NotifyCropCollected, since LevelData.totalCropsRequired is
    /// computed once at LevelData build time from the grid's own kernel/vegetable/pellet counts
    /// and has no knowledge of this runtime-only addition — counting it there would make the
    /// crops-remaining tally never reach zero if a coin went uncollected.</summary>
    public class CoinPickup : MonoBehaviour
    {
        public int coinValue = 1;
    }
}
