using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Generic horizontal card carousel, first built for Level Select's world-badge picker and reused
    /// as-is for Choose Character's animal-card picker (2026-07-31 Canva mockups — both show the same
    /// "front card enlarged, side cards smaller/pushed back" arc). Cards are laid out around a
    /// continuous <see cref="_offset"/> (in item-index units): the centred item sits at full scale,
    /// items further from centre are pushed down and shrunk (a cheap 2D stand-in for "receding into a
    /// circle" — full 3D perspective isn't needed for a handful of items). Flicking drags the offset
    /// directly; releasing snaps to the nearest integer index. Tapping the already-centred item
    /// invokes the selection callback; tapping any other visible item just re-centres the carousel on
    /// it first, so a stray tap mid-flick can't accidentally commit to the wrong item.
    ///
    /// The owning screen (LevelSelectController / ChooseCharacterScreen) instantiates/destroys the
    /// card GameObjects itself — it already knows what content/sprite each one needs — this component
    /// only ever positions/scales whatever RectTransforms it's handed via <see cref="SetItems"/> and
    /// reports taps back through the callback. Attached to the same GameObject as the container's own
    /// (invisible, raycastTarget) Image so empty space between cards still catches drag input.
    /// </summary>
    public class CardCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float itemSpacing = 380f;
        [SerializeField] private float sideScale = 0.72f;
        [SerializeField] private float snapSpeed = 10f;

        /// <summary>Radius of the circle items are arranged along — replaces the old flat
        /// "linear x-offset + fixed y-dip" layout with a true arc: itemSpacing is now the arc-length
        /// distance between adjacent items (converted to an angle via itemSpacing/arcRadius), so
        /// items curve away and dip down together as they move from centre, instead of sliding on a
        /// straight horizontal line with a separate vertical offset bolted on.</summary>
        [SerializeField] private float arcRadius = 2800f;

        private readonly List<RectTransform> _items = new List<RectTransform>();
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<int> _drawOrder = new List<int>();

        private float _offset;
        private float _targetOffset;
        private bool _dragging;
        private Action<int> _onCenterTapped;

        /// <summary>Replaces whatever cards the carousel currently knows about. Does not destroy
        /// GameObjects — the owning screen does that (it also owns picking each card's content).</summary>
        public void SetItems(IReadOnlyList<RectTransform> items, IReadOnlyList<Button> buttons, int startIndex, Action<int> onCenterTapped)
        {
            ClearListeners();
            _items.Clear();
            _buttons.Clear();
            _onCenterTapped = onCenterTapped;

            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
                _buttons.Add(buttons[i]);
                int capturedIndex = i;
                buttons[i].onClick.AddListener(() => OnItemTapped(capturedIndex));
            }

            _offset = _targetOffset = items.Count > 0 ? Mathf.Clamp(startIndex, 0, items.Count - 1) : 0f;
            ApplyLayout();
        }

        public void ClearItems()
        {
            ClearListeners();
            _items.Clear();
            _buttons.Clear();
        }

        private void ClearListeners()
        {
            foreach (var button in _buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_items.Count == 0)
            {
                return;
            }
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _items.Count == 0)
            {
                return;
            }
            _offset = Mathf.Clamp(_offset - eventData.delta.x / itemSpacing, 0f, _items.Count - 1);
            _targetOffset = _offset;
            ApplyLayout();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_items.Count > 0)
            {
                _targetOffset = Mathf.Clamp(Mathf.Round(_offset), 0, _items.Count - 1);
            }
        }

        private void Update()
        {
            if (_dragging || _items.Count == 0 || Mathf.Approximately(_offset, _targetOffset))
            {
                return;
            }
            _offset = Mathf.Lerp(_offset, _targetOffset, Time.unscaledDeltaTime * snapSpeed);
            if (Mathf.Abs(_offset - _targetOffset) < 0.001f)
            {
                _offset = _targetOffset;
            }
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var rect = _items[i];
                if (rect == null)
                {
                    continue;
                }
                float delta = i - _offset;
                float absDelta = Mathf.Abs(delta);
                float angle = delta * (itemSpacing / arcRadius);
                float x = arcRadius * Mathf.Sin(angle);
                float y = -arcRadius * (1f - Mathf.Cos(angle));
                rect.anchoredPosition = new Vector2(x, y);
                float scale = Mathf.Lerp(1f, sideScale, Mathf.Clamp01(absDelta));
                rect.localScale = new Vector3(scale, scale, 1f);
            }

            // Nearest-to-centre item drawn last (on top) — otherwise a side item sliding past centre
            // would briefly render over the one snapping into place. Sorts a scratch index list
            // rather than _items itself — _items' order is the index space OnItemTapped and the
            // SetItems callback both rely on, so it must never be permuted.
            _drawOrder.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                _drawOrder.Add(i);
            }
            _drawOrder.Sort((a, b) => Mathf.Abs(b - _offset).CompareTo(Mathf.Abs(a - _offset)));
            foreach (int index in _drawOrder)
            {
                _items[index].SetAsLastSibling();
            }
        }

        private void OnItemTapped(int index)
        {
            if (_items.Count == 0)
            {
                return;
            }
            if (Mathf.RoundToInt(_offset) == index && Mathf.Approximately(_offset, _targetOffset))
            {
                _onCenterTapped?.Invoke(index);
            }
            else
            {
                _targetOffset = index;
            }
        }
    }
}
