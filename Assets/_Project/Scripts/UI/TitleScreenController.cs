using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Attract-mode title screen (2026-08-30) — shown once at launch, before Main Menu.
    /// Tap anywhere to continue. Built new to use FFArcade_Icon.png/PressStart.png, which landed
    /// with no existing slot to wire into — there was no title/splash screen in the flow before
    /// this (Main Menu opened directly). The PRESS START prompt pulses via a plain alpha lerp,
    /// same "sine-wave pulse" convention GameplayHUD's ability-ready flash uses.</summary>
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private Button tapButton;
        [SerializeField] private CanvasGroup pressStartGroup;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private float pulseSecondsPerCycle = 1.2f;

        private void Awake()
        {
            if (tapButton != null)
            {
                tapButton.onClick.AddListener(HandleTap);
            }
        }

        private void OnEnable()
        {
            // Same track Main Menu itself starts on OnEnable — the title screen hands off to Main
            // Menu without a music restart since both play the same clip.
            AudioManager.Instance?.PlayLandingMusic();
        }

        private void Update()
        {
            if (pressStartGroup == null)
            {
                return;
            }
            float t = Mathf.PingPong(Time.unscaledTime, pulseSecondsPerCycle) / pulseSecondsPerCycle;
            pressStartGroup.alpha = Mathf.Lerp(0.35f, 1f, t);
        }

        private void HandleTap()
        {
            if (mainMenuScreen != null && SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
            }
        }
    }
}
