using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// World Map screen. Just two nav buttons over the Map.png background art — Play opens Level
    /// Select (a scrollable 100-tile grid, LevelSelectController), Home returns to Main Menu.
    ///
    /// Play used to jump straight into whichever level the player would naturally continue on; now
    /// that Level Select exists as a real destination, that "pick where to continue" job belongs to
    /// LevelSelectController.ScrollToCurrentLevel instead, so Play just opens the screen. There is
    /// deliberately no intermediate "Matchup Screen" step in this flow (World Map -> Level Select ->
    /// Gameplay directly) — that screen was removed from this project after playtesting read it as
    /// tonally mismatched; see CLAUDE.md's "Removed: Matchup screen".
    ///
    /// Replaced an earlier scrolling level-marker strip (LevelMarker/StarDisplay) — see CLAUDE.md's
    /// World Map "known gap" note — that infrastructure is still built by Phase5ProjectBuilder but
    /// unused; Level Select is the real replacement for it now.
    /// </summary>
    public class WorldMapController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject levelSelectScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayTapped);
            homeButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnPlayTapped()
        {
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }
    }
}
