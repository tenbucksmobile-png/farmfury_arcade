using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// In-maze cosmetics "Locker" — reached from Gameplay HUD's Locker button. Freezes time the
    /// same way ChooseCharacterScreen does (see that class's own doc comment for the real bug —
    /// the level timer burning for real behind an unpaused overlay — that motivated this pattern)
    /// while the player browses.
    ///
    /// Lists the same 7 hat/trail items CosmeticsHubScreen sells (Baseball Cap resolves to the
    /// active character's own per-character variant — see IAPManager.GrantBaseballCapSet; the
    /// other 6 are character-agnostic):
    /// - Owned: tap equips it immediately (SaveManager.SetEquippedCosmetic/SetEquippedTrail, then
    ///   CharacterCosmeticRenderer.Refresh() on the active character) — tapping the already-equipped
    ///   tile again unequips it. No purchase flow involved, purely "try what you own."
    /// - Not owned: dimmed, shows its real IAP price (IAPManager.GetPriceString); tapping it opens
    ///   the existing CosmeticsHubScreen purchase surface directly rather than duplicating purchase
    ///   logic here.
    /// A "You may like" banner picks one random not-yet-owned item every time the Locker opens and
    /// offers the same shortcut into the purchase screen — pure discovery/upsell.
    /// </summary>
    public class LockerScreen : MonoBehaviour
    {
        private readonly struct CatalogEntry
        {
            public readonly string displayName;
            public readonly CosmeticType type;
            public readonly string productId;
            // Null only for Baseball Cap — its real cosmeticId depends on the active character
            // (baseball_cap_<character>) and is resolved per-tile via ResolveCosmeticId instead.
            public readonly string fixedCosmeticId;

            public CatalogEntry(string displayName, CosmeticType type, string productId, string fixedCosmeticId)
            {
                this.displayName = displayName;
                this.type = type;
                this.productId = productId;
                this.fixedCosmeticId = fixedCosmeticId;
            }
        }

        // Same 7 items CosmeticsHubScreen sells (Phase5ProjectBuilder.BuildCosmeticsHubScreen) —
        // kept in sync by hand, same convention CosmeticWiringBuilder's own local copies of
        // IAPManager's cosmeticId constants already use.
        private static readonly CatalogEntry[] Catalog =
        {
            new CatalogEntry("Baseball Cap", CosmeticType.Hat, IAPManager.HatBaseballCapProductId, null),
            new CatalogEntry("Cowboy Hat", CosmeticType.Hat, IAPManager.HatCowboyHatProductId, IAPManager.CowboyHatCosmeticId),
            new CatalogEntry("Sombrero", CosmeticType.Hat, IAPManager.HatSombreroProductId, IAPManager.SombreroCosmeticId),
            // Trail product ids intentionally match their CosmeticData.cosmeticId exactly (see
            // IAPManager's own doc comment on TrailCornHuskProductId etc.).
            new CatalogEntry("Corn Husk Trail", CosmeticType.Trail, IAPManager.TrailCornHuskProductId, IAPManager.TrailCornHuskProductId),
            new CatalogEntry("Ember Trail", CosmeticType.Trail, IAPManager.TrailEmberProductId, IAPManager.TrailEmberProductId),
            new CatalogEntry("Sparkle Dust Trail", CosmeticType.Trail, IAPManager.TrailSparkleDustProductId, IAPManager.TrailSparkleDustProductId),
            new CatalogEntry("Rainbow Ribbon Trail", CosmeticType.Trail, IAPManager.TrailRainbowRibbonProductId, IAPManager.TrailRainbowRibbonProductId),
        };

        private const float TileIconSize = 90f;

        /// <summary>Fraction inset from each edge of the tile's own square, marking where
        /// PurchaseCardFrame.png's wood border ends and its parchment interior begins — measured by
        /// pixel-sampling the actual 500x500 source (not eyeballed): the flat parchment tone starts
        /// at x=116/y=110 and ends at x=393/y=400, i.e. ~0.22-0.23 in from every edge. 0.23 stays
        /// just inside that measured boundary so tile content can never bleed onto the wood.</summary>
        private const float TileContentInset = 0.23f;

        private static readonly Color LockedTint = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color NameColor = new Color(0.35f, 0.18f, 0.05f);
        private static readonly Color EquippedStatusColor = new Color(0.15f, 0.5f, 0.2f);
        private static readonly Color OwnedStatusColor = new Color(0.35f, 0.28f, 0.18f);
        private static readonly Color PriceStatusColor = new Color(0.6f, 0.15f, 0.1f);

        private const float SuggestionDelaySeconds = 0.6f;
        private const float SuggestionFadeSeconds = 0.35f;
        private const float EquippedBadgeSize = 52f;

        [SerializeField] private Transform tileContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private CosmeticPurchaseScreen purchaseScreen;

        /// <summary>Real wood-frame-with-parchment art (PurchaseCardFrame.png, 500x500, square) —
        /// replaces the earlier flat PlaceholderSprite border+background composition. Tinted
        /// LockedTint for a not-yet-owned item (same "dim it" convention the icon already used) so
        /// a locked tile still reads as locked at a glance, not just via its status text.</summary>
        [SerializeField] private Sprite tileFrameSprite;

        /// <summary>Same green checkmark ribbon CosmeticPurchaseScreen overlays on an owned item —
        /// shown top-left on a tile only while it's actually equipped (not merely owned), since
        /// "Tap to Equip" vs "Equipped" status text already distinguishes owned-but-not-equipped;
        /// the badge is the equip-specific reinforcement.</summary>
        [SerializeField] private Sprite equippedBadgeSprite;

        [SerializeField] private GameObject suggestionRoot;
        [SerializeField] private CanvasGroup suggestionGroup;
        [SerializeField] private Image suggestionIcon;
        [SerializeField] private TextMeshProUGUI suggestionText;
        [SerializeField] private Button suggestionButton;

        private readonly List<GameObject> _tiles = new List<GameObject>();
        private readonly List<CatalogEntry> _notOwnedScratch = new List<CatalogEntry>();
        private Coroutine _suggestionRoutine;

        /// <summary>Same "did this screen freeze time itself, or was the game already paused" flag
        /// ChooseCharacterScreen uses — see that class's own doc comment for the bug this avoids.
        /// The Locker is only ever opened directly from Gameplay HUD (never from Pause), so unlike
        /// ChooseCharacterScreen there's no "hand back to the Pause menu" branch to worry about.</summary>
        private bool _gameWasAlreadyPaused;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            if (suggestionButton != null)
            {
                suggestionButton.onClick.AddListener(OpenPurchaseScreen);
            }
        }

        public void Show()
        {
            _gameWasAlreadyPaused = GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Paused;
            if (!_gameWasAlreadyPaused && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
            {
                GameManager.Instance.PauseGame();
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            if (!_gameWasAlreadyPaused && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.ResumeGame();
            }
        }

        private void Refresh()
        {
            if (tileContainer == null || SaveManager.Instance == null || DataManager.Instance == null)
            {
                return;
            }

            foreach (var tile in _tiles)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }
            _tiles.Clear();

            _notOwnedScratch.Clear();
            foreach (var entry in Catalog)
            {
                string cosmeticId = ResolveCosmeticId(entry);
                bool owned = SaveManager.Instance.IsCosmeticOwned(cosmeticId);
                BuildTile(entry, cosmeticId, owned);
                if (!owned)
                {
                    _notOwnedScratch.Add(entry);
                }
            }

            RefreshSuggestion();
        }

        private string ResolveCosmeticId(CatalogEntry entry)
        {
            if (entry.fixedCosmeticId != null)
            {
                return entry.fixedCosmeticId;
            }
            // Baseball Cap — per-character variant id, matching IAPManager.GrantBaseballCapSet's
            // own $"baseball_cap_{character}".ToLowerInvariant() pattern exactly.
            CharacterType active = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacter : CharacterType.Cluck;
            return $"baseball_cap_{active}".ToLowerInvariant();
        }

        private bool IsEquipped(CatalogEntry entry, string cosmeticId)
        {
            if (entry.type == CosmeticType.Trail)
            {
                return SaveManager.Instance.GetEquippedTrail() == cosmeticId;
            }
            CharacterType active = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacter : CharacterType.Cluck;
            return SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Hat, active) == cosmeticId;
        }

        private void BuildTile(CatalogEntry entry, string cosmeticId, bool owned)
        {
            bool equipped = owned && IsEquipped(entry, cosmeticId);

            var tileGO = new GameObject($"Tile_{cosmeticId}", typeof(RectTransform), typeof(Image), typeof(Button));
            tileGO.transform.SetParent(tileContainer, false);
            var tileImage = tileGO.GetComponent<Image>();
            tileImage.sprite = tileFrameSprite;
            tileImage.preserveAspect = true;
            tileImage.color = owned ? Color.white : LockedTint;
            tileGO.GetComponent<Button>().onClick.AddListener(() => HandleTileTapped(entry, cosmeticId, owned));
            _tiles.Add(tileGO);

            // Content sits inside TileContentInset..1-TileContentInset — the pixel-measured
            // parchment interior of tileFrameSprite — so nothing (icon, name, status) ever
            // overlaps the wood border baked into the art itself.
            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentGO.transform.SetParent(tileGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = new Vector2(TileContentInset, TileContentInset);
            contentRect.anchorMax = new Vector2(1f - TileContentInset, 1f - TileContentInset);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGO.transform.SetParent(contentGO.transform, false);
            var iconImage = iconGO.GetComponent<Image>();
            var cosmeticData = DataManager.Instance.GetCosmeticData(cosmeticId);
            iconImage.sprite = cosmeticData != null && cosmeticData.previewSprite != null
                ? cosmeticData.previewSprite
                : PlaceholderSprite.Get(Color.gray);
            iconImage.preserveAspect = true;
            iconImage.color = owned ? Color.white : LockedTint;
            var iconLayout = iconGO.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = TileIconSize;
            iconLayout.preferredHeight = TileIconSize;

            CreateTileText(contentGO.transform, entry.displayName, 18f, FontStyles.Bold, NameColor, 26f);

            string statusLabel;
            Color statusColor;
            if (equipped)
            {
                statusLabel = "Equipped";
                statusColor = EquippedStatusColor;
            }
            else if (owned)
            {
                statusLabel = "Tap to Equip";
                statusColor = OwnedStatusColor;
            }
            else
            {
                statusLabel = IAPManager.Instance != null ? IAPManager.Instance.GetPriceString(entry.productId) : string.Empty;
                statusColor = PriceStatusColor;
            }
            CreateTileText(contentGO.transform, statusLabel, 16f, FontStyles.Normal, statusColor, 22f);

            if (equipped)
            {
                BuildEquippedBadge(tileGO.transform);
            }
        }

        /// <summary>Top-left checkmark badge, same corner/inset convention
        /// CosmeticPurchaseScreen.BuildOwnedBadge uses — a fixed size here (rather than derived
        /// from the tile's own live RectTransform.rect.width) since this tile is parented under a
        /// GridLayoutGroup, whose own layout pass hasn't necessarily run yet the instant this
        /// GameObject is created, which would make rect.width unreliable at this exact point.</summary>
        private void BuildEquippedBadge(Transform tileTransform)
        {
            var badgeGO = new GameObject("EquippedBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(tileTransform, false);
            var badgeRect = (RectTransform)badgeGO.transform;
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(EquippedBadgeSize, EquippedBadgeSize);
            badgeRect.anchoredPosition = new Vector2(EquippedBadgeSize * 0.35f, -EquippedBadgeSize * 0.35f);
            var badgeImage = badgeGO.GetComponent<Image>();
            badgeImage.sprite = equippedBadgeSprite;
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateTileText(Transform parent, string text, float fontSize, FontStyles style, Color color, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return tmp;
        }

        private void HandleTileTapped(CatalogEntry entry, string cosmeticId, bool owned)
        {
            if (!owned)
            {
                OpenPurchaseScreen();
                return;
            }

            bool equipped = IsEquipped(entry, cosmeticId);
            string newValue = equipped ? string.Empty : cosmeticId;

            if (entry.type == CosmeticType.Trail)
            {
                SaveManager.Instance.SetEquippedTrail(newValue);
            }
            else
            {
                CharacterType active = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacter : CharacterType.Cluck;
                SaveManager.Instance.SetEquippedCosmetic(CosmeticType.Hat, active, newValue);
            }

            CharacterManager.Instance?.ActiveCharacterObject?.GetComponent<CharacterCosmeticRenderer>()?.Refresh();
            Refresh(); // rebuilds every tile's border/status so the new equip state reads immediately
        }

        private void OpenPurchaseScreen()
        {
            purchaseScreen?.Show();
        }

        private void RefreshSuggestion()
        {
            if (suggestionRoot == null || suggestionGroup == null)
            {
                return;
            }

            if (_suggestionRoutine != null)
            {
                StopCoroutine(_suggestionRoutine);
                _suggestionRoutine = null;
            }

            if (_notOwnedScratch.Count == 0)
            {
                suggestionGroup.alpha = 0f;
                suggestionRoot.SetActive(false);
                return;
            }

            var pick = _notOwnedScratch[Random.Range(0, _notOwnedScratch.Count)];
            if (suggestionText != null)
            {
                suggestionText.text = $"You may like: {pick.displayName}!";
            }
            if (suggestionIcon != null && DataManager.Instance != null)
            {
                var data = DataManager.Instance.GetCosmeticData(ResolveCosmeticId(pick));
                suggestionIcon.sprite = data != null && data.previewSprite != null
                    ? data.previewSprite
                    : PlaceholderSprite.Get(Color.white);
            }

            suggestionRoot.SetActive(true);
            suggestionGroup.alpha = 0f;
            _suggestionRoutine = StartCoroutine(FadeInSuggestion());
        }

        private IEnumerator FadeInSuggestion()
        {
            yield return new WaitForSecondsRealtime(SuggestionDelaySeconds);
            float t = 0f;
            while (t < SuggestionFadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                suggestionGroup.alpha = Mathf.Lerp(0f, 1f, t / SuggestionFadeSeconds);
                yield return null;
            }
            suggestionGroup.alpha = 1f;
            _suggestionRoutine = null;
        }
    }
}
