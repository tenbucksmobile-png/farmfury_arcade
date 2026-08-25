namespace FarmFuryArcade.Data
{
    public enum MazeType
    {
        CornField,
        VegPatch,
        Orchard,
        Wheat,
        Endless,

        /// <summary>Purchased worlds (Monetisation "World Purchase" — $3.99 IAP each unlocks a
        /// full 25-level world, same shape as the 4 free worlds above but gated by ownership
        /// instead of star progress). Appended after Endless, not inserted earlier in the list, so
        /// no existing LevelData/MazeArtSet asset's serialized enum ordinal shifts. Order here
        /// matches UnlockProgression.PurchasedWorldMazeTypes (world index 4/5/6).</summary>
        FrostbiteGarden,
        GoldenSunset,
        HarvestMoon
    }
}
