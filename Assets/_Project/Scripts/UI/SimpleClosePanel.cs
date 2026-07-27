using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Trivial "close this panel" wiring for one-button overlays (e.g. the Store
    /// "coming in Phase 6" placeholder) — a button.onClick.AddListener call made directly from an
    /// editor script at build time would NOT survive a scene save/reload (UnityEvent's
    /// non-persistent listeners aren't serialized), so even a single-button panel needs a real
    /// MonoBehaviour wiring its listener in Awake(), same as every other screen controller.</summary>
    public class SimpleClosePanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }
    }
}
