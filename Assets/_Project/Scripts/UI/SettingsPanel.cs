using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Modal, reachable from Main Menu or Pause. "Restore Progress" is explicitly Phase 6
    /// scope per spec ("from cloud — Phase 6") — its button just logs, no real action yet.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Toggle leftHandedToggle;
        [SerializeField] private Button restoreProgressButton;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private GameObject resetConfirmPanel;
        [SerializeField] private Button confirmResetButton;
        [SerializeField] private Button cancelResetButton;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            musicToggle.onValueChanged.AddListener(HandleMusicToggle);
            sfxToggle.onValueChanged.AddListener(HandleSfxToggle);
            musicVolumeSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));
            sfxVolumeSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));
            vibrationToggle.onValueChanged.AddListener(v => SaveManager.Instance.VibrationOn = v);
            languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
            leftHandedToggle.onValueChanged.AddListener(v => SaveManager.Instance.LeftHanded = v);
            restoreProgressButton.onClick.AddListener(() =>
                Debug.Log("[SettingsPanel] Restore Progress is Phase 6 scope (cloud save)."));
            resetProgressButton.onClick.AddListener(() => resetConfirmPanel.SetActive(true));
            confirmResetButton.onClick.AddListener(ConfirmReset);
            cancelResetButton.onClick.AddListener(() => resetConfirmPanel.SetActive(false));

            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }
        }

        public void Show()
        {
            RefreshFromSave();
            resetConfirmPanel.SetActive(false);
            gameObject.SetActive(true);
        }

        private void Hide() => gameObject.SetActive(false);

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
            musicVolumeSlider.SetValueWithoutNotify(SaveManager.Instance.MusicVolume);
            sfxVolumeSlider.SetValueWithoutNotify(SaveManager.Instance.SfxVolume);
            vibrationToggle.SetIsOnWithoutNotify(SaveManager.Instance.VibrationOn);
            leftHandedToggle.SetIsOnWithoutNotify(SaveManager.Instance.LeftHanded);
        }

        private void ConfirmReset()
        {
            SaveManager.Instance.ResetAllProgress();
            resetConfirmPanel.SetActive(false);
            RefreshFromSave();
        }
    }
}
