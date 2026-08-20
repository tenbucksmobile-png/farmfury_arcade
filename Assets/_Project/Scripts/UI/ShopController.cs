using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Coin-pack purchase surface (2026-08-20 redesign) — matches the new Shop mockup exactly:
    /// landing.png background, a "Shop" wood sign, 4 self-contained coin-pack plaques (each bakes
    /// in its own coin count + $ price, so no separate label text is needed the way the old plain
    /// text-button version required), a "Cosmetics" button that opens
    /// <see cref="CosmeticsHubScreen"/>, and a back button. Remove Ads is no longer sold from this
    /// screen — the new mockup only shows the 4 coin packs + Cosmetics, so that IAP product (still
    /// registered in IAPManager) currently has no purchase surface; revisit if Remove Ads needs a
    /// new home.
    ///
    /// Overlay convention, same as SettingsPanel/RevivePromptController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Serializable]
        private struct CoinPackButton
        {
            public string productId;
            public Button button;
        }

        [SerializeField] private CoinPackButton[] coinPackButtons;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        // Opens the cosmetics purchase hub, layered on top of this screen (same "overlay on top of
        // overlay" convention ChooseCharacterScreen uses over Pause).
        [SerializeField] private Button cosmeticsButton;
        [SerializeField] private CosmeticsHubScreen cosmeticsHubScreen;

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

            if (coinPackButtons == null)
            {
                return;
            }

            foreach (var entry in coinPackButtons)
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
