using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>Character Story overlay — 5 tabs (Story / How to Play / Combos / Characters /
    /// Cosmetics), each its own independent ScrollRect, switched via SelectTab. Rebuilt 2026-09-09
    /// (per direct feedback) from one long continuous scrollable list holding everything at once,
    /// which read as "a very long scrolling list" once the How to Play section (GameplayTopics —
    /// coins, scoring/stars, power crops/chains, abilities/combos) was added on top of the
    /// narrative intro and all 8 character rows. Cosmetics tab added 2026-09-11 (per direct
    /// feedback), same day How to Play's rows were made more illustrated/kid-friendly. Combos tab
    /// added 2026-09-11 (per a follow-up request to "break down the combos"): How to Play's old
    /// single "Abilities & Combos" row only gestured at the system's existence — this tab actually
    /// lists all 8 of ComboSystem's real combos, each with its own real banner art (the same
    /// Combo_*.png files ComboHypeScreen shows full-screen in-maze) and a Trigger/Effect blurb, so a
    /// player can learn exactly what to do to earn each one.
    ///
    /// Story tab: the game's narrative intro (IntroStory). How to Play tab: one bordered card per
    /// GameplayTopics entry (BuildInfoRow), icon+short-text like the other tabs' rows rather than a
    /// text-only wall — this screen is aimed at kids, so a glance at the icon should carry half the
    /// meaning before they even read the (now much shorter) copy; no longer covers combos in detail
    /// at all — see the Combos tab. Combos tab: one bordered card per ComboSystem combo
    /// (BuildComboRow), same icon+text silhouette, real Combo_*.png banner art per combo, and a
    /// "Trigger:"/"Effect:" two-line blurb spelling out how to get each one and what it does —
    /// same data ComboSystem.cs and CLAUDE.md's own combo table describe, written in player-facing
    /// language. Characters tab: one row per DataManager.GetAllCharacterData() entry (their
    /// CharacterSelectCard next to a short story/ability blurb, BuildRow), pulled from the GDD's
    /// narrative section and the characters' actual current abilities (several diverged from the
    /// GDD's original spec since v1.0 — e.g. Percy's wall-phase became a robot-charging roll, and
    /// Billy's wall-destroy became a robot-charging headbutt — so the blurbs describe what the
    /// ability does today, not the original GDD text). Same Cluck-first hierarchy order
    /// ChooseCharacterScreen uses; every card shows unlocked/non-active/non-interactive — this is a
    /// browsing list, not the swap gate ChooseCharacterScreen enforces, and tapping a card does
    /// nothing since there's no per-character sub-screen (the story IS the blurb next to it).
    /// Cosmetics tab: one row per purchasable hat/trail (BuildCosmeticRow), reusing the exact same
    /// price-baked icon art (sombrero_price.png etc.) CosmeticsHubScreen's own purchase buttons
    /// show, so a browsing kid recognises the same picture when they later go looking for it in the
    /// Shop — with a short, playful blurb per item instead of the Shop's bare price tag. Purely
    /// informational, same as the Characters tab: tapping a row does nothing, there's no purchase
    /// flow here.</summary>
    public class CharacterStoryScreen : MonoBehaviour
    {
        [System.Serializable]
        private struct CosmeticEntry
        {
            public string displayName;
            public Sprite icon;
        }

        [SerializeField] private Transform charactersContainer;
        [SerializeField] private Transform howToPlayContainer;
        [SerializeField] private Transform combosContainer;
        [SerializeField] private Transform cosmeticsContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI introText;
        [SerializeField] private RectTransform introBorderRect;
        [SerializeField] private RectTransform introBackgroundRect;

        // Wired by Phase5ProjectBuilder.BuildCharacterStoryPlaceholder — one icon per GameplayTopics
        // entry (same order), one icon per ComboEntries entry (same order — the real Combo_*.png
        // banner art), and the 7 Cosmetics rows (icon + display name; blurb text lives here, keyed
        // by displayName, same convention CharacterStories uses for characters).
        [SerializeField] private Sprite[] gameplayTopicIcons;
        [SerializeField] private Sprite[] comboIcons;
        [SerializeField] private CosmeticEntry[] cosmeticEntries;

        [SerializeField] private Button storyTabButton;
        [SerializeField] private GameObject storyTabContent;
        [SerializeField] private Button howToPlayTabButton;
        [SerializeField] private GameObject howToPlayTabContent;
        [SerializeField] private Button combosTabButton;
        [SerializeField] private GameObject combosTabContent;
        [SerializeField] private Button charactersTabButton;
        [SerializeField] private GameObject charactersTabContent;
        [SerializeField] private Button cosmeticsTabButton;
        [SerializeField] private GameObject cosmeticsTabContent;

        // Same warm-gold-active / brown-inactive tint convention used throughout this project for
        // on/off feedback with no dedicated per-state art (LockedTint, InactiveTabTint, etc.) —
        // matches Phase5ProjectBuilder.BuildCharacterStoryPlaceholder's own TabActiveColor/
        // TabInactiveColor values, which set each tab button's initial (Story-selected) tint at
        // build time; this is what SelectTab re-applies at runtime on every tap.
        private static readonly Color TabActiveColor = new Color(0.85f, 0.65f, 0.2f);
        private static readonly Color TabInactiveColor = new Color(0.35f, 0.28f, 0.18f);

        private const float RowLeftPadding = 32f;
        // 34pt — a real, visible increase over the original 26pt auto-size ceiling (a flat 16pt,
        // tried first, was actually a decrease from what auto-sizing had usually been rendering,
        // which is why it read as "not enlarged" — see PopulateIfNeeded's own comment).
        private const float IntroFontSize = 34f;
        private const float IntroTextVerticalPadding = 40f; // 20 top + 20 bottom, inside IntroBackground
        private const float IntroBorderThickness = 12f; // matches the built RowWidth vs RowWidth-12 gap (6px/side)
        private const float IntroTextWidth = RowWidth - 12f - 68f; // IntroBackground width minus its own 34px/side text padding

        // Public: Phase5ProjectBuilder.BuildCharacterStoryPlaceholder sizes IntroBorder/every row to
        // this same width so everything within a tab shares the same left/right edges.
        public const float RowWidth = 1650f;
        private const float RowHeight = 380f;
        private const float RowSpacing = 40f;
        private const float TextWidth = RowWidth - 340f - RowSpacing - 20f;
        private const float RowBorderThickness = 6f;
        private static readonly Color RowBorderColor = new Color(0.70f, 0.55f, 0.20f);
        private static readonly Color RowBackgroundColor = new Color(0.97f, 0.94f, 0.86f, 0.92f);

        private const string IntroStory =
            "When the sun goes down on Old MacDonald's farm, the real fight begins.\n\n" +
            "The Harvest Robots didn't come to smash barns and topple fences — they came for the crops. " +
            "Every stalk of corn, every ripe vegetable, every last grain of wheat: the robots are hauling it " +
            "all away in the dead of night, and by morning it'll be gone for good.\n\n" +
            "So the animals strike back the only way they can — in secret. While the robots patrol the " +
            "dark fields on their mechanical rounds, the Farm Squad slips between the rows, ducking " +
            "searchlights and snatching back what's theirs, one crop at a time. Grab a power crop and the " +
            "tables turn — for a few glorious seconds, the hunters become the hunted.\n\n" +
            "Four fields. One farm. All night to save the harvest.\n\n" +
            "Cluck. Chase. Collect. Chaos.";

        // "How to Play" tab content — rewritten 2026-09-11 (per direct feedback: this screen is for
        // kids, it needs to be fun and illustrated, not a wall of text) into short, punchy 1-2
        // sentence blurbs, each paired with a real gameplay icon (gameplayTopicIcons, same index
        // order, wired by Phase5ProjectBuilder) instead of the original paragraph-per-topic text
        // block. Still deliberately player-facing/rounded rather than quoting exact internal
        // constants that are easy to retune later (e.g. GameManager.BaseCoinsPerLevel/CoinsPerStar,
        // LevelData.ComputeMaxPossibleScoreEstimate's chain cap) — if those change, this copy still
        // reads correctly without needing a matching edit. The old 4th entry here ("Abilities &
        // Combos") was pulled out into its own dedicated Combos tab (ComboEntries below) — a single
        // teaser row couldn't actually explain any of the 8 real combos, just gesture at the system
        // existing.
        private static readonly (string title, string body)[] GameplayTopics =
        {
            ("Coins", "Finish a level to earn coins — more stars means more coins! Spend them on " +
                "revives, ability skips, cosmetics, and new worlds."),
            ("Scoring & Stars", "Collect crops and zap robots to score big! Earn 1 to 3 stars a " +
                "level — just 1 star unlocks the next world."),
            ("Power Crops & Robot Chains", "Grab a power crop and the robots get scared! Zap them " +
                "one after another for huge bonus points."),
        };

        // Combos tab content (2026-09-11) — every combo ComboSystem.cs actually detects, in the
        // same order CLAUDE.md's own combo table uses. "trigger" is written as a direct instruction
        // (what to actually swap/do), "effect" as what the buff does the NEXT time the named
        // ability fires (ComboSystem stores these as one-shot Pending* flags consumed on that
        // ability's next activation — Full Fury is the one exception, an immediate effect on
        // trigger, called out as such in its own blurb). icon is matched up with comboIcons by
        // array index in Phase5ProjectBuilder, same convention gameplayTopicIcons uses.
        private static readonly (string title, string trigger, string effect)[] ComboEntries =
        {
            ("Feather Storm", "Swap Cluck → Woolly.",
                "Woolly's clones drop eggs as they wander, tripping up extra robots!"),
            ("Earthquake Roll", "Swap Bessie → Percy.",
                "Percy's next Bounce Roll blasts 9 tiles instead of 3!"),
            ("Skip Shatter", "Swap Ducky → Woolly.",
                "Ducky's next Skip Shot spawns 2 wool clones right where she lands!"),
            ("Double Slam", "Swap to Bessie, then swap to Bessie again (2nd time this maze).",
                "Her Ground Slam radius doubles to 4 tiles!"),
            ("Crossfire", "Swap Billy → Horace.",
                "Horace's Rear Kick sends robots flying twice as far — 8 tiles!"),
            ("Iron Stampede", "Swap Bessie → Gerald.",
                "Gerald's Puff Up smashes through nearby walls too!"),
            ("Kick and Roll", "Swap Horace → Percy.",
                "Same big boost as Earthquake Roll — Percy's next roll goes 9 tiles!"),
            ("Full Fury", "Play as 5 or more different animals in one maze.",
                "Every robot on the board freezes in fear for 5 seconds — right away!"),
        };

        // Cosmetics tab (2026-09-11) — same 7 purchasable items CosmeticsHubScreen sells, short
        // playful blurbs keyed by display name (matches cosmeticEntries' own displayName field,
        // wired by Phase5ProjectBuilder). Kid-facing tone to match the rest of this screen.
        private static readonly Dictionary<string, string> CosmeticBlurbs = new Dictionary<string, string>
        {
            { "Sombrero", "Ole! A wide, sun-shading hat with a ton of farm-fiesta flair." },
            { "Baseball Cap", "Sporty and snug — every animal's got a favourite colour." },
            { "Cowboy Hat", "Yeehaw! Perfect for rounding up robots instead of cattle." },
            { "Rainbow Ribbon", "A trail of shimmering rainbow colour follows every step." },
            { "Sparkle Dust", "Leaves a shimmering trail of magic sparkles behind you." },
            { "Corn Husk Trail", "A rustling trail of golden corn husks, straight off the stalk." },
            { "Ember Trail", "A trail of glowing embers — warm, cozy, and a little bit fiery." },
        };

        private static readonly Dictionary<CharacterType, string> CharacterStories = new Dictionary<CharacterType, string>
        {
            { CharacterType.Cluck, "Small, quick, and never short on nerve, Cluck is the fastest set of " +
                "feathers on the farm — first through the gate and first to reach the crops every single " +
                "night. She doesn't wait for trouble to find her: drop an egg in a robot's path, and its " +
                "night ends early." },
            { CharacterType.Bessie, "What Bessie lacks in speed, she makes up for in sheer presence. When " +
                "she plants her hooves and slams the ground, every robot nearby is finished on the spot " +
                "— and the shockwave keeps rolling for a few seconds after, catching anything foolish " +
                "enough to wander too close." },
            { CharacterType.Percy, "Percy may look built for napping, but tuck him into a ball and he's the " +
                "fastest thing in the field. His signature roll sends him barreling forward through the " +
                "maze, and any robot caught in his path gets flattened before it knows what hit it." },
            { CharacterType.Woolly, "Woolly figured out early that one sheep can only be in one place at a " +
                "time — so now she doesn't have to be. Two wandering look-alikes fan out to gather crops " +
                "on their own, while the real Woolly covers ground no robot can track." },
            { CharacterType.Ducky, "No fence, wall, or robot patrol has ever stopped Ducky — she just goes " +
                "around them. Paddle into one water tile, pop out the other side of the maze in an " +
                "instant, and slip past trouble the Harvest Robots never see coming." },
            { CharacterType.Horace, "Horace doesn't run from a fight — he ends them. One well-aimed kick " +
                "sends the nearest robot flying clear across the maze, and it doesn't get back up. " +
                "Farmhands three fields over say they can still hear the clang." },
            { CharacterType.Gerald, "Gerald's temper is legendary, and when he puffs up, the whole farm " +
                "knows it. He swells in furious pulses, and every robot dumb enough to get close during " +
                "the display doesn't get a second chance." },
            { CharacterType.Billy, "Billy's never met an obstacle he'd rather walk around than through. " +
                "Lower the horns, charge full speed across the maze — any robot standing in the way gets " +
                "sent packing on contact." },
        };

        private bool _populated;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (storyTabButton != null)
            {
                storyTabButton.onClick.AddListener(() => SelectTab(0));
            }
            if (howToPlayTabButton != null)
            {
                howToPlayTabButton.onClick.AddListener(() => SelectTab(1));
            }
            if (combosTabButton != null)
            {
                combosTabButton.onClick.AddListener(() => SelectTab(2));
            }
            if (charactersTabButton != null)
            {
                charactersTabButton.onClick.AddListener(() => SelectTab(3));
            }
            if (cosmeticsTabButton != null)
            {
                cosmeticsTabButton.onClick.AddListener(() => SelectTab(4));
            }
        }

        private void OnEnable()
        {
            PopulateIfNeeded();
            SelectTab(0);
        }

        /// <summary>Shows exactly one of the 5 tab content ScrollRects and tints the tab buttons to
        /// match, same "gold = active, brown = inactive" convention as everywhere else in this
        /// project uses tint-only on/off feedback (no dedicated per-tab art exists).</summary>
        private void SelectTab(int index)
        {
            if (storyTabContent != null) storyTabContent.SetActive(index == 0);
            if (howToPlayTabContent != null) howToPlayTabContent.SetActive(index == 1);
            if (combosTabContent != null) combosTabContent.SetActive(index == 2);
            if (charactersTabContent != null) charactersTabContent.SetActive(index == 3);
            if (cosmeticsTabContent != null) cosmeticsTabContent.SetActive(index == 4);

            SetTabButtonActive(storyTabButton, index == 0);
            SetTabButtonActive(howToPlayTabButton, index == 1);
            SetTabButtonActive(combosTabButton, index == 2);
            SetTabButtonActive(charactersTabButton, index == 3);
            SetTabButtonActive(cosmeticsTabButton, index == 4);
        }

        private static void SetTabButtonActive(Button button, bool active)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }
            button.targetGraphic.color = active ? TabActiveColor : TabInactiveColor;
        }

        private void PopulateIfNeeded()
        {
            if (_populated)
            {
                return;
            }

            if (introText != null)
            {
                introText.text = IntroStory;
                // Fixed 34pt rather than shrink-to-fit — auto-sizing against a fixed box was making
                // long copy shrink toward its 14pt floor, reading as small/cramped even though the
                // box's own max was 26pt. Per feedback, the container now grows to fit a genuinely
                // larger, constant reading size instead of the text shrinking to fit a fixed
                // container (a first pass at a flat 16pt actually rendered SMALLER than the old
                // auto-sized text usually had been — not an enlargement at all; corrected here).
                introText.enableAutoSizing = false;
                introText.fontSize = IntroFontSize;
                introText.overflowMode = TextOverflowModes.Overflow;
                ResizeIntroContainerToFitText();
            }

            if (howToPlayContainer != null)
            {
                for (int i = 0; i < GameplayTopics.Length; i++)
                {
                    var (title, body) = GameplayTopics[i];
                    Sprite icon = gameplayTopicIcons != null && i < gameplayTopicIcons.Length ? gameplayTopicIcons[i] : null;
                    BuildInfoRow(howToPlayContainer, title, body, icon);
                }
            }

            if (combosContainer != null)
            {
                for (int i = 0; i < ComboEntries.Length; i++)
                {
                    var (title, trigger, effect) = ComboEntries[i];
                    Sprite icon = comboIcons != null && i < comboIcons.Length ? comboIcons[i] : null;
                    BuildComboRow(title, trigger, effect, icon);
                }
            }

            if (cosmeticsContainer != null && cosmeticEntries != null)
            {
                if (cosmeticsContainer.TryGetComponent<VerticalLayoutGroup>(out var cosmeticsLayout))
                {
                    cosmeticsLayout.childAlignment = TextAnchor.UpperLeft;
                    var padding = cosmeticsLayout.padding;
                    padding.left = (int)RowLeftPadding;
                    cosmeticsLayout.padding = padding;
                }

                foreach (var entry in cosmeticEntries)
                {
                    string blurb = CosmeticBlurbs.TryGetValue(entry.displayName, out var text) ? text : string.Empty;
                    BuildCosmeticRow(entry.displayName, entry.icon, blurb);
                }
            }

            if (charactersContainer != null && cardPrefab != null && DataManager.Instance != null)
            {
                // 32px left padding for every character row's own bordered container, measured from
                // the scroll view's left edge (i.e. from the safe-area guide the whole tab already
                // sits inside) — per feedback. Scoped to this screen's own charactersContainer
                // instance (not CreateVerticalScrollView's shared default) so Character Roster's
                // identical helper call elsewhere isn't affected.
                if (charactersContainer.TryGetComponent<VerticalLayoutGroup>(out var charactersLayout))
                {
                    charactersLayout.childAlignment = TextAnchor.UpperLeft;
                    var padding = charactersLayout.padding;
                    padding.left = (int)RowLeftPadding;
                    charactersLayout.padding = padding;
                }

                foreach (var data in DataManager.Instance.GetAllCharacterData())
                {
                    BuildRow(data);
                }
            }

            _populated = true;
        }

        /// <summary>One "How to Play" topic — rebuilt 2026-09-11 (per direct feedback: this screen
        /// is for kids, it needs to be illustrated and fun, not a text-only wall) from a full-width
        /// title-above-body text card into a real icon beside a short blurb, same left-icon/
        /// right-text silhouette as BuildRow/BuildCosmeticRow so all 3 illustrated tabs read as one
        /// family. Row height shrunk (260 -> 180) to match the now much shorter copy — the tall box
        /// this row used to need for a full paragraph would just be awkward empty space around 1-2
        /// short sentences now.</summary>
        private void BuildInfoRow(Transform parent, string title, string body, Sprite icon)
        {
            const float rowHeight = 180f;
            const float iconSize = 130f;

            var rowGO = new GameObject($"InfoRow_{title}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(parent, false);
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.sizeDelta = new Vector2(RowWidth, rowHeight);
            rowGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBorderColor);

            var backgroundGO = new GameObject("RowBackground", typeof(RectTransform), typeof(Image));
            backgroundGO.transform.SetParent(rowGO.transform, false);
            var backgroundRect = (RectTransform)backgroundGO.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(RowBorderThickness, RowBorderThickness);
            backgroundRect.offsetMax = new Vector2(-RowBorderThickness, -RowBorderThickness);
            backgroundGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBackgroundColor);

            var contentGO = new GameObject("RowContent", typeof(RectTransform));
            contentGO.transform.SetParent(backgroundGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(30, 30, 20, 20);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)iconGO.transform).sizeDelta = new Vector2(iconSize, iconSize);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.preserveAspect = true;
            if (icon != null)
            {
                iconImage.sprite = icon;
            }
            else
            {
                iconImage.sprite = PlaceholderSprite.GetCircle(new Color(0.85f, 0.65f, 0.2f));
            }

            float innerWidth = RowWidth - RowBorderThickness * 2f - 60f - 30f - iconSize - 60f;

            var textColumnGO = new GameObject("TextColumn", typeof(RectTransform));
            textColumnGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)textColumnGO.transform).sizeDelta = new Vector2(innerWidth, rowHeight - 40f);
            var vlg = textColumnGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.spacing = 8f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)titleGO.transform).sizeDelta = new Vector2(innerWidth, 40f);
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.font = TMP_Settings.defaultFontAsset;
            titleTmp.fontSize = 30f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.35f, 0.18f, 0.05f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var bodyGO = new GameObject("Body", typeof(RectTransform));
            bodyGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)bodyGO.transform).sizeDelta = new Vector2(innerWidth, rowHeight - 40f - 40f - 8f);
            var bodyTmp = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = body;
            bodyTmp.font = TMP_Settings.defaultFontAsset;
            bodyTmp.fontSize = 24f;
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.color = Color.black;
            bodyTmp.enableWordWrapping = true;
            // Shrink-to-fit so a topic's body text can never spill past its own bordered card, same
            // convention BuildRow's story blurb uses.
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 16f;
            bodyTmp.fontSizeMax = 24f;
            bodyTmp.overflowMode = TextOverflowModes.Truncate;
        }

        /// <summary>One Combos row — icon-left/text-right silhouette, same general shape
        /// BuildInfoRow/BuildCosmeticRow/BuildRow all use on this screen (BuildRow's own
        /// CharacterSelectCard is the closest reference — a real, generously-sized piece of art
        /// filling its own left-hand column, story text beside it). Reworked twice on 2026-09-11:
        /// first into a full-width banner-on-top layout (per "enlarge the artwork"), then walked
        /// back per direct follow-up feedback ("sized too big... keep the container size as it
        /// was... enlarge the artwork to fit the width - not fill the entire container - very much
        /// like the character cards") — rowHeight is back to its original fixed 220 (not a
        /// per-combo computed height), and the art sits in its own left column instead of spanning
        /// the whole row.
        ///
        /// iconWidth (300, up from the original 130/190 square) is sized like BuildRow's character
        /// card column — big relative to the row, but still just ONE column beside the text, not
        /// the whole card. Combo_*.png banners are landscape art (roughly 1.3:1 to 2.3:1 depending
        /// on the combo, not square), so the icon box itself is sized to the row's real available
        /// content height (iconHeight, not a square) with preserveAspect=true — this fills the box's
        /// full WIDTH for any combo whose own aspect is wider than the box (several of the 8 are),
        /// and fills its full HEIGHT with a little side margin for the few narrower/more-square
        /// ones, but never stretches or overflows either way.</summary>
        private void BuildComboRow(string title, string trigger, string effect, Sprite icon)
        {
            const float rowHeight = 220f; // original container size, unchanged
            const float iconWidth = 300f; // was a 130 (then 190) square — now a real card-sized column
            const float iconHeight = 180f; // rowHeight minus the hlg's own 20px top/bottom padding

            var rowGO = new GameObject($"ComboRow_{title}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(combosContainer, false);
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.sizeDelta = new Vector2(RowWidth, rowHeight);
            rowGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBorderColor);

            var backgroundGO = new GameObject("RowBackground", typeof(RectTransform), typeof(Image));
            backgroundGO.transform.SetParent(rowGO.transform, false);
            var backgroundRect = (RectTransform)backgroundGO.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(RowBorderThickness, RowBorderThickness);
            backgroundRect.offsetMax = new Vector2(-RowBorderThickness, -RowBorderThickness);
            backgroundGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBackgroundColor);

            var contentGO = new GameObject("RowContent", typeof(RectTransform));
            contentGO.transform.SetParent(backgroundGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(30, 30, 20, 20);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)iconGO.transform).sizeDelta = new Vector2(iconWidth, iconHeight);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.sprite = icon != null ? icon : PlaceholderSprite.GetCircle(new Color(0.85f, 0.65f, 0.2f));

            float innerWidth = RowWidth - RowBorderThickness * 2f - 60f - 30f - iconWidth - 60f;

            var textColumnGO = new GameObject("TextColumn", typeof(RectTransform));
            textColumnGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)textColumnGO.transform).sizeDelta = new Vector2(innerWidth, rowHeight - 40f);
            var vlg = textColumnGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.spacing = 8f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)titleGO.transform).sizeDelta = new Vector2(innerWidth, 40f);
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = title;
            titleTmp.font = TMP_Settings.defaultFontAsset;
            titleTmp.fontSize = 30f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.35f, 0.18f, 0.05f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

            float lineHeight = (rowHeight - 40f - 40f - 8f - 6f) / 2f;

            var triggerGO = new GameObject("Trigger", typeof(RectTransform));
            triggerGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)triggerGO.transform).sizeDelta = new Vector2(innerWidth, lineHeight);
            var triggerTmp = triggerGO.AddComponent<TextMeshProUGUI>();
            triggerTmp.text = $"<b>Trigger:</b> {trigger}";
            triggerTmp.font = TMP_Settings.defaultFontAsset;
            triggerTmp.fontSize = 22f;
            triggerTmp.alignment = TextAlignmentOptions.TopLeft;
            triggerTmp.color = new Color(0.45f, 0.32f, 0.12f);
            triggerTmp.enableWordWrapping = true;
            triggerTmp.enableAutoSizing = true;
            triggerTmp.fontSizeMin = 14f;
            triggerTmp.fontSizeMax = 22f;
            triggerTmp.overflowMode = TextOverflowModes.Truncate;

            var effectGO = new GameObject("Effect", typeof(RectTransform));
            effectGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)effectGO.transform).sizeDelta = new Vector2(innerWidth, lineHeight);
            var effectTmp = effectGO.AddComponent<TextMeshProUGUI>();
            effectTmp.text = $"<b>Effect:</b> {effect}";
            effectTmp.font = TMP_Settings.defaultFontAsset;
            effectTmp.fontSize = 22f;
            effectTmp.alignment = TextAlignmentOptions.TopLeft;
            effectTmp.color = new Color(0.15f, 0.5f, 0.2f);
            effectTmp.enableWordWrapping = true;
            // Shrink-to-fit so a combo's trigger/effect text can never spill past its own bordered
            // card, same convention BuildInfoRow's body / BuildRow's story blurb use.
            effectTmp.enableAutoSizing = true;
            effectTmp.fontSizeMin = 14f;
            effectTmp.fontSizeMax = 22f;
            effectTmp.overflowMode = TextOverflowModes.Truncate;
        }

        /// <summary>One Cosmetics row — same left-icon/right-text silhouette as BuildInfoRow, using
        /// the item's own real price-baked art (the same sprite CosmeticsHubScreen's purchase
        /// button shows) instead of a generic placeholder, so a kid recognises the exact same
        /// picture when they go looking for it in the Shop later. Purely informational — no tap
        /// action, no purchase flow here (matches BuildRow's own "browsing list" convention for
        /// characters).</summary>
        private void BuildCosmeticRow(string displayName, Sprite icon, string blurb)
        {
            const float rowHeight = 180f;
            const float iconSize = 130f;

            var rowGO = new GameObject($"CosmeticRow_{displayName}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(cosmeticsContainer, false);
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.sizeDelta = new Vector2(RowWidth, rowHeight);
            rowGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBorderColor);

            var backgroundGO = new GameObject("RowBackground", typeof(RectTransform), typeof(Image));
            backgroundGO.transform.SetParent(rowGO.transform, false);
            var backgroundRect = (RectTransform)backgroundGO.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(RowBorderThickness, RowBorderThickness);
            backgroundRect.offsetMax = new Vector2(-RowBorderThickness, -RowBorderThickness);
            backgroundGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBackgroundColor);

            var contentGO = new GameObject("RowContent", typeof(RectTransform));
            contentGO.transform.SetParent(backgroundGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 30f;
            hlg.padding = new RectOffset(30, 30, 20, 20);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)iconGO.transform).sizeDelta = new Vector2(iconSize, iconSize);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.preserveAspect = true;
            if (icon != null)
            {
                iconImage.sprite = icon;
            }
            else
            {
                iconImage.sprite = PlaceholderSprite.GetCircle(new Color(0.85f, 0.65f, 0.2f));
            }

            float innerWidth = RowWidth - RowBorderThickness * 2f - 60f - 30f - iconSize - 60f;

            var textColumnGO = new GameObject("TextColumn", typeof(RectTransform));
            textColumnGO.transform.SetParent(contentGO.transform, false);
            ((RectTransform)textColumnGO.transform).sizeDelta = new Vector2(innerWidth, rowHeight - 40f);
            var vlg = textColumnGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.spacing = 8f;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)titleGO.transform).sizeDelta = new Vector2(innerWidth, 40f);
            var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
            titleTmp.text = displayName;
            titleTmp.font = TMP_Settings.defaultFontAsset;
            titleTmp.fontSize = 30f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.35f, 0.18f, 0.05f);
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var bodyGO = new GameObject("Body", typeof(RectTransform));
            bodyGO.transform.SetParent(textColumnGO.transform, false);
            ((RectTransform)bodyGO.transform).sizeDelta = new Vector2(innerWidth, rowHeight - 40f - 40f - 8f);
            var bodyTmp = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = blurb;
            bodyTmp.font = TMP_Settings.defaultFontAsset;
            bodyTmp.fontSize = 24f;
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.color = Color.black;
            bodyTmp.enableWordWrapping = true;
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 16f;
            bodyTmp.fontSizeMax = 24f;
            bodyTmp.overflowMode = TextOverflowModes.Truncate;
        }

        /// <summary>Grows IntroBorder/IntroBackground (and IntroText's own box) to fit IntroStory at
        /// the fixed IntroFontSize. IntroBorder is the sole child of the Story tab's own
        /// VerticalLayoutGroup content (see BuildCharacterStoryPlaceholder) — a
        /// RectTransform.sizeDelta write marks that layout dirty automatically, so growing it here
        /// resizes the Story tab's own scrollable area with no manual repositioning needed.</summary>
        private void ResizeIntroContainerToFitText()
        {
            if (introBackgroundRect == null || introBorderRect == null)
            {
                return;
            }

            introText.ForceMeshUpdate();
            float preferredHeight = introText.GetPreferredValues(IntroTextWidth, 0f).y;

            var introTextRect = introText.rectTransform;
            introTextRect.sizeDelta = new Vector2(IntroTextWidth, preferredHeight);

            float backgroundHeight = preferredHeight + IntroTextVerticalPadding;
            float borderHeight = backgroundHeight + IntroBorderThickness;
            introBackgroundRect.sizeDelta = new Vector2(introBackgroundRect.sizeDelta.x, backgroundHeight);
            introBorderRect.sizeDelta = new Vector2(introBorderRect.sizeDelta.x, borderHeight);
        }

        /// <summary>One row = the character's CharacterSelectCard on the left, a word-wrapped story
        /// blurb on the right. Built entirely at runtime (no dedicated row prefab) since neither
        /// child needs pre-wired serialized references beyond the card prefab this screen already
        /// has. The row's own RectTransform.sizeDelta is set explicitly rather than left to a
        /// LayoutElement — charactersContainer's outer VerticalLayoutGroup has childControlHeight/
        /// Width = false (CreateVerticalScrollView's convention), which silently ignores
        /// LayoutElement hints and reads each child's raw sizeDelta directly instead (the same
        /// gotcha Level Select's own tile-grid section hit — see LevelSelectController.
        /// PopulateLevelGrid's doc comment). The row's HorizontalLayoutGroup has childControlWidth/
        /// Height = false too, so it only positions the card and text side by side using their own
        /// already-set sizes rather than resizing either of them (the card already carries an
        /// explicit 340x360 sizeDelta from BuildCharacterSelectCardPrefab). Wrapped in a bordered
        /// frame (RowBorderColor/RowBackgroundColor) per feedback that each card+story needed a
        /// visible border.</summary>
        private void BuildRow(CharacterData data)
        {
            // Outer frame: a gold-bordered card, same two-layer border/background composition
            // IntroBorder/IntroBackground already use above (no dedicated wood-frame art exists for
            // a box this shape yet) — per feedback that each character's card+story needed a visible
            // border around it, not just floating on the dimmed backdrop.
            var rowGO = new GameObject($"Row_{data.characterType}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(charactersContainer, false);
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.sizeDelta = new Vector2(RowWidth, RowHeight);
            rowGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBorderColor);

            var backgroundGO = new GameObject("RowBackground", typeof(RectTransform), typeof(Image));
            backgroundGO.transform.SetParent(rowGO.transform, false);
            var backgroundRect = (RectTransform)backgroundGO.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(RowBorderThickness, RowBorderThickness);
            backgroundRect.offsetMax = new Vector2(-RowBorderThickness, -RowBorderThickness);
            backgroundGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(RowBackgroundColor);

            // Content: card + story text, laid out inside the bordered background rather than
            // directly on rowGO, so the border/background aren't also treated as HorizontalLayoutGroup
            // children.
            var contentGO = new GameObject("RowContent", typeof(RectTransform));
            contentGO.transform.SetParent(backgroundGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = RowSpacing;
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var cardGO = Instantiate(cardPrefab, contentGO.transform);
            var card = cardGO.GetComponent<CharacterSelectCard>();
            if (card != null)
            {
                card.Initialize(data, unlocked: true, isActive: false, onSelected: null);
            }

            var textGO = new GameObject("StoryText", typeof(RectTransform));
            textGO.transform.SetParent(contentGO.transform, false);
            var textRect = (RectTransform)textGO.transform;
            textRect.sizeDelta = new Vector2(TextWidth - 40f, RowHeight - 20f);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = CharacterStories.TryGetValue(data.characterType, out var story) ? story : string.Empty;
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 30f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            // Black per feedback — the previous white text was unreadable against this row's own
            // background. Only IntroText (which sits on the dark backdrop, not this light card
            // background) stays white/cream.
            tmp.color = Color.black;
            // Shrink-to-fit so a long blurb can never spill past the card's own bordered background
            // (the "keep text inside the container" feedback) — same convention as IntroText above.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = 30f;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }
    }
}
