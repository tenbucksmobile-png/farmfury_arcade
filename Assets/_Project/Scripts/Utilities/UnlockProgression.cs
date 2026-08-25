using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Utilities
{
    /// <summary>
    /// Level-select unlock/star lookups for LevelSelectController and LevelTileController.
    /// Distinct from Core.UnlockManager, which gates CHARACTER unlocks on mazes-completed — this is
    /// purely about which level tiles are tappable.
    ///
    /// 0-indexed throughout (matches LevelData.levelNumber, GameManager.LoadLevel, and
    /// SaveManager's per-level PlayerPrefs keys everywhere else in this project). The UI-facing
    /// "Level 1..100" display numbers are levelIndex + 1 — that conversion happens at the call site
    /// (LevelTileController), not here.
    ///
    /// The world-boundary gates (levels 26-50 also need Level 25 at 2+ stars, etc.) are specified
    /// for a 100-level, 4-world structure — all 4 worlds (levelNumber 0-99, LevelData_01-100) now
    /// have real content.
    /// </summary>
    public static class UnlockProgression
    {
        /// <summary>100 (worlds 0-3, the free star-progress worlds) + 25 per purchased world (see
        /// PurchasedWorldMazeTypes). Grows by 25 for every purchased world added.</summary>
        public const int TotalLevels = 175;
        public const int LevelsPerWorld = 25;
        public const int WorldGateStarRequirement = 2;

        /// <summary>Worlds 4+ (Monetisation "World Purchase") — owned-or-not via SaveManager.
        /// IsWorldPurchased, completely independent of the free worlds' sequential star-progress
        /// chain below. World index = 4 + array index (world 4 = FrostbiteGarden, 5 = GoldenSunset,
        /// 6 = HarvestMoon). Add future purchased worlds here, in order, and bump TotalLevels by 25
        /// for each.</summary>
        private static readonly MazeType[] PurchasedWorldMazeTypes = { MazeType.FrostbiteGarden, MazeType.GoldenSunset, MazeType.HarvestMoon };

        private const int FirstPurchasedWorldIndex = 4;

        public static bool IsPurchaseGatedWorld(int world)
        {
            return world >= FirstPurchasedWorldIndex && (world - FirstPurchasedWorldIndex) < PurchasedWorldMazeTypes.Length;
        }

        private static MazeType MazeTypeForPurchasedWorld(int world)
        {
            return PurchasedWorldMazeTypes[world - FirstPurchasedWorldIndex];
        }

        /// <summary>Level 1 (index 0) is always unlocked. Level N unlocks once Level N-1 has at
        /// least 1 star; levels 26-50/51-75/76-100 additionally require their world's gate level
        /// (25/50/75) at 2+ stars. A purchase-gated world's own first level instead requires only
        /// that world's purchase (no star chain from the previous world at all — these worlds
        /// aren't part of the sequential CornField-&gt;Wheat chain); every level after that world's
        /// first still needs its own predecessor's star, same as any other world.</summary>
        public static bool IsLevelUnlocked(int levelIndex)
        {
            // Only 2 LevelData assets exist right now (levelNumber 0 and 4) against this 100-slot
            // UI — without this check, a slot with no authored content would still read as
            // "unlocked" once its predecessor had a star, and tapping it would silently no-op via
            // GameManager.LoadLevel's own null check with no feedback to the player. Gating on real
            // content keeps every unlocked tile actually playable.
            if (DataManager.Instance == null || DataManager.Instance.GetLevelData(levelIndex) == null)
            {
                return false;
            }

            int world = levelIndex / LevelsPerWorld;
            int indexWithinWorld = levelIndex % LevelsPerWorld;

            if (IsPurchaseGatedWorld(world))
            {
                if (SaveManager.Instance == null || !SaveManager.Instance.IsWorldPurchased(MazeTypeForPurchasedWorld(world)))
                {
                    return false;
                }
                if (indexWithinWorld == 0)
                {
                    return true;
                }
            }
            else
            {
                if (levelIndex <= 0)
                {
                    return true;
                }
                if (SaveManager.Instance == null)
                {
                    return false;
                }

                int worldGateIndex = WorldGateIndexFor(levelIndex);
                if (worldGateIndex >= 0 && SaveManager.Instance.GetLevelStars(worldGateIndex) < WorldGateStarRequirement)
                {
                    return false;
                }
            }

            if (SaveManager.Instance == null)
            {
                return false;
            }
            return SaveManager.Instance.GetLevelStars(levelIndex - 1) >= 1;
        }

        /// <summary>0-indexed gate level (24, 49, or 74) for a level index that falls inside worlds
        /// 2-4, or -1 if levelIndex isn't gated (world 1, or out of range).</summary>
        private static int WorldGateIndexFor(int levelIndex)
        {
            if (levelIndex >= 25 && levelIndex <= 49) return 24;
            if (levelIndex >= 50 && levelIndex <= 74) return 49;
            if (levelIndex >= 75 && levelIndex <= 99) return 74;
            return -1;
        }

        public static int GetStarsForLevel(int levelIndex)
        {
            return SaveManager.Instance != null ? SaveManager.Instance.GetLevelStars(levelIndex) : 0;
        }

        public static int GetTotalStars()
        {
            return SaveManager.Instance != null ? SaveManager.Instance.GetTotalStars() : 0;
        }

        /// <summary>Highest levelIndex currently unlocked, walking up from Level 1 — the chain
        /// requirement (each level needs its predecessor's star) makes unlock status monotonic, so
        /// stopping at the first locked level is always correct, not just an optimisation.</summary>
        public static int GetHighestUnlockedLevel()
        {
            int highest = 0;
            for (int i = 0; i < TotalLevels; i++)
            {
                if (!IsLevelUnlocked(i))
                {
                    break;
                }
                highest = i;
            }
            return highest;
        }

        /// <summary>Human-readable reason a locked tile can't be played yet, for LockedHintPanel.
        /// Checks the (more specific) world gate before falling back to the plain predecessor
        /// requirement, since a level can satisfy the chain requirement but still be blocked by its
        /// world's gate.</summary>
        public static string GetUnlockHint(int levelIndex)
        {
            if (DataManager.Instance == null || DataManager.Instance.GetLevelData(levelIndex) == null)
            {
                return "This level isn't available yet";
            }

            int world = levelIndex / LevelsPerWorld;
            if (IsPurchaseGatedWorld(world))
            {
                return $"Purchase {GetWorldNameForLevel(levelIndex)} to unlock this level";
            }

            int worldGateIndex = WorldGateIndexFor(levelIndex);
            if (worldGateIndex >= 0 && GetStarsForLevel(worldGateIndex) < WorldGateStarRequirement)
            {
                return $"Complete Level {worldGateIndex + 1} with {WorldGateStarRequirement}+ stars to unlock this level";
            }

            return $"Complete Level {levelIndex} to unlock this level";
        }

        /// <summary>World 0 is always available. World N (N&gt;0, and not purchase-gated) becomes
        /// available once the last level of world N-1 has 2+ stars — the same gate IsLevelUnlocked
        /// already applies via WorldGateIndexFor. A purchase-gated world (see
        /// PurchasedWorldMazeTypes) ignores all of that and is simply owned-or-not. Exposed here as
        /// a standalone world-level check for callers (Level Select's carousel,
        /// DailyChallengeManager's world pool) that need "is this whole world open" rather than "is
        /// this specific level open."</summary>
        public static bool IsWorldUnlocked(int world)
        {
            if (IsPurchaseGatedWorld(world))
            {
                return SaveManager.Instance != null && SaveManager.Instance.IsWorldPurchased(MazeTypeForPurchasedWorld(world));
            }
            if (world <= 0)
            {
                return true;
            }
            int gateLevelIndex = world * LevelsPerWorld - 1;
            return GetStarsForLevel(gateLevelIndex) >= WorldGateStarRequirement;
        }

        public static string GetWorldNameForLevel(int levelIndex)
        {
            return (levelIndex / LevelsPerWorld) switch
            {
                0 => "Corn Field",
                1 => "Vegetable Patch",
                2 => "Orchard",
                3 => "Wheat Field",
                4 => "Frostbite Garden",
                5 => "Golden Sunset",
                6 => "Harvest Moon",
                _ => "Wheat Field"
            };
        }
    }
}
