using System;

namespace FarmFuryArcade.Core
{
    /// <summary>Web demo: what the game needs to know about where it's running. Game code asks
    /// this instead of checking build defines itself.
    ///
    /// Build defines (set by WebDemoBuilder's build menu):
    /// - FF_YOUTUBE: the YouTube Playables build. IsYouTube becomes true at runtime only when
    ///   the page really is inside YouTube (PlatformBootstrap checks ytgame.IN_PLAYABLES_ENV), so a
    ///   YouTube build opened locally behaves like the website build.
    /// - neither: the website (farmfurygames.com/play) build.</summary>
    public static class Platform
    {
        /// <summary>True when running inside YouTube Playables.</summary>
        public static bool IsYouTube { get; internal set; }

        /// <summary>YouTube Playables forbids external links, store buttons and URLs. Screens that
        /// would open a store page must check this and show text instead.</summary>
        public static bool CanOpenExternalLinks => !IsYouTube;

        /// <summary>The YouTube player's own audio setting (mute button / settings). Always true
        /// outside YouTube.</summary>
        public static bool IsAudioEnabled { get; internal set; } = true;

        /// <summary>Set by PlatformBootstrap; null in the Editor when Game.unity is played directly.</summary>
        internal static Action GameReadyReporter;

        private static bool _gameReadyReported;

        /// <summary>Tells YouTube the player can now interact (YouTube removes its loading spinner).
        /// Called by the title screen on its first appearance; later calls are ignored.</summary>
        public static void ReportGameReady()
        {
            if (_gameReadyReported)
            {
                return;
            }
            _gameReadyReported = true;
            GameReadyReporter?.Invoke();
        }
    }
}
