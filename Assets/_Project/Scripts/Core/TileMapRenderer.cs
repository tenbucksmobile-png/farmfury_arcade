using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Reads a LevelData's tile grid and instantiates the corresponding tile/pickup prefab per
    /// cell, using the tile id convention from the GDD:
    /// 0 empty ground, 1 wall, 2 crop kernel, 3 vegetable, 4 power pellet, 5 warp tunnel edge,
    /// 6 robot factory, 7 player start. Every non-wall cell also gets a ground tile underneath
    /// (crop/pellet/warp prefabs sit on top of it). Also exposes grid/world conversion and
    /// walkability queries used by GridMovement.
    /// </summary>
    public class TileMapRenderer : MonoBehaviour
    {
        [SerializeField] private Transform mazeParent;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject groundPrefab;
        [SerializeField] private GameObject cropKernelPrefab;
        [SerializeField] private GameObject cropVegetablePrefab;
        [SerializeField] private GameObject powerPelletPrefab;
        [SerializeField] private GameObject warpTunnelPrefab;
        [SerializeField] private GameObject waterTilePrefab;
        [SerializeField] private Sprite sunflowerPelletSprite;
        [SerializeField] private Sprite goldenWheatPelletSprite;
        [SerializeField] private Sprite rainbowPelletSprite;

        /// <summary>World units per grid cell. Raised from 1 to make tiles/sprites read bigger on
        /// screen without touching the camera's orthographicSize — since the camera's view stays a
        /// fixed number of world units, doubling the world size of each cell means the same camera
        /// view now covers proportionally less of the (now bigger) board, which is the "zoom in"
        /// effect requested without changing the Camera component itself. Every prefab that's
        /// supposed to fill exactly one tile has its localScale baked as CellSize * <the sprite's
        /// own 1-unit-tile base scale> in Phase2/3/4ProjectBuilder — see those files' scale literals.</summary>
        public const float CellSize = 2f;

        private const int TileWall = 1;
        private const int TileCropKernel = 2;
        private const int TileCropVegetable = 3;
        private const int TilePowerPellet = 4;
        private const int TileWarpEdge = 5;
        private const int TileWater = 8;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _wallsByCell = new Dictionary<Vector2Int, GameObject>();
        private readonly HashSet<Vector2Int> _temporaryWalkableCells = new HashSet<Vector2Int>();
        private LevelData _currentLevel;

        /// <summary>Caps the whole maze to at most 1 "rare" (non-Sunflower) power pellet — reset per
        /// RenderMaze call. RollPelletTier still rolls independently per pellet, but ConfigurePelletTier
        /// forces any roll beyond the first rare one back down to Sunflower.</summary>
        private int _rarePelletsSpawned;

        /// <summary>0 when no level is loaded. Used by CameraFollow to clamp the camera to the
        /// maze bounds — GridToWorld has no offset, so world extents are simply
        /// [0, (MazeWidth-1)*CellSize] x [0, (MazeHeight-1)*CellSize].</summary>
        public int MazeWidth => _currentLevel != null ? _currentLevel.mazeWidth : 0;
        public int MazeHeight => _currentLevel != null ? _currentLevel.mazeHeight : 0;

        public void RenderMaze(LevelData data)
        {
            ClearMaze();
            _currentLevel = data;
            _rarePelletsSpawned = 0;

            var layout = data.MazeLayout;
            var warpTunnelsByRow = new Dictionary<int, List<WarpTunnel>>();
            var waterTilesByRow = new Dictionary<int, List<WaterTile>>();

            for (int x = 0; x < data.mazeWidth; x++)
            {
                for (int y = 0; y < data.mazeHeight; y++)
                {
                    int tileId = layout[x, y];
                    var cell = new Vector2Int(x, y);
                    Vector3 worldPos = GridToWorld(cell);

                    if (tileId == TileWall)
                    {
                        var wallGO = Instantiate(wallPrefab, worldPos, Quaternion.identity, mazeParent);
                        _spawned.Add(wallGO);
                        _wallsByCell[cell] = wallGO;
                        continue;
                    }

                    _spawned.Add(Instantiate(groundPrefab, worldPos, Quaternion.identity, mazeParent));

                    switch (tileId)
                    {
                        case TileCropKernel:
                            _spawned.Add(Instantiate(cropKernelPrefab, worldPos, Quaternion.identity, mazeParent));
                            break;
                        case TileCropVegetable:
                            _spawned.Add(Instantiate(cropVegetablePrefab, worldPos, Quaternion.identity, mazeParent));
                            break;
                        case TilePowerPellet:
                            var pelletGO = Instantiate(powerPelletPrefab, worldPos, Quaternion.identity, mazeParent);
                            _spawned.Add(pelletGO);
                            ConfigurePelletTier(pelletGO);
                            break;
                        case TileWarpEdge:
                            var warpGO = Instantiate(warpTunnelPrefab, worldPos, Quaternion.identity, mazeParent);
                            _spawned.Add(warpGO);
                            var warp = warpGO.GetComponent<WarpTunnel>();
                            if (!warpTunnelsByRow.TryGetValue(y, out var list))
                            {
                                list = new List<WarpTunnel>();
                                warpTunnelsByRow[y] = list;
                            }
                            list.Add(warp);
                            break;
                        case TileWater:
                            var waterGO = Instantiate(waterTilePrefab, worldPos, Quaternion.identity, mazeParent);
                            _spawned.Add(waterGO);
                            var water = waterGO.GetComponent<WaterTile>();
                            if (!waterTilesByRow.TryGetValue(y, out var waterList))
                            {
                                waterList = new List<WaterTile>();
                                waterTilesByRow[y] = waterList;
                            }
                            waterList.Add(water);
                            break;
                    }
                }
            }

            PairWarpTunnels(warpTunnelsByRow);
            PairWaterTiles(waterTilesByRow);
        }

        /// <summary>Rolls a weighted tier (Sunflower common, GoldenWheat uncommon, Rainbow rare —
        /// matching the "RarePellets" art naming) and applies its sprite/type to a spawned pellet.
        /// Sprite fields are optional — a pellet with no tier art assigned keeps the prefab's
        /// default sprite, same fallback convention as everywhere else art isn't wired yet.</summary>
        private void ConfigurePelletTier(GameObject pelletGO)
        {
            var tier = RollPelletTier();

            // Only 1 rare (non-Sunflower) pellet is allowed per maze — any roll beyond the first
            // falls back to Sunflower rather than being re-rolled, keeping the odds honest for
            // whichever pellet does end up claiming the "rare" slot.
            if (tier != PowerPelletType.Sunflower)
            {
                if (_rarePelletsSpawned >= 1)
                {
                    tier = PowerPelletType.Sunflower;
                }
                else
                {
                    _rarePelletsSpawned++;
                }
            }

            var pickup = pelletGO.GetComponent<PowerPelletPickup>();
            if (pickup != null)
            {
                pickup.pelletType = tier;
            }

            var sr = pelletGO.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                return;
            }

            Sprite tierSprite = tier switch
            {
                PowerPelletType.GoldenWheat => goldenWheatPelletSprite,
                PowerPelletType.Rainbow => rainbowPelletSprite,
                _ => sunflowerPelletSprite
            };

            if (tierSprite != null)
            {
                sr.sprite = tierSprite;
            }
        }

        private static PowerPelletType RollPelletTier()
        {
            float roll = Random.value;
            if (roll < 0.10f)
            {
                return PowerPelletType.Rainbow;
            }
            if (roll < 0.30f)
            {
                return PowerPelletType.GoldenWheat;
            }
            return PowerPelletType.Sunflower;
        }

        private static void PairWarpTunnels(Dictionary<int, List<WarpTunnel>> warpTunnelsByRow)
        {
            foreach (var kvp in warpTunnelsByRow)
            {
                var tunnels = kvp.Value;
                if (tunnels.Count == 2)
                {
                    tunnels[0].PairedWarp = tunnels[1];
                    tunnels[1].PairedWarp = tunnels[0];
                }
                else
                {
                    Debug.LogWarning($"[TileMapRenderer] Row {kvp.Key} has {tunnels.Count} warp tunnel " +
                                      "tiles; expected exactly 2 (left/right edge) to pair them.");
                }
            }
        }

        private static void PairWaterTiles(Dictionary<int, List<WaterTile>> waterTilesByRow)
        {
            foreach (var kvp in waterTilesByRow)
            {
                var tiles = kvp.Value;
                if (tiles.Count == 2)
                {
                    tiles[0].PairedWater = tiles[1];
                    tiles[1].PairedWater = tiles[0];
                }
                else
                {
                    Debug.LogWarning($"[TileMapRenderer] Row {kvp.Key} has {tiles.Count} water " +
                                      "tiles; expected exactly 2 to pair them for SkipShotAbility.");
                }
            }
        }

        public void ClearMaze()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            _spawned.Clear();
            _wallsByCell.Clear();
            _temporaryWalkableCells.Clear();
            _currentLevel = null;
        }

        public Vector3 GridToWorld(Vector2Int grid)
        {
            return new Vector3(grid.x * CellSize, grid.y * CellSize, 0f);
        }

        public Vector2Int WorldToGrid(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x / CellSize), Mathf.RoundToInt(world.y / CellSize));
        }

        public bool IsWalkable(Vector2Int grid) => IsWalkable(grid, false);

        /// <summary>canCrossWater lets a character (Ducky, via CharacterData.canCrossWater) treat
        /// water tiles (id 8) as walkable; everyone else — including every robot and WoollyClone,
        /// which always call the 1-arg overload — is blocked by water like a soft wall.</summary>
        public bool IsWalkable(Vector2Int grid, bool canCrossWater)
        {
            if (!IsInBounds(grid))
            {
                return false;
            }

            if (_temporaryWalkableCells.Contains(grid))
            {
                return true;
            }

            int tileId = _currentLevel.MazeLayout[grid.x, grid.y];
            if (tileId == TileWall)
            {
                return false;
            }
            if (tileId == TileWater && !canCrossWater)
            {
                return false;
            }

            return true;
        }

        /// <summary>Bounds-only check (ignores wall tiles) — used by DroneRobot, which is allowed
        /// to move through walls but not off the edge of the maze.</summary>
        public bool IsInBounds(Vector2Int grid)
        {
            if (_currentLevel == null)
            {
                return false;
            }

            return grid.x >= 0 && grid.x < _currentLevel.mazeWidth && grid.y >= 0 && grid.y < _currentLevel.mazeHeight;
        }

        /// <summary>Overrides a single cell's walkability without touching LevelData — used for
        /// BounceRollAbility's temporary wall-phase (call again with walkable=false to revert) and
        /// as the permanent backing for DestroyWallAt (never reverted).</summary>
        public void SetTemporaryWalkable(Vector2Int cell, bool walkable)
        {
            if (walkable)
            {
                _temporaryWalkableCells.Add(cell);
            }
            else
            {
                _temporaryWalkableCells.Remove(cell);
            }
        }

        /// <summary>The spawned wall GameObject at a cell, if any — used to tint a wall while
        /// BounceRollAbility's phase window is active.</summary>
        public GameObject GetWallAt(Vector2Int cell)
        {
            return _wallsByCell.TryGetValue(cell, out var go) ? go : null;
        }

        /// <summary>Permanently destroys the wall at a cell (visually and for walkability) — used
        /// by HeadbuttThroughAbility and the Iron Stampede combo buff on PuffUpAbility.</summary>
        public void DestroyWallAt(Vector2Int cell)
        {
            if (_wallsByCell.TryGetValue(cell, out var wallGO))
            {
                if (wallGO != null)
                {
                    Destroy(wallGO);
                    _spawned.Remove(wallGO);
                }
                _wallsByCell.Remove(cell);
            }

            SetTemporaryWalkable(cell, true);
        }
    }
}
