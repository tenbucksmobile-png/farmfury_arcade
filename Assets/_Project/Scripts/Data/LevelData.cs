using UnityEngine;

namespace FarmFuryArcade.Data
{
    /// <summary>
    /// Data-driven definition of a single maze level. Unity cannot serialize a
    /// multi-dimensional int[,] field directly, so the grid is stored as a flat
    /// int[] (row-major, width * height) plus explicit dimensions, and exposed
    /// through the <see cref="MazeLayout"/> accessor as the int[,] the design
    /// doc specifies.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData_XX", menuName = "Farm Fury Arcade/Level Data")]
    public class LevelData : ScriptableObject
    {
        public int levelNumber;
        public string levelName;
        public MazeType mazeType;

        [Tooltip("Row-major flattened grid, length must equal mazeWidth * mazeHeight.")]
        public int[] mazeLayoutFlat;
        public int mazeWidth = 28;
        public int mazeHeight = 31;

        public Vector2Int playerStartPosition;
        public Vector2Int robotFactoryPosition;

        public RobotSpawnData[] robotSpawns;

        [Tooltip("Row (y) values where tile id 5 wraps between x=0 and x=mazeWidth-1. " +
                 "Phase 2 superseded the separate CropPlacement/PowerPelletPlacement arrays " +
                 "from Phase 1 with tile-id scanning (see TileMapRenderer), since the GDD's " +
                 "own tile-id table already encodes crop/pellet/warp spawn points and keeping " +
                 "both would mean two sources of truth for the same data.")]
        public int[] warpTunnelRows;

        [Tooltip("Row (y) values where tile id 8 (water) pairs between its two occurrences in " +
                 "that row, same convention as warpTunnelRows — but water is NOT an automatic " +
                 "teleport: it blocks movement for everyone except a character with " +
                 "CharacterData.canCrossWater (Ducky), and even Ducky only teleports across a " +
                 "pair via SkipShotAbility, not by walking into it.")]
        public int[] waterTeleportRows;

        public int totalCropsRequired;
        public float baseRobotSpeed;
        public float baseCharacterSpeed;

        public Sprite backgroundSprite;
        public AudioClip levelMusic;

        /// <summary>Reconstructs the 2D tile grid described in the GDD (int[,] mazeLayout).</summary>
        public int[,] MazeLayout
        {
            get
            {
                var grid = new int[mazeWidth, mazeHeight];
                if (mazeLayoutFlat == null)
                {
                    return grid;
                }

                for (int i = 0; i < mazeLayoutFlat.Length && i < mazeWidth * mazeHeight; i++)
                {
                    int x = i % mazeWidth;
                    int y = i / mazeWidth;
                    grid[x, y] = mazeLayoutFlat[i];
                }

                return grid;
            }
        }

        public void SetMazeLayout(int[,] layout)
        {
            mazeWidth = layout.GetLength(0);
            mazeHeight = layout.GetLength(1);
            mazeLayoutFlat = new int[mazeWidth * mazeHeight];
            for (int x = 0; x < mazeWidth; x++)
            {
                for (int y = 0; y < mazeHeight; y++)
                {
                    mazeLayoutFlat[y * mazeWidth + x] = layout[x, y];
                }
            }
        }

        private const int TileCropKernel = 2;
        private const int TileCropVegetable = 3;
        private const int TilePowerPellet = 4;
        private const int KernelPoints = 10;
        private const int VegetablePoints = 50;
        private const int PelletPoints = 500;

        /// <summary>Sum of crop/vegetable/power-pellet points on the board — guaranteed to be
        /// earned by any level completion, since clearing the board is required to complete a
        /// level (see GameManager.NotifyCropCollected).</summary>
        public int ComputeGuaranteedCollectionScore()
        {
            var layout = MazeLayout;
            int kernels = 0, vegetables = 0, pellets = 0;

            for (int x = 0; x < mazeWidth; x++)
            {
                for (int y = 0; y < mazeHeight; y++)
                {
                    switch (layout[x, y])
                    {
                        case TileCropKernel: kernels++; break;
                        case TileCropVegetable: vegetables++; break;
                        case TilePowerPellet: pellets++; break;
                    }
                }
            }

            return kernels * KernelPoints + vegetables * VegetablePoints + pellets * PelletPoints;
        }

        /// <summary>
        /// Approximate ceiling used only for star-threshold math (LevelCompleteController: 1 star
        /// = complete, 2 = 75% of this, 3 = 95%) — NOT a precise "perfect play" score, since the
        /// real robot-chain bonus achievable depends on how many robots happen to be near the
        /// player when each power pellet triggers, which varies run to run. Assumes every power
        /// pellet could theoretically chain all 4 non-Heavy robots (200+400+800+1600+5000=8000)
        /// plus flat caps for the time and perfect-run bonuses (see GameManager.EndLevel). Revisit
        /// with real playtest data once more levels exist — this is a deliberately simple,
        /// documented estimate, not a tuned formula.
        /// </summary>
        public int ComputeMaxPossibleScoreEstimate()
        {
            const int maxChainPerPellet = 8000;
            const int timeBonusCap = 500;
            const int perfectBonusCap = 500;

            var layout = MazeLayout;
            int pellets = 0;
            for (int x = 0; x < mazeWidth; x++)
            {
                for (int y = 0; y < mazeHeight; y++)
                {
                    if (layout[x, y] == TilePowerPellet)
                    {
                        pellets++;
                    }
                }
            }

            return ComputeGuaranteedCollectionScore() + pellets * maxChainPerPellet + timeBonusCap + perfectBonusCap;
        }
    }
}
