using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Legal hub — reached from Settings' Policies.png icon (2026-08-27). Houses links to
    /// the Privacy Policy and Terms of Use.
    ///
    /// Both open published draft pages (external Artifact URLs, not in-app content — there's no
    /// web view in this project, so this is a plain Application.OpenURL hand-off to the device
    /// browser). Audit finding F9.6: Terms of Use previously had no drafted content and stayed
    /// non-interactable with a "Coming Soon" label; it now points at a real draft (paired with the
    /// Privacy Policy, same design system, same "Draft — pending legal review" status) the same way
    /// Privacy Policy always has.</summary>
    public class LegalScreen : MonoBehaviour
    {
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button termsOfUseButton;
        [SerializeField] private Button closeButton;

        // Studio-owned hosting (2026-09-20): plain static pages in public_html on farmfurygames.com,
        // replacing the private claude.ai artifact links. Both still marked "Draft — pending legal
        // review" on their own pages. The same Privacy Policy URL is what App Store Connect and
        // Play Console's Privacy Policy field must hold — keep all three in sync.
        private const string PrivacyPolicyUrl = "https://www.farmfurygames.com/privacy/";
        private const string TermsOfUseUrl = "https://www.farmfurygames.com/terms/";

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
            if (termsOfUseButton != null)
            {
                termsOfUseButton.onClick.AddListener(() => Application.OpenURL(TermsOfUseUrl));
            }
        }
    }
}
