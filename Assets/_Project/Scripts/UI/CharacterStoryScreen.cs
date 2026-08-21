using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

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

        private const float RowWidth = 1650f;
        private const float RowHeight = 380f;
        private const float RowSpacing = 40f;
        private const float TextWidth = RowWidth - 340f - RowSpacing - 20f;

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
            }

            if (cardContainer == null || cardPrefab == null || DataManager.Instance == null)
            {
                return;
            }

            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                BuildRow(data);
            }

            _populated = true;
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
        /// BuildCharacterSelectCardPrefab).</summary>
        private void BuildRow(CharacterData data)
        {
            var rowGO = new GameObject($"Row_{data.characterType}", typeof(RectTransform));
            rowGO.transform.SetParent(cardContainer, false);
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.sizeDelta = new Vector2(RowWidth, RowHeight);

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = RowSpacing;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var cardGO = Instantiate(cardPrefab, rowGO.transform);
            var card = cardGO.GetComponent<CharacterSelectCard>();
            if (card != null)
            {
                card.Initialize(data, unlocked: true, isActive: false, onSelected: null);
            }

            var textGO = new GameObject("StoryText", typeof(RectTransform));
            textGO.transform.SetParent(rowGO.transform, false);
            var textRect = (RectTransform)textGO.transform;
            textRect.sizeDelta = new Vector2(TextWidth, RowHeight);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = CharacterStories.TryGetValue(data.characterType, out var story) ? story : string.Empty;
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 30f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            tmp.color = Color.white;
        }
    }
}
