using FarmFuryArcade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Main Menu's Settings button (Btn_settings, 2026-08-27 redesign) no longer opens
    /// SettingsPanel directly — it opens this hub first, matching a new mockup: the dimmed
    /// Landing_Opacity.png background (same convention Settings/Shop/Cosmetics hub already use,
    /// via ApplyDimmedLandingBackground) with two stacked wood-sign buttons, "SETTINGS"
    /// (SettingsSign.png) and "Shop" (ShopBanner.png) — the exact same sign art each of those
    /// screens already uses as its own header, reused here as tap targets instead.
    ///
    /// Overlay convention, same as SettingsPanel/ShopController — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. Layers on top of Main Menu; its own
    /// back button just closes it, revealing Main Menu underneath.
    ///
    /// **Real bug found and fixed here (2026-08-27):** this hub never deactivates itself when it
    /// opens Settings/Shop, and since it was added to the scene AFTER SettingsPanel/ShopController
    /// in Phase5ProjectBuilder.BuildAll, it sat at a HIGHER sibling index under Canvas than either
    /// — meaning it drew (and raycast-intercepted) on top of them even once they were active,
    /// which read as "tapping the sign does nothing." Fixed generally, not by reordering the
    /// build: every overlay's own Show() now calls transform.SetAsLastSibling() before activating
    /// itself (SettingsPanel, ShopController, CoinPurchaseScreen, CosmeticsHubScreen,
    /// CosmeticPurchaseScreen, and this screen too), so each always draws above whatever's
    /// currently showing regardless of build/sibling order — check this pattern first if a future
    /// overlay's tap silently "does nothing" while the underlying data/wiring checks out.
    ///
    /// **Cross-promo cards (2026-11-18, Cannonfire's / Stampede's design):** either side of the signs, a card for
    /// each sister game (Farm Fury Stampede left, Farm Fury Cannonfire right) - its app icon on a wood rim, gently
    /// bobbing, a PLAY FREE plaque under it; a tap goes through the parental gate to that game's store page
    /// (<see cref="CrossPromo"/>). Built here at runtime from Resources/CrossPromo (card art + Btn_plaque copy) and
    /// TMP's Bangers SDF, so the scene / Phase5ProjectBuilder need no re-run. Cannonfire's card stays off Android
    /// until it is on Google Play.</summary>
    public class MenuHubScreen : MonoBehaviour
    {
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settingsScreen;

        [SerializeField] private Button shopButton;
        [SerializeField] private ShopController shopScreen;

        [SerializeField] private Button closeButton;

        // Cross-promo layout on the 1920x1080 reference canvas (same numbers as Cannonfire / Stampede).
        private const float PromoCentreX = 735f;
        private const float PromoCentreY = 30f;
        private const float PromoIcon = 290f;
        private static readonly Vector2 PromoPlaque = new(270f, 78f);
        // Btn_plaque.png's 9-slice border at its 485x256 source size (left, bottom, right, top).
        private static readonly Vector4 PlaqueBorder = new(90f, 70f, 90f, 70f);

        private void Awake()
        {
            PromoCard(CrossPromo.Game.Stampede, -1f);
            PromoCard(CrossPromo.Game.Cannonfire, 1f);

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (settingsButton != null && settingsScreen != null)
            {
                settingsButton.onClick.AddListener(() => settingsScreen.Show(gameObject));
            }
            if (shopButton != null && shopScreen != null)
            {
                shopButton.onClick.AddListener(() => shopScreen.Show());
            }
        }

        private void OnEnable()
        {
            foreach (CrossPromo.Game g in System.Enum.GetValues(typeof(CrossPromo.Game)))
            {
                if (CrossPromo.Available(g) && AnalyticsManager.Instance != null)
                {
                    AnalyticsManager.Instance.LogPromoShown(CrossPromo.PromoId(g));
                }
            }
        }

        private void PromoCard(CrossPromo.Game game, float side)
        {
            if (!CrossPromo.Available(game)) return;
            void Open()
            {
                if (ParentalGateController.Instance != null) ParentalGateController.Instance.Show(() => CrossPromo.Open(game));
                else CrossPromo.Open(game);
            }

            var card = new GameObject($"CrossPromo_{game}", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(transform, false);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(side * PromoCentreX, PromoCentreY),
                new Vector2(PromoIcon, PromoIcon + PromoPlaque.y));

            var icon = NewImage(card, "Icon", CrossPromo.Icon(game));
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PromoIcon, PromoIcon));
            icon.gameObject.AddComponent<Button>().onClick.AddListener(Open);
            // A slow bob, out of step between the two cards, so they catch the eye without flashing.
            icon.gameObject.AddComponent<PromoBob>().period = side < 0f ? 2.2f : 2.6f;

            var plaque = NewImage(card, "PlayFree", LoadSprite("CrossPromo/Plaque", PlaqueBorder));
            if (plaque.sprite != null)
            {
                plaque.type = Image.Type.Sliced;
                // The art's border is ~90px at source size; draw it at the button's scale, not full size.
                plaque.pixelsPerUnitMultiplier = plaque.sprite.rect.height / 110f;
            }
            else
            {
                plaque.color = new Color(0.62f, 0.38f, 0.17f, 1f);
            }
            Place(plaque.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -6f), PromoPlaque);
            plaque.gameObject.AddComponent<Button>().onClick.AddListener(Open);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(plaque.transform, false);
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
            if (font != null) label.font = font;
            label.text = "PLAY FREE";
            label.fontSize = 40f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(60, 32, 12, 255);
            label.raycastTarget = false;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            return image;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static Sprite LoadSprite(string path, Vector4 border)
        {
            var tex = Resources.Load<Texture2D>(path);
            return tex == null ? null : Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }
    }
}
