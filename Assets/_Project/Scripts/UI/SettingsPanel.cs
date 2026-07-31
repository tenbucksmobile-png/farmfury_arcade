using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Modal, reachable from Main Menu or Pause. Rebuilt to match a Canva mockup
    /// (2026-07-31): a 2-column grid of whole-plaque toggle buttons. Restore/Reset Progress were
    /// removed earlier (Restore was Phase 6/cloud-save scope with no real action; Reset's confirm
    /// sub-panel went with it). Music/SFX volume sliders were dropped in the mockup rebuild too —
    /// each plaque is now a single mute on/off tap, same as Vibration/Left-Handed, since the grid
    /// cells aren't large enough to host both a tap target and a drag target cleanly. Volume level
    /// itself still exists in SaveManager (MusicVolume/SfxVolume) for whenever a volume control
    /// gets a dedicated slot again; only the in-panel UI for it is gone.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Toggle leftHandedToggle;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            musicToggle.onValueChanged.AddListener(HandleMusicToggle);
            sfxToggle.onValueChanged.AddListener(HandleSfxToggle);
            vibrationToggle.onValueChanged.AddListener(v => SaveManager.Instance.VibrationOn = v);
            languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
            leftHandedToggle.onValueChanged.AddListener(v => SaveManager.Instance.LeftHanded = v);

            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }
        }

        public void Show()
        {
            RefreshFromSave();
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
            vibrationToggle.SetIsOnWithoutNotify(SaveManager.Instance.VibrationOn);
            leftHandedToggle.SetIsOnWithoutNotify(SaveManager.Instance.LeftHanded);
        }
    }
}
