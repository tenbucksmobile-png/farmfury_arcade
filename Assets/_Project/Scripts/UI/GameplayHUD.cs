using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

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
        [SerializeField] private Image abilityCooldownRing;
        [SerializeField] private Button abilityButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject powerPelletTimerBar;
        [SerializeField] private Image powerPelletTimerFill;
        [SerializeField] private GameObject chainCounterRoot;
        [SerializeField] private TextMeshProUGUI chainCounterText;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private GameObject levelCompleteScreen;
        [SerializeField] private GameObject levelFailedScreen;

        private static readonly int[] ChainPoints = { 200, 400, 800, 1600 };

        /// <summary>Portrait tint while its ability is on cooldown — the "greying out" cue for the
        /// on-screen ability button (see RefreshPortrait/RefreshActiveAbility). Distinct from the
        /// "ready" colour, which is white for a wired real portraitSprite or the gold placeholder
        /// tint otherwise (see RefreshPortrait's own comment on that).</summary>
        private static readonly Color AbilityCooldownTint = new Color(0.4f, 0.4f, 0.4f, 1f);

        /// <summary>Pulse colour the portrait flashes toward once its ability is fully off cooldown
        /// — a bright gold, distinct enough from both the grey cooldown tint and the plain white/gold
        /// "ready" colour to read as "flashing" rather than a static tint.</summary>
        private static readonly Color AbilityFlashColor = new Color(1f, 0.95f, 0.3f, 1f);
        private const float FlashCyclesPerSecond = 2f;

        private Coroutine _readyFlashRoutine;
        private int _displayedScore;
        private int _targetScore;
        private GameState _lastObservedState;
        private AbilityBase _activeAbility;
        private Color _portraitReadyColor = Color.white;

        private void Awake()
        {
            pauseButton.onClick.AddListener(OpenPauseMenu);
            // The portrait doubles as the on-screen ability button (Space has no touch
            // equivalent) — wired here, not in the editor-script builder, since a listener added
            // directly from editor-script code doesn't survive a scene save/reload.
            abilityButton.onClick.AddListener(InputController.RaiseAbilityActivateInput);
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
            if (_activeAbility != null)
            {
                _activeAbility.OnCooldownChanged -= HandleAbilityCooldownChanged;
            }
            StopReadyFlash();
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
                float remaining = Mathf.Max(0f, GameManager.LevelTimeLimitSeconds - GameManager.Instance.GetElapsedSeconds());
                timerText.text = FormatTime(remaining);
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
            }

            // Image.color (the tint this ability-cooldown indicator multiplies) defaults to white
            // regardless of whether a real portraitSprite or the gold-tinted placeholder texture is
            // showing — the placeholder's gold comes from the sprite's own pixels, not Image.color.
            _portraitReadyColor = Color.white;
            RefreshActiveAbility();
        }

        /// <summary>The portrait doubles as the on-screen ability button (see Phase5ProjectBuilder's
        /// BuildGameplayHUD — Space has no touch equivalent, so this is the only way to activate an
        /// ability on a device with no keyboard). AbilityBase lives on the active character's
        /// GameObject, which CharacterManager destroys/recreates on every swap, so it's re-fetched
        /// live here rather than cached across swaps — same convention as CameraFollow's target and
        /// RobotBase.playerMovement.</summary>
        private void RefreshActiveAbility()
        {
            if (_activeAbility != null)
            {
                _activeAbility.OnCooldownChanged -= HandleAbilityCooldownChanged;
            }
            StopReadyFlash();

            var characterObject = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            _activeAbility = characterObject != null ? characterObject.GetComponent<AbilityBase>() : null;

            if (_activeAbility != null)
            {
                _activeAbility.OnCooldownChanged += HandleAbilityCooldownChanged;
                HandleAbilityCooldownChanged(_activeAbility.CooldownRemaining, _activeAbility.TotalCooldown);
            }
            else
            {
                characterPortrait.color = _portraitReadyColor;
                if (abilityCooldownRing != null)
                {
                    abilityCooldownRing.fillAmount = 1f;
                }
            }
        }

        /// <summary>Portrait greys out the instant the ability is used and the ring drains to empty;
        /// both fill/lighten back up in step with the cooldown (see AbilityBase.UpdateCooldown, which
        /// fires this every frame while on cooldown). The moment the cooldown actually reaches zero,
        /// the portrait starts a continuous flash (see StartReadyFlash) rather than just sitting at a
        /// static "ready" colour, so the player gets a clear "you can use this now" cue instead of
        /// having to notice the ring quietly finished filling.</summary>
        private void HandleAbilityCooldownChanged(float remaining, float total)
        {
            if (characterPortrait == null)
            {
                return;
            }

            if (remaining > 0f)
            {
                StopReadyFlash();
                characterPortrait.color = AbilityCooldownTint;
            }
            else if (_readyFlashRoutine == null)
            {
                StartReadyFlash();
            }

            if (abilityCooldownRing != null)
            {
                abilityCooldownRing.fillAmount = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 1f;
            }
        }

        private void StartReadyFlash()
        {
            StopReadyFlash();
            _readyFlashRoutine = StartCoroutine(ReadyFlashRoutine());
        }

        /// <summary>Also resets the portrait back to its plain ready colour — called both when the
        /// ability is used again (cooldown restarts) and from OnDisable/RefreshActiveAbility so a
        /// leftover flash coroutine never keeps running against a portrait that no longer belongs to
        /// the active ability (e.g. after a character swap).</summary>
        private void StopReadyFlash()
        {
            if (_readyFlashRoutine != null)
            {
                StopCoroutine(_readyFlashRoutine);
                _readyFlashRoutine = null;
            }
            if (characterPortrait != null)
            {
                characterPortrait.color = _portraitReadyColor;
            }
        }

        private IEnumerator ReadyFlashRoutine()
        {
            while (true)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * FlashCyclesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
                characterPortrait.color = Color.Lerp(_portraitReadyColor, AbilityFlashColor, pulse);
                yield return null;
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
