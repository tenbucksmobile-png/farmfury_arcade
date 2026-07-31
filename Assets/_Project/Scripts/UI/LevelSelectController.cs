using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Level Select screen. Opens into a "world select" state showing a horizontally flickable
    /// carousel (CardCarouselController) of world badges — one per currently-available world (Corn
    /// Field only, on a fresh save — Vegetable Patch/Orchard/Wheat Field badges only appear once
    /// their gate level, per UnlockProgression, has 2+ stars). Flicking cycles which badge sits
    /// centred/full-scale; tapping the already-centred badge reveals that world's level tile grid
    /// and shrinks the badge down into a small persistent indicator top-left of the screen; tapping
    /// that indicator again returns to world select. Reached from World Map's Play button (see
    /// WorldMapController.levelSelectScreen) instead of jumping straight into gameplay.
    ///
    /// Tapping an unlocked tile loads that level and shows the gameplay screen directly
    /// (GameManager.LoadLevel + SceneTransitionManager.ShowOnly) — the same two calls
    /// WorldMapController.OnPlayTapped already used before this screen existed. There is
    /// deliberately no "Matchup Screen" step: that screen (a VS-card + 3-2-1-GO countdown between
    /// World Map and Gameplay) was removed from this project after playtesting read it as tonally
    /// mismatched (see CLAUDE.md's "Removed: Matchup screen"), so this doesn't resurrect it.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [SerializeField] private GameObject levelTilePrefab;
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private TextMeshProUGUI starCounter;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private LockedHintPanel lockedHintPanel;
        [SerializeField] private Button backButton;
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

        private readonly Dictionary<int, LevelTileController> _tilesByIndex = new Dictionary<int, LevelTileController>();
        private readonly List<int> _availableWorlds = new List<int>();
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
        /// world — the star counter and which world shields are available can both have changed
        /// since the player was last here.</summary>
        public void OpenLevelSelect()
        {
            UpdateStarCounter();
            ShowWorldSelect();
        }

        /// <summary>Shows one badge per currently-available world in the carousel and hides the tile
        /// grid. World 0 (Corn Field) is always available; world N beyond that only once its gate
        /// level (the last level of world N-1) has 2+ stars — the same threshold
        /// UnlockProgression.IsLevelUnlocked already gates level access on, so "the world is
        /// selectable" and "its levels are reachable" never disagree.</summary>
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

            foreach (Transform child in worldShieldContainer)
            {
                Destroy(child.gameObject);
            }
            worldCarousel.enabled = true;
            worldCarousel.ClearItems();
            _availableWorlds.Clear();
            _shieldObjects.Clear();
            worldShieldContainer.gameObject.SetActive(true);

            int worldCount = Mathf.CeilToInt((float)UnlockProgression.TotalLevels / UnlockProgression.LevelsPerWorld);
            for (int world = 0; world < worldCount; world++)
            {
                if (!IsWorldAvailable(world))
                {
                    continue;
                }

                var shieldGO = Instantiate(worldShieldPrefab, worldShieldContainer);
                SetWorldSignSprite(shieldGO.GetComponent<Image>(), world);
                _availableWorlds.Add(world);
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
            int startLocalIndex = Mathf.Max(0, _availableWorlds.IndexOf(Mathf.Clamp(highestWorld, 0, worldCount - 1)));
            worldCarousel.SetItems(rects, buttons, startLocalIndex, OnCarouselCenterTapped);
        }

        private void OnCarouselCenterTapped(int localIndex)
        {
            if (localIndex < 0 || localIndex >= _availableWorlds.Count)
            {
                return;
            }
            SelectWorld(_availableWorlds[localIndex], _shieldObjects[localIndex]);
        }

        /// <summary>World 0 is always available. World N (N>0) becomes available once the last
        /// level of world N-1 has 2+ stars — matching UnlockProgression's own world-gate threshold
        /// for level access, so a shield is never shown for a world whose levels are actually still
        /// locked.</summary>
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
            grid.cellSize = new Vector2(150f, 150f);
            grid.spacing = new Vector2(20f, 20f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            int firstIndex = world * UnlockProgression.LevelsPerWorld;
            int lastIndex = Mathf.Min(firstIndex + UnlockProgression.LevelsPerWorld, UnlockProgression.TotalLevels);
            for (int levelIndex = firstIndex; levelIndex < lastIndex; levelIndex++)
            {
                InstantiateTile(levelIndex, section.transform);
            }
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

        public void UpdateStarCounter()
        {
            if (starCounter != null)
            {
                starCounter.text = $"{UnlockProgression.GetTotalStars()} ★";
            }
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

        public void OnBackButtonClicked()
        {
            SaveManager.Instance?.SaveProgress();
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
        }
    }
}
