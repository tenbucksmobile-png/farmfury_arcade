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
        [SerializeField] private TextMeshProUGUI coinBalanceText;
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
        [SerializeField] private RevivePromptController revivePrompt;
        [SerializeField] private Button skipCooldownButton;
        [SerializeField] private Button watchAdSkipCooldownButton;

        /// <summary>Monetisation: coin cost of the "skip cooldown" button next to the ability
        /// portrait — only enabled/shown while the active ability actually is on cooldown (see
        /// HandleAbilityCooldownChanged/RefreshActiveAbility).</summary>
        private const int SkipCooldownCoinsCost = 3;

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

        /// <summary>Seconds remaining at which the timer starts pulsing red — per feedback that
        /// the level time limit (GameManager.LevelTimeLimitSeconds) ending a run felt "random"
        /// with nothing warning it was about to happen; the countdown text itself was the only
        /// signal, easy to miss while focused on the maze.</summary>
        private const float TimerWarningThresholdSeconds = 15f;
        private static readonly Color TimerWarningColor = new Color(0.9f, 0.15f, 0.15f, 1f);
        private Color _timerNormalColor = Color.white;
        private bool _timerNormalColorCaptured;

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
            if (skipCooldownButton != null)
            {
                skipCooldownButton.onClick.AddListener(HandleSkipCooldownClicked);
            }
            if (watchAdSkipCooldownButton != null)
            {
                watchAdSkipCooldownButton.onClick.AddListener(HandleWatchAdSkipCooldownClicked);
            }
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
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnReviveOffered += HandleReviveOffered;
            }

            _lastObservedState = GameState.Playing;
            RefreshPortrait();
            UpdateScoreText();
            RefreshCoinBalanceText();
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
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnReviveOffered -= HandleReviveOffered;
            }
            if (_activeAbility != null)
            {
                _activeAbility.OnCooldownChanged -= HandleAbilityCooldownChanged;
            }
            StopReadyFlash();
        }

        private void HandleScoreChanged(int newScore) => _targetScore = newScore;
        private void HandleCharacterChanged(CharacterType previous, CharacterType next) => RefreshPortrait();
        private void HandleReviveOffered() => revivePrompt?.Show();

        /// <summary>Monetisation: spends SkipCooldownCoinsCost coins to zero the active ability's
        /// cooldown early. No-ops (silently — the button is only shown/interactable while genuinely
        /// on cooldown and affordable, see HandleAbilityCooldownChanged) if either check fails.</summary>
        private void HandleSkipCooldownClicked()
        {
            if (_activeAbility == null || _activeAbility.IsReady)
            {
                return;
            }
            if (SaveManager.Instance != null && SaveManager.Instance.SpendCoins(SkipCooldownCoinsCost))
            {
                _activeAbility.SkipCooldown();
            }
        }

        /// <summary>Monetisation (Phase 2, "extra ability charge"/"skip cooldown via ad" — per the
        /// Monetisation Build Plan doc, these are the same button: a Watch Ad alternative next to
        /// the coin-cost skip-cooldown button above, ad as the free option instead of spending
        /// coins). Same no-op guards as HandleSkipCooldownClicked; only calls SkipCooldown once
        /// AdManager confirms the reward actually fired (see AdManager.ShowRewardedAd's own doc
        /// comment on that distinction) — a closed-early/failed ad leaves the cooldown untouched.</summary>
        private void HandleWatchAdSkipCooldownClicked()
        {
            if (_activeAbility == null || _activeAbility.IsReady || AdManager.Instance == null)
            {
                return;
            }

            watchAdSkipCooldownButton.interactable = false;
            AdManager.Instance.ShowRewardedAd("skip_cooldown_via_ad", rewarded =>
            {
                if (rewarded && _activeAbility != null && !_activeAbility.IsReady)
                {
                    _activeAbility.SkipCooldown();
                }
                else if (watchAdSkipCooldownButton != null)
                {
                    // Ad closed early/failed, or nothing left to skip — re-check readiness rather
                    // than leaving a stuck-disabled button, same re-check convention
                    // RevivePromptController's own Watch Ad button uses.
                    watchAdSkipCooldownButton.interactable = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
                }
            });
        }

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
            RefreshCoinBalanceText();
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
            if (timerText == null)
            {
                return;
            }

            if (!_timerNormalColorCaptured)
            {
                _timerNormalColor = timerText.color;
                _timerNormalColorCaptured = true;
            }

            float remaining = Mathf.Max(0f, GameManager.LevelTimeLimitSeconds - GameManager.Instance.GetElapsedSeconds());
            timerText.text = FormatTime(remaining);

            if (remaining <= TimerWarningThresholdSeconds)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * FlashCyclesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
                timerText.color = Color.Lerp(_timerNormalColor, TimerWarningColor, pulse);
            }
            else
            {
                timerText.color = _timerNormalColor;
            }
        }

        /// <summary>Coin_Balance_Chip.png's right half is reserved for the number (see
        /// ArtWiringBuilder.WireMonetisationArt) — this is the only place the player's running coin
        /// balance is shown anywhere in the game; previously SaveManager.CoinBalance had no on-screen
        /// display at all, only ever surfaced indirectly via the Revive prompt's cost text/the skip-
        /// cooldown button's cost label. Polled every frame (same convention as RefreshTimerText/
        /// AnimateScoreTowardTarget) rather than event-driven, since SaveManager has no
        /// OnCoinBalanceChanged event to hook.</summary>
        private void RefreshCoinBalanceText()
        {
            if (coinBalanceText != null && SaveManager.Instance != null)
            {
                coinBalanceText.text = SaveManager.Instance.CoinBalance.ToString("N0");
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
            // The button shows a dedicated ability-icon sprite (per-character {Name}_ability.png)
            // rather than the character's plain portrait — falls back to portraitSprite for any
            // character without one yet (currently just Horace).
            var data = DataManager.Instance != null ? DataManager.Instance.GetCharacterData(CharacterManager.Instance.ActiveCharacter) : null;
            var iconSprite = data != null ? (data.abilityIconSprite != null ? data.abilityIconSprite : data.portraitSprite) : null;
            if (iconSprite != null)
            {
                characterPortrait.sprite = iconSprite;
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
                if (skipCooldownButton != null)
                {
                    skipCooldownButton.gameObject.SetActive(false);
                }
                if (watchAdSkipCooldownButton != null)
                {
                    watchAdSkipCooldownButton.gameObject.SetActive(false);
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

            // Monetisation: only worth showing while genuinely on cooldown and only tappable while
            // affordable — checked every tick (this handler already fires every frame during a
            // cooldown, see AbilityBase.UpdateCooldown) rather than once when the cooldown starts,
            // so a coin pickup mid-cooldown makes the button interactable immediately instead of
            // needing the cooldown to restart first.
            if (skipCooldownButton != null)
            {
                skipCooldownButton.gameObject.SetActive(remaining > 0f);
                skipCooldownButton.interactable = SaveManager.Instance != null &&
                    SaveManager.Instance.CoinBalance >= SkipCooldownCoinsCost;
            }
            // Never shown as a dead button — only while on cooldown AND a rewarded ad is actually
            // ready (same rule RevivePromptController's own Watch Ad button follows).
            if (watchAdSkipCooldownButton != null)
            {
                watchAdSkipCooldownButton.gameObject.SetActive(
                    remaining > 0f && AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady);
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
