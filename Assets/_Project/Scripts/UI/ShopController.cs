using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shop hub (2026-08-27 redesign) — matches a new mockup: "Shop" wood sign header
    /// (ShopBanner.png) and a single row of 4 icons: Cash (Shop.png, opens
    /// <see cref="CoinPurchaseScreen"/> for the actual coin packs), Worlds (WorldMaze.png, opens
    /// the World Purchase screen), Ads (Ads.png, a direct Remove Ads purchase — no sub-screen), and
    /// Cosmetics (Cosmetics_Icon.png, opens <see cref="CosmeticsHubScreen"/>). Discards the old
    /// layout entirely (the 4 coin-pack icons + a big Cosmetics banner button used to live directly
    /// on this screen — they're now one tap further in, behind the Cash/Cosmetics icons).
    ///
    /// Reached from <see cref="MenuHubScreen"/>'s "Shop" sign, and directly from wherever else
    /// already held a ShopController reference (e.g. Level Select's own Shop icon) — same
    /// GameObject, new content underneath.
    ///
    /// Remove Ads is the one icon here that's a direct purchase rather than a navigation — since
    /// it's a non-consumable that can only ever be bought once, RefreshRemoveAdsButtonState dims it
    /// and makes it non-interactable once SaveManager.AdsRemoved is already true (same convention
    /// Level Complete's DoubleCoinsButton uses for an icon-only "already owned" state).
    ///
    /// Overlay convention, same as SettingsPanel/CosmeticsHubScreen — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [SerializeField] private Button cashButton;
        [SerializeField] private CoinPurchaseScreen coinPurchaseScreen;

        [SerializeField] private Button worldsButton;
        [SerializeField] private CosmeticPurchaseScreen worldPurchaseScreen;

        [SerializeField] private Button cosmeticsButton;
        [SerializeField] private CosmeticsHubScreen cosmeticsHubScreen;

        [SerializeField] private Button removeAdsButton;
        [SerializeField] private Image removeAdsButtonIcon;

        /// <summary>Dims the Remove Ads icon once already owned — same tint convention
        /// SettingsPanel's MutedTint used before this button lived there.</summary>
        private static readonly Color OwnedTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (cashButton != null && coinPurchaseScreen != null)
            {
                cashButton.onClick.AddListener(() => coinPurchaseScreen.Show());
            }
            if (worldsButton != null && worldPurchaseScreen != null)
            {
                worldsButton.onClick.AddListener(() => worldPurchaseScreen.Show());
            }
            if (cosmeticsButton != null && cosmeticsHubScreen != null)
            {
                cosmeticsButton.onClick.AddListener(() => cosmeticsHubScreen.Show());
            }
            if (removeAdsButton != null)
            {
                removeAdsButton.onClick.AddListener(HandleRemoveAdsTapped);
            }
        }

        private void OnEnable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnPurchaseSucceeded += HandleRemoveAdsPurchaseSucceeded;
                IAPManager.Instance.OnPurchaseFailed += HandleRemoveAdsPurchaseFailed;
            }
            RefreshRemoveAdsButtonState();
        }

        private void OnDisable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnPurchaseSucceeded -= HandleRemoveAdsPurchaseSucceeded;
                IAPManager.Instance.OnPurchaseFailed -= HandleRemoveAdsPurchaseFailed;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        private void HandleRemoveAdsTapped()
        {
            if (IAPManager.Instance == null)
            {
                return;
            }
            IAPManager.Instance.PurchaseProduct(IAPManager.RemoveAdsProductId);
        }

        private void HandleRemoveAdsPurchaseSucceeded(string productId)
        {
            if (productId != IAPManager.RemoveAdsProductId)
            {
                return;
            }
            RefreshRemoveAdsButtonState();
        }

        private void HandleRemoveAdsPurchaseFailed(string productId, string reason)
        {
            // No status text on this icon row (see CoinPurchaseScreen for purchase feedback text) —
            // a failed Remove Ads tap just leaves the icon exactly as it was, tappable again.
        }

        /// <summary>Disables the Remove Ads icon (and dims it) once SaveManager.AdsRemoved is
        /// already true — a non-consumable can't be purchased twice.</summary>
        private void RefreshRemoveAdsButtonState()
        {
            bool owned = SaveManager.Instance != null && SaveManager.Instance.AdsRemoved;

            if (removeAdsButton != null)
            {
                removeAdsButton.interactable = !owned;
            }
            if (removeAdsButtonIcon != null)
            {
                removeAdsButtonIcon.color = owned ? OwnedTint : Color.white;
            }
        }
    }
}
