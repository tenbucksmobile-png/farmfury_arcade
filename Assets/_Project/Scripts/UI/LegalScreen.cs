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

        // Draft policy pages, grounded in this project's actual AdManager/IAPManager/SaveManager
        // data handling and purchase flows — see the privacy-policy-link memory. Both marked
        // "Draft — pending legal review" on their own pages; swap these URLs if either page is
        // ever moved instead of republished to the same one.
        private const string PrivacyPolicyUrl = "https://claude.ai/code/artifact/3cf566fc-e324-4c7a-a552-81391e24aa5d";
        private const string TermsOfUseUrl = "https://claude.ai/code/artifact/ada226cb-625c-4875-8969-e34f71355f9d";

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
