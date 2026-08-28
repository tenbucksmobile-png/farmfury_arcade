using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Audit finding C3.3: shrinks its own RectTransform's anchors to Screen.safeArea so
    /// whatever's parented under it never renders under a notch, Dynamic Island, punch-hole camera,
    /// or gesture-navigation bar. Standard, well-established pattern (matches Unity's own published
    /// sample) — recomputes only when the safe area, screen size, or orientation actually changes,
    /// not every frame unconditionally.
    ///
    /// Used by Phase5ProjectBuilder.BuildGameplayHUD as an intermediate parent every corner HUD
    /// element is built under instead of directly onto the screen root — every existing
    /// AnchorTopLeft/AnchorBottomRight/etc. call already measures its inset relative to its
    /// immediate parent's edges, so parenting under this instead needed zero changes to any of that
    /// positioning code; the same insets are just now measured from the safe rectangle instead of
    /// the raw physical screen edge.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height ||
                _lastOrientation != Screen.orientation)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 anchorMin = _lastSafeArea.position;
            Vector2 anchorMax = _lastSafeArea.position + _lastSafeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
        }
    }
}
