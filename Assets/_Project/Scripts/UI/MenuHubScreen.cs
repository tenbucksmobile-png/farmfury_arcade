using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Main Menu's Settings button (Btn_settings, 2026-08-27 redesign) no longer opens
    /// SettingsPanel directly — it opens this hub first, matching a new mockup: the full-brightness
    /// landing.png background (same undimmed treatment Main Menu itself uses, not the dimmed-poster
    /// convention overlays like Settings/Shop use) with two stacked wood-sign buttons, "SETTINGS"
    /// (SettingsSign.png) and "Shop" (ShopBanner.png) — the exact same sign art each of those
    /// screens already uses as its own header, reused here as tap targets instead.
    ///
    /// Overlay convention, same as SettingsPanel/ShopController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. Layers on top of Main Menu; its own
    /// back button just closes it, revealing Main Menu underneath.</summary>
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
            gameObject.SetActive(true);
        }
    }
}
