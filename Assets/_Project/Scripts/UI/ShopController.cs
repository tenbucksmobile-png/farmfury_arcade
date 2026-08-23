using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Purchase surface — matches the 2026-08-20 Shop mockup (landing.png background, "Shop" wood
    /// sign, 4 self-contained coin-pack plaques each baking in its own coin count + $ price) plus a
    /// Remove Ads button (2026-08-23, see BuildShopOverlay's own comment — the mockup itself only
    /// showed the 4 coin packs, so this product had no purchase surface at all until now), a
    /// "Cosmetics" button that opens <see cref="CosmeticsHubScreen"/>, and a back button.
    ///
    /// purchaseButtons is generic (any IAP product id + its Button), not coin-pack-specific despite
    /// the field's earlier name — Remove Ads is just another entry in the same array. The one thing
    /// that IS special-cased is Remove Ads itself: since it's a non-consumable that can only ever be
    /// bought once, RefreshRemoveAdsButtonState disables it (and swaps its label) once
    /// SaveManager.AdsRemoved is already true, both on enable and right after a successful purchase
    /// — every other entry here is a repeatable consumable with no such state to track.
    ///
    /// Overlay convention, same as SettingsPanel/RevivePromptController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Serializable]
        private struct PurchaseButton
        {
            public string productId;
            public Button button;
        }

        [SerializeField] private PurchaseButton[] purchaseButtons;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        // Opens the cosmetics purchase hub, layered on top of this screen (same "overlay on top of
        // overlay" convention ChooseCharacterScreen uses over Pause).
        [SerializeField] private Button cosmeticsButton;
        [SerializeField] private CosmeticsHubScreen cosmeticsHubScreen;

        // Remove Ads is also wired into purchaseButtons above (for the actual purchase click), but
        // needs its own button/label references here too — it's the only entry that can only ever
        // be bought once, so its button (and label, since it has no dedicated icon art yet and shows
        // a plain text label instead) needs to reflect "already owned" state that no other product
        // in this array tracks.
        [SerializeField] private Button removeAdsButton;
        [SerializeField] private TextMeshProUGUI removeAdsLabel;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }

            if (cosmeticsButton != null && cosmeticsHubScreen != null)
            {
                cosmeticsButton.onClick.AddListener(() => cosmeticsHubScreen.Show());
            }

            if (purchaseButtons == null)
            {
                return;
            }

            foreach (var entry in purchaseButtons)
            {
                if (entry.button == null)
                {
                    continue;
                }
                string productId = entry.productId;
                entry.button.onClick.AddListener(() => HandlePurchaseTapped(productId));
            }
        }

        private void OnEnable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnPurchaseSucceeded += HandlePurchaseSucceeded;
                IAPManager.Instance.OnPurchaseFailed += HandlePurchaseFailed;
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }

            RefreshRemoveAdsButtonState();
        }

        private void OnDisable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnPurchaseSucceeded -= HandlePurchaseSucceeded;
                IAPManager.Instance.OnPurchaseFailed -= HandlePurchaseFailed;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        private void HandlePurchaseTapped(string productId)
        {
            if (IAPManager.Instance == null)
            {
                if (statusText != null)
                {
                    statusText.text = "Store unavailable.";
                }
                return;
            }

            if (statusText != null)
            {
                statusText.text = "Processing...";
            }
            IAPManager.Instance.PurchaseProduct(productId);
        }

        private void HandlePurchaseSucceeded(string productId)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase complete!";
            }

            if (productId == IAPManager.RemoveAdsProductId)
            {
                RefreshRemoveAdsButtonState();
            }
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase failed.";
            }
        }

        /// <summary>Disables the Remove Ads button (and swaps its label to reflect ownership) once
        /// SaveManager.AdsRemoved is already true — a non-consumable can't be purchased twice, and
        /// tapping it again would just fail with a confusing error rather than obviously being a
        /// no-op.</summary>
        private void RefreshRemoveAdsButtonState()
        {
            bool owned = SaveManager.Instance != null && SaveManager.Instance.AdsRemoved;

            if (removeAdsButton != null)
            {
                removeAdsButton.interactable = !owned;
            }

            if (removeAdsLabel != null)
            {
                removeAdsLabel.text = owned ? "Ads Removed" : "Remove Ads";
            }
        }
    }
}
