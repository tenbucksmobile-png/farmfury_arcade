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
    /// (ChooseCharacterScreen.ToggleOpen) and Space (AbilityBase.OnAbilityActivateInput) still work
    /// directly via InputController, so neither feature is actually gone, just no longer duplicated as on-screen
    /// buttons here. Sound and Home were removed from the HUD's icon cluster too (per playtest
    /// feedback) — both are still reachable via the Pause menu (Settings' music/SFX toggles, and
    /// Pause's own Quit button), so only PauseButton remains here now. The vacant Btn_plaque
    /// backdrop that used to run down the right side ("SideBackdrop") was removed entirely — it had
    /// no behaviour and read as an oversized, unexplained button.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Button pauseButton;
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
            RefreshTimerText();
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

        private void RefreshTimerText()
        {
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
    }
}
