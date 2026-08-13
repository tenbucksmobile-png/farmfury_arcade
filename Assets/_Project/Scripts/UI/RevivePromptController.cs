using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Overlay shown by GameplayHUD when GameManager.OnReviveOffered fires (the 4th death this
    /// maze, which would otherwise end the run) — offers spending GameManager.ReviveCoinsCost
    /// coins for one more life. Same "layered on top of Gameplay, not through
    /// SceneTransitionManager" overlay convention as Pause/Settings — GameplayHUD owns showing
    /// it (via Show()) rather than this component reaching for GameManager's event itself, since
    /// GameplayHUD's OnEnable/OnDisable already reliably brackets every window a death could
    /// possibly happen in (an Awake-time subscription here would risk firing before/after that
    /// window on an object that starts inactive).
    /// </summary>
    public class RevivePromptController : MonoBehaviour
    {
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private TextMeshProUGUI costText;

        private void Awake()
        {
            // Wired here, not by the editor-script builder — a listener added directly from
            // editor-script code doesn't survive a scene save/reload (UnityEvent's non-persistent
            // listeners aren't serialized), same pitfall SimpleClosePanel exists to work around
            // elsewhere in this project.
            reviveButton.onClick.AddListener(HandleRevive);
            declineButton.onClick.AddListener(HandleDecline);
        }

        /// <summary>Called by GameplayHUD in response to GameManager.OnReviveOffered. Disables the
        /// Revive button up front if the player can't afford it, rather than letting them tap it
        /// and discover that after the fact — GameManager.AcceptRevive would just no-op on
        /// insufficient funds, but a dead button is bad UX regardless of that safety net.</summary>
        public void Show()
        {
            if (costText != null)
            {
                costText.text = $"Revive for {GameManager.ReviveCoinsCost} coins?";
            }
            if (reviveButton != null)
            {
                reviveButton.interactable = SaveManager.Instance != null &&
                    SaveManager.Instance.CoinBalance >= GameManager.ReviveCoinsCost;
            }
            gameObject.SetActive(true);
        }

        private void HandleRevive()
        {
            GameManager.Instance?.AcceptRevive();
            gameObject.SetActive(false);
        }

        private void HandleDecline()
        {
            GameManager.Instance?.DeclineRevive();
            gameObject.SetActive(false);
        }
    }
}
