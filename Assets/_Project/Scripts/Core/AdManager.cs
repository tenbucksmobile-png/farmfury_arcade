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

        [Tooltip("Enables LevelPlay's in-app test suite UI (SetMetaData \"is_test_suite\") — for " +
                 "QA/dev builds only. Must be false in a release build; there is deliberately no " +
                 "automatic RELEASE-vs-DEBUG switch here yet (see CLAUDE.md's monetisation plan, " +
                 "Phase 2's \"Technical needed\" list) — toggle this by hand before cutting a " +
                 "release build until that build-config split exists.")]
        [SerializeField] private bool enableTestSuite = true;

        [Tooltip("Levels between forced interstitials, per the GDD's 5-8 range.")]
        [SerializeField] private int interstitialLevelInterval = 6;

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
            if (enableTestSuite)
            {
                LevelPlay.SetMetaData("is_test_suite", "enable");
            }

            LevelPlay.OnInitSuccess += HandleInitSuccess;
            LevelPlay.OnInitFailed += HandleInitFailed;
            LevelPlay.Init(AppKey);
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

        private void CreateRewardedAd()
        {
            if (string.IsNullOrEmpty(RewardedAdUnitId))
            {
                Debug.LogWarning("[AdManager] No rewarded ad unit ID configured for this platform.");
                return;
            }

            _rewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            _rewardedAd.OnAdLoadFailed += (LevelPlayAdError error) =>
                Debug.LogWarning($"[AdManager] Rewarded ad failed to load: {error}");
            _rewardedAd.OnAdDisplayFailed += (LevelPlayAdInfo info, LevelPlayAdError error) =>
                Debug.LogWarning($"[AdManager] Rewarded ad failed to display: {error}");
            _rewardedAd.OnAdClosed += (LevelPlayAdInfo info) => _rewardedAd.LoadAd();

            _rewardedAd.LoadAd();
        }

        public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.IsAdReady();

        /// <summary>Shows a rewarded ad if one's ready. onResult fires exactly once — true only if
        /// LevelPlay actually granted the reward (OnAdRewarded), false if the ad was closed early,
        /// failed to display, or wasn't ready to show at all. Callers should gate their own reward
        /// logic on this rather than assuming ShowAd() succeeding means the reward was earned — a
        /// player can close a rewarded ad before it finishes.</summary>
        public void ShowRewardedAd(string placementName, System.Action<bool> onResult)
        {
            if (!IsRewardedAdReady)
            {
                onResult?.Invoke(false);
                return;
            }

            bool rewarded = false;

            void HandleRewarded(LevelPlayAdInfo info, LevelPlayReward reward) => rewarded = true;
            void HandleClosed(LevelPlayAdInfo info)
            {
                _rewardedAd.OnAdRewarded -= HandleRewarded;
                _rewardedAd.OnAdClosed -= HandleClosed;
                onResult?.Invoke(rewarded);
            }

            _rewardedAd.OnAdRewarded += HandleRewarded;
            _rewardedAd.OnAdClosed += HandleClosed;
            _rewardedAd.ShowAd(placementName: placementName);
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
            _interstitialAd.OnAdLoadFailed += (LevelPlayAdError error) =>
                Debug.LogWarning($"[AdManager] Interstitial failed to load: {error}");
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
            _interstitialAd.ShowAd();
        }
    }
}
