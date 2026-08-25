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
    /// - Trail: CosmeticData.trailEffectPrefab is instantiated as a child if the cosmetic has one;
    ///   otherwise falls back to a built-in TrailRenderer tinted per cosmeticId (same "procedural
    ///   placeholder until dedicated VFX art lands" convention PelletCollectBurst uses for rare
    ///   pellets) — a real trail sprite/particle prefab dropped into trailEffectPrefab replaces it
    ///   with zero code changes needed here.
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

        private CharacterAnimator _animator;
        private CharacterBase _characterBase;
        private Transform _hatTransform;
        private SpriteRenderer _hatRenderer;
        private CosmeticData _equippedHat;

        private TrailRenderer _trailRenderer;
        private GameObject _trailEffectInstance;
        private Coroutine _rainbowTrailRoutine;
        private string _appliedTrailId;

        private void Awake()
        {
            _animator = GetComponent<CharacterAnimator>();
            _characterBase = GetComponent<CharacterBase>();

            var hatObject = new GameObject("EquippedHat");
            hatObject.transform.SetParent(transform, false);
            _hatTransform = hatObject.transform;
            _hatRenderer = hatObject.AddComponent<SpriteRenderer>();
            _hatRenderer.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 1;
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
            SpriteRenderer baseRenderer = GetComponent<SpriteRenderer>();
            _trailRenderer.sortingLayerID = baseRenderer.sortingLayerID;
            _trailRenderer.sortingOrder = baseRenderer.sortingOrder - 1;
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
            if (!_hatRenderer.enabled || _equippedHat == null)
            {
                return;
            }

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
    }
}
