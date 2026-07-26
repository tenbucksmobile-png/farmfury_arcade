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

        private const int TileWall = 1;
        private const int TileCropKernel = 2;
        private const int TileCropVegetable = 3;
        private const int TilePowerPellet = 4;
        private const int TileWarpEdge = 5;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private LevelData _currentLevel;

        public void RenderMaze(LevelData data)
        {
            ClearMaze();
            _currentLevel = data;

            var layout = data.MazeLayout;
            var warpTunnelsByRow = new Dictionary<int, List<WarpTunnel>>();

            for (int x = 0; x < data.mazeWidth; x++)
            {
                for (int y = 0; y < data.mazeHeight; y++)
                {
                    int tileId = layout[x, y];
                    Vector3 worldPos = GridToWorld(new Vector2Int(x, y));

                    if (tileId == TileWall)
                    {
                        _spawned.Add(Instantiate(wallPrefab, worldPos, Quaternion.identity, mazeParent));
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
                            _spawned.Add(Instantiate(powerPelletPrefab, worldPos, Quaternion.identity, mazeParent));
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
                    }
                }
            }

            PairWarpTunnels(warpTunnelsByRow);
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
            _currentLevel = null;
        }

        public Vector3 GridToWorld(Vector2Int grid)
        {
            return new Vector3(grid.x, grid.y, 0f);
        }

        public Vector2Int WorldToGrid(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
        }

        public bool IsWalkable(Vector2Int grid)
        {
            if (_currentLevel == null)
            {
                return false;
            }

            if (grid.x < 0 || grid.x >= _currentLevel.mazeWidth || grid.y < 0 || grid.y >= _currentLevel.mazeHeight)
            {
                return false;
            }

            return _currentLevel.MazeLayout[grid.x, grid.y] != TileWall;
        }
    }
}
