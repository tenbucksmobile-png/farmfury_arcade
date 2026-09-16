using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>A thrown/launched ability hazard — travels tile-by-tile in a straight line,
    /// spinning as it flies, instantly defeating any robot it touches along the way (ForceDefeat,
    /// same "deployed hazard kills outright" convention as EggHazard/BounceRollAbility), then
    /// destroys itself once it stops (out of tiles, or blocked by a wall — same "stop early at an
    /// obstacle" convention BounceRollAbility's own roll uses). Used by HorseshoeThrowAbility
    /// (Horace's horseshoe, or a hay bale with the Hay Baler Machine Cosmetic skin equipped) —
    /// self-contained the same way EggHazard/WoollyClone are: Launch() starts its own travel
    /// coroutine, the spawning ability doesn't own its lifecycle after that.
    ///
    /// Stops and destroys itself the instant it hits a robot (2026-09-16, per direct feedback —
    /// was passing through to its full distance/a wall regardless of contact, same "flatten
    /// through multiple enemies" convention BounceRollAbility uses; the user wants a single-target
    /// hit here instead, both the robot and the thrown object visibly disappearing together on
    /// impact rather than the horseshoe/hay bale carrying on afterward).
    ///
    /// Impact art (2026-09-16, `impactSprite`, optional) — on hit, if a dedicated impact sprite is
    /// wired (currently only the Hay Baler skin's HayBaleEffect.prefab has one,
    /// Haybail_Damaged.png), swaps to it and holds/fades out over a real, visible duration instead
    /// of vanishing instantly, per direct feedback the disappearance needs to actually read as an
    /// effect happening. Horseshoe.prefab has no impactSprite wired, so it keeps the plain instant
    /// destroy — same "only apply real art where it exists" convention this project uses elsewhere.
    ///
    /// Replaces HoraceBuckEffect (a static one-shot fade-flash at the target's landing position,
    /// this file's own previous incarnation — renamed 2026-09-16, guid preserved) — that component
    /// needed a distinct per-direction "impact" sprite Kling AI was never able to generate (see
    /// CLAUDE.md's own history of this), and the launched-projectile rework doesn't need one at
    /// all: a single symmetric prop sprite (a horseshoe, or a hay bale) reused for every direction
    /// via runtime rotation is enough — no leftSprite/rightSprite fields needed any more.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ThrownProjectileEffect : MonoBehaviour
    {
        private const float SpinDegreesPerSecond = 540f;
        private const float ImpactHoldSeconds = 0.5f;
        private const float ImpactFadeSeconds = 0.4f;

        [SerializeField] private Sprite impactSprite;

        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>Starts the throw immediately — tileCount tiles in `direction`, `secondsPerTile`
        /// each, stopping early at a wall. Call once, right after Instantiate.</summary>
        public void Launch(TileMapRenderer tileMap, Vector2Int startCell, Vector2Int direction, int tileCount, float secondsPerTile)
        {
            StartCoroutine(TravelRoutine(tileMap, startCell, direction, tileCount, secondsPerTile));
        }

        private IEnumerator TravelRoutine(TileMapRenderer tileMap, Vector2Int cell, Vector2Int direction, int tileCount, float secondsPerTile)
        {
            for (int i = 0; i < tileCount; i++)
            {
                Vector2Int nextCell = cell + direction;
                if (!tileMap.IsWalkable(nextCell))
                {
                    break;
                }

                Vector3 from = tileMap.GridToWorld(cell);
                Vector3 to = tileMap.GridToWorld(nextCell);
                float t = 0f;
                while (t < secondsPerTile)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / secondsPerTile));
                    transform.Rotate(0f, 0f, SpinDegreesPerSecond * Time.deltaTime);
                    yield return null;
                }
                transform.position = to;
                cell = nextCell;
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var robot = other.GetComponent<RobotBase>();
            if (robot == null)
            {
                return;
            }

            robot.ForceDefeat();
            StopAllCoroutines();

            var ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                ownCollider.enabled = false;
            }

            if (impactSprite != null)
            {
                StartCoroutine(ImpactAndDissipate());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator ImpactAndDissipate()
        {
            transform.rotation = Quaternion.identity; // stop displaying it mid-spin on the impact frame
            _spriteRenderer.sprite = impactSprite;

            yield return new WaitForSeconds(ImpactHoldSeconds);

            Color start = _spriteRenderer.color;
            float t = 0f;
            while (t < ImpactFadeSeconds)
            {
                t += Time.deltaTime;
                Color c = start;
                c.a = Mathf.Lerp(start.a, 0f, t / ImpactFadeSeconds);
                _spriteRenderer.color = c;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
