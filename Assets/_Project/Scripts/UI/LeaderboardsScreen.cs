using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>Leaderboards world-select page (2026-09-12 redesign) — replaces the old flat
    /// "HighScore + a single plaque number" screen with a scattered collage of all 7 world banners
    /// over the FarmFury backdrop (fixed, randomized-once layout baked at Editor-build time — see
    /// Phase5ProjectBuilder.BuildLeaderboards' own doc comment for the placement algorithm), each
    /// tinted per its live unlock state every time this screen opens. Tapping an unlocked world opens
    /// WorldLeaderboardDetailScreen as an overlay on top (same "layers on top, never hidden"
    /// convention ChooseCharacterScreen uses over Pause); tapping a locked, non-purchase-gated world
    /// shows a brief hint via LockedHintPanel; tapping a locked, purchase-gated world (the 3
    /// Monetisation worlds) opens the World Purchase screen directly.
    ///
    /// Back button (2026-09-09): this screen is only ever reached via SettingsPanel's own
    /// Leaderboards icon (SettingsPanel.leaderboardsButton), which — since LeaderboardsScreen is a
    /// real SceneTransitionManager screenRoot, not an overlay — has to swap the active screenRoot
    /// away to get here, closing Settings (and whatever opened Settings) in the process. Fixed by
    /// restoring the mainMenu screenRoot AND reopening Settings (via MenuHubScreen, its own real
    /// opener) on top of it on the way back — see HandleBack.</summary>
    public class LeaderboardsScreen : MonoBehaviour
    {
        private static readonly Color LockedWorldTint = new Color(0.65f, 0.65f, 0.65f, 1f);

        [SerializeField] private Button backButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private MenuHubScreen menuHubScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("Index-aligned with UnlockProgression's own world numbering (0=Corn Field .. " +
                 "6=Harvest Moon) — see Phase5ProjectBuilder.BuildLeaderboards for the fixed " +
                 "scattered placement each of these was built at.")]
        [SerializeField] private Button[] worldButtons;

        [SerializeField] private LockedHintPanel lockedHintPanel;
        [SerializeField] private WorldLeaderboardDetailScreen detailScreen;
        [SerializeField] private CosmeticPurchaseScreen worldPurchaseScreen;

        private void Awake()
        {
            backButton.onClick.AddListener(HandleBack);
            if (worldButtons != null)
            {
                for (int i = 0; i < worldButtons.Length; i++)
                {
                    int world = i; // capture
                    if (worldButtons[i] != null)
                    {
                        worldButtons[i].onClick.AddListener(() => HandleWorldTapped(world));
                    }
                }
            }
        }

        private void OnEnable()
        {
            RefreshWorldTints();
        }

        /// <summary>Re-checked every time this screen opens, not just once — a world's unlock state
        /// can change mid-session (finishing the previous world's gate level, or a purchase), and
        /// the player might return here without the app restarting.</summary>
        private void RefreshWorldTints()
        {
            if (worldButtons == null)
            {
                return;
            }
            for (int world = 0; world < worldButtons.Length; world++)
            {
                if (worldButtons[world] == null)
                {
                    continue;
                }
                bool unlocked = UnlockProgression.IsWorldUnlocked(world);
                var image = worldButtons[world].GetComponent<Image>();
                if (image != null)
                {
                    image.color = unlocked ? Color.white : LockedWorldTint;
                }
            }
        }

        private void HandleWorldTapped(int world)
        {
            if (UnlockProgression.IsWorldUnlocked(world))
            {
                detailScreen?.Show(world);
                return;
            }

            if (UnlockProgression.IsPurchaseGatedWorld(world))
            {
                worldPurchaseScreen?.Show();
                return;
            }

            lockedHintPanel?.Show(UnlockProgression.GetUnlockHint(world * UnlockProgression.LevelsPerWorld));
        }

        private void HandleBack()
        {
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen, () =>
            {
                menuHubScreen?.Show();
                settingsPanel?.Show(menuHubScreen != null ? menuHubScreen.gameObject : null);
            });
        }
    }
}
