using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Generic cosmetic-style purchase surface (2026-08-20 redesign) — reused for both the Hats
    /// screen (3 items: Baseball Cap / Cowboy Hat / Sombrero) and the Trails screen (4 items: Corn
    /// Husk / Rainbow Ribbon / Sparkle Dust / Ember), since both mockups share the same layout: a
    /// breadcrumb icon, a row of framed purchase items (each item's own frame art is baked in by
    /// the builder, same "art wiring is baked at construction time" convention
    /// CosmeticCardController's PurchaseCardFrame used), a single price plaque (every item on a
    /// given screen costs the same $3.99), and a back button.
    ///
    /// Every item is a real-money IAP purchase via IAPManager — tapping a framed item purchases it
    /// directly (no separate confirm step), same "tap to buy" convention the old coin-priced
    /// CosmeticStoreScreen used. IAPManager grants ownership + auto-equips on a confirmed purchase;
    /// this screen only needs to kick off the purchase and show processing/success/failure
    /// feedback.
    ///
    /// Overlay convention, same as CosmeticsHubScreen/ShopController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class CosmeticPurchaseScreen : MonoBehaviour
    {
        [Serializable]
        private struct ItemButton
        {
            public string productId;
            public Button button;
        }

        [SerializeField] private ItemButton[] itemButtons;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }

            if (itemButtons == null)
            {
                return;
            }

            foreach (var entry in itemButtons)
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
    }
}
