using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>Character Story overlay — the game's narrative intro plus one row per character
    /// (their CharacterSelectCard next to a short story/ability blurb), pulled from the GDD's
    /// narrative section and the characters' actual current abilities (several diverged from the
    /// GDD's original spec since v1.0 — e.g. Percy's wall-phase became a robot-charging roll, and
    /// Billy's wall-destroy became a robot-charging headbutt — so the blurbs describe what the
    /// ability does today, not the original GDD text). Populates cardContainer with one row per
    /// DataManager.GetAllCharacterData() entry, same Cluck-first hierarchy order
    /// ChooseCharacterScreen uses. Every card shows unlocked/non-active/non-interactive — this is a
    /// browsing list, not the swap gate ChooseCharacterScreen enforces, and tapping a card does
    /// nothing since there's no per-character sub-screen (the story IS the blurb next to it).</summary>
    public class CharacterStoryScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI introText;
        [SerializeField] private RectTransform introBorderRect;
        [SerializeField] private RectTransform introBackgroundRect;

        private const float RowLeftPadding = 32f;
        // 34pt — a real, visible increase over the original 26pt auto-size ceiling (a flat 16pt,
        // tried first, was actually a decrease from what auto-sizing had usually been rendering,
        // which is why it read as "not enlarged" — see PopulateIfNeeded's own comment).
        private const float IntroFontSize = 34f;
        private const float IntroTextVerticalPadding = 40f; // 20 top + 20 bottom, inside IntroBackground
        private const float IntroBorderThickness = 12f; // matches the built RowWidth vs RowWidth-12 gap (6px/side)
        private const float IntroTextWidth = RowWidth - 12f - 68f; // IntroBackground width minus its own 34px/side text padding

        // Public: Phase5ProjectBuilder.BuildCharacterStoryPlaceholder sizes IntroBorder to this same
        // width so it lines up with every row beneath it now that both live in one scrollable list
        // (see PopulateIfNeeded's own comment on why the intro is a list item now, not a separate
        // fixed-position element above the list).
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
        }

        private void OnEnable()
        {
            PopulateIfNeeded();
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

            if (cardContainer == null || cardPrefab == null || DataManager.Instance == null)
            {
                return;
            }

            // 32px left padding for every character row's own bordered container, measured from the
            // scroll view's left edge (i.e. from the safe-area guide the whole list already sits
            // inside) — per feedback. Scoped to this screen's own cardContainer instance (not
            // CreateVerticalScrollView's shared default) so Character Roster's identical helper call
            // elsewhere isn't affected.
            if (cardContainer.TryGetComponent<VerticalLayoutGroup>(out var cardContainerLayout))
            {
                cardContainerLayout.childAlignment = TextAnchor.UpperLeft;
                var padding = cardContainerLayout.padding;
                padding.left = (int)RowLeftPadding;
                cardContainerLayout.padding = padding;
            }

            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                BuildRow(data);
            }

            _populated = true;
        }

        /// <summary>Grows IntroBorder/IntroBackground (and IntroText's own box) to fit IntroStory at
        /// the fixed IntroFontSize. No longer touches any scroll-view margin — IntroBorder is now
        /// the first child inside cardContainer's own VerticalLayoutGroup (see
        /// BuildCharacterStoryPlaceholder), the same list the character rows live in further down.
        /// Unity's layout group always positions siblings from their actual RectTransform.sizeDelta
        /// even with childControlHeight=false (only whether it OVERWRITES that size is gated by
        /// childControlHeight — reading it for stacking purposes isn't), and a RectTransform.
        /// sizeDelta write marks the layout dirty automatically — so growing IntroBorder here simply
        /// pushes every row below it down the list on its own, with no manual repositioning needed.
        /// That's the whole point of merging them into one list (per feedback: a tall intro box was
        /// eating into a separate, fixed-height area reserved for the character cards) — the list is
        /// just taller now, and the ScrollRect around it handles the rest.</summary>
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
        /// LayoutElement — cardContainer's outer VerticalLayoutGroup has childControlHeight/Width =
        /// false (CreateVerticalScrollView's convention), which silently ignores LayoutElement
        /// hints and reads each child's raw sizeDelta directly instead (the same gotcha Level
        /// Select's own tile-grid section hit — see LevelSelectController.PopulateLevelGrid's doc
        /// comment). The row's HorizontalLayoutGroup has childControlWidth/Height = false too, so it
        /// only positions the card and text side by side using their own already-set sizes rather
        /// than resizing either of them (the card already carries an explicit 340x360 sizeDelta from
        /// BuildCharacterSelectCardPrefab). Wrapped in a bordered frame (RowBorderColor/
        /// RowBackgroundColor) per feedback that each card+story needed a visible border.</summary>
        private void BuildRow(CharacterData data)
        {
            // Outer frame: a gold-bordered card, same two-layer border/background composition
            // IntroBorder/IntroBackground already use above (no dedicated wood-frame art exists for
            // a box this shape yet) — per feedback that each character's card+story needed a visible
            // border around it, not just floating on the dimmed backdrop.
            var rowGO = new GameObject($"Row_{data.characterType}", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(cardContainer, false);
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
