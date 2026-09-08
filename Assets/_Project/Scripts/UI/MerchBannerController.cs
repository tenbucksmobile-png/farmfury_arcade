using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Main Menu's "Visit Our Store" banner (2026-09-08) — a promotional link-out to the
    /// FarmFury franchise's real-goods merchandise store on www.farmfury.games. Fulfillment/checkout
    /// live entirely on that website; this app never touches payment, so this is deliberately NOT an
    /// IAP product and NOT registered in IAPManager — physical goods are exempt from Apple/Google's
    /// in-app purchase requirement (Apple Guideline 3.1.3), same accepted pattern as any app linking
    /// out to its own web store.
    ///
    /// Gated behind ParentalGateController before the hand-off, same convention every real-money
    /// purchase surface in this project already uses (see ShopController.HandleRemoveAdsTapped) —
    /// even though no purchase happens in-app, this is still a link to a real external checkout and
    /// AdManager already treats every player as child-directed.
    ///
    /// Application.OpenURL hands off to the device's system browser, not an in-app WebView — same
    /// pattern LegalScreen.cs uses for Privacy Policy/Terms — so App Review sees an unambiguous
    /// "leaves the app to a website" link, never anything purchase-flow-adjacent inside the app
    /// itself.</summary>
    public class MerchBannerController : MonoBehaviour
    {
        [SerializeField] private Button merchButton;

        // Update this if the real merch page ends up at a different path once it's live.
        private const string MerchUrl = "https://www.farmfury.games/merch";

        private void Awake()
        {
            if (merchButton != null)
            {
                merchButton.onClick.AddListener(HandleMerchTapped);
            }
        }

        private void HandleMerchTapped()
        {
            if (ParentalGateController.Instance != null)
            {
                ParentalGateController.Instance.Show(OpenMerchSite);
            }
            else
            {
                OpenMerchSite();
            }
        }

        private static void OpenMerchSite()
        {
            Application.OpenURL(MerchUrl);
        }
    }
}
