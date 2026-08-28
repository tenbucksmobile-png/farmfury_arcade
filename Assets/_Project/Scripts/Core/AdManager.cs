using UnityEngine;
using Unity.Services.LevelPlay;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Wraps the LevelPlay Unity SDK (mediating AdMob + Unity Ads — see CLAUDE.md's monetisation
    /// section for why LevelPlay over AdMob-direct: a prior app's iOS ad fill was too thin on a
    /// single network, worse under child-directed treatment which excludes most personalized/
    /// programmatic demand). Parallel to AudioManager: one singleton on GameManagers, owns all SDK
    /// interaction so gameplay code never touches Unity.Services.LevelPlay directly.
    ///
    /// Every ad request is configured as child-directed (COPPA) — the GDD's "8-45" target audience
    /// pulls in under-13 users, and treating the whole app as child-directed by default (rather than
    /// runtime age-gating individual users) was the deliberate, simpler-to-implement-correctly
    /// choice. SetMetaData calls for this must run before Init, per LevelPlay's own requirement.
    ///
    /// Rewarded and interstitial ads each auto-reload immediately after being shown/closed, so
    /// IsRewardedAdReady/IsInterstitialAdReady stay accurate for UI to poll before offering a
    /// "Watch Ad" option — never show a dead button.
    /// </summary>
    public class AdManager : Singleton<AdManager>
    {
        [Header("LevelPlay app key (per platform — from the LevelPlay dashboard's Apps page)")]
        [SerializeField] private string androidAppKey;
        [SerializeField] private string iosAppKey;

        [Header("Ad unit IDs (per platform — same dashboard, per app)")]
        [SerializeField] private string androidRewardedAdUnitId;
        [SerializeField] private string iosRewardedAdUnitId;
        [SerializeField] private string androidInterstitialAdUnitId;
        [SerializeField] private string iosInterstitialAdUnitId;

        [Tooltip("Enables LevelPlay's in-app test suite UI (SetMetaData \"is_test_suite\") in an " +
                 "Editor or Development Build only — see EnableTestSuite below, which forces this " +
                 "off in a release build regardless of what this Inspector value says. Toggle this " +
                 "freely for QA; it can no longer ship enabled by accident.")]
        [SerializeField] private bool enableTestSuite = true;

        [Tooltip("Levels between forced interstitials, per the GDD's 5-8 range.")]
        [SerializeField] private int interstitialLevelInterval = 6;

        /// <summary>Audit finding F6.3: enableTestSuite used to be a plain, never-automatically-
        /// gated Inspector bool — a release build could ship with LevelPlay's test-ad UI still
        /// active (zero real revenue, and a potential ad-network policy issue) if nobody remembered
        /// to flip it by hand. This compiles the release build's answer to a hardcoded false, so
        /// there's no longer a manual step to forget.</summary>
        private bool EnableTestSuite =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            enableTestSuite;
#else
            false;
#endif

        public bool IsInitialized { get; private set; }

        private LevelPlayRewardedAd _rewardedAd;
        private LevelPlayInterstitialAd _interstitialAd;

        private static string AppKey =>
#if UNITY_IOS
            Instance != null ? Instance.iosAppKey : null;
#else
            Instance != null ? Instance.androidAppKey : null;
#endif

        private string RewardedAdUnitId =>
#if UNITY_IOS
            iosRewardedAdUnitId;
#else
            androidRewardedAdUnitId;
#endif

        private string InterstitialAdUnitId =>
#if UNITY_IOS
            iosInterstitialAdUnitId;
#else
            androidInterstitialAdUnitId;
#endif

        private void Start()
        {
            if (string.IsNullOrEmpty(androidAppKey) && string.IsNullOrEmpty(iosAppKey))
            {
                Debug.LogWarning("[AdManager] No LevelPlay app key configured for either platform — " +
                                 "ads disabled until real IDs are wired in (see CLAUDE.md's monetisation plan).");
                return;
            }

            // Must run before Init — see class doc comment.
            LevelPlay.SetMetaData("is_child_directed", "true");
            LevelPlay.SetMetaData("is_deviceid_optout", "true");
            if (EnableTestSuite)
            {
                LevelPlay.SetMetaData("is_test_suite", "enable");
            }

            LevelPlay.OnInitSuccess += HandleInitSuccess;
            LevelPlay.OnInitFailed += HandleInitFailed;
            // Audit finding C8.2: this SDK boundary call had no containment — a malformed native
            // response or SDK misbehavior here would have propagated as an unhandled exception with
            // no crash reporting (C8.3) to ever surface it. Ads simply stay uninitialized on
            // failure rather than crashing the app.
            try
            {
                LevelPlay.Init(AppKey);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AdManager] LevelPlay.Init threw: {e}");
            }
        }

        private void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= HandleInitSuccess;
            LevelPlay.OnInitFailed -= HandleInitFailed;
        }

        private void HandleInitSuccess(LevelPlayConfiguration configuration)
        {
            IsInitialized = true;
            Debug.Log("[AdManager] LevelPlay initialized.");
            CreateRewardedAd();
            CreateInterstitialAd();
        }

        private void HandleInitFailed(LevelPlayInitError error)
        {
            IsInitialized = false;
            Debug.LogError($"[AdManager] LevelPlay init failed: {error}");
        }

        // ---- Rewarded ---------------------------------------------------------------------------

        /// <summary>Audit finding C6.1: the only place either ad type's LoadAd() was ever called
        /// again was inside OnAdClosed — which requires a full successful show-and-close cycle to
        /// fire. A single failed FIRST load (cold network, a momentary ad-network outage right at
        /// app start) left that ad type dead — IsRewardedAdReady/IsInterstitialAdReady false — for
        /// the rest of the session, with no error surfaced to the player (the "never show a dead
        /// button" logic just correctly hides the option) and no path to recovery short of
        /// restarting the app. Exponential backoff (5s/10s/20s/40s, capped at 60s, retrying
        /// indefinitely at the cap rather than giving up) fixes this for both ad types
        /// independently; the retry chain resets to the base delay the moment a load actually
        /// succeeds, via OnAdLoaded.</summary>
        private const float AdRetryBaseDelaySeconds = 5f;
        private const float AdRetryMaxDelaySeconds = 60f;

        private Coroutine _rewardedRetryRoutine;
        private float _rewardedRetryDelay = AdRetryBaseDelaySeconds;
        private Coroutine _interstitialRetryRoutine;
        private float _interstitialRetryDelay = AdRetryBaseDelaySeconds;

        private System.Collections.IEnumerator RetryLoadAfterDelay(System.Action loadAction, float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            loadAction?.Invoke();
        }

        private void CreateRewardedAd()
        {
            if (string.IsNullOrEmpty(RewardedAdUnitId))
            {
                Debug.LogWarning("[AdManager] No rewarded ad unit ID configured for this platform.");
                return;
            }

            _rewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            _rewardedAd.OnAdLoaded += (LevelPlayAdInfo info) => _rewardedRetryDelay = AdRetryBaseDelaySeconds;
            _rewardedAd.OnAdLoadFailed += (LevelPlayAdError error) =>
            {
                Debug.LogWarning($"[AdManager] Rewarded ad failed to load: {error} — retrying in {_rewardedRetryDelay}s.");
                if (_rewardedRetryRoutine != null)
                {
                    StopCoroutine(_rewardedRetryRoutine);
                }
                _rewardedRetryRoutine = StartCoroutine(RetryLoadAfterDelay(() => _rewardedAd.LoadAd(), _rewardedRetryDelay));
                _rewardedRetryDelay = Mathf.Min(_rewardedRetryDelay * 2f, AdRetryMaxDelaySeconds);
            };
            _rewardedAd.OnAdDisplayFailed += (LevelPlayAdInfo info, LevelPlayAdError error) =>
                Debug.LogWarning($"[AdManager] Rewarded ad failed to display: {error}");
            _rewardedAd.OnAdClosed += (LevelPlayAdInfo info) => _rewardedAd.LoadAd();

            _rewardedAd.LoadAd();
        }

        public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.IsAdReady();

        /// <summary>Seconds to wait for LevelPlay's OnAdRewarded/OnAdClosed callbacks before giving
        /// up on a shown rewarded ad. Audit finding F5.6: without this, a hung SDK callback (rare,
        /// but a real network/SDK edge case) left onResult never firing at all — and two of the
        /// three call sites pre-disabled their own button before calling this method, so a single
        /// hung callback permanently disabled that button (the skip-cooldown one, for the rest of
        /// the session, since nothing ever re-enables it). This timeout guarantees onResult always
        /// fires, so any caller that reacts to it (re-enabling its button in the false case) can no
        /// longer get stuck.</summary>
        private const float RewardedAdTimeoutSeconds = 8f;

        /// <summary>Shows a rewarded ad if one's ready. onResult fires exactly once, always — true
        /// only if LevelPlay actually granted the reward (OnAdRewarded); false if the ad was closed
        /// early, failed to display, wasn't ready to show at all, or the SDK never called back within
        /// RewardedAdTimeoutSeconds. Callers should gate their own reward logic on this rather than
        /// assuming ShowAd() succeeding means the reward was earned — a player can close a rewarded
        /// ad before it finishes.</summary>
        public void ShowRewardedAd(string placementName, System.Action<bool> onResult)
        {
            if (!IsRewardedAdReady)
            {
                onResult?.Invoke(false);
                return;
            }

            bool rewarded = false;
            bool resolved = false;
            Coroutine timeoutRoutine = null;

            void Resolve(bool result)
            {
                if (resolved)
                {
                    return;
                }
                resolved = true;
                _rewardedAd.OnAdRewarded -= HandleRewarded;
                _rewardedAd.OnAdClosed -= HandleClosed;
                if (timeoutRoutine != null)
                {
                    StopCoroutine(timeoutRoutine);
                }
                onResult?.Invoke(result);
            }

            void HandleRewarded(LevelPlayAdInfo info, LevelPlayReward reward) => rewarded = true;
            void HandleClosed(LevelPlayAdInfo info) => Resolve(rewarded);

            _rewardedAd.OnAdRewarded += HandleRewarded;
            _rewardedAd.OnAdClosed += HandleClosed;
            timeoutRoutine = StartCoroutine(RewardedAdTimeoutFallback(() => Resolve(false)));
            try
            {
                _rewardedAd.ShowAd(placementName: placementName);
            }
            catch (System.Exception e)
            {
                // C8.2 — the timeout coroutine already started above still resolves this call
                // correctly (false) if ShowAd throws instead of calling back normally.
                Debug.LogError($"[AdManager] ShowRewardedAd threw: {e}");
            }
        }

        private System.Collections.IEnumerator RewardedAdTimeoutFallback(System.Action onTimeout)
        {
            yield return new WaitForSecondsRealtime(RewardedAdTimeoutSeconds);
            onTimeout?.Invoke();
        }

        // ---- Interstitial ------------------------------------------------------------------------

        private void CreateInterstitialAd()
        {
            if (string.IsNullOrEmpty(InterstitialAdUnitId))
            {
                Debug.LogWarning("[AdManager] No interstitial ad unit ID configured for this platform.");
                return;
            }

            _interstitialAd = new LevelPlayInterstitialAd(InterstitialAdUnitId);
            _interstitialAd.OnAdLoaded += (LevelPlayAdInfo info) => _interstitialRetryDelay = AdRetryBaseDelaySeconds;
            _interstitialAd.OnAdLoadFailed += (LevelPlayAdError error) =>
            {
                Debug.LogWarning($"[AdManager] Interstitial failed to load: {error} — retrying in {_interstitialRetryDelay}s.");
                if (_interstitialRetryRoutine != null)
                {
                    StopCoroutine(_interstitialRetryRoutine);
                }
                _interstitialRetryRoutine = StartCoroutine(RetryLoadAfterDelay(() => _interstitialAd.LoadAd(), _interstitialRetryDelay));
                _interstitialRetryDelay = Mathf.Min(_interstitialRetryDelay * 2f, AdRetryMaxDelaySeconds);
            };
            _interstitialAd.OnAdDisplayFailed += (LevelPlayAdInfo info, LevelPlayAdError error) =>
                Debug.LogWarning($"[AdManager] Interstitial failed to display: {error}");
            _interstitialAd.OnAdClosed += (LevelPlayAdInfo info) => _interstitialAd.LoadAd();

            _interstitialAd.LoadAd();
        }

        public bool IsInterstitialAdReady => _interstitialAd != null && _interstitialAd.IsAdReady();

        /// <summary>Called from GameManager.LoadLevel — increments SaveManager's rolling level
        /// counter and shows an interstitial once it reaches interstitialLevelInterval, resetting
        /// the counter either way (so a skipped/unready ad doesn't mean the NEXT check fires
        /// immediately). Deliberately never called while GameState.Playing (LoadLevel always runs
        /// between levels, never mid-run) and respects SaveManager.AdsRemoved.
        ///
        /// <paramref name="onReady"/> fires exactly once, either back-to-back on the same frame (no
        /// interstitial was due, wasn't ready, or ads are removed) or once a shown interstitial
        /// actually closes (including a failed display, so a frozen caller — GameManager.LoadLevel
        /// freezes Time.timeScale around this call — can never hang waiting on an ad that never
        /// shows). GameManager relies on this to gate the start of gameplay behind the ad instead of
        /// letting the level run in the background underneath it.</summary>
        public void NotifyLevelLoaded(System.Action onReady)
        {
            if (SaveManager.Instance == null || SaveManager.Instance.AdsRemoved)
            {
                onReady?.Invoke();
                return;
            }

            int count = SaveManager.Instance.LevelsSinceLastInterstitial + 1;
            if (count < interstitialLevelInterval)
            {
                SaveManager.Instance.SetLevelsSinceLastInterstitial(count);
                onReady?.Invoke();
                return;
            }

            SaveManager.Instance.SetLevelsSinceLastInterstitial(0);
            if (!IsInterstitialAdReady)
            {
                onReady?.Invoke();
                return;
            }

            void HandleClosed(LevelPlayAdInfo info)
            {
                _interstitialAd.OnAdClosed -= HandleClosed;
                _interstitialAd.OnAdDisplayFailed -= HandleDisplayFailed;
                onReady?.Invoke();
            }
            void HandleDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
            {
                _interstitialAd.OnAdClosed -= HandleClosed;
                _interstitialAd.OnAdDisplayFailed -= HandleDisplayFailed;
                onReady?.Invoke();
            }

            _interstitialAd.OnAdClosed += HandleClosed;
            _interstitialAd.OnAdDisplayFailed += HandleDisplayFailed;
            try
            {
                _interstitialAd.ShowAd();
            }
            catch (System.Exception e)
            {
                // C8.2 — the caller (GameManager.LoadLevel) freezes Time.timeScale around this
                // whole call and is relying on onReady firing no matter what; a thrown exception
                // here without this catch would have soft-locked the game frozen forever.
                Debug.LogError($"[AdManager] Interstitial ShowAd threw: {e}");
                HandleDisplayFailed(default, default);
            }
        }
    }
}
