using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>AI-controlled clone spawned by TripleCloneAbility. Wanders the maze picking a
    /// random valid (non-reversing) direction at each intersection — "splits at each intersection"
    /// per spec, reusing RobotAI's wall-respecting neighbour query so it never leaves the maze.
    /// Has no PlayerHealth and never calls RobotBase.RegisterHit, so it "ignores robots": it can't
    /// be killed by one and can't defeat a Vulnerable one either. CropCollector (the same component
    /// the player uses) is reused as-is for crop collection. Fades out and destroys itself after
    /// lifetimeSeconds. When the Feather Storm combo buff is active, eggPrefab is non-null and the
    /// clone drops an egg every few tiles as it walks.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class WoollyClone : MonoBehaviour
    {
        private const float AlignmentEpsilon = 0.02f;

        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float lifetimeSeconds = 10f;
        [SerializeField] private float eggDropIntervalTiles = 3f;
        [SerializeField] private float fadeOutSeconds = 1f;

        private TileMapRenderer _tileMap;
        private SpriteRenderer _spriteRenderer;
        private GameObject _eggPrefab;
        private Direction _currentDirection = Direction.None;
        private Vector2Int _currentGridPosition;
        private float _lifeTimer;
        private float _tilesSinceLastEgg;
        private bool _fading;

        public void Initialize(TileMapRenderer tileMap, Vector2Int spawnCell, GameObject eggPrefabIfBuffed)
        {
            _tileMap = tileMap;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _eggPrefab = eggPrefabIfBuffed;
            _currentGridPosition = spawnCell;
            transform.position = tileMap.GridToWorld(spawnCell);
            _lifeTimer = lifetimeSeconds;

            var validDirs = RobotAI.GetValidDirections(spawnCell, Direction.None, tileMap);
            if (validDirs.Length > 0)
            {
                _currentDirection = validDirs[Random.Range(0, validDirs.Length)];
            }
        }

        private void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                if (!_fading)
                {
                    _fading = true;
                    StartCoroutine(FadeOutAndDestroy());
                }
                return;
            }

            if (_tileMap == null)
            {
                return;
            }

            Vector2Int cell = _tileMap.WorldToGrid(transform.position);

            // Hard containment guard: cell is derived from raw transform.position every frame with
            // no clamping (WorldToGrid/GridToWorld do no bounds checking of their own), and
            // _currentDirection is only re-validated at "atCenter" events — GetValidDirections
            // already stops the clone from ever CHOOSING a wall/water/out-of-bounds neighbour, but
            // this is a belt-and-suspenders check against ending up on (and dropping an egg onto) an
            // invalid cell some other way — e.g. a wall a Headbutt Through/Iron Stampede use
            // destroyed mid-run turning a former border cell walkable, or any other edge case not
            // already covered by the direction-selection logic. If the cell we've arrived at isn't
            // actually walkable, snap back to the last confirmed-good cell and re-pick a direction
            // from there instead of trusting it.
            if (!_tileMap.IsWalkable(cell))
            {
                cell = _currentGridPosition;
                transform.position = _tileMap.GridToWorld(cell);
                _currentDirection = Direction.None;
            }

            Vector3 cellCenter = _tileMap.GridToWorld(cell);
            bool atCenter = Vector3.Distance(transform.position, cellCenter) < AlignmentEpsilon;

            if (atCenter)
            {
                transform.position = cellCenter;
                _currentGridPosition = cell;

                var valid = RobotAI.GetValidDirections(cell, _currentDirection, _tileMap);
                _currentDirection = valid.Length > 0 ? valid[Random.Range(0, valid.Length)] : Direction.None;

                if (_eggPrefab != null)
                {
                    _tilesSinceLastEgg += 1f;
                    if (_tilesSinceLastEgg >= eggDropIntervalTiles)
                    {
                        _tilesSinceLastEgg = 0f;
                        Instantiate(_eggPrefab, cellCenter, Quaternion.identity);
                    }
                }
            }

            if (_currentDirection != Direction.None)
            {
                Vector2Int dv = DirectionUtils.ToVector(_currentDirection);
                transform.position += new Vector3(dv.x, dv.y, 0f) * speed * Time.deltaTime;
            }
        }

        private System.Collections.IEnumerator FadeOutAndDestroy()
        {
            if (_spriteRenderer == null)
            {
                Destroy(gameObject);
                yield break;
            }

            Color start = _spriteRenderer.color;
            float t = 0f;
            while (t < fadeOutSeconds)
            {
                t += Time.deltaTime;
                Color c = start;
                c.a = Mathf.Lerp(start.a, 0f, t / fadeOutSeconds);
                _spriteRenderer.color = c;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
