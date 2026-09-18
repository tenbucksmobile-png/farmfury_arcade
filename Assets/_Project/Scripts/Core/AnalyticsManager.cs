using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Wraps Unity Gaming Services Analytics (com.unity.services.analytics). Parallel to
    /// AdManager/IAPManager/AudioManager — one singleton on GameManagers, owns all SDK interaction
    /// so gameplay code never touches Unity.Services.Analytics directly. Added 2026-09-18 to close
    /// the "no analytics or attribution instrumentation exists" gap flagged as launch-blocking in
    /// the GDD (FarmFury_Arcade_GDD.md, Sections 13/15/17).
    ///
    /// Picked over Firebase/GameAnalytics specifically because this Unity Cloud project is already
    /// linked for Unity Ads Mediation (LevelPlay) and Unity Cloud Build — Analytics reuses that same
    /// linked project with no extra native config files (no GoogleService-Info.plist/
    /// google-services.json) and shows up in the same dashboard family.
    ///
    /// COPPA note: this app treats every player as child-directed everywhere else it touches an ad
    /// SDK (see AdManager's own class doc comment) via the Unity Gaming Services project-level
    /// "Will this app be targeted to children" toggle (Project Settings > Services). That's a
    /// project-wide setting, not per-service — Unity's own docs describe it as what designates a
    /// Unity Cloud project as a "Child App" for Analytics too, at which point Unity's Analytics
    /// backend automatically restricts what gets collected (no cross-app/cross-device user tracking,
    /// COPPA-appropriate identifier handling) rather than this needing bespoke code here.
    /// **Confirm that toggle is still set to Yes** before relying on this — it was set for
    /// LevelPlay's sake originally and hasn't been separately re-verified for Analytics.
    ///
    /// StartDataCollection() is used deliberately over the newer EndUserConsent.SetConsentState(...)
    /// API package 6.2+ prefers (compiles with an [Obsolete] CS0618 warning naming that exact
    /// replacement — confirmed directly against this project's installed 6.3.0 package), since this
    /// app has no separate consent-collection UI of its own (same "child-directed by default, not
    /// per-user age-gated" choice AdManager makes) — StartDataCollection's own contract is "confirm
    /// consent has been obtained OR IS NOT REQUIRED under applicable law," which this project
    /// satisfies via that blanket posture. Deliberately NOT migrated to EndUserConsent yet: its
    /// defining type ships as a precompiled binary with no decompiled source available in this
    /// session to confirm its real namespace/method signature/enum values against, and guessing at
    /// an unverified API here risks a worse outcome (a wrong call, possibly silently misconfigured
    /// consent) than the current soft-deprecation warning. StartDataCollection remains fully
    /// functional at 6.3.0 — migrate once the real EndUserConsent shape can be confirmed with
    /// Editor/IntelliSense access, not urgent before then.
    ///
    /// **Custom events must be registered in the Unity Analytics Event Manager (the Unity Cloud
    /// dashboard) before they'll actually be ingested** — sending an unregistered event name is a
    /// silent no-op from the dashboard's perspective, not a code error. Register level_start,
    /// level_complete, level_failed, purchase, and ad_shown (with their parameters below) there
    /// before expecting to see any data.
    /// </summary>
    public class AnalyticsManager : Singleton<AnalyticsManager>
    {
        public bool IsInitialized { get; private set; }

        private async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();
                AnalyticsService.Instance.StartDataCollection();
                IsInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnalyticsManager] Unity Gaming Services init failed: {e.Message}");
            }
        }

        /// <summary>Every LogXxx method below funnels through here — one place to keep the
        /// try/catch discipline every other SDK-boundary call in this project already uses
        /// (AdManager/IAPManager's own "a malformed native response shouldn't crash the app"
        /// convention), and one place a no-op-while-uninitialized guard lives.</summary>
        private void LogEvent(string eventName, params (string key, object value)[] parameters)
        {
            if (!IsInitialized)
            {
                return;
            }

            try
            {
                var customEvent = new CustomEvent(eventName);
                foreach (var (key, value) in parameters)
                {
                    switch (value)
                    {
                        case string s: customEvent.Add(key, s); break;
                        case int i: customEvent.Add(key, i); break;
                        case long l: customEvent.Add(key, l); break;
                        case float f: customEvent.Add(key, f); break;
                        case double d: customEvent.Add(key, d); break;
                        case bool b: customEvent.Add(key, b); break;
                        default: customEvent.Add(key, value?.ToString() ?? string.Empty); break;
                    }
                }
                AnalyticsService.Instance.RecordEvent(customEvent);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnalyticsManager] RecordEvent({eventName}) failed: {e.Message}");
            }
        }

        /// <summary>GameManager.LoadLevel — fires for every level start, including Daily
        /// Challenge attempts (isDailyChallenge is its own parameter so those are distinguishable
        /// in the dashboard rather than looking like ordinary levels).</summary>
        public void LogLevelStart(int levelIndex, string levelName, bool isDailyChallenge) =>
            LogEvent("level_start",
                ("level_index", levelIndex),
                ("level_name", levelName ?? string.Empty),
                ("is_daily_challenge", isDailyChallenge));

        /// <summary>GameManager.EndLevel(true) — fires once per successful completion, after
        /// ComputeLevelResult has run.</summary>
        public void LogLevelComplete(int levelIndex, int stars, int score, float elapsedSeconds) =>
            LogEvent("level_complete",
                ("level_index", levelIndex),
                ("stars", stars),
                ("score", score),
                ("elapsed_seconds", elapsedSeconds));

        /// <summary>GameManager.EndLevel(false) — fires on both a timeout end and a declined
        /// revive; doesn't currently distinguish which, since both are "the run ended unsuccessfully"
        /// from an analytics standpoint.</summary>
        public void LogLevelFailed(int levelIndex, float elapsedSeconds) =>
            LogEvent("level_failed", ("level_index", levelIndex), ("elapsed_seconds", elapsedSeconds));

        /// <summary>IAPManager.HandlePurchasePendingInner — fires once per confirmed real-money
        /// purchase, after the effect has already been granted. priceString is whatever
        /// IAPManager.GetPriceString resolved (a real localized price once the store connection is
        /// live, otherwise the static fallback) — kept as a string rather than parsed into a
        /// currency/amount pair, since that parsing isn't reliable across locales/currencies.</summary>
        public void LogPurchase(string productId, string priceString) =>
            LogEvent("purchase", ("product_id", productId ?? string.Empty), ("price", priceString ?? string.Empty));

        /// <summary>AdManager — fires only on a confirmed-successful ad (a rewarded ad that
        /// actually granted its reward, or an interstitial that actually closed after showing) —
        /// never for a skipped/unready/failed attempt, so this tracks real ad exposure, not just
        /// button taps.</summary>
        public void LogAdShown(string placementType, string placementName) =>
            LogEvent("ad_shown",
                ("placement_type", placementType ?? string.Empty),
                ("placement_name", placementName ?? string.Empty));
    }
}
