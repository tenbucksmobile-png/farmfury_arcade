using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmFuryArcade.Core
{
    /// <summary>Web demo: lives in Boot.unity (build index 0) on the GameObject named exactly
    /// "YTGameWrapper", next to Google's YTGameWrapper component (its JavaScript bridge sends
    /// callbacks to that GameObject by name). Persists for the whole session.
    ///
    /// Boot order:
    /// 1. YouTube build inside YouTube: report firstFrameReady (the boot frame is the loading
    ///    screen), load the YouTube cloud save, and hook up pause/resume/audio.
    /// 2. Open Game.unity. The game reads its save in Awake/Start, so the save must already be in
    ///    place; loading it here, before the game scene exists, avoids a late-arriving save.
    /// 3. gameReady is reported later by the title screen (Platform.ReportGameReady), the first
    ///    moment the player can actually interact.
    ///
    /// The website build (and a YouTube build opened outside YouTube) skips step 1.</summary>
    public class PlatformBootstrap : MonoBehaviour
    {
        private const string GameSceneName = "Game";

        /// <summary>Google's wrapper gives no callback if loadData() fails, so don't wait forever.</summary>
        private const float SaveLoadTimeoutSeconds = 5f;

        /// <summary>At most one upload to YouTube per this many seconds; pause always flushes.</summary>
        private const float SaveThrottleSeconds = 1f;

#if FF_YOUTUBE
        private YTGameSDK.YTGameWrapper _wrapper;
        private float _lastUploadTime = -999f;
        private bool _pausedByYouTube;
        private float _timeScaleBeforePause = 1f;
#endif

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);

#if FF_YOUTUBE
            _wrapper = GetComponent<YTGameSDK.YTGameWrapper>();
            if (_wrapper != null && _wrapper.InPlayablesEnv())
            {
                Platform.IsYouTube = true;
                Platform.GameReadyReporter = () => _wrapper.SendGameIsReady();
                _wrapper.SendGameFirstFrameReady();

                bool loaded = false;
                string json = null;
                _wrapper.LoadGameSaveData(data => { json = data; loaded = true; });
                float deadline = Time.realtimeSinceStartup + SaveLoadTimeoutSeconds;
                while (!loaded && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                if (!loaded)
                {
                    _wrapper.SendYTGameWarning("loadData timed out; starting with an empty save");
                }
                PlatformPrefs.UseCloudStore(json);

                Platform.IsAudioEnabled = _wrapper.IsYTGameAudioEnabled();
                ApplyAudioEnabled();
                _wrapper.SetOnAudioEnabledChangeCallback(enabled =>
                {
                    Platform.IsAudioEnabled = enabled;
                    ApplyAudioEnabled();
                });
                _wrapper.SetOnPauseCallback(HandleYouTubePause);
                _wrapper.SetOnResumeCallback(HandleYouTubeResume);
            }
#endif

            yield return SceneManager.LoadSceneAsync(GameSceneName);
        }

#if FF_YOUTUBE
        private void Update()
        {
            if (Platform.IsYouTube && PlatformPrefs.IsDirty && Time.realtimeSinceStartup - _lastUploadTime >= SaveThrottleSeconds)
            {
                UploadSave();
            }
        }

        private void UploadSave()
        {
            string json = PlatformPrefs.TakeSerializedForUpload();
            if (json != null)
            {
                _wrapper.SendGameSaveData(json);
                _lastUploadTime = Time.realtimeSinceStartup;
            }
        }

        private static void ApplyAudioEnabled()
        {
            // The YouTube mute overrides everything; the game's own Music toggle still works
            // underneath it (it controls the music sources, not the listener).
            AudioListener.volume = Platform.IsAudioEnabled ? 1f : 0f;
        }

        /// <summary>YouTube requires all execution to stop until onResume. Mid-level this also opens
        /// the Pause menu (same path as backgrounding the mobile app), so play doesn't resume by itself.</summary>
        private void HandleYouTubePause()
        {
            if (_pausedByYouTube)
            {
                return;
            }
            _pausedByYouTube = true;

            GameManager.Instance?.PauseFromBackground();
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            if (PlatformPrefs.IsDirty)
            {
                UploadSave();
            }
        }

        private void HandleYouTubeResume()
        {
            if (!_pausedByYouTube)
            {
                return;
            }
            _pausedByYouTube = false;

            AudioListener.pause = false;
            // If a level was running, the Pause menu is now showing and the player resumes with its
            // Play button - keep time frozen. Otherwise (menus) carry on as before.
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Paused)
            {
                Time.timeScale = _timeScaleBeforePause;
            }
        }
#endif
    }
}
