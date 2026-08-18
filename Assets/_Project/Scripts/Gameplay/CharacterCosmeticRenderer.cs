using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Sibling to CharacterAnimator (Monetisation Build Plan Phase 4's cosmetics rendering hook).
    /// Reads this character's currently-equipped Hat/Skin from SaveManager on Awake/Refresh and:
    /// - Skin: pushes CosmeticData.skinFrames into CharacterAnimator.SetCosmeticFrameOverride,
    ///   which then drives the base SpriteRenderer as if it were the character's own art.
    /// - Hat: draws CosmeticData.hatFrames on a separate child SpriteRenderer layered above the
    ///   character, positioned/scaled per CosmeticData.hatOffset/hatScale (per-cosmetic, not
    ///   per-renderer — a sombrero and a party hat don't sit the same way on the same character),
    ///   and tracking CharacterAnimator's CurrentDisplayDirection/CurrentFrameIndex/IsFlippedX every
    ///   frame so it never drifts out of sync with the base walk cycle.
    ///
    /// No CosmeticData assets or hat/skin art exist yet (see CLAUDE.md's Phase 4 art-scope note) —
    /// with nothing equipped this component is a no-op: SaveManager.GetEquippedCosmetic returns ""
    /// for every character until a Store purchase+equip flow writes to it, so the hat child stays
    /// inactive and the skin override stays null. Safe to add to every character prefab now, ahead
    /// of any real cosmetic content.
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public class CharacterCosmeticRenderer : MonoBehaviour
    {
        private CharacterAnimator _animator;
        private CharacterBase _characterBase;
        private Transform _hatTransform;
        private SpriteRenderer _hatRenderer;
        private CosmeticData _equippedHat;

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
