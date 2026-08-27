using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Main Menu's Settings button (Btn_settings, 2026-08-27 redesign) no longer opens
    /// SettingsPanel directly — it opens this hub first, matching a new mockup: the dimmed
    /// Landing_Opacity.png background (same convention Settings/Shop/Cosmetics hub already use,
    /// via ApplyDimmedLandingBackground) with two stacked wood-sign buttons, "SETTINGS"
    /// (SettingsSign.png) and "Shop" (ShopBanner.png) — the exact same sign art each of those
    /// screens already uses as its own header, reused here as tap targets instead.
    ///
    /// Overlay convention, same as SettingsPanel/ShopController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. Layers on top of Main Menu; its own
    /// back button just closes it, revealing Main Menu underneath.
    ///
    /// **Real bug found and fixed here (2026-08-27):** this hub never deactivates itself when it
    /// opens Settings/Shop, and since it was added to the scene AFTER SettingsPanel/ShopController
    /// in Phase5ProjectBuilder.BuildAll, it sat at a HIGHER sibling index under Canvas than either
    /// — meaning it drew (and raycast-intercepted) on top of them even once they were active,
    /// which read as "tapping the sign does nothing." Fixed generally, not by reordering the
    /// build: every overlay's own Show() now calls transform.SetAsLastSibling() before activating
    /// itself (SettingsPanel, ShopController, CoinPurchaseScreen, CosmeticsHubScreen,
    /// CosmeticPurchaseScreen, and this screen too), so each always draws above whatever's
    /// currently showing regardless of build/sibling order — check this pattern first if a future
    /// overlay's tap silently "does nothing" while the underlying data/wiring checks out.</summary>
    public class MenuHubScreen : MonoBehaviour
    {
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settingsScreen;

        [SerializeField] private Button shopButton;
        [SerializeField] private ShopController shopScreen;

        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (settingsButton != null && settingsScreen != null)
            {
                settingsButton.onClick.AddListener(() => settingsScreen.Show());
            }
            if (shopButton != null && shopScreen != null)
            {
                shopButton.onClick.AddListener(() => shopScreen.Show());
            }
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }
    }
}
