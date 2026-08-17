using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Monetisation Build Plan Phase 3's minimal purchase surface: 5 coin packs + Remove Ads,
    /// reached from Main Menu. Replaces the old "Store is coming in Phase 6!" placeholder — the
    /// full cosmetics Store (hats/skins/trails/themes) is still Phase 4 scope and doesn't exist
    /// yet; this screen is deliberately just the IAP plumbing's purchase surface, with plain text
    /// labels since no purchase-card art has been commissioned yet (see the plan doc's own "Art
    /// needed" list for Phase 3 — currently zero art exists here).
    ///
    /// Overlay convention, same as SettingsPanel/RevivePromptController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Serializable]
        private struct ProductButton
        {
            public string productId;
            public string displayName;
            public Button button;
            public TextMeshProUGUI label;
        }

        [SerializeField] private ProductButton[] productButtons;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }

            if (productButtons == null)
            {
                return;
            }

            foreach (var entry in productButtons)
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
                IAPManager.Instance.OnProductsReady += RefreshPrices;
            }

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
            RefreshPrices();
            RefreshRemoveAdsState();
        }

        private void OnDisable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnPurchaseSucceeded -= HandlePurchaseSucceeded;
                IAPManager.Instance.OnPurchaseFailed -= HandlePurchaseFailed;
                IAPManager.Instance.OnProductsReady -= RefreshPrices;
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
                RefreshRemoveAdsState();
            }
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase failed.";
            }
        }

        /// <summary>Swaps in real localized prices once IAPManager's store connection resolves them
        /// (IAPManager.GetPriceString falls back to the GDD's static price table until then, so the
        /// label is never blank).</summary>
        private void RefreshPrices()
        {
            if (productButtons == null)
            {
                return;
            }

            foreach (var entry in productButtons)
            {
                if (entry.label == null)
                {
                    continue;
                }
                string price = IAPManager.Instance != null ? IAPManager.Instance.GetPriceString(entry.productId) : string.Empty;
                entry.label.text = string.IsNullOrEmpty(price) ? entry.displayName : $"{entry.displayName}\n{price}";
            }
        }

        /// <summary>Remove Ads is non-consumable — once owned, disable its button rather than
        /// letting the player tap a purchase they already have.</summary>
        private void RefreshRemoveAdsState()
        {
            if (productButtons == null || SaveManager.Instance == null)
            {
                return;
            }

            foreach (var entry in productButtons)
            {
                if (entry.productId == IAPManager.RemoveAdsProductId && entry.button != null)
                {
                    entry.button.interactable = !SaveManager.Instance.AdsRemoved;
                }
            }
        }
    }
}
