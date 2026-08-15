using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;

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
        /// <summary>Per-world wall/ground/warp-tunnel prefabs and gameplay backdrop, keyed by
        /// MazeType — introduced when World 2 (VegPatch) art landed, since every level up to then
        /// was CornField and a single global prefab/backdrop set was enough. Ground has no
        /// dedicated per-world art yet (only wall/warp/backdrop images have been uploaded per
        /// world so far), so multiple MazeArtSets are free to point at the same shared
        /// groundPrefab — see Phase2ProjectBuilder.WireScene, which does exactly that for
        /// VegPatch (reuses Ground_CornField's FloorTile.png/soil look, which reads fine for a
        /// vegetable patch too).</summary>
        [System.Serializable]
        public class MazeArtSet
        {
            public MazeType mazeType;
            public GameObject wallPrefab;
            public GameObject groundPrefab;
            public GameObject warpTunnelPrefab;
            public Sprite backdropSprite;

            /// <summary>Per-world crop prefabs — CornField uses CornKernel.png/CornCob.png,
            /// VegPatch uses carrot.png/cabbage.png. Moved off TileMapRenderer's own fields (which
            /// used to be global/shared across every world) once World 2 needed its own crop art.</summary>
            public GameObject cropKernelPrefab;
            public GameObject cropVegetablePrefab;

            /// <summary>Single sprite shown on EVERY power pellet in this world, regardless of
            /// which tier (Sunflower/GoldenWheat/Rainbow) it rolled — replaces the old 3
            /// tier-specific sprite fields, which were shared globally across every world and
            /// didn't reflect any world's own theme. RollPelletTier's random duration roll (8s/15s/
            /// 30s) and the "only 1 non-Sunflower pellet per maze" cap are untouched; only the
            /// VISUAL got simplified to one look per world (sunflower glow for CornField, apple for
            /// VegPatch) — SpawnCollectEffectIfRare/PlayRarePelletPickupSfx still key off the real
            /// tier, so collecting the special pellet still feels distinct even though every pellet
            /// in the maze looks the same up front.</summary>
            public Sprite pelletSprite;

            /// <summary>Optional distinct look for the single "rare" (non-Sunflower-tier) pellet a
            /// maze is allowed to roll — see ConfigurePelletTier's _rarePelletsSpawned cap. Null for
            /// any world that hasn't had dedicated rare-pellet art uploaded yet, in which case
            /// ConfigurePelletTier falls back to pelletSprite exactly as before (every pellet, rare
            /// or not, showing the one themed look) — this field only ever narrows that "always the
            /// same sprite" behaviour for worlds that opt in, it never changes it for ones that
            /// don't.</summary>
            public Sprite rarePelletSprite;

            /// <summary>Extra pickup scattered on random walkable ground cells (not tied to any
            /// grid tile id, but excludes crop/pellet cells — see SpawnScatteredPickups) — the
            /// per-world THEMED bonus (Orchard's cherry, Wheat's grain sack; CornField and VegPatch
            /// have none of their own). Independent of universalCoinPrefab,
            /// which spawns a coin on every world's mazes regardless of this field — the two are
            /// scattered separately and can coexist on the same maze. Deliberately NOT counted in
            /// LevelData.totalCropsRequired (that's computed once at LevelData build time from the
            /// grid's own kernel/vegetable/pellet counts, with no knowledge of this runtime-only
            /// addition), so collecting it is optional and never blocks level completion. Null/0 for
            /// a world that doesn't have one yet.</summary>
            public GameObject bonusPickupPrefab;
            public int bonusPickupCount;

            /// <summary>VegPatch-only: ignores the maze grid's own tile-id-2-vs-3 split and instead
            /// randomly picks `vegetableQuota` of the maze's crop-eligible cells (either id) to
            /// render as the vegetable (cabbage) prefab, rendering the rest as the kernel (carrot)
            /// prefab — guarantees exactly `vegetableQuota` cabbages per level regardless of how a
            /// hand-authored or generated grid happened to split id 2 vs id 3, since that request
            /// was for a fixed count ("10 cabbages") rather than whatever the grid design put there.
            /// Doesn't change LevelData.totalCropsRequired either — it's the same total number of
            /// crop-eligible cells either way, just relabeling which prefab renders at each one.</summary>
            public bool useRandomVegetableQuota;
            public int vegetableQuota;
        }

        [SerializeField] private Transform mazeParent;
        [SerializeField] private List<MazeArtSet> mazeArtSets = new List<MazeArtSet>();
        [SerializeField] private GameObject powerPelletPrefab;
        [SerializeField] private GameObject waterTilePrefab;

        /// <summary>Spawned on EVERY maze regardless of world/MazeArtSet — guarantees a
        /// collectible coin exists on every level, not just CornField's (the only world that had
        /// one, via its own MazeArtSet.bonusPickupPrefab; VegPatch had no bonus pickup configured
        /// at all, and Orchard/Wheat's bonus slot is already spoken for by Cherry/GrainSack).
        /// Deliberately independent of MazeArtSet.bonusPickupPrefab so each world's own themed
        /// bonus keeps working unchanged alongside this — CornField's old bonusPickupPrefab entry
        /// (also Pickup_Coin) was removed from Phase2ProjectBuilder.WireScene's MazeArtSet list once
        /// this was added, so CornField levels get exactly one coin, not two.</summary>
        [SerializeField] private GameObject universalCoinPrefab;
        [SerializeField] private int coinsPerMaze = 1;

        private SpriteRenderer _gameplayBackdrop;

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

            var artSet = ResolveArtSet(data.mazeType);
            var layout = data.MazeLayout;
            var warpTunnels = new List<(int x, int y, WarpTunnel warp)>();
            var waterTilesByRow = new Dictionary<int, List<WaterTile>>();
            var forcedVegetableCells = BuildForcedVegetableCells(artSet, data, layout);

            for (int x = 0; x < data.mazeWidth; x++)
            {
                for (int y = 0; y < data.mazeHeight; y++)
                {
                    int tileId = layout[x, y];
                    var cell = new Vector2Int(x, y);
                    Vector3 worldPos = GridToWorld(cell);

                    if (tileId == TileWall)
                    {
                        var wallGO = Instantiate(artSet.wallPrefab, worldPos, Quaternion.identity, mazeParent);
                        _spawned.Add(wallGO);
                        _wallsByCell[cell] = wallGO;
                        continue;
                    }

                    _spawned.Add(Instantiate(artSet.groundPrefab, worldPos, Quaternion.identity, mazeParent));

                    switch (tileId)
                    {
                        case TileCropKernel:
                        case TileCropVegetable:
                            bool renderAsVegetable = artSet.useRandomVegetableQuota
                                ? forcedVegetableCells.Contains(cell)
                                : tileId == TileCropVegetable;
                            var cropPrefab = renderAsVegetable ? artSet.cropVegetablePrefab : artSet.cropKernelPrefab;
                            _spawned.Add(Instantiate(cropPrefab, worldPos, Quaternion.identity, mazeParent));
                            break;
                        case TilePowerPellet:
                            var pelletGO = Instantiate(powerPelletPrefab, worldPos, Quaternion.identity, mazeParent);
                            _spawned.Add(pelletGO);
                            ConfigurePelletTier(pelletGO, artSet);
                            break;
                        case TileWarpEdge:
                            var warpGO = Instantiate(artSet.warpTunnelPrefab, worldPos, Quaternion.identity, mazeParent);
                            _spawned.Add(warpGO);
                            var warp = warpGO.GetComponent<WarpTunnel>();
                            warpTunnels.Add((x, y, warp));
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

            PairWarpTunnels(warpTunnels);
            PairWaterTiles(waterTilesByRow);
            ApplyBackdrop(data, artSet);
            SpawnScatteredPickups(artSet.bonusPickupPrefab, artSet.bonusPickupCount, data);
            SpawnScatteredPickups(universalCoinPrefab, coinsPerMaze, data);
        }

        /// <summary>Builds the set of crop-eligible cells (tile id 2 or 3) that should render the
        /// vegetable prefab instead of the kernel prefab, for worlds using
        /// MazeArtSet.useRandomVegetableQuota. Returns an empty (non-null) set when the world
        /// doesn't use this — callers can Contains() unconditionally without a null check.</summary>
        private static HashSet<Vector2Int> BuildForcedVegetableCells(MazeArtSet artSet, LevelData data, int[,] layout)
        {
            var result = new HashSet<Vector2Int>();
            if (!artSet.useRandomVegetableQuota)
            {
                return result;
            }

            var cropCells = new List<Vector2Int>();
            for (int x = 0; x < data.mazeWidth; x++)
            {
                for (int y = 0; y < data.mazeHeight; y++)
                {
                    if (layout[x, y] == TileCropKernel || layout[x, y] == TileCropVegetable)
                    {
                        cropCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            Shuffle(cropCells);
            int quota = Mathf.Min(artSet.vegetableQuota, cropCells.Count);
            for (int i = 0; i < quota; i++)
            {
                result.Add(cropCells[i]);
            }
            return result;
        }

        /// <summary>Scatters `count` copies of `prefab` onto random walkable, crop/pellet-free
        /// cells — see MazeArtSet.bonusPickupPrefab's doc comment for why this is separate from
        /// totalCropsRequired. Shared by both the per-world themed bonus (MazeArtSet.
        /// bonusPickupPrefab — cherry, grain sack, ...) and the world-independent
        /// universalCoinPrefab, so a maze can carry both scattered independently. Excludes crop
        /// kernel/vegetable/power-pellet cells (tile ids 2-4) — those already render their own
        /// pickup sprite, and stacking a second one on top (the original "on top of whatever else
        /// is already there" behaviour) reads as one pickup swallowing the other rather than two
        /// distinct items, especially now that Orchard's crop-apple sprite is large enough to
        /// visually dominate a smaller bonus cherry landing on the same cell. A no-op if prefab is
        /// null or count <= 0.</summary>
        private void SpawnScatteredPickups(GameObject prefab, int count, LevelData data)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            var candidates = new List<Vector2Int>();
            for (int x = 0; x < data.mazeWidth; x++)
            {
                for (int y = 0; y < data.mazeHeight; y++)
                {
                    int tileId = data.MazeLayout[x, y];
                    if (tileId != TileWall && tileId != TileCropKernel && tileId != TileCropVegetable
                        && tileId != TilePowerPellet)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }

            Shuffle(candidates);
            int spawnCount = Mathf.Min(count, candidates.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                _spawned.Add(Instantiate(prefab, GridToWorld(candidates[i]), Quaternion.identity, mazeParent));
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Falls back to the first configured set (CornField, always index 0 per
        /// Phase2ProjectBuilder.WireScene) if a level's mazeType has no matching entry yet — e.g. a
        /// future World 3/4 maze authored before its own art has been wired. Logs once per call
        /// rather than throwing, since a missing art set is a content gap, not a fatal error.</summary>
        private MazeArtSet ResolveArtSet(MazeType mazeType)
        {
            var match = mazeArtSets.Find(set => set.mazeType == mazeType);
            if (match != null)
            {
                return match;
            }

            if (mazeArtSets.Count > 0)
            {
                Debug.LogWarning($"[TileMapRenderer] No MazeArtSet configured for {mazeType} — falling back to {mazeArtSets[0].mazeType}'s art.");
                return mazeArtSets[0];
            }

            Debug.LogError("[TileMapRenderer] No MazeArtSets configured at all — maze will render with null prefabs.");
            return new MazeArtSet();
        }

        /// <summary>Swaps the shared GameplayBackdrop sprite to match the level's world every time a
        /// maze loads (ArtWiringBuilder.WireGameplayBackdrop only sets an Editor-time preview
        /// default and doesn't run at runtime). Re-derives the "cover" scale from the sprite's own
        /// aspect ratio each call — cheap (once per level load, not per frame) and correct even if a
        /// future world's backdrop art has a different aspect ratio than CornField's ~16:9. See
        /// ArtWiringBuilder.WireGameplayBackdrop's doc comment for why this formula looks the way it
        /// does (matching camera view width vs. maze world footprint, whichever needs more coverage).</summary>
        private void ApplyBackdrop(LevelData data, MazeArtSet artSet)
        {
            if (artSet.backdropSprite == null)
            {
                return;
            }

            EnsureGameplayBackdrop();
            _gameplayBackdrop.sprite = artSet.backdropSprite;
            _gameplayBackdrop.sortingOrder = -5;

            float mazeWorldWidth = (data.mazeWidth - 1) * CellSize;
            float mazeWorldHeight = (data.mazeHeight - 1) * CellSize;

            const float safetyMargin = 1.6f;
            float orthoSize = CellSize / (2f * CameraFollow.CellScreenHeightFraction);
            float targetCameraViewWidth = 2f * orthoSize * CameraFollow.MaxSupportedAspect;
            float requiredWidth = (Mathf.Max(mazeWorldWidth, targetCameraViewWidth) + CellSize) * safetyMargin;
            float requiredHeight = (mazeWorldHeight + CellSize) * safetyMargin;

            float imageAspect = artSet.backdropSprite.rect.width / artSet.backdropSprite.rect.height;
            float widthUnits = Mathf.Max(requiredWidth, requiredHeight * imageAspect);
            float heightUnits = widthUnits / imageAspect;
            _gameplayBackdrop.transform.localScale = new Vector3(widthUnits, heightUnits, 1f);
            _gameplayBackdrop.transform.position = new Vector3(mazeWorldWidth / 2f, mazeWorldHeight / 2f, 0f);
        }

        private void EnsureGameplayBackdrop()
        {
            if (_gameplayBackdrop != null)
            {
                return;
            }

            var go = GameObject.Find("GameplayBackdrop");
            if (go == null)
            {
                go = new GameObject("GameplayBackdrop");
                go.transform.SetParent(mazeParent, false);
                go.AddComponent<SpriteRenderer>();
            }
            _gameplayBackdrop = go.GetComponent<SpriteRenderer>();
        }

        /// <summary>Called once from Phase2ProjectBuilder.WireScene to configure the full per-world
        /// prefab list; ArtWiringBuilder.SetBackdropSprite mutates individual entries' backdropSprite
        /// afterward once art is uploaded, rather than needing to rebuild this whole list.</summary>
        public void SetMazeArtSets(List<MazeArtSet> sets)
        {
            mazeArtSets = sets;
        }

        /// <summary>Assigns (or updates) the backdrop sprite for one world's MazeArtSet, adding a
        /// new bare entry if that MazeType hasn't been configured yet — used by
        /// ArtWiringBuilder.WireMazeTiles once a world's backdrop art is uploaded, independent of
        /// whether its wall/warp prefabs were wired in the same pass.</summary>
        public void SetBackdropSprite(MazeType mazeType, Sprite sprite)
        {
            var match = mazeArtSets.Find(set => set.mazeType == mazeType);
            if (match == null)
            {
                match = new MazeArtSet { mazeType = mazeType };
                mazeArtSets.Add(match);
            }
            match.backdropSprite = sprite;
        }

        /// <summary>Assigns (or updates) the single pellet sprite for one world's MazeArtSet — see
        /// MazeArtSet.pelletSprite's doc comment. Used by ArtWiringBuilder.WireCropsAndPellets.</summary>
        public void SetPelletSprite(MazeType mazeType, Sprite sprite)
        {
            var match = mazeArtSets.Find(set => set.mazeType == mazeType);
            if (match == null)
            {
                match = new MazeArtSet { mazeType = mazeType };
                mazeArtSets.Add(match);
            }
            match.pelletSprite = sprite;
        }

        /// <summary>Fetches (or creates) a world's MazeArtSet entry directly, for callers that need
        /// to set several fields at once — e.g. ArtWiringBuilder wiring an entire new world's wall/
        /// ground/bonus-pickup art in one pass — rather than round-tripping through a single-field
        /// setter like SetBackdropSprite/SetPelletSprite for each one. MazeArtSet's fields are
        /// public, so the caller just assigns directly onto the returned reference.</summary>
        public MazeArtSet GetOrAddArtSet(MazeType mazeType)
        {
            var match = mazeArtSets.Find(set => set.mazeType == mazeType);
            if (match == null)
            {
                match = new MazeArtSet { mazeType = mazeType };
                mazeArtSets.Add(match);
            }
            return match;
        }

        /// <summary>Rolls a weighted tier (Sunflower common, GoldenWheat uncommon, Rainbow rare)
        /// purely for PowerPelletManager.GetDuration's 8s/15s/30s variety and
        /// SpawnCollectEffectIfRare/PlayRarePelletPickupSfx's "something extra-special" cue. Visual
        /// is artSet.pelletSprite for every pellet EXCEPT the one that actually won the maze's
        /// single rare-tier slot, which shows artSet.rarePelletSprite instead if that world has one
        /// — see MazeArtSet.rarePelletSprite's doc comment. A world with no rarePelletSprite set
        /// keeps the older "every pellet, rare or not, shows the one themed look" behaviour exactly
        /// as before.</summary>
        private void ConfigurePelletTier(GameObject pelletGO, MazeArtSet artSet)
        {
            var tier = RollPelletTier();
            bool wonRareSlot = false;

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
                    wonRareSlot = true;
                }
            }

            var pickup = pelletGO.GetComponent<PowerPelletPickup>();
            if (pickup != null)
            {
                pickup.pelletType = tier;
            }

            var sr = pelletGO.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Sprite chosen = wonRareSlot && artSet.rarePelletSprite != null ? artSet.rarePelletSprite : artSet.pelletSprite;
                if (chosen != null)
                {
                    sr.sprite = chosen;
                }
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

        /// <summary>Two-pass pairing: same-row first (this covers both the classic left/right-edge
        /// case AND same-row pairs that sit at non-edge columns, e.g. two openings through the same
        /// top wall at different x positions — several algorithmically generated mazes use exactly
        /// that shape), then whatever's left over gets paired by same-column (covers a genuinely
        /// vertical pair, e.g. one tile at (x,0) and another at (x,mazeHeight-1), which never share a
        /// row at all). An earlier version tried to guess the pairing axis per-tile from whether it
        /// sat on a left/right-edge column — that heuristic was wrong for the same-row/non-edge-column
        /// case above, misrouting those tiles into column pairing and stranding them, since they don't
        /// actually share a column with anything. Row-first-then-column needs no per-tile guessing:
        /// it just keeps trying the two conventions a maze can use until every tile is paired.</summary>
        private static void PairWarpTunnels(List<(int x, int y, WarpTunnel warp)> warpTunnels)
        {
            var remaining = new List<(int x, int y, WarpTunnel warp)>(warpTunnels);

            PairByAxis(remaining, t => t.y, "row");
            PairByAxis(remaining, t => t.x, "column");

            foreach (var (x, y, _) in remaining)
            {
                Debug.LogWarning($"[TileMapRenderer] Warp tile at ({x},{y}) has no row-mate or " +
                                  "column-mate to pair with — it will not teleport anything.");
            }
        }

        /// <summary>Groups tunnels still in `remaining` by the given axis key; any group of exactly
        /// 2 gets paired and removed from `remaining`. Groups of 1 (or 3+) are left in `remaining`
        /// for the next pass (or the final unpaired warning) rather than logged here, since a lone
        /// leftover from the row pass is expected to resolve in the column pass.</summary>
        private static void PairByAxis(List<(int x, int y, WarpTunnel warp)> remaining,
            System.Func<(int x, int y, WarpTunnel warp), int> axisKey, string axisLabel)
        {
            var groups = new Dictionary<int, List<(int x, int y, WarpTunnel warp)>>();
            foreach (var tunnel in remaining)
            {
                int key = axisKey(tunnel);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<(int x, int y, WarpTunnel warp)>();
                    groups[key] = list;
                }
                list.Add(tunnel);
            }

            foreach (var kvp in groups)
            {
                if (kvp.Value.Count != 2)
                {
                    continue;
                }

                var a = kvp.Value[0];
                var b = kvp.Value[1];
                a.warp.PairedWarp = b.warp;
                b.warp.PairedWarp = a.warp;
                remaining.Remove(a);
                remaining.Remove(b);
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
