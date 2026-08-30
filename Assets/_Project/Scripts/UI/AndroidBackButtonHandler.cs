using UnityEngine;
using UnityEngine.InputSystem;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Audit finding C3.2: no screen in the project had any handler for Android's hardware/
    /// gesture back button — Unity's Input System maps it to the same Escape key used by desktop's
    /// own Escape, but nothing was listening for it. Android's default behaviour on an unhandled
    /// back press is to finish() the Activity, meaning pressing back anywhere (mid-maze, in Pause,
    /// in Shop, in a purchase screen) very likely hard-quit the app with zero confirmation and lost
    /// run state — a real, undocumented divergence from iOS, which has no back-gesture equivalent.
    ///
    /// One centralized handler, not a per-screen one, closes whichever overlay is currently
    /// topmost — checked in priority order (innermost/most-recently-opened first) since these
    /// overlays can legitimately stack (e.g. a purchase screen open on top of the Shop hub on top
    /// of the Menu Hub). Each screen's existing close button already just does a plain
    /// gameObject.SetActive(false) (verified directly per screen before writing this, not assumed
    /// uniform) except ChooseCharacterScreen and PauseMenuController, which have real state to
    /// unwind on close — those go through their own real methods (ToggleOpen()/Resume()) instead of
    /// a raw SetActive so this handler can't skip that logic.
    ///
    /// Lives on GameManagers (wired in Phase5ProjectBuilder.WireCrossReferences, same pass that
    /// already wires every one of these screens' other cross-references) rather than being a
    /// per-screen component, since a single persistent Update() poll is cheaper and simpler to
    /// reason about than the same listener duplicated across ten screens.</summary>
    public class AndroidBackButtonHandler : MonoBehaviour
    {
        [SerializeField] private ParentalGateController parentalGate;
        [SerializeField] private CosmeticPurchaseScreen worldPurchaseScreen;
        [SerializeField] private CoinPurchaseScreen coinPurchaseScreen;
        [SerializeField] private CosmeticPurchaseScreen cosmeticsHubScreen;
        [SerializeField] private LegalScreen legalScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private ShopController shopController;
        [SerializeField] private MenuHubScreen menuHubScreen;
        [SerializeField] private ChooseCharacterScreen chooseCharacterScreen;
        [SerializeField] private PauseMenuController pauseMenuScreen;
        [SerializeField] private LevelSelectController levelSelectController;
        [SerializeField] private GameplayHUD gameplayHud;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Innermost overlays first — a purchase screen or the parental gate can be open on top
            // of the Shop hub, which can be open on top of the Menu Hub, and each step should only
            // close the one topmost layer, matching how a real back button behaves everywhere else.
            if (CloseIfActive(parentalGate)) return;
            if (CloseIfActive(worldPurchaseScreen)) return;
            if (CloseIfActive(coinPurchaseScreen)) return;
            if (CloseIfActive(cosmeticsHubScreen)) return;
            if (CloseIfActive(legalScreen)) return;
            if (CloseIfActive(settingsPanel)) return;
            if (CloseIfActive(shopController)) return;
            if (CloseIfActive(menuHubScreen)) return;

            if (chooseCharacterScreen != null && chooseCharacterScreen.gameObject.activeSelf)
            {
                chooseCharacterScreen.ToggleOpen();
                return;
            }
            if (pauseMenuScreen != null && pauseMenuScreen.gameObject.activeSelf)
            {
                pauseMenuScreen.Resume();
                return;
            }
            if (levelSelectController != null && levelSelectController.gameObject.activeSelf)
            {
                levelSelectController.OnBackButtonClicked();
                return;
            }
            // Mid-maze with no overlay already open is the single case the audit finding named
            // explicitly ("pressing back anywhere, including mid-maze... very likely hard-quits") —
            // open Pause instead of falling through to Android's default Activity-finish behaviour.
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing &&
                gameplayHud != null)
            {
                gameplayHud.OpenPauseMenu();
                return;
            }
            // At Main Menu (or anywhere none of the above matched) — deliberately left as Android's
            // own default for now rather than guessing at a "Quit?" confirmation UI that doesn't
            // exist yet; the goal here was closing the far larger "back mid-maze/mid-purchase
            // silently hard-quits" gap, not adding new UI.
        }

        private static bool CloseIfActive(MonoBehaviour screen)
        {
            if (screen == null || !screen.gameObject.activeSelf)
            {
                return false;
            }
            screen.gameObject.SetActive(false);
            return true;
        }
    }
}
