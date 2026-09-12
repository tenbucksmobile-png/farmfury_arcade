using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Shop hub's Cosmetics icon (2026-09-12) no longer jumps straight into a single flat
    /// screen with all 11 hat/trail items — it opens this small chooser first, matching a new
    /// mockup: the dimmed Landing_Opacity.png background (same convention every screen in this
    /// family uses) with two stacked wood-sign banners, "Hats & Caps" (Hats&Caps.png) and "Trails"
    /// (Trails.png) — real per-page header art, not a shared generic sign, reused here as tap
    /// targets. Same shape as MenuHubScreen (Settings/Shop signs opening their own screens).
    ///
    /// Overlay convention, same as every other screen in this family — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. Layers on top of the Shop hub; its own
    /// close button just closes it, revealing the Shop hub underneath. Tapping a banner does NOT
    /// close this screen — it stays active underneath the Hats/Trails page it opens (same "layers
    /// on top, never hidden" convention ChooseCharacterScreen uses over Pause), so that page's own
    /// generic close button (a plain SetActive(false)) reveals this chooser again automatically.</summary>
    public class CosmeticsChooserScreen : MonoBehaviour
    {
        [SerializeField] private Button hatsButton;
        [SerializeField] private CosmeticPurchaseScreen hatsScreen;

        [SerializeField] private Button trailsButton;
        [SerializeField] private CosmeticPurchaseScreen trailsScreen;

        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (hatsButton != null && hatsScreen != null)
            {
                hatsButton.onClick.AddListener(() => hatsScreen.Show());
            }
            if (trailsButton != null && trailsScreen != null)
            {
                trailsButton.onClick.AddListener(() => trailsScreen.Show());
            }
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }
    }
}
