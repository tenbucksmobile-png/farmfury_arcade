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
    /// Cosmetics (Cosmetics_Icon.png, opens CosmeticsChooserScreen — a small chooser distinguishing
    /// Hats and Caps from Trails, each opening its own dedicated purchase page; see
    /// Phase5ProjectBuilder.BuildCosmeticsChooserScreen). Discards the old layout entirely (the 4
    /// coin-pack icons + a big Cosmetics banner button used to live directly on this screen —
    /// they're now one tap further in, behind the Cash/Cosmetics icons; the Cosmetics icon
    /// itself used to open one flat 11-item screen directly, replaced 2026-09-12 by this chooser).</summary>
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
    /// Overlay convention, same as SettingsPanel — shown/hidden directly via Show()/SetActive, not
    /// through SceneTransitionManager.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [SerializeField] private Button cashButton;
        [SerializeField] private CoinPurchaseScreen coinPurchaseScreen;

        [SerializeField] private Button worldsButton;
        [SerializeField] private CosmeticPurchaseScreen worldPurchaseScreen;

        [SerializeField] private Button cosmeticsButton;
        [SerializeField] private CosmeticsChooserScreen cosmeticsChooserScreen;

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
            if (cosmeticsButton != null && cosmeticsChooserScreen != null)
            {
                cosmeticsButton.onClick.AddListener(() => cosmeticsChooserScreen.Show());
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
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        /// <summary>Audit findings F3.5/F4.4: Remove Ads is a direct-purchase icon (no sub-screen)
        /// so it had no gate of its own, unlike the other 3 purchase surfaces (Coin Purchase,
        /// Cosmetic/World Purchase). See ParentalGateController's own doc comment.</summary>
        private void HandleRemoveAdsTapped()
        {
            if (IAPManager.Instance == null)
            {
                Debug.LogWarning("[ShopController] Remove Ads tapped but IAPManager.Instance is null " +
                    "— can't purchase.");
                return;
            }

            // Diagnostic only, not a hard block — the store connection can still resolve after this
            // (Start() connects asynchronously), so a tap right at launch shouldn't be refused
            // outright. But if a real device report ever says "tapping Remove Ads does nothing," this
            // is the first thing to check in the Console: IAPManager.HandleStoreConnected/
            // HandleProductsFetched never firing usually means the platform's IAP agreement (App
            // Store Connect's Paid Applications Agreement / Tax & Banking, or the Play Console
            // equivalent) isn't active yet — the store silently never returns any products, and
            // PurchaseProduct's own string-id fallback then fails against the live store with
            // whatever reason HandleRemoveAdsPurchaseFailed below now actually logs.
            if (!IAPManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[ShopController] Remove Ads tapped before IAPManager finished " +
                    "connecting to the store (IsInitialized is false) — the purchase attempt below " +
                    "may fail silently from the store's side. If this keeps happening, check the " +
                    "platform's IAP agreement/banking setup, not this code.");
            }

            if (ParentalGateController.Instance != null)
            {
                ParentalGateController.Instance.Show(BeginRemoveAdsPurchase);
            }
            else
            {
                BeginRemoveAdsPurchase();
            }
        }

        private void BeginRemoveAdsPurchase()
        {
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
            if (productId != IAPManager.RemoveAdsProductId)
            {
                return;
            }

            // Real gap found and fixed (2026-09-18): this used to do nothing at all on a failure —
            // no log, no UI change — which reads to a player as "I tapped it and nothing happened."
            // Still no status text on this icon row (see CoinPurchaseScreen for purchase feedback
            // text) — the icon just stays tappable again, same as before — but a failure is now at
            // least diagnosable from the Console instead of silent.
            Debug.LogWarning($"[ShopController] Remove Ads purchase failed: {reason}");
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
