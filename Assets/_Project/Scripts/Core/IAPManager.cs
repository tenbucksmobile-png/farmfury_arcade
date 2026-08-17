using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Wraps Unity IAP (com.unity.purchasing 5.x's async UnityIAPServices/StoreController API —
    /// not the older IStoreListener/ConfigurationBuilder pattern). Parallel to AdManager: one
    /// singleton on GameManagers, owns all SDK interaction so gameplay/UI code never touches
    /// UnityEngine.Purchasing directly.
    ///
    /// Product catalogue (Monetisation Build Plan Phase 3, values from the GDD's Section 11):
    /// 1 non-consumable (RemoveAds, $4.99, also grants RemoveAdsBonusCoins) + 5 consumable coin
    /// packs. Server-side receipt validation is deliberately skipped (Unity IAP's own client-side
    /// validation is enough for a solo project per the plan doc; revisit only if coin fraud
    /// becomes a real problem post-launch).
    ///
    /// Coin-pack/RemoveAds purchases do NOT affect rewarded-ad availability — the GDD is explicit
    /// that Remove Ads "still shows rewarded ad prompts as an option," so only AdManager's
    /// interstitial path checks SaveManager.AdsRemoved. Rewarded placements are untouched by this.
    /// </summary>
    public class IAPManager : Singleton<IAPManager>
    {
        public const string RemoveAdsProductId = "remove_ads";
        public const string Coins100ProductId = "coins_100";
        public const string Coins500ProductId = "coins_500";
        public const string Coins1500ProductId = "coins_1500";
        public const string Coins5000ProductId = "coins_5000";
        public const string Coins15000ProductId = "coins_15000";

        /// <summary>GDD Section 11: "Includes 100 bonus coins."</summary>
        public const int RemoveAdsBonusCoins = 100;

        private static readonly Dictionary<string, int> CoinPackAmounts = new Dictionary<string, int>
        {
            { Coins100ProductId, 100 },
            { Coins500ProductId, 500 },
            { Coins1500ProductId, 1500 },
            { Coins5000ProductId, 5000 },
            { Coins15000ProductId, 15000 },
        };

        /// <summary>Fallback price strings shown before the store connection resolves real
        /// localized prices (or if it never does, e.g. no store configured yet during
        /// development) — GDD Section 11's own price table, not live pricing.</summary>
        private static readonly Dictionary<string, string> FallbackPrices = new Dictionary<string, string>
        {
            { RemoveAdsProductId, "$4.99" },
            { Coins100ProductId, "$0.99" },
            { Coins500ProductId, "$3.99" },
            { Coins1500ProductId, "$9.99" },
            { Coins5000ProductId, "$19.99" },
            { Coins15000ProductId, "$49.99" },
        };

        public bool IsInitialized { get; private set; }

        /// <summary>Fires once per successful purchase confirmation with the product id — UI (e.g.
        /// ShopController) uses this to refresh owned/afford state rather than polling.</summary>
        public event Action<string> OnPurchaseSucceeded;
        public event Action<string, string> OnPurchaseFailed;
        /// <summary>Fires once product metadata (real localized prices) is available.</summary>
        public event Action OnProductsReady;

        private StoreController _storeController;
        private readonly Dictionary<string, Product> _products = new Dictionary<string, Product>();

        private async void Start()
        {
            _storeController = UnityIAPServices.StoreController();

            _storeController.OnStoreConnected += HandleStoreConnected;
            _storeController.OnStoreDisconnected += HandleStoreDisconnected;
            _storeController.OnProductsFetched += HandleProductsFetched;
            _storeController.OnProductsFetchFailed += HandleProductsFetchFailed;
            _storeController.OnPurchasePending += HandlePurchasePending;
            _storeController.OnPurchaseFailed += HandlePurchaseFailed;

            try
            {
                await _storeController.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAPManager] Store connect failed (expected until real store " +
                                  $"products are configured in App Store Connect/Play Console): {e.Message}");
            }
        }

        private void HandleStoreConnected()
        {
            var definitions = new List<ProductDefinition>
            {
                new ProductDefinition(RemoveAdsProductId, ProductType.NonConsumable),
                new ProductDefinition(Coins100ProductId, ProductType.Consumable),
                new ProductDefinition(Coins500ProductId, ProductType.Consumable),
                new ProductDefinition(Coins1500ProductId, ProductType.Consumable),
                new ProductDefinition(Coins5000ProductId, ProductType.Consumable),
                new ProductDefinition(Coins15000ProductId, ProductType.Consumable),
            };
            _storeController.FetchProducts(definitions);
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription description)
        {
            Debug.LogWarning($"[IAPManager] Store disconnected: {description.message}");
        }

        private void HandleProductsFetched(List<Product> products)
        {
            foreach (var product in products)
            {
                _products[product.definition.id] = product;
            }
            IsInitialized = true;
            OnProductsReady?.Invoke();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning($"[IAPManager] Product fetch failed for {failure.FailedFetchProducts.Count} products: {failure.FailureReason}");
        }

        /// <summary>Localized price string for a product if the store connection has resolved it
        /// yet, otherwise the GDD's static fallback price — UI should never show a blank price
        /// while waiting on the store.</summary>
        public string GetPriceString(string productId)
        {
            if (_products.TryGetValue(productId, out var product) && product.metadata != null &&
                !string.IsNullOrEmpty(product.metadata.localizedPriceString))
            {
                return product.metadata.localizedPriceString;
            }
            return FallbackPrices.TryGetValue(productId, out var fallback) ? fallback : string.Empty;
        }

        public void PurchaseProduct(string productId)
        {
            if (_storeController == null)
            {
                Debug.LogWarning("[IAPManager] Store not connected — can't purchase yet.");
                return;
            }
            if (_products.TryGetValue(productId, out var product))
            {
                _storeController.PurchaseProduct(product);
            }
            else
            {
                _storeController.PurchaseProduct(productId);
            }
        }

        /// <summary>Required by Apple for non-consumable products (Remove Ads); good practice on
        /// Android too. callback reports whether the restore call itself succeeded — individual
        /// restored purchases still flow through the normal OnPurchasePending/HandlePurchasePending
        /// path (same as a fresh purchase), so RemoveAds/coin-pack effects apply identically either
        /// way.</summary>
        public void RestorePurchases(Action<bool> callback)
        {
            if (_storeController == null)
            {
                callback?.Invoke(false);
                return;
            }
            _storeController.RestoreTransactions((success, error) =>
            {
                if (!success && !string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[IAPManager] Restore failed: {error}");
                }
                callback?.Invoke(success);
            });
        }

        /// <summary>Grants the purchased product's effect and confirms the order — mirrors the
        /// package's own BuyingConsumables sample (grant-then-ConfirmPurchase), extended to also
        /// cover the non-consumable Remove Ads product. SaveManager.AddCoins/AdsRemoved only touch
        /// in-memory state plus (for AdsRemoved) an immediate PlayerPrefs write; SaveProgress()
        /// flushes the coin balance the same way every other coin-earning call site in this project
        /// already does.</summary>
        private void HandlePurchasePending(PendingOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (product == null)
            {
                Debug.LogWarning("[IAPManager] Could not find product in pending order.");
                return;
            }

            string productId = product.definition.id;

            if (productId == RemoveAdsProductId)
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.AdsRemoved = true;
                    SaveManager.Instance.AddCoins(RemoveAdsBonusCoins);
                    SaveManager.Instance.SaveProgress();
                }
            }
            else if (CoinPackAmounts.TryGetValue(productId, out int amount))
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.AddCoins(amount);
                    SaveManager.Instance.SaveProgress();
                }
            }
            else
            {
                Debug.LogWarning($"[IAPManager] Unrecognized product id in pending order: {productId}");
            }

            _storeController.ConfirmPurchase(order);
            OnPurchaseSucceeded?.Invoke(productId);
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            string productId = product?.definition.id ?? "unknown";
            Debug.LogWarning($"[IAPManager] Purchase failed - Product: '{productId}', Reason: {order.FailureReason}, Details: {order.Details}");
            OnPurchaseFailed?.Invoke(productId, order.FailureReason.ToString());
        }
    }
}
