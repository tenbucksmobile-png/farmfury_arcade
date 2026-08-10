using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Level Select screen. Opens into a "world select" state showing a horizontally flickable
    /// carousel (CardCarouselController) of all 4 world badges — Corn Field is always coloured and
    /// tappable; Vegetable Patch/Orchard/Wheat Field render greyed out and non-interactable until
    /// their gate level (per UnlockProgression) has 2+ stars, so the player can see/flick past the
    /// whole world roadmap even before it's unlocked. Flicking cycles which badge sits
    /// centred/full-scale; tapping the already-centred (unlocked) badge reveals that world's level tile grid
    /// and shrinks the badge down into a small persistent indicator top-left of the screen; tapping
    /// that indicator again returns to world select. Reached directly from Main Menu's Play
    /// button (see MainMenuController.levelSelectScreen) — an intermediate "World Map" screen used
    /// to sit here but was removed outright once this screen made it redundant (see CLAUDE.md's
    /// "Removed: World Map screen").
    ///
    /// Tapping an unlocked tile loads that level and shows the gameplay screen directly
    /// (GameManager.LoadLevel + SceneTransitionManager.ShowOnly). There is deliberately no
    /// "Matchup Screen" step either: that screen (a VS-card + 3-2-1-GO countdown) was removed from
    /// this project after playtesting read it as tonally mismatched (see CLAUDE.md's "Removed:
    /// Matchup screen"), so this doesn't resurrect it.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [SerializeField] private GameObject levelTilePrefab;
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private LockedHintPanel lockedHintPanel;
        [SerializeField] private Button backButton;
        [SerializeField] private Image backButtonImage;
        [SerializeField] private Sprite backButtonWorldSelectSprite;
        [SerializeField] private Sprite backButtonTileGridSprite;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameplayScreen;

        [Header("World Select")]
        [SerializeField] private GameObject worldShieldPrefab;
        [SerializeField] private RectTransform worldShieldContainer;
        [SerializeField] private CardCarouselController worldCarousel;
        [SerializeField] private RectTransform currentWorldIndicator;
        [SerializeField] private Image currentWorldIndicatorImage;
        [SerializeField] private Button currentWorldIndicatorButton;

        /// <summary>Complete world badges (shield shape + rope + name text all baked into one
        /// sprite), indexed by world number (0=Corn Field, 1=Vegetable Patch, 2=Orchard, 3=Wheat
        /// Field — matches UnlockProgression.GetWorldNameForLevel's own mapping). Used directly as
        /// both a world-select carousel badge's Image.sprite and the small current-world indicator's
        /// — no separate background + name overlay needed now that CornFieldSign/VegetablePatchSign/
        /// OrchardSign/WheatfieldSign.png each already contain the full badge art. Wired by
        /// ArtWiringBuilder.</summary>
        [SerializeField] private Sprite[] worldSignSprites;

        private const float ScrollTweenSeconds = 0.5f;
        private const float ShieldRevealSeconds = 0.45f;

        /// <summary>Multiply-tint applied to a locked world's badge Image — dimmed/desaturated enough
        /// to read as "not yet playable" while still being clearly visible (not just a dark silhouette)
        /// against the coloured art of an unlocked badge. Lightened from 0.35 per feedback that the
        /// original tint read as almost black.</summary>
        private static readonly Color LockedWorldTint = new Color(0.65f, 0.65f, 0.65f, 1f);

        private readonly Dictionary<int, LevelTileController> _tilesByIndex = new Dictionary<int, LevelTileController>();
        private readonly List<int> _shownWorlds = new List<int>();
        private readonly List<GameObject> _shieldObjects = new List<GameObject>();
        private Coroutine _scrollRoutine;
        private Coroutine _shieldRevealRoutine;
        private int? _selectedWorld;

        private void Awake()
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
            currentWorldIndicatorButton.onClick.AddListener(ShowWorldSelect);
        }

        private void OnEnable()
        {
            OpenLevelSelect();
        }

        /// <summary>Entry point called every time the screen becomes active (wired to OnEnable, not
        /// just Awake, so re-opening this screen after playing a level always reflects the latest
        /// save data). Always re-opens into world select rather than remembering the last-viewed
        /// world — which world shields are available can have changed since the player was last
        /// here.</summary>
        public void OpenLevelSelect()
        {
            ShowWorldSelect();
        }

        /// <summary>Shows all 4 world badges in the carousel (not just unlocked ones) and hides the
        /// tile grid. World 0 (Corn Field) is always available; world N beyond that only once its
        /// gate level (the last level of world N-1) has 2+ stars — the same threshold
        /// UnlockProgression.IsLevelUnlocked already gates level access on, so "the world is
        /// selectable" and "its levels are reachable" never disagree. Locked worlds still get a
        /// badge (greyed out, non-interactable) so the player can see/flick past all 4 worlds and
        /// understand there's more content coming, rather than the carousel just not existing for
        /// worlds they haven't reached yet — only unlocked worlds are coloured and tappable.</summary>
        public void ShowWorldSelect()
        {
            if (_shieldRevealRoutine != null)
            {
                StopCoroutine(_shieldRevealRoutine);
                _shieldRevealRoutine = null;
            }

            _selectedWorld = null;
            currentWorldIndicator.gameObject.SetActive(false);
            scrollRect.gameObject.SetActive(false);
            SetBackButtonSprite(backButtonWorldSelectSprite);

            foreach (Transform child in worldShieldContainer)
            {
                Destroy(child.gameObject);
            }
            worldCarousel.enabled = true;
            worldCarousel.ClearItems();
            _shownWorlds.Clear();
            _shieldObjects.Clear();
            worldShieldContainer.gameObject.SetActive(true);

            int worldCount = Mathf.CeilToInt((float)UnlockProgression.TotalLevels / UnlockProgression.LevelsPerWorld);
            for (int world = 0; world < worldCount; world++)
            {
                bool unlocked = IsWorldAvailable(world);

                var shieldGO = Instantiate(worldShieldPrefab, worldShieldContainer);
                var shieldImage = shieldGO.GetComponent<Image>();
                SetWorldSignSprite(shieldImage, world);
                shieldImage.color = unlocked ? Color.white : LockedWorldTint;
                shieldGO.GetComponent<Button>().interactable = unlocked;

                _shownWorlds.Add(world);
                _shieldObjects.Add(shieldGO);
            }

            var rects = new List<RectTransform>(_shieldObjects.Count);
            var buttons = new List<Button>(_shieldObjects.Count);
            foreach (var shieldGO in _shieldObjects)
            {
                rects.Add((RectTransform)shieldGO.transform);
                buttons.Add(shieldGO.GetComponent<Button>());
            }

            // Default to the highest-unlocked world's badge, clamped into range — same "pick up
            // where the player left off" intent ScrollToCurrentLevel already applies to the tile
            // grid, just one level up (which world, not which level).
            int highestWorld = UnlockProgression.GetHighestUnlockedLevel() / UnlockProgression.LevelsPerWorld;
            int startLocalIndex = Mathf.Max(0, _shownWorlds.IndexOf(Mathf.Clamp(highestWorld, 0, worldCount - 1)));
            worldCarousel.SetItems(rects, buttons, startLocalIndex, OnCarouselCenterTapped);
        }

        private void OnCarouselCenterTapped(int localIndex)
        {
            if (localIndex < 0 || localIndex >= _shownWorlds.Count)
            {
                return;
            }
            // A locked badge's Button.interactable is false, so its onClick (and therefore this
            // callback) never fires for it via CardCarouselController's tap-to-select path — no
            // extra guard needed here beyond the bounds check above.
            SelectWorld(_shownWorlds[localIndex], _shieldObjects[localIndex]);
        }

        /// <summary>World 0 is always available. World N (N>0) becomes available once the last
        /// level of world N-1 has 2+ stars — matching UnlockProgression's own world-gate threshold
        /// for level access, so a badge is never coloured/tappable for a world whose levels are
        /// actually still locked.</summary>
        private static bool IsWorldAvailable(int world)
        {
            if (world <= 0)
            {
                return true;
            }
            int gateLevelIndex = world * UnlockProgression.LevelsPerWorld - 1;
            return UnlockProgression.GetStarsForLevel(gateLevelIndex) >= 2;
        }

        private void SelectWorld(int world, GameObject shieldGO)
        {
            if (_shieldRevealRoutine != null)
            {
                StopCoroutine(_shieldRevealRoutine);
            }
            _shieldRevealRoutine = StartCoroutine(RevealWorld(world, shieldGO));
        }

        /// <summary>The tapped shield shrinks and fades out in place while the small current-world
        /// indicator (same art) fades in at its fixed top-left header position — a cross-dissolve
        /// reads as "the shield magically moves to the top-left" without needing fragile position
        /// tweening across two different RectTransform parents/anchor spaces.</summary>
        private IEnumerator RevealWorld(int world, GameObject shieldGO)
        {
            // Carousel's own Update() re-applies every badge's scale from its distance-to-centre
            // every frame — left enabled, it would fight this routine's scale-to-zero tween for the
            // same RectTransform.localScale and the shrink would never visibly happen. Re-enabled at
            // the top of ShowWorldSelect on next open, not here.
            worldCarousel.enabled = false;
            var shieldRect = (RectTransform)shieldGO.transform;
            var canvasGroup = shieldGO.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = shieldGO.AddComponent<CanvasGroup>();
            }
            Vector3 startScale = shieldRect.localScale;

            float t = 0f;
            while (t < ShieldRevealSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / ShieldRevealSeconds);
                shieldRect.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                canvasGroup.alpha = 1f - p;
                yield return null;
            }

            _selectedWorld = world;
            worldShieldContainer.gameObject.SetActive(false);
            SetBackButtonSprite(backButtonTileGridSprite);

            SetWorldSignSprite(currentWorldIndicatorImage, world);
            currentWorldIndicator.gameObject.SetActive(true);

            PopulateLevelGrid(world);
            scrollRect.gameObject.SetActive(true);
            ScrollToCurrentLevel(world);

            _shieldRevealRoutine = null;
        }

        private void SetWorldSignSprite(Image image, int world)
        {
            if (image != null && worldSignSprites != null && world >= 0 && world < worldSignSprites.Length)
            {
                image.sprite = worldSignSprites[world];
            }
        }

        /// <summary>Builds a single GridLayoutGroup section (4 columns, per the 2026-07-31 Canva
        /// mockup) under contentParent with
        /// just the given world's UnlockProgression.LevelsPerWorld (25) tiles — one world at a time
        /// now that world select handles picking which one, so no WorldDivider banners are needed
        /// here anymore (those made sense for a single continuous 100-tile scroll; this replaced
        /// that design). UnlockProgression.IsLevelUnlocked already treats a slot with no real
        /// LevelData as locked, so unauthored slots simply render locked rather than needing to be
        /// skipped here.</summary>
        private void PopulateLevelGrid(int world)
        {
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
            _tilesByIndex.Clear();

            var section = new GameObject($"World{world + 1}Section", typeof(RectTransform));
            section.transform.SetParent(contentParent, false);
            var grid = section.AddComponent<GridLayoutGroup>();
            // Shrunk from 150x150 per a follow-up mockup review (tiles were reading as oversized
            // against the header). padding.top guarantees at least 16px of clear space below the
            // header banner even before the ScrollView's own reserved 200px header offset.
            grid.cellSize = new Vector2(128f, 128f);
            grid.spacing = new Vector2(45f, 45f);
            grid.padding = new RectOffset(0, 0, 16, 0);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            int firstIndex = world * UnlockProgression.LevelsPerWorld;
            int lastIndex = Mathf.Min(firstIndex + UnlockProgression.LevelsPerWorld, UnlockProgression.TotalLevels);
            int tileCount = lastIndex - firstIndex;
            for (int levelIndex = firstIndex; levelIndex < lastIndex; levelIndex++)
            {
                InstantiateTile(levelIndex, section.transform);
            }

            // contentParent's own VerticalLayoutGroup has childControlHeight/Width = false (see
            // CreateVerticalScrollView), so it reads this section's size directly rather than
            // forcing one — an explicit LayoutElement guarantees ContentSizeFitter sees the full
            // multi-row height regardless of GridLayoutGroup's own ILayoutElement reporting, so the
            // ScrollRect always has real scrollable content instead of silently fitting on one screen.
            int rows = Mathf.CeilToInt(tileCount / (float)grid.constraintCount);
            var sectionLayout = section.AddComponent<LayoutElement>();
            sectionLayout.preferredWidth = grid.constraintCount * grid.cellSize.x + (grid.constraintCount - 1) * grid.spacing.x;
            sectionLayout.preferredHeight = grid.padding.vertical + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        }

        private void InstantiateTile(int levelIndex, Transform parent)
        {
            var tileGO = Instantiate(levelTilePrefab, parent);
            var tile = tileGO.GetComponent<LevelTileController>();
            tile.Initialise(levelIndex, OnTilePlayRequested, OnTileLockedTapped);
            _tilesByIndex[levelIndex] = tile;
        }

        private void OnTilePlayRequested(int levelIndex)
        {
            GameManager.Instance.LoadLevel(levelIndex);
            SceneTransitionManager.Instance.ShowOnly(gameplayScreen);
        }

        private void OnTileLockedTapped(int levelIndex)
        {
            lockedHintPanel?.Show(UnlockProgression.GetUnlockHint(levelIndex));
        }

        /// <summary>Smoothly scrolls so the highest-unlocked level within the given world sits
        /// centred in the viewport (clamped to that world's own 25-tile range, since a level from a
        /// different world might otherwise be "the" highest-unlocked overall). Forces an immediate
        /// layout rebuild first — GridLayoutGroup/VerticalLayoutGroup only compute child positions
        /// during Unity's own layout pass, which hasn't necessarily run yet on the same frame
        /// PopulateLevelGrid just instantiated everything.</summary>
        private void ScrollToCurrentLevel(int world)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);

            int firstIndex = world * UnlockProgression.LevelsPerWorld;
            int lastIndex = Mathf.Min(firstIndex + UnlockProgression.LevelsPerWorld, UnlockProgression.TotalLevels) - 1;
            int currentLevel = Mathf.Clamp(UnlockProgression.GetHighestUnlockedLevel(), firstIndex, lastIndex);

            if (!_tilesByIndex.TryGetValue(currentLevel, out var tile) || scrollRect == null)
            {
                return;
            }

            float targetNormalized = ComputeNormalizedScrollForTile((RectTransform)tile.transform);

            if (_scrollRoutine != null)
            {
                StopCoroutine(_scrollRoutine);
            }
            _scrollRoutine = StartCoroutine(ScrollTween(targetNormalized));
        }

        /// <summary>contentParent is anchored top-centre (pivot y=1), so a child's local Y position
        /// within it is <= 0 and grows more negative further down the list — this converts that into
        /// the [0,1] verticalNormalizedPosition ScrollRect expects (1 = top, 0 = bottom), offset so
        /// the target tile lands in the middle of the viewport rather than flush against the top.</summary>
        private float ComputeNormalizedScrollForTile(RectTransform tile)
        {
            Vector3 targetWorldCenter = tile.TransformPoint(tile.rect.center);
            Vector3 targetLocalInContent = contentParent.InverseTransformPoint(targetWorldCenter);

            float contentHeight = contentParent.rect.height;
            float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : contentHeight;
            float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
            if (maxScroll <= 0f)
            {
                return 1f;
            }

            float distanceFromTop = -targetLocalInContent.y;
            float desiredContentOffset = Mathf.Clamp(distanceFromTop - viewportHeight / 2f, 0f, maxScroll);
            return 1f - desiredContentOffset / maxScroll;
        }

        private IEnumerator ScrollTween(float targetNormalized)
        {
            float start = scrollRect.verticalNormalizedPosition;
            float t = 0f;
            while (t < ScrollTweenSeconds)
            {
                t += Time.unscaledDeltaTime;
                scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, targetNormalized, t / ScrollTweenSeconds);
                yield return null;
            }
            scrollRect.verticalNormalizedPosition = targetNormalized;
            _scrollRoutine = null;
        }

        /// <summary>One step back at a time, not always straight to Main Menu: from the tile grid
        /// (a world selected — Btn_back icon) it returns to world select; from world select itself
        /// (Btn_home icon) it leaves the screen entirely, same as before.</summary>
        public void OnBackButtonClicked()
        {
            if (_selectedWorld.HasValue)
            {
                ShowWorldSelect();
                return;
            }

            SaveManager.Instance?.SaveProgress();
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
        }

        private void SetBackButtonSprite(Sprite sprite)
        {
            if (backButtonImage != null && sprite != null)
            {
                backButtonImage.sprite = sprite;
            }
        }
    }
}
