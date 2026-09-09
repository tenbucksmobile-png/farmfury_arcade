using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Generic cosmetic-style purchase surface. As of the 2026-08-30 mockup, one instance
    /// (Phase5ProjectBuilder.BuildCosmeticsHubScreen) hosts all 7 hat/trail items at once, each
    /// item's own plaque art baking in both its icon AND its $1.99 price — no separate breadcrumb
    /// icon or shared price plaque needed (the earlier 2026-08-20 design split Hats/Trails across
    /// two screens, each with a shared $3.99 price sign; both are gone now). The same component
    /// also backs the World Purchase screen (Phase5ProjectBuilder.BuildWorldPurchaseScreen, 3 items
    /// at $3.99 each with its own real price plaque, since that art wasn't baked with a price).
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
        private readonly List<(string productId, Image badge)> _badges = new List<(string, Image)>();

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
                entry.button.onClick.AddListener(() => HandlePurchaseTapped(productId));
                _badges.Add((productId, BuildOwnedBadge(entry.button.transform)));
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

            foreach (var (productId, badge) in _badges)
            {
                if (badge == null)
                {
                    continue;
                }
                badge.gameObject.SetActive(IsProductOwned(productId));
            }
        }

        private static bool IsProductOwned(string productId)
        {
            switch (productId)
            {
                case IAPManager.HatBaseballCapProductId:
                    CharacterType active = CharacterManager.Instance != null
                        ? CharacterManager.Instance.ActiveCharacter
                        : CharacterType.Cluck;
                    return SaveManager.Instance.IsCosmeticOwned($"baseball_cap_{active}".ToLowerInvariant());
                case IAPManager.HatCowboyHatProductId:
                    return SaveManager.Instance.IsCosmeticOwned(IAPManager.CowboyHatCosmeticId);
                case IAPManager.HatSombreroProductId:
                    return SaveManager.Instance.IsCosmeticOwned(IAPManager.SombreroCosmeticId);
                case IAPManager.TrailCornHuskProductId:
                case IAPManager.TrailEmberProductId:
                case IAPManager.TrailSparkleDustProductId:
                case IAPManager.TrailRainbowRibbonProductId:
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

        /// <summary>Audit findings F3.5/F4.4: this screen backs Hat/Trail purchase AND World
        /// Purchase (Phase5ProjectBuilder.BuildWorldPurchaseScreen reuses this same component), so
        /// gating it here closes the gap for all three at once. See ParentalGateController's own
        /// doc comment.</summary>
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

        private void HandlePurchaseSucceeded(string productId)
        {
            if (statusText != null)
            {
                statusText.text = "Purchase complete!";
            }
            RefreshOwnedBadges();
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
