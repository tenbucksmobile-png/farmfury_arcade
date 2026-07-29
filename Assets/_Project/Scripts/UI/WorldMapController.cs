using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Renders one LevelMarker per LevelData DataManager knows about (not a hardcoded 100 — only a
    /// couple of levels exist so far; authoring the full GDD world map is a content task, this
    /// just needs to scale to however many LevelData assets exist). markerContainer sits inside a
    /// ScrollRect so it's "draggable/scrollable if levels don't fit on screen" via standard uGUI
    /// rather than custom camera-drag code. Auto-centres on the next unlocked level on show.
    /// </summary>
    public class WorldMapController : MonoBehaviour
    {
        [SerializeField] private Transform markerContainer;
        [SerializeField] private GameObject levelMarkerPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameplayScreen;

        private readonly List<LevelMarker> _markers = new List<LevelMarker>();

        private void Awake()
        {
            homeButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnEnable()
        {
            RefreshMarkers();
        }

        private void RefreshMarkers()
        {
            foreach (Transform child in markerContainer)
            {
                Destroy(child.gameObject);
            }
            _markers.Clear();

            int highestReached = SaveManager.Instance.HighestLevelReached;
            int nextAvailable = -1;

            foreach (var level in DataManager.Instance.GetAllLevelData())
            {
                var go = Instantiate(levelMarkerPrefab, markerContainer);
                var marker = go.GetComponent<LevelMarker>();

                // First level (0) is always unlocked; otherwise unlocked once the previous level
                // has been completed (levelNumber <= highestReached + 1).
                bool unlocked = level.levelNumber == 0 || level.levelNumber <= highestReached + 1;
                int stars = SaveManager.Instance.GetLevelStars(level.levelNumber);
                marker.Initialize(level.levelNumber, unlocked, stars, OnMarkerTapped);
                _markers.Add(marker);

                if (unlocked && stars == 0 && nextAvailable < 0)
                {
                    nextAvailable = level.levelNumber;
                }
            }

            CenterOnLevel(nextAvailable >= 0 ? nextAvailable : highestReached);
        }

        private void CenterOnLevel(int levelIndex)
        {
            if (scrollRect == null || _markers.Count <= 1)
            {
                return;
            }

            int index = _markers.FindIndex(m => m.LevelIndex == levelIndex);
            if (index < 0)
            {
                return;
            }

            float normalized = _markers.Count > 1 ? (float)index / (_markers.Count - 1) : 0f;
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        private void OnMarkerTapped(int levelIndex, bool unlocked)
        {
            if (!unlocked)
            {
                _markers.Find(m => m.LevelIndex == levelIndex)?.PlayLockedShake();
                return;
            }

            // The Matchup (VS card) screen was removed — tapping an unlocked level goes straight
            // into gameplay, no countdown.
            GameManager.Instance.LoadLevel(levelIndex);
            SceneTransitionManager.Instance.ShowOnly(gameplayScreen);
        }
    }
}
