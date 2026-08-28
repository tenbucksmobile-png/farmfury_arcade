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
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TextMeshProUGUI costText;

        private void Awake()
        {
            // Wired here, not by the editor-script builder — a listener added directly from
            // editor-script code doesn't survive a scene save/reload (UnityEvent's non-persistent
            // listeners aren't serialized), same pitfall SimpleClosePanel exists to work around
            // elsewhere in this project.
            reviveButton.onClick.AddListener(HandleRevive);
            declineButton.onClick.AddListener(HandleDecline);
            if (watchAdButton != null)
            {
                watchAdButton.onClick.AddListener(HandleWatchAd);
            }
        }

        /// <summary>Called by GameplayHUD in response to GameManager.OnReviveOffered. Disables the
        /// Revive button up front if the player can't afford it, rather than letting them tap it
        /// and discover that after the fact — GameManager.AcceptRevive would just no-op on
        /// insufficient funds, but a dead button is bad UX regardless of that safety net. Same
        /// "never show a dead button" rule applies to Watch Ad — hidden entirely unless AdManager
        /// actually has a rewarded ad ready to show.</summary>
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
            if (watchAdButton != null)
            {
                watchAdButton.gameObject.SetActive(
                    Core.AdManager.Instance != null && Core.AdManager.Instance.IsRewardedAdReady);
            }
            gameObject.SetActive(true);
        }

        /// <summary>Audit finding C6.3: watchAdButton's visibility used to be set once, in Show(),
        /// and never re-checked — if an ad finished loading in the background while the prompt was
        /// already open (a real, if narrow, timing window), the button never appeared until the
        /// player's next death. Update() only runs while this GameObject is active (Show()/
        /// HandleRevive/HandleDecline are the only places that toggle it), so this is a no-op cost
        /// the rest of the time — same per-frame re-check convention GameplayHUD's own
        /// skip-cooldown-via-ad button already uses.</summary>
        private void Update()
        {
            if (watchAdButton == null)
            {
                return;
            }
            bool ready = Core.AdManager.Instance != null && Core.AdManager.Instance.IsRewardedAdReady;
            if (watchAdButton.gameObject.activeSelf != ready)
            {
                watchAdButton.gameObject.SetActive(ready);
            }
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

        /// <summary>onResult only ever reports true from AdManager when the reward was actually
        /// granted (not just that the ad was shown/closed) — see AdManager.ShowRewardedAd's own doc
        /// comment. A declined/failed/incomplete ad leaves the prompt open exactly as it was, rather
        /// than dismissing or ending the run, so the player can still fall back to paying coins or
        /// tapping Decline.</summary>
        private void HandleWatchAd()
        {
            if (Core.AdManager.Instance == null)
            {
                return;
            }

            Core.AdManager.Instance.ShowRewardedAd("continue_after_death", rewarded =>
            {
                if (rewarded)
                {
                    GameManager.Instance?.AcceptReviveViaAd();
                    gameObject.SetActive(false);
                }
                else if (watchAdButton != null)
                {
                    // Ad may have exhausted itself (LevelPlay reloads automatically, but not
                    // instantly) — re-check readiness rather than leaving a dead button up.
                    watchAdButton.gameObject.SetActive(
                        Core.AdManager.Instance != null && Core.AdManager.Instance.IsRewardedAdReady);
                }
            });
        }
    }
}
