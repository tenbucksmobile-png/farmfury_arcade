using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Abilities;
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
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image characterPortrait;
        [SerializeField] private Button abilityButton;
        [SerializeField] private Image abilityCooldownRing;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject powerPelletTimerBar;
        [SerializeField] private Image powerPelletTimerFill;
        [SerializeField] private GameObject chainCounterRoot;
        [SerializeField] private TextMeshProUGUI chainCounterText;
        [SerializeField] private CharacterSwapUI characterSwapUI;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private GameObject levelCompleteScreen;
        [SerializeField] private GameObject levelFailedScreen;

        private static readonly int[] ChainPoints = { 200, 400, 800, 1600 };

        private int _displayedScore;
        private int _targetScore;
        private GameState _lastObservedState;

        private void Awake()
        {
            abilityButton.onClick.AddListener(ActivateAbility);
            swapButton.onClick.AddListener(() => characterSwapUI.ToggleOpen());
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
            RefreshLevelAndTimerText();
            UpdateAbilityCooldownRing();
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
            if (characterPortrait == null)
            {
                return;
            }

            var activeObject = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            var sr = activeObject != null ? activeObject.GetComponent<SpriteRenderer>() : null;
            if (sr != null)
            {
                characterPortrait.color = sr.color;
            }
        }

        private void UpdateAbilityCooldownRing()
        {
            if (abilityCooldownRing == null)
            {
                return;
            }

            var ability = GetActiveAbility();
            abilityCooldownRing.fillAmount = ability != null && ability.TotalCooldown > 0f
                ? ability.CooldownRemaining / ability.TotalCooldown
                : 0f;
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

        private void ActivateAbility()
        {
            GetActiveAbility()?.TryActivate();
        }

        private static AbilityBase GetActiveAbility()
        {
            var obj = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            return obj != null ? obj.GetComponent<AbilityBase>() : null;
        }

        private void OpenPauseMenu()
        {
            GameManager.Instance.PauseGame();
            pauseMenu.Show();
        }
    }
}
