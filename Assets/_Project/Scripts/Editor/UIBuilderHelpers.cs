using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Shared programmatic-uGUI construction helpers for Phase5ProjectBuilder — there's no visual
    /// Editor access in this Claude Code session, so every screen is built via AddComponent calls
    /// the same way Phase1-4's ProjectBuilders build prefabs. Layouts lean on VerticalLayoutGroup/
    /// HorizontalLayoutGroup + LayoutElement (auto-sizing rows) rather than hand-computed
    /// RectTransform offsets everywhere, to keep ~10 screens' worth of construction code tractable
    /// — correctness and scaling over pixel-perfect placement, same trade-off every earlier phase
    /// made for art (solid-colour placeholders instead of real sprites).
    /// </summary>
    public static class UIBuilderHelpers
    {
        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFull((RectTransform)go.transform);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(color);
            return go;
        }

        public static GameObject CreateEmpty(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static CanvasGroup CreateOverlayPanel(string name, Transform parent, Color color, bool startInactive = true)
        {
            var go = CreatePanel(name, parent, color);
            var group = go.AddComponent<CanvasGroup>();
            if (startInactive)
            {
                go.SetActive(false);
            }
            return group;
        }

        public static VerticalLayoutGroup CreateVerticalGroup(string name, Transform parent, float spacing = 10f,
            int paddingAll = 20, TextAnchor childAlignment = TextAnchor.UpperCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700f, 0f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(paddingAll, paddingAll, paddingAll, paddingAll);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = childAlignment;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return vlg;
        }

        public static HorizontalLayoutGroup CreateHorizontalGroup(string name, Transform parent, float spacing = 10f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            go.GetComponent<LayoutElement>().preferredHeight = 60f;
            return hlg;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, float preferredHeight = 40f, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color ?? Color.white;
            tmp.enableWordWrapping = true;
            go.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return tmp;
        }

        public static Image CreateImage(string name, Transform parent, Color color, float preferredWidth = 60f, float preferredHeight = 60f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(color);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            return go.GetComponent<Image>();
        }

        public static Button CreateButton(string name, Transform parent, string label, Color color, out TextMeshProUGUI labelText)
        {
            return CreateButton(name, parent, label, color, 26f, 60f, out labelText);
        }

        public static Button CreateButton(string name, Transform parent, string label, Color color,
            float fontSize, float preferredHeight, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(color);
            go.GetComponent<LayoutElement>().preferredHeight = preferredHeight;

            var button = go.GetComponent<Button>();

            var textGO = new GameObject(name + "_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)textGO.transform);
            labelText = textGO.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;

            return button;
        }

        public static Toggle CreateToggle(string name, Transform parent, string label, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            // Explicit height here matters even though LayoutElement.preferredHeight is also 40 —
            // whichever layout group this ends up parented under (e.g. Settings' "Content", which
            // has childControlHeight=false so it never applies preferredHeight to the child rect)
            // would otherwise leave this at Unity's ~100px default RectTransform height instead.
            ((RectTransform)go.transform).sizeDelta = new Vector2(0f, 40f);
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;
            go.GetComponent<LayoutElement>().preferredHeight = 40f;

            var checkboxGO = new GameObject(name + "_Box", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            checkboxGO.transform.SetParent(go.transform, false);
            checkboxGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.25f, 0.2f, 0.15f));
            checkboxGO.GetComponent<LayoutElement>().preferredWidth = 40f;

            var checkmarkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkGO.transform.SetParent(checkboxGO.transform, false);
            var checkmarkRect = (RectTransform)checkmarkGO.transform;
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;
            // Warm gold instead of the original bright green — reads as "on" without clashing with
            // the wood/parchment plaque backdrop behind it.
            checkmarkGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.85f, 0.65f, 0.2f));

            var toggle = checkboxGO.GetComponent<Toggle>();
            toggle.targetGraphic = checkboxGO.GetComponent<Image>();
            toggle.graphic = checkmarkGO.GetComponent<Image>();
            toggle.isOn = true;

            labelText = CreateText(name + "_Label", go.transform, label, 22f, TextAlignmentOptions.Left, 40f);

            return toggle;
        }

        public static Slider CreateSlider(string name, Transform parent, float value = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            // Same reasoning as CreateToggle above — an explicit height is needed regardless of
            // LayoutElement.preferredHeight, since Settings' "Content" group has
            // childControlHeight=false and won't otherwise shrink this down from Unity's ~100px
            // RectTransform default. This was the direct cause of the oversized slider bars.
            ((RectTransform)go.transform).sizeDelta = new Vector2(0f, 30f);
            go.GetComponent<LayoutElement>().preferredHeight = 30f;

            var bgGO = CreatePanel(name + "_Background", go.transform, new Color(0.25f, 0.2f, 0.15f));

            var fillAreaGO = CreateEmpty(name + "_FillArea", go.transform);
            StretchFull((RectTransform)fillAreaGO.transform);
            // Warm gold fill instead of the original bright green, matching CreateToggle's checkmark.
            var fillGO = CreatePanel(name + "_Fill", fillAreaGO.transform, new Color(0.85f, 0.65f, 0.2f));

            var handleAreaGO = CreateEmpty(name + "_HandleArea", go.transform);
            StretchFull((RectTransform)handleAreaGO.transform);
            var handleGO = CreateImage(name + "_Handle", handleAreaGO.transform, new Color(0.95f, 0.9f, 0.78f), 20f, 20f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fillGO.transform;
            slider.handleRect = (RectTransform)handleGO.transform;
            slider.targetGraphic = handleGO;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            return slider;
        }

        public static ScrollRect CreateHorizontalScrollView(string name, Transform parent, out Transform content)
        {
            var viewportParentGO = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            viewportParentGO.transform.SetParent(parent, false);
            StretchFull((RectTransform)viewportParentGO.transform);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(viewportParentGO.transform, false);
            StretchFull((RectTransform)viewportGO.transform);
            viewportGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(1f, 1f, 1f, 0.01f));
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var hlg = contentGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.padding = new RectOffset(40, 40, 20, 20);
            hlg.childControlWidth = false;
            // false on both: a "true" control/expand pair here stretches every child (LevelMarker,
            // RosterCard) to fill the full viewport height regardless of its own prefab-set size —
            // this is what turned 140x140 level markers and 200x220 roster cards into full-height
            // vertical bars. false leaves each child exactly the size its own prefab defines.
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            contentGO.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = viewportParentGO.GetComponent<ScrollRect>();
            scrollRect.viewport = (RectTransform)viewportGO.transform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;

            content = contentGO.transform;
            return scrollRect;
        }

        public static GameObject CreateStarDisplay(string name, Transform parent, int starSize = 30)
        {
            var rowGO = CreateHorizontalGroup(name, parent, 4f);
            rowGO.GetComponent<LayoutElement>().preferredHeight = starSize;

            var images = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                images[i] = CreateImage($"{name}_Star{i}", rowGO.transform, new Color(0.35f, 0.35f, 0.35f), starSize, starSize);
            }

            var starDisplay = rowGO.gameObject.AddComponent<FarmFuryArcade.UI.StarDisplay>();
            var so = new UnityEditor.SerializedObject(starDisplay);
            var arrayProp = so.FindProperty("starImages");
            arrayProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return rowGO.gameObject;
        }
    }
}
