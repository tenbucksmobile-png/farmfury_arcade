using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Direct "jump straight back to Main Menu" shortcut (2026-09-14) — placed next to the
    /// round back button on every Settings/Shop-family overlay (MenuHubScreen, SettingsPanel,
    /// LegalScreen, ShopController, CoinPurchaseScreen, CosmeticsChooserScreen, and both
    /// CosmeticPurchaseScreen instances for Hats/Trails/World Purchase), since reaching some of
    /// these from Main Menu takes several nested taps (e.g. Shop -> Cosmetics -> Hats) and
    /// previously required tapping Back the same number of times to return.
    ///
    /// None of these screens are SceneTransitionManager screenRoots — they're all plain overlays
    /// stacked on top of whatever screenRoot is actually active (Main Menu normally, or Gameplay if
    /// reached via Pause), so getting back to Main Menu needs two things together: a real screenRoot
    /// swap (ShowOnly, in case Gameplay is the active screenRoot) AND explicitly closing every
    /// overlay in the ancestor chain (ShowOnly has no idea any of them exist). The same fixed
    /// overlaysToClose list is wired onto every instance of this component — closing an overlay
    /// that was never actually open is a harmless no-op (SetActive(false) on an already-inactive
    /// object), so this doesn't need to know the exact ancestor chain for wherever it happens to be
    /// placed.</summary>
    public class GoHomeButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private GameObject[] overlaysToClose;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(GoHome);
            }
        }

        private void GoHome()
        {
            if (SceneTransitionManager.Instance == null || mainMenuScreen == null)
            {
                return;
            }

            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen, () =>
            {
                if (overlaysToClose == null)
                {
                    return;
                }
                foreach (var overlay in overlaysToClose)
                {
                    if (overlay == null)
                    {
                        continue;
                    }
                    var pause = overlay.GetComponent<PauseMenuController>();
                    if (pause != null)
                    {
                        pause.CloseForNavigation();
                    }
                    else
                    {
                        overlay.SetActive(false);
                    }
                }
            });
        }
    }
}
