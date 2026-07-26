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
    }
}
