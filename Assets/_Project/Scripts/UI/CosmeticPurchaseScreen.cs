using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Generic cosmetic-style purchase surface. As of the 2026-09-12 mockup, one instance backs
    /// the Hats & Caps page (Phase5ProjectBuilder.BuildCosmeticsHatsScreen, 5 items) and another
    /// backs the Trails page (BuildCosmeticsTrailsScreen, 6 items) — reached via
    /// CosmeticsChooserScreen's own two banners, replacing an earlier single flat screen that hosted
    /// all 11 items at once. Each item's own plaque art bakes in both its icon AND its $1.99 price —
    /// no separate breadcrumb icon or shared price plaque needed (an even earlier 2026-08-20 design
    /// split Hats/Trails the same way, but with a shared $3.99 price sign; that art is gone now).
    /// The same component also backs the World Purchase screen (Phase5ProjectBuilder.
    /// BuildWorldPurchaseScreen, 3 items at $3.99 each with its own real price plaque, since that
    /// art wasn't baked with a price).
    ///
    /// Every item is a real-money IAP purchase via IAPManager — tapping an item purchases it
    /// directly (no separate confirm step), same "tap to buy" convention the old coin-priced
    /// CosmeticStoreScreen used. IAPManager grants ownership + auto-equips on a confirmed purchase;
    /// this screen only needs to kick off the purchase and show processing/success/failure
    /// feedback.
    ///
    /// Overlay convention, same as ShopController — shown/hidden directly via Show()/SetActive, not
    /// through SceneTransitionManager.
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

        /// <summary>Green "owned" checkmark ribbon, overlaid on the top-left corner of an item's
        /// button the moment SaveManager/IAPManager's own records say it's already owned — same
        /// "art wired, falls back to invisible until it lands" convention this project uses
        /// elsewhere (see ArtWiringBuilder.Load's own doc comment). Built as a child of each
        /// itemButtons[i].button at Awake() rather than baked in by Phase5ProjectBuilder, so this
        /// one component covers every screen that reuses it (Cosmetics hub, World Purchase) with no
        /// per-call-site duplication.</summary>
        [SerializeField] private Sprite ownedBadgeSprite;

        private struct ItemState
        {
            public string productId;
            public Button button;
            public Image icon;
            public Image badge;
        }

        private readonly List<ItemState> _itemStates = new List<ItemState>();

        /// <summary>Dims an already-owned item's own icon so it visually matches its
        /// non-interactable state (the green ownedBadgeSprite checkmark alone wasn't a strong
        /// enough "don't bother tapping this" cue — see HandleItemTapped's own real-bug-fix doc
        /// comment for the tap-side half of this). A 50% ALPHA fade (not a grey multiply tint —
        /// the original 0.55 grey read as too heavily faded to still recognize the art underneath,
        /// per direct feedback) keeps the actual artwork visible while still reading as disabled.</summary>
        private static readonly Color OwnedIconTint = new Color(1f, 1f, 1f, 0.5f);

        /// <summary>"Purchase Complete!" banner (2026-09-13) — real commissioned art
        /// (PurchaseComplete.png) replacing the old plain "Purchase complete!" statusText message
        /// for a successful purchase (real-money or coins) specifically. statusText itself is
        /// unchanged and still used for every other message (Processing/Not enough coins/Purchase
        /// failed) — only the success case moved to this banner, per direct instruction. Shown at
        /// full opacity, held for PurchaseCompleteHoldSeconds, then fades out over
        /// PurchaseCompleteFadeSeconds — both real-time (WaitForSecondsRealtime/
        /// Time.unscaledDeltaTime) so it behaves the same whether or not gameplay happens to be
        /// paused/frozen elsewhere.</summary>
        [SerializeField] private CanvasGroup purchaseCompleteBanner;
        private const float PurchaseCompleteHoldSeconds = 4f;
        private const float PurchaseCompleteFadeSeconds = 0.5f;
        private Coroutine _purchaseCompleteRoutine;

        /// <summary>"Use Coins?" confirmation modal (2026-09-13) — only for products
        /// IAPManager.TryGetCoinCost recognizes (the 11 cosmetics; World Purchase's 3 products
        /// deliberately have no coin-purchase alternative, so tapping one always goes straight to
        /// the real-money flow below — see IAPManager.CosmeticCoinCosts' own doc comment for why).
        /// Null-safe: a screen this hasn't been wired onto (shouldn't happen, but defensively) just
        /// always falls through to the real-money flow.</summary>
        [SerializeField] private UseCoinsPromptController useCoinsPrompt;

        /// <summary>Items shown on this screen with no real IAP product behind them yet (e.g. a
        /// purchasable world whose 25 levels haven't been built/verified out yet) — tapping shows
        /// statusText's "Coming Soon!" feedback instead of a purchase attempt, rather than either
        /// silently doing nothing (reads as broken) or letting the player buy something with no
        /// content behind it.</summary>
        [SerializeField] private Button[] comingSoonButtons;

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }

            if (comingSoonButtons != null)
            {
                foreach (var button in comingSoonButtons)
                {
                    if (button == null)
                    {
                        continue;
                    }
                    button.onClick.AddListener(() =>
                    {
                        if (statusText != null)
                        {
                            statusText.text = "Coming Soon!";
                        }
                    });
                }
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
                entry.button.onClick.AddListener(() => HandleItemTapped(productId));
                _itemStates.Add(new ItemState
                {
                    productId = productId,
                    button = entry.button,
                    icon = entry.button.GetComponent<Image>(),
                    badge = BuildOwnedBadge(entry.button.transform),
                });
            }
        }

        /// <summary>Small badge Image, top-left corner of the item button, anchored (0,1)/(0,1) so
        /// it sits in the button's own top-left corner regardless of that button's size (World
        /// Purchase's 350px shields vs. the Cosmetics hub's 170px icons). Inset a small fraction of
        /// the button's own width rather than a fixed pixel offset, so it reads at a consistent
        /// relative size/position across both. Starts hidden — RefreshOwnedBadges shows it once
        /// SaveManager/IAPManager's own records say this item is owned.</summary>
        private Image BuildOwnedBadge(Transform buttonTransform)
        {
            var badgeGO = new GameObject("OwnedBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(buttonTransform, false);
            var badgeRect = (RectTransform)badgeGO.transform;
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            float buttonWidth = ((RectTransform)buttonTransform).rect.width;
            float badgeSize = buttonWidth > 0f ? buttonWidth * 0.32f : 40f;
            badgeRect.sizeDelta = new Vector2(badgeSize, badgeSize);
            badgeRect.anchoredPosition = new Vector2(badgeSize * -0.15f, badgeSize * 0.15f);
            var badgeImage = badgeGO.GetComponent<Image>();
            badgeImage.sprite = ownedBadgeSprite;
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = false; // decorative only — never steals the button's own tap
            badgeGO.SetActive(false);
            return badgeImage;
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

            // Reset any in-progress banner from a previous visit — OnDisable already stops the
            // coroutine automatically (Unity behaviour), but the banner's own GameObject/alpha
            // could otherwise be left visible if the screen was closed mid-fade.
            _purchaseCompleteRoutine = null;
            if (purchaseCompleteBanner != null)
            {
                purchaseCompleteBanner.alpha = 0f;
                purchaseCompleteBanner.gameObject.SetActive(false);
            }

            RefreshOwnedBadges();
        }

        /// <summary>Re-checked every time this screen opens (OnEnable) and right after a purchase
        /// confirms (HandlePurchaseSucceeded), so a badge appears immediately without needing the
        /// player to close and reopen the screen. Covers every product family this generic screen
        /// is ever used for — the 7 Cosmetics-hub items (hats resolved by their real cosmeticId,
        /// Baseball Cap by the active character's own per-character variant, since
        /// IAPManager.GrantBaseballCapSet marks every character's variant at once — see that
        /// method's own doc comment) and the 3 World Purchase items.</summary>
        private void RefreshOwnedBadges()
        {
            if (SaveManager.Instance == null)
            {
                return;
            }

            foreach (var state in _itemStates)
            {
                bool owned = IsProductOwned(state.productId);
                if (state.badge != null)
                {
                    state.badge.gameObject.SetActive(owned);
                }
                // Real bug fix (2026-09-13): the button used to stay fully interactable/tappable
                // after the item was already owned — the green badge was the only visual cue, and
                // tapping it still ran the full purchase flow (coin popup or straight to real-money
                // IAP), which for a NonConsumable is a harmless store-side no-op, but for the coin
                // path would happily re-charge coins for something already owned. Disabling the
                // button blocks both paths at the single shared tap entry point (HandleItemTapped
                // never even runs once the Button itself refuses the tap), and the icon is dimmed
                // to visually match that disabled state, not just rely on the badge alone.
                if (state.button != null)
                {
                    state.button.interactable = !owned;
                }
                if (state.icon != null)
                {
                    state.icon.color = owned ? OwnedIconTint : Color.white;
                }
            }
        }

        private static bool IsProductOwned(string productId)
        {
            switch (productId)
            {
                case IAPManager.HatBaseballCapProductId:
                    CharacterType activeCap = CharacterManager.Instance != null
                        ? CharacterManager.Instance.ActiveCharacter
                        : CharacterType.Cluck;
                    return SaveManager.Instance.IsCosmeticOwned($"baseball_cap_{activeCap}".ToLowerInvariant());
                case IAPManager.HatCowboyHatProductId:
                    // Per-character asset set (2026-09-11), same shape as Baseball Cap — see
                    // IAPManager.GrantCowboyHatSet.
                    CharacterType activeCowboy = CharacterManager.Instance != null
                        ? CharacterManager.Instance.ActiveCharacter
                        : CharacterType.Cluck;
                    return SaveManager.Instance.IsCosmeticOwned($"cowboy_hat_{activeCowboy}".ToLowerInvariant());
                case IAPManager.HatSombreroProductId:
                    return SaveManager.Instance.IsCosmeticOwned(IAPManager.SombreroCosmeticId);
                case IAPManager.HatChefHatProductId:
                    return SaveManager.Instance.IsCosmeticOwned(IAPManager.ChefHatCosmeticId);
                case IAPManager.HatCrownProductId:
                    return SaveManager.Instance.IsCosmeticOwned(IAPManager.CrownCosmeticId);
                case IAPManager.TrailCornHuskProductId:
                case IAPManager.TrailEmberProductId:
                case IAPManager.TrailSparkleDustProductId:
                case IAPManager.TrailRainbowRibbonProductId:
                case IAPManager.TrailConfettiProductId:
                case IAPManager.TrailBubblesProductId:
                    // Trail product ids intentionally match their CosmeticData.cosmeticId exactly.
                    return SaveManager.Instance.IsCosmeticOwned(productId);
                case IAPManager.WorldFrostbiteGardenProductId:
                    return SaveManager.Instance.IsWorldPurchased(MazeType.FrostbiteGarden);
                case IAPManager.WorldGoldenSunsetProductId:
                    return SaveManager.Instance.IsWorldPurchased(MazeType.GoldenSunset);
                case IAPManager.WorldHarvestMoonProductId:
                    return SaveManager.Instance.IsWorldPurchased(MazeType.HarvestMoon);
                default:
                    return false;
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
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        /// <summary>Single entry point for every item tap (2026-09-13 "Use Coins?" redesign,
        /// replacing the earlier always-visible corner coin badge). Only if the item has a coin
        /// price AND the player can actually afford it does this branch into the confirmation
        /// popup — an unaffordable or coin-less item goes straight to the normal real-money flow,
        /// same "never show a dead-end control" rule this project uses throughout (e.g.
        /// RevivePromptController disables Revive rather than letting the tap fail after the
        /// fact) — here that means never offering a choice the player can't actually take.</summary>
        private void HandleItemTapped(string productId)
        {
            // Defense in depth alongside RefreshOwnedBadges' own button.interactable=false — an
            // owned item should never be able to re-enter either purchase path, regardless of how
            // the tap reached here.
            if (IsProductOwned(productId))
            {
                return;
            }

            bool canAffordCoins = IAPManager.TryGetCoinCost(productId, out int coinCost) &&
                SaveManager.Instance != null && SaveManager.Instance.CoinBalance >= coinCost;

            if (canAffordCoins && useCoinsPrompt != null)
            {
                useCoinsPrompt.Show(
                    onYes: () => BeginCoinPurchase(productId),
                    onNo: () => HandlePurchaseTapped(productId));
            }
            else
            {
                HandlePurchaseTapped(productId);
            }
        }

        /// <summary>Audit findings F3.5/F4.4: this screen backs Hat/Trail purchase AND World
        /// Purchase (Phase5ProjectBuilder.BuildWorldPurchaseScreen reuses this same component), so
        /// gating it here closes the gap for all three at once. See ParentalGateController's own
        /// doc comment. Coins spends (BeginCoinPurchase below) deliberately do NOT go through this
        /// gate — same convention Revive/Skip-Cooldown's own coin spends already use elsewhere in
        /// this project (the gate exists specifically for real-money surfaces), and here the "Use
        /// Coins?" popup's own explicit Yes tap already serves as the confirmation step.</summary>
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

            if (ParentalGateController.Instance != null)
            {
                ParentalGateController.Instance.Show(() => BeginPurchase(productId));
            }
            else
            {
                BeginPurchase(productId);
            }
        }

        private void BeginPurchase(string productId)
        {
            if (statusText != null)
            {
                statusText.text = "Processing...";
            }
            IAPManager.Instance.PurchaseProduct(productId);
        }

        /// <summary>Coin-purchase path — reached only from the "Use Coins?" popup's own Yes button
        /// (HandleItemTapped only offers that popup when the balance is already confirmed
        /// sufficient), so no separate affordability check is needed here. PurchaseProductWithCoins
        /// still refuses an unaffordable spend defensively (e.g. the balance changed elsewhere
        /// between the tap and this call), so a failure just means "insufficient coins," never a
        /// lost purchase.</summary>
        private void BeginCoinPurchase(string productId)
        {
            if (IAPManager.Instance == null)
            {
                return;
            }

            bool success = IAPManager.Instance.PurchaseProductWithCoins(productId);
            if (success)
            {
                ShowPurchaseCompleteBanner();
                RefreshOwnedBadges();
            }
            else if (statusText != null)
            {
                statusText.text = "Not enough coins.";
            }
        }

        private void HandlePurchaseSucceeded(string productId)
        {
            ShowPurchaseCompleteBanner();
            RefreshOwnedBadges();
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase failed.";
            }
        }

        private void ShowPurchaseCompleteBanner()
        {
            if (purchaseCompleteBanner == null)
            {
                return;
            }
            if (_purchaseCompleteRoutine != null)
            {
                StopCoroutine(_purchaseCompleteRoutine);
            }
            _purchaseCompleteRoutine = StartCoroutine(PurchaseCompleteRoutine());
        }

        private IEnumerator PurchaseCompleteRoutine()
        {
            purchaseCompleteBanner.gameObject.SetActive(true);
            purchaseCompleteBanner.alpha = 1f;

            yield return new WaitForSecondsRealtime(PurchaseCompleteHoldSeconds);

            float elapsed = 0f;
            while (elapsed < PurchaseCompleteFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                purchaseCompleteBanner.alpha = 1f - Mathf.Clamp01(elapsed / PurchaseCompleteFadeSeconds);
                yield return null;
            }

            purchaseCompleteBanner.alpha = 0f;
            purchaseCompleteBanner.gameObject.SetActive(false);
            _purchaseCompleteRoutine = null;
        }
    }
}
