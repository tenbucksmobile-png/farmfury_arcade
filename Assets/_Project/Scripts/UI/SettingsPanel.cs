using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Modal, reachable from Main Menu or Pause. Rebuilt to match a Canva mockup
    /// (2026-07-31): a 2-column grid of whole-plaque toggle buttons. The original Restore/Reset
    /// Progress buttons were removed in that pass (Restore meant cloud-save restore, Phase 6 scope
    /// with no real action at the time). The grid's 6th cell — left empty since then — is now used
    /// for IAP "Restore Purchases" instead (Monetisation Build Plan Phase 3; required by Apple for
    /// non-consumable products, good practice on Android too) — a different, now-real feature that
    /// happens to reuse the same empty slot, not a revival of the old cloud-save button. Music/SFX
    /// volume sliders were dropped in the mockup rebuild too — each plaque is now a single mute
    /// on/off tap, same as Vibration/Left-Handed, since the grid cells aren't large enough to host
    /// both a tap target and a drag target cleanly. Volume level itself still exists in SaveManager
    /// (MusicVolume/SfxVolume) for whenever a volume control gets a dedicated slot again; only the
    /// in-panel UI for it is gone.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Toggle leftHandedToggle;
        [SerializeField] private Button restorePurchasesButton;
        [SerializeField] private TextMeshProUGUI restorePurchasesLabel;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject mainMenuScreen;
        // Leaderboards moved here (top-right, Leaderboard.png sign) per feedback that Main Menu
        // should stay to just Play/Settings/Shop — Settings is reachable from both Main Menu and
        // Pause, so this keeps Leaderboards reachable from gameplay too, not just the landing page.
        [SerializeField] private Button leaderboardsButton;
        [SerializeField] private GameObject leaderboardsScreen;

        private const string RestoreIdleText = "Restore\nPurchases";

        private void Awake()
        {
            closeButton.onClick.AddListener(HandleHomeTapped);
            musicToggle.onValueChanged.AddListener(HandleMusicToggle);
            sfxToggle.onValueChanged.AddListener(HandleSfxToggle);
            vibrationToggle.onValueChanged.AddListener(v => SaveManager.Instance.VibrationOn = v);
            languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
            leftHandedToggle.onValueChanged.AddListener(v => SaveManager.Instance.LeftHanded = v);
            if (restorePurchasesButton != null)
            {
                restorePurchasesButton.onClick.AddListener(HandleRestorePurchasesTapped);
            }
            if (leaderboardsButton != null && leaderboardsScreen != null)
            {
                leaderboardsButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(leaderboardsScreen));
            }

            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }
        }

        /// <summary>Restore is a plain action button, not a toggle — reports through the plaque's
        /// own label text (Restoring... -> Restored! / No purchases found) rather than a separate
        /// toast, since this panel has no toast system and the label is already right there.</summary>
        private void HandleRestorePurchasesTapped()
        {
            if (IAPManager.Instance == null)
            {
                return;
            }

            restorePurchasesButton.interactable = false;
            if (restorePurchasesLabel != null)
            {
                restorePurchasesLabel.text = "Restoring...";
            }

            IAPManager.Instance.RestorePurchases(success =>
            {
                restorePurchasesButton.interactable = true;
                if (restorePurchasesLabel != null)
                {
                    restorePurchasesLabel.text = success ? "Restored!" : RestoreIdleText;
                }
            });
        }

        public void Show()
        {
            RefreshFromSave();
            gameObject.SetActive(true);
        }

        /// <summary>Btn_home always returns to the landing page (Main Menu), regardless of whether
        /// Settings was opened from Main Menu or from Pause — matching the button's "home" icon
        /// rather than just closing back to wherever it was opened from.</summary>
        private void HandleHomeTapped()
        {
            gameObject.SetActive(false);
            if (mainMenuScreen != null)
            {
                SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
            }
        }

        private void HandleMusicToggle(bool on)
        {
            SaveManager.Instance.MusicOn = on;
            AudioManager.Instance?.SetMusicMuted(!on);
        }

        private void HandleSfxToggle(bool on)
        {
            SaveManager.Instance.SfxOn = on;
            AudioManager.Instance?.SetSFXMuted(!on);
        }

        private void HandleLanguageChanged(int index)
        {
            if (index >= 0 && index < languageDropdown.options.Count)
            {
                SaveManager.Instance.Language = languageDropdown.options[index].text;
            }
        }

        private void RefreshFromSave()
        {
            musicToggle.SetIsOnWithoutNotify(SaveManager.Instance.MusicOn);
            sfxToggle.SetIsOnWithoutNotify(SaveManager.Instance.SfxOn);
            vibrationToggle.SetIsOnWithoutNotify(SaveManager.Instance.VibrationOn);
            leftHandedToggle.SetIsOnWithoutNotify(SaveManager.Instance.LeftHanded);
        }
    }
}
