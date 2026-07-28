using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// The always-on-during-Playing HUD. Also doubles as the trigger point for the
    /// Gameplay -> LevelComplete/LevelFailed transition: it's the one thing guaranteed active
    /// (Update keeps running even while Time.timeScale == 0 during Pause) whenever GameManager's
    /// state changes away from Playing, so it's the natural single place to react to that instead
    /// of scattering "watch for LevelComplete" checks across multiple controllers.
    ///
    /// Swap/Ability buttons were removed from the HUD in the gameplay-screen cleanup — Tab
    /// (CharacterSwapUI) and Space (AbilityBase.OnAbilityActivateInput) still work directly via
    /// InputController, so neither feature is actually gone, just no longer duplicated as on-screen
    /// buttons here. Pause/Sound/Home are now a single bottom-left icon cluster (160x160 each,
    /// matching the Main Menu's Play/Settings buttons) instead of being scattered across the
    /// screen. The vacant Btn_plaque backdrop that used to run down the right side ("SideBackdrop")
    /// was removed entirely — it had no behaviour and read as an oversized, unexplained button.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private Image soundButtonIcon;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject powerPelletTimerBar;
        [SerializeField] private Image powerPelletTimerFill;
        [SerializeField] private GameObject chainCounterRoot;
        [SerializeField] private TextMeshProUGUI chainCounterText;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private GameObject levelCompleteScreen;
        [SerializeField] private GameObject levelFailedScreen;

        private static readonly int[] ChainPoints = { 200, 400, 800, 1600 };

        private int _displayedScore;
        private int _targetScore;
        private GameState _lastObservedState;

        private void Awake()
        {
            pauseButton.onClick.AddListener(OpenPauseMenu);
            soundButton.onClick.AddListener(ToggleSound);
            homeButton.onClick.AddListener(QuitToHome);
        }

        private void OnEnable()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
                _displayedScore = _targetScore = ScoreManager.Instance.CurrentMazeScore;
            }
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterChanged += HandleCharacterChanged;
            }

            _lastObservedState = GameState.Playing;
            RefreshPortrait();
            RefreshSoundIcon();
            UpdateScoreText();
        }

        private void OnDisable()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
            }
            if (CharacterManager.Instance != null)
            {
                CharacterManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
            }
        }

        private void HandleScoreChanged(int newScore) => _targetScore = newScore;
        private void HandleCharacterChanged(CharacterType previous, CharacterType next) => RefreshPortrait();

        private void Update()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            var state = GameManager.Instance.CurrentState;
            if (state != _lastObservedState)
            {
                _lastObservedState = state;
                HandleStateChanged(state);
            }

            if (state != GameState.Playing && state != GameState.Paused)
            {
                return;
            }

            AnimateScoreTowardTarget();
            RefreshLevelAndTimerText();
            UpdatePowerPelletUI();
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.LevelComplete)
            {
                SceneTransitionManager.Instance.ShowOnly(levelCompleteScreen);
            }
            else if (newState == GameState.LevelFailed)
            {
                SceneTransitionManager.Instance.ShowOnly(levelFailedScreen);
            }
        }

        private void AnimateScoreTowardTarget()
        {
            if (_displayedScore == _targetScore)
            {
                return;
            }

            float step = Mathf.Max(50f, Mathf.Abs(_targetScore - _displayedScore)) * Time.unscaledDeltaTime * 4f;
            _displayedScore = Mathf.RoundToInt(Mathf.MoveTowards(_displayedScore, _targetScore, step));
            UpdateScoreText();
        }

        private void UpdateScoreText()
        {
            if (scoreText != null)
            {
                scoreText.text = _displayedScore.ToString("N0");
            }
        }

        private void RefreshLevelAndTimerText()
        {
            if (levelText != null && GameManager.Instance.CurrentLevel != null)
            {
                levelText.text = GameManager.Instance.CurrentLevel.levelName;
            }
            if (timerText != null)
            {
                timerText.text = FormatTime(GameManager.Instance.GetElapsedSeconds());
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private void RefreshPortrait()
        {
            if (characterPortrait == null || CharacterManager.Instance == null)
            {
                return;
            }

            // Used to just tint the placeholder square with the active character's SpriteRenderer
            // color (near-white for every character, since art is wired via .sprite not .color) —
            // that left a plain gold placeholder block on screen even after CharacterData.
            // portraitSprite got wired for every character with real art (see ArtWiringBuilder).
            // Same tint-vs-real-art convention as RobotVisual.BaseTintColor: show the real portrait
            // at full white once one exists, otherwise fall back to the gold placeholder tint.
            var data = DataManager.Instance != null ? DataManager.Instance.GetCharacterData(CharacterManager.Instance.ActiveCharacter) : null;
            if (data != null && data.portraitSprite != null)
            {
                characterPortrait.sprite = data.portraitSprite;
                characterPortrait.color = Color.white;
            }
        }

        private void UpdatePowerPelletUI()
        {
            bool active = PowerPelletManager.Instance != null && PowerPelletManager.Instance.IsPowerActive;

            if (powerPelletTimerBar != null) powerPelletTimerBar.SetActive(active);
            if (chainCounterRoot != null) chainCounterRoot.SetActive(active);
            if (!active)
            {
                return;
            }

            if (powerPelletTimerFill != null)
            {
                float duration = Mathf.Max(0.01f, PowerPelletManager.Instance.ActivatedDuration);
                powerPelletTimerFill.fillAmount = Mathf.Clamp01(PowerPelletManager.Instance.TimeRemaining / duration);
            }

            if (chainCounterText != null && ChaseScoreManager.Instance != null)
            {
                int chain = ChaseScoreManager.Instance.ChainCount;
                chainCounterText.text = chain < ChainPoints.Length ? ChainPoints[chain].ToString() : "MAX";
            }
        }

        private void OpenPauseMenu()
        {
            GameManager.Instance.PauseGame();
            pauseMenu.Show();
        }

        /// <summary>Single icon toggles both music and SFX together — the HUD only needs one
        /// "sound on/off" concept, unlike SettingsPanel's separate Music/SFX toggles. Uses
        /// SaveManager.MusicOn as the on/off state for the icon since both are muted/unmuted in
        /// lockstep here.</summary>
        private void ToggleSound()
        {
            bool goingOff = SaveManager.Instance != null && SaveManager.Instance.MusicOn;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicMuted(goingOff);
                AudioManager.Instance.SetSFXMuted(goingOff);
            }

            RefreshSoundIcon();
        }

        private void RefreshSoundIcon()
        {
            if (soundButtonIcon == null)
            {
                return;
            }

            bool on = SaveManager.Instance == null || SaveManager.Instance.MusicOn;
            soundButtonIcon.sprite = on ? soundOnSprite : soundOffSprite;
        }

        /// <summary>Same semantics as PauseMenuController.QuitToMenu — quitting mid-run counts as
        /// a failed attempt (EndLevel(false)); GameplayHUD's own state-change watcher then shows
        /// LevelFailedController, whose own Home button is the one that actually returns to World
        /// Map. This button just skips the pause-menu detour to get there.</summary>
        private void QuitToHome()
        {
            Time.timeScale = 1f;
            GameManager.Instance.EndLevel(false);
        }
    }
}
