using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Legal hub — reached from Settings' Policies.png icon (2026-08-27). Houses links to
    /// the Privacy Policy and, once drafted, Terms of Use and any other required legal copy.
    ///
    /// Privacy Policy opens the published draft policy page (an external Artifact URL, not
    /// in-app content — there's no web view in this project, so this is a plain
    /// Application.OpenURL hand-off to the device browser) via privacyPolicyButton. Terms of Use
    /// has no drafted content yet, so termsOfUseButton stays non-interactable with a "Coming Soon"
    /// label — same convention CosmeticPurchaseScreen.comingSoonButtons and the original
    /// Character Story placeholder both used for a destination with no real content yet.
    ///
    /// Overlay convention, same as SettingsPanel/ShopController — shown/hidden directly via
    /// SetActive, not through SceneTransitionManager.</summary>
    public class LegalScreen : MonoBehaviour
    {
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button termsOfUseButton;
        [SerializeField] private Button closeButton;

        // Draft policy page, grounded in this project's actual AdManager/IAPManager/SaveManager
        // data handling — see the privacy-policy-link memory. Marked "Draft — pending legal
        // review" on the page itself; swap this URL if the page is ever moved instead of
        // republished to the same one.
        private const string PrivacyPolicyUrl = "https://claude.ai/code/artifact/3cf566fc-e324-4c7a-a552-81391e24aa5d";

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (privacyPolicyButton != null)
            {
                privacyPolicyButton.onClick.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));
            }
            // termsOfUseButton is intentionally left non-interactable (set at build time) with no
            // listener — nothing to open until real Terms of Use copy exists.
        }
    }
}
