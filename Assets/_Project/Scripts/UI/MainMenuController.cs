using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Top-level menu. Stripped to just Play/Settings per the landing-page cleanup —
    /// Character Roster, Daily Challenge, Store, and Leaderboards no longer have an entry point
    /// here. Their screens/scripts are untouched (still built, still reachable if something else
    /// calls SceneTransitionManager.ShowOnly on them directly) — only the Main Menu buttons and
    /// this controller's references to them were removed.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;

        [SerializeField] private GameObject worldMapScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            playButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(worldMapScreen));
            settingsButton.onClick.AddListener(() => settingsPanel.Show());
        }
    }
}
