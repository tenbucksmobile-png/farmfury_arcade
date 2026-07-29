using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// World Map screen. Just two nav buttons over the Map.png background art — Play jumps
    /// straight into whichever level the player would naturally continue on (first unlocked level
    /// with no stars yet, falling back to the highest level reached), Home returns to Main Menu.
    /// Replaced an earlier scrolling level-marker strip (LevelMarker/StarDisplay) — see CLAUDE.md's
    /// World Map "known gap" note — that infrastructure is still built by Phase5ProjectBuilder for
    /// future multi-level content but is no longer wired into this screen.
    /// </summary>
    public class WorldMapController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject gameplayScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayTapped);
            homeButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnPlayTapped()
        {
            int highestReached = SaveManager.Instance.HighestLevelReached;
            int nextAvailable = -1;

            foreach (var level in DataManager.Instance.GetAllLevelData())
            {
                bool unlocked = level.levelNumber == 0 || level.levelNumber <= highestReached + 1;
                int stars = SaveManager.Instance.GetLevelStars(level.levelNumber);

                if (unlocked && stars == 0 && nextAvailable < 0)
                {
                    nextAvailable = level.levelNumber;
                }
            }

            int target = nextAvailable >= 0 ? nextAvailable : highestReached;

            GameManager.Instance.LoadLevel(target);
            SceneTransitionManager.Instance.ShowOnly(gameplayScreen);
        }
    }
}
