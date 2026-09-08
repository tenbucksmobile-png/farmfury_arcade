using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Sibling to CharacterAnimator (Monetisation Build Plan Phase 4's cosmetics rendering hook).
    /// Reads this character's currently-equipped Hat/Skin/Trail from SaveManager on Awake/Refresh
    /// and:
    /// - Skin: pushes CosmeticData.skinFrames into CharacterAnimator.SetCosmeticFrameOverride,
    ///   which then drives the base SpriteRenderer as if it were the character's own art.
    /// - Hat: draws CosmeticData.hatFrames on a separate child SpriteRenderer layered above the
    ///   character, positioned/scaled per CosmeticData.hatOffset/hatScale (per-cosmetic, not
    ///   per-renderer — a sombrero and a party hat don't sit the same way on the same character),
    ///   and tracking CharacterAnimator's CurrentDisplayDirection/CurrentFrameIndex/IsFlippedX every
    ///   frame so it never drifts out of sync with the base walk cycle.
    /// - Trail: CosmeticData.trailEffectPrefab is instantiated as a child if the cosmetic has one
    ///   (for a future dedicated particle/VFX prefab); otherwise, if the cosmetic has a real
    ///   previewSprite (all 4 shipped trails do), periodic fading CosmeticTrailGhost afterimages of
    ///   that actual art spawn as the character moves; only falls all the way back to a flat
    ///   procedural TrailRenderer colour line (same "placeholder until dedicated art lands"
    ///   convention PelletCollectBurst uses for rare pellets) if neither exists.
    ///
    /// With nothing equipped this component is a no-op: SaveManager.GetEquippedCosmetic/
    /// GetEquippedTrail return "" until a Store purchase+equip flow writes to them, so the hat
    /// child and trail renderer both stay inactive and the skin override stays null.
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public class CharacterCosmeticRenderer : MonoBehaviour
    {
        private const float TrailDurationSeconds = 0.35f;
        private const float TrailRainbowCycleSeconds = 1.2f;

        /// <summary>Ghost-afterimage trail (real art path) tuning — spawns a fading copy of the
        /// equipped trail's own CosmeticData.previewSprite every GhostSpawnDistance world units of
        /// movement, sized relative to the character (previewSprite's import PPU is already set to
        /// its own texture width, same convention every character/cosmetic sprite in this project
        /// uses, so localScale 1 == the character's own ~1-unit size; GhostScale shrinks it to read
        /// as a trailing prop rather than a second full-size character).
        ///
        /// Re-tuned 2026-09-08, twice. First pass (per feedback the trail was "too light and
        /// sparse") went dense-and-small: GhostSpawnDistance 0.18 (many overlapping ghosts),
        /// GhostScale 0.65. That fixed density/visibility but broke legibility — packed that
        /// tightly, each ghost's actual art (e.g. EmberTrail.png's ember cluster) blurred into its
        /// neighbours and just read as an indistinct colour smear, worse on a larger/higher-res
        /// display where the eye can resolve individual ghosts and notices they're NOT distinct
        /// artwork. Second pass inverts the trade: fewer, BIGGER, more spaced-out ghosts — each one
        /// large enough (GhostScale 1.0, matching the character's own size) to actually read as the
        /// real art, spaced far enough apart (GhostSpawnDistance 0.4) that neighbours don't merge
        /// into mush. Every character's own movement speed is unified to 4.0
        /// (CharacterData.movementSpeed), and GridMovement covers speed * TileMapRenderer.CellSize
        /// (2) world units per second — i.e. 8 units/sec — so GhostLifetimeSeconds 0.7 still covers
        /// well over 2 tiles (0.7 * 8 = 5.6 world units) despite the wider spacing.</summary>
        private const float GhostSpawnDistance = 0.4f;
        private const float GhostLifetimeSeconds = 0.7f;
        private const float GhostScale = 1f;

        private CharacterAnimator _animator;
        private CharacterBase _characterBase;
        private SpriteRenderer _baseRenderer;
        private Transform _hatTransform;
        private SpriteRenderer _hatRenderer;
        private CosmeticData _equippedHat;

        private TrailRenderer _trailRenderer;
        private GameObject _trailEffectInstance;
        private Coroutine _rainbowTrailRoutine;
        private string _appliedTrailId;
        private Sprite _activeGhostSprite;
        private Vector3 _lastGhostSpawnPosition;

        private void Awake()
        {
            _animator = GetComponent<CharacterAnimator>();
            _characterBase = GetComponent<CharacterBase>();
            _baseRenderer = GetComponent<SpriteRenderer>();

            var hatObject = new GameObject("EquippedHat");
            hatObject.transform.SetParent(transform, false);
            _hatTransform = hatObject.transform;
            _hatRenderer = hatObject.AddComponent<SpriteRenderer>();
            _hatRenderer.sortingOrder = _baseRenderer.sortingOrder + 1;
            _hatRenderer.enabled = false;

            var trailObject = new GameObject("EquippedTrail");
            trailObject.transform.SetParent(transform, false);
            _trailRenderer = trailObject.AddComponent<TrailRenderer>();
            _trailRenderer.time = TrailDurationSeconds;
            _trailRenderer.startWidth = 0.5f * TileMapRenderer.CellSize;
            _trailRenderer.endWidth = 0f;
            _trailRenderer.minVertexDistance = 0.05f;
            _trailRenderer.textureMode = LineTextureMode.Stretch;
            _trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _trailRenderer.sortingLayerID = _baseRenderer.sortingLayerID;
            _trailRenderer.sortingOrder = _baseRenderer.sortingOrder - 1;
            _trailRenderer.enabled = false;
        }

        private void Start()
        {
            Refresh();
        }

        /// <summary>Call after a character swap/spawn, or right after a Store equip action, to
        /// re-read SaveManager's equipped-cosmetic state — CharacterBase.Initialize already calls
        /// this so a freshly swapped-to character picks up its cosmetics automatically.</summary>
        public void Refresh()
        {
            if (SaveManager.Instance == null || DataManager.Instance == null || _characterBase == null)
            {
                return;
            }

            CharacterType character = _characterBase.CharacterType;

            string equippedSkinId = SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Skin, character);
            CosmeticData skin = DataManager.Instance.GetCosmeticData(equippedSkinId);
            _animator.SetCosmeticFrameOverride(skin != null ? skin.skinFrames : null);

            string equippedHatId = SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Hat, character);
            _equippedHat = DataManager.Instance.GetCosmeticData(equippedHatId);
            _hatRenderer.enabled = _equippedHat != null && _equippedHat.hatFrames != null && _equippedHat.hatFrames.Length >= 8;
            if (_hatRenderer.enabled)
            {
                _hatTransform.localPosition = _equippedHat.hatOffset;
                _hatTransform.localScale = Vector3.one * _equippedHat.hatScale;
            }

            string equippedTrailId = SaveManager.Instance.GetEquippedTrail();
            CosmeticData trail = DataManager.Instance.GetCosmeticData(equippedTrailId);
            ApplyTrail(trail);
        }

        private void ApplyTrail(CosmeticData trail)
        {
            string trailId = trail != null ? trail.cosmeticId : null;
            if (trailId == _appliedTrailId)
            {
                return;
            }
            _appliedTrailId = trailId;

            if (_rainbowTrailRoutine != null)
            {
                StopCoroutine(_rainbowTrailRoutine);
                _rainbowTrailRoutine = null;
            }
            if (_trailEffectInstance != null)
            {
                Destroy(_trailEffectInstance);
                _trailEffectInstance = null;
            }
            _trailRenderer.enabled = false;
            _activeGhostSprite = null;

            if (trail == null)
            {
                return;
            }

            if (trail.trailEffectPrefab != null)
            {
                _trailEffectInstance = Instantiate(trail.trailEffectPrefab, transform);
                _trailEffectInstance.transform.localPosition = Vector3.zero;
                return;
            }

            if (trail.previewSprite != null)
            {
                // Real uploaded art (e.g. EmberTrail.png) exists for every shipped trail — use it
                // as a fading afterimage instead of the flat procedural colour line below, which is
                // only a last-resort fallback for a trail with neither a dedicated prefab nor art.
                _activeGhostSprite = trail.previewSprite;
                _lastGhostSpawnPosition = transform.position;
                return;
            }

            _trailRenderer.enabled = true;
            _trailRenderer.Clear();
            if (trailId == "trail_rainbowribbon")
            {
                _rainbowTrailRoutine = StartCoroutine(AnimateRainbowTrail());
            }
            else
            {
                _trailRenderer.colorGradient = SolidFadeGradient(PlaceholderTrailColor(trailId));
            }
        }

        /// <summary>Procedural stand-in colour per trail cosmetic, until dedicated VFX art/prefabs
        /// exist for trailEffectPrefab — chosen to at least gesture at each trail's real-world
        /// theme (corn husk = tan, ember = orange, sparkle dust = pale cyan-white).</summary>
        private static Color PlaceholderTrailColor(string trailId)
        {
            switch (trailId)
            {
                case "trail_cornhusk": return new Color(0.82f, 0.68f, 0.4f);
                case "trail_ember": return new Color(1f, 0.35f, 0.1f);
                case "trail_sparkledust": return new Color(0.8f, 0.95f, 1f);
                default: return Color.white;
            }
        }

        private static Gradient SolidFadeGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private IEnumerator AnimateRainbowTrail()
        {
            while (true)
            {
                float hue = (Time.time % TrailRainbowCycleSeconds) / TrailRainbowCycleSeconds;
                _trailRenderer.colorGradient = SolidFadeGradient(Color.HSVToRGB(hue, 1f, 1f));
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (_hatRenderer.enabled && _equippedHat != null)
            {
                int baseIndex;
                switch (_animator.CurrentDisplayDirection)
                {
                    case Direction.Up: baseIndex = 0; break;
                    case Direction.Left: baseIndex = 4; break;
                    case Direction.Right: baseIndex = 6; break;
                    default: baseIndex = 2; break; // Down
                }

                int frameOffset = Mathf.Clamp(_animator.CurrentFrameIndex, 0, 1);
                _hatRenderer.sprite = _equippedHat.hatFrames[baseIndex + frameOffset];
                _hatRenderer.flipX = _animator.IsFlippedX;
            }

            if (_activeGhostSprite != null &&
                Vector3.Distance(transform.position, _lastGhostSpawnPosition) >= GhostSpawnDistance)
            {
                SpawnGhost();
                _lastGhostSpawnPosition = transform.position;
            }
        }

        private void SpawnGhost()
        {
            var go = new GameObject("TrailGhost");
            go.transform.position = transform.position;
            var ghost = go.AddComponent<CosmeticTrailGhost>();
            ghost.Configure(_activeGhostSprite, _baseRenderer.sortingLayerID, _baseRenderer.sortingOrder - 1, GhostScale, GhostLifetimeSeconds);
        }
    }
}
