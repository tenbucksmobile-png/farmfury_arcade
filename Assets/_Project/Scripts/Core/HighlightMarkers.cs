using System;
using System.Diagnostics;
using UnityEngine;
using FarmFuryArcade.Enemies;
using Debug = UnityEngine.Debug;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Development-build-only log lines marking the moments worth cutting into a gameplay short
    /// (combos, robot kills, a full chain, near misses, unlocks, level start/end). The content
    /// pipeline (Tools/content-pipeline) records the phone screen with scrcpy while capturing
    /// logcat, then uses these lines to find clip boundaries in the video.
    ///
    /// Line format (one per marker, parsed by Tools/content-pipeline/parse_markers.py):
    ///   [Highlight] type=combo ms=1790000000000 name=Crossfire
    /// ms = device Unix time in milliseconds, so markers line up with the recording via the clock
    /// offset record_session.py measures at the start of a session.
    ///
    /// details is a FormattableString so numbers are always written with a '.' decimal point
    /// (FormattableString.Invariant) - the Honor test phone's locale wrote "33,6", which a later
    /// numeric parse would misread.
    ///
    /// Every Mark() call is removed by the compiler in release builds ([Conditional] on both
    /// symbols means "either one"), including evaluation of its arguments, so nothing here reaches
    /// the Play Store build.
    /// </summary>
    public static class HighlightMarkers
    {
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Mark(string type, FormattableString details = null)
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Debug.Log(details == null
                ? $"[Highlight] type={type} ms={ms}"
                : $"[Highlight] type={type} ms={ms} {FormattableString.Invariant(details)}");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // Creates the near-miss watcher at startup without adding anything to Game.unity.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateNearMissWatcher()
        {
            var go = new GameObject("HighlightNearMissWatcher") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<HighlightNearMissWatcher>();
        }
#endif
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>Marks a "near_miss" when a hostile (Chase/Scatter, not stunned) robot comes within
    /// NearMissCells of the player during play. It doesn't know yet whether the player survived;
    /// parse_markers.py drops a near miss followed shortly by a player_death.</summary>
    public class HighlightNearMissWatcher : MonoBehaviour
    {
        private const float NearMissCells = 1.4f;
        private const float CooldownSeconds = 3f;

        private RobotSpawner _spawner;
        private float _nextAllowedTime;

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing || Time.timeScale == 0f)
            {
                return;
            }
            if (Time.unscaledTime < _nextAllowedTime)
            {
                return;
            }

            var player = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            if (player == null)
            {
                return;
            }
            if (_spawner == null)
            {
                _spawner = FindAnyObjectByType<RobotSpawner>();
                if (_spawner == null)
                {
                    return;
                }
            }

            float threshold = NearMissCells * TileMapRenderer.CellSize;
            Vector3 playerPos = player.transform.position;
            foreach (var robot in _spawner.ActiveRobots)
            {
                if (robot == null || robot.IsStunned ||
                    (robot.CurrentState != RobotState.Chase && robot.CurrentState != RobotState.Scatter))
                {
                    continue;
                }
                float distance = Vector2.Distance(playerPos, robot.transform.position);
                if (distance < threshold)
                {
                    string robotType = robot.Data != null ? robot.Data.robotType.ToString() : "Unknown";
                    HighlightMarkers.Mark("near_miss", $"robot={robotType} cells={distance / TileMapRenderer.CellSize:F2}");
                    _nextAllowedTime = Time.unscaledTime + CooldownSeconds;
                    return;
                }
            }
        }
    }
#endif
}
