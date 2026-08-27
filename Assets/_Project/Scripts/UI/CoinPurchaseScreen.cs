using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Coin-pack purchase surface (100/500/5000/15000) — extracted from the old ShopController
    /// (2026-08-27 redesign) once that screen was repurposed into a 4-icon navigation hub (Cash/
    /// Worlds/Ads/Cosmetics — see ShopController's own doc comment). Reached by tapping the Cash
    /// icon (Shop.png) there.
    ///
    /// Also owns Restore Purchases (moved here from Settings the same session) — Apple requires a
    /// restore entry point somewhere for non-consumable IAPs, and this is the actual store surface
    /// real purchases happen on, the natural place for it.
    ///
    /// Overlay convention, same as ShopController/SettingsPanel — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class CoinPurchaseScreen : MonoBehaviour
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

        [SerializeField] private Button restorePurchasesButton;
        [SerializeField] private TextMeshProUGUI restoreStatusText;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (restorePurchasesButton != null)
            {
                restorePurchasesButton.onClick.AddListener(HandleRestorePurchasesTapped);
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
            if (restoreStatusText != null)
            {
                restoreStatusText.text = string.Empty;
            }
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
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase failed.";
            }
        }

        /// <summary>Required by Apple for non-consumable products (Remove Ads/hats/trails); good
        /// practice on Android too. Individual restored purchases flow through IAPManager's normal
        /// HandlePurchasePending path (same as a fresh purchase), so their effects apply identically
        /// either way — this button only needs to kick off the restore call and show feedback while
        /// it runs.</summary>
        private void HandleRestorePurchasesTapped()
        {
            if (IAPManager.Instance == null)
            {
                if (restoreStatusText != null)
                {
                    restoreStatusText.text = "Store unavailable.";
                }
                return;
            }

            if (restoreStatusText != null)
            {
                restoreStatusText.text = "Restoring...";
            }
            IAPManager.Instance.RestorePurchases(success =>
            {
                if (restoreStatusText != null)
                {
                    restoreStatusText.text = success ? "Purchases restored!" : "Restore failed.";
                }
            });
        }
    }
}
