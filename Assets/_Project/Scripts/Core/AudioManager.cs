using System;
using System.Collections;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Full playback API and pooling/volume/mute plumbing, exercised in Phase5Test with synthetic
    /// AudioClips as well as real content. Gameplay music is now per-world (worldMusicClips, wired
    /// by ArtWiringBuilder from Assets/_Project/Audio/Music/ — one track per MazeType, e.g.
    /// CornField.mp3/Orchard.mp3/Wheatfield.mp3/VegetablePatch.mp3) and starts only during an
    /// active run — GameManager.LoadLevel starts the level's own world track (PlayWorldMusic) and
    /// EndLevel/QuitToLevelSelect stop it (StopMusic) and hand back to the "Theme" landing track.
    /// A MazeType with no dedicated entry in worldMusicClips (currently the 3 purchased worlds,
    /// which have no dedicated music yet) falls back to backgroundMusicClip — same "reuse
    /// CornField's own art as a placeholder" convention this project uses elsewhere for those
    /// worlds' still-missing crop/vegetable art. Main Menu instead plays its own dedicated "Theme"
    /// track (landingMusicClip via PlayLandingMusic, hooked to MainMenuController.OnEnable only —
    /// OnDisable deliberately doesn't stop it, so the landing track keeps playing through Level
    /// Select browsing instead of cutting to silence the moment Play is tapped, right up until
    /// GameManager.LoadLevel's PlayWorldMusic call actually starts the level). The two tracks still
    /// never overlap: PlayMusic's crossfade always fades the previous track out as the new one
    /// fades in, whichever two call sites are involved.
    /// SFX clips under Assets/_Project/Audio/SFX/ are wired to specific gameplay triggers — see
    /// PlayAnimalDeathSfx/PlayCornPickupSfx/PlayCoinPickupSfx/PlayPowerReadySfx/
    /// PlayRarePelletPickupSfx/PlayRobotRespawnSfx and their call sites (PlayerHealth,
    /// CropCollector, AbilityBase, RobotBase respectively). Unchanged by the per-world music work.
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource[] sfxPool;
        [SerializeField] private float musicCrossfadeSeconds = 0.6f;

        [Serializable]
        private struct WorldMusicEntry
        {
            public MazeType mazeType;
            public AudioClip clip;
        }

        [Tooltip("Per-world gameplay music (one entry per MazeType) — PlayWorldMusic looks this up " +
                 "by the level's own mazeType. A world with no entry here (or a null clip) falls " +
                 "back to backgroundMusicClip below.")]
        [SerializeField] private WorldMusicEntry[] worldMusicClips;

        [Tooltip("Fallback gameplay track used only when the level's MazeType has no entry in " +
                 "worldMusicClips (currently the 3 purchased worlds, pending their own dedicated " +
                 "music) — also the Start() safety-net track for a context with no LoadLevel call " +
                 "at all (e.g. a test harness).")]
        [SerializeField] private AudioClip backgroundMusicClip;

        [Tooltip("Swapped in for the duration of a power pellet's effect (PowerPelletManager " +
                 "crossfades to this and back via PlayEatRobotMusic/ResumeBackgroundMusic).")]
        [SerializeField] private AudioClip eatRobotMusicClip;

        [Tooltip("The 'Theme' track — starts on Main Menu (MainMenuController.OnEnable) and keeps " +
                 "playing through Level Select browsing — crossfades out once a level actually " +
                 "starts (GameManager.LoadLevel's PlayWorldMusic call) and crossfades back in the " +
                 "moment the level ends (GameManager.EndLevel/QuitToLevelSelect).")]
        [SerializeField] private AudioClip landingMusicClip;

        // The world track PlayWorldMusic most recently started — ResumeBackgroundMusic (called by
        // PowerPelletManager when a power pellet's effect ends, and by Start()'s safety net) replays
        // this specific clip rather than always falling back to a single flat track, so resuming
        // after a power pellet correctly returns to whichever world's music was actually playing.
        private AudioClip _currentWorldMusicClip;

        [Header("SFX clips")]
        [SerializeField] private AudioClip animalDeathClip;
        [SerializeField] private AudioClip cornPickupClip;
        [SerializeField] private AudioClip coinPickupClip;
        [SerializeField] private AudioClip powerReadyClip;
        [SerializeField] private AudioClip rarePelletPickupClip;
        [SerializeField] private AudioClip robotRespawnClip;

        private int _sfxPoolCursor;
        private bool _usingSourceA = true;
        private Coroutine _musicFadeRoutine;
        private bool _musicStarted;

        protected override void Awake()
        {
            base.Awake();
            ApplyMusicVolume();
            ApplySfxVolume();
        }

        /// <summary>Safety-net auto-play matching backgroundMusicClip's own tooltip ("started once
        /// as soon as the app launches") — this used to be documented but never actually
        /// implemented, so nothing played until whatever screen's OnEnable happened to call
        /// PlayMusic first. Guarded by _musicStarted so it can never override Main Menu's own
        /// landingMusicClip: Unity calls every active object's OnEnable before any object's Start()
        /// runs, so MainMenuController.OnEnable (which calls PlayLandingMusic on the same launch
        /// frame) always sets _musicStarted first when Main Menu is present — this only fires the
        /// fallback in a context with no such screen (e.g. a test harness).</summary>
        private void Start()
        {
            if (!_musicStarted)
            {
                ResumeBackgroundMusic();
            }
        }

        public void PlayMusic(AudioClip clip, bool fade = true)
        {
            if (clip == null)
            {
                return;
            }

            _musicStarted = true;

            var incoming = _usingSourceA ? musicSourceB : musicSourceA;
            var outgoing = _usingSourceA ? musicSourceA : musicSourceB;
            _usingSourceA = !_usingSourceA;

            if (incoming == null)
            {
                return;
            }

            incoming.clip = clip;
            incoming.loop = true;

            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
            }

            _musicFadeRoutine = StartCoroutine(fade
                ? CrossfadeMusic(incoming, outgoing)
                : CutMusic(incoming, outgoing));
        }

        private IEnumerator CrossfadeMusic(AudioSource incoming, AudioSource outgoing)
        {
            float targetVolume = MutedOrVolume(SaveManager.Instance != null && SaveManager.Instance.MusicOn, GetMusicVolume());
            incoming.volume = 0f;
            incoming.Play();

            float t = 0f;
            float startOutgoingVolume = outgoing != null ? outgoing.volume : 0f;
            while (t < musicCrossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / musicCrossfadeSeconds);
                incoming.volume = Mathf.Lerp(0f, targetVolume, p);
                if (outgoing != null)
                {
                    outgoing.volume = Mathf.Lerp(startOutgoingVolume, 0f, p);
                }
                yield return null;
            }

            incoming.volume = targetVolume;
            if (outgoing != null)
            {
                outgoing.Stop();
            }
        }

        private IEnumerator CutMusic(AudioSource incoming, AudioSource outgoing)
        {
            if (outgoing != null)
            {
                outgoing.Stop();
            }
            incoming.volume = MutedOrVolume(SaveManager.Instance != null && SaveManager.Instance.MusicOn, GetMusicVolume());
            incoming.Play();
            yield break;
        }

        /// <summary>Named SFX intents — one per gameplay trigger (PlayerHealth's death sequence,
        /// CropCollector's corn/rare-pellet pickups, PowerPelletManager's power-on, RobotBase's
        /// defeated-then-returned respawn) — so call sites read as what happened, not which clip
        /// field to reach into.</summary>
        public void PlayAnimalDeathSfx() => PlaySFX(animalDeathClip);
        public void PlayCornPickupSfx() => PlaySFX(cornPickupClip);
        public void PlayCoinPickupSfx() => PlaySFX(coinPickupClip);
        public void PlayPowerReadySfx() => PlaySFX(powerReadyClip);
        public void PlayRarePelletPickupSfx() => PlaySFX(rarePelletPickupClip);
        public void PlayRobotRespawnSfx() => PlaySFX(robotRespawnClip);

        /// <summary>Crossfades the looping music track to the "power active" cue for as long as a
        /// power pellet's effect lasts — PowerPelletManager calls this on activation and
        /// ResumeBackgroundMusic when the effect ends (including a fresh pellet refreshing an
        /// already-active timer, which doesn't retrigger this — see its OnPowerStateChanged
        /// call site).</summary>
        public void PlayEatRobotMusic() => PlayMusic(eatRobotMusicClip);

        /// <summary>Crossfades to the Main Menu's own track — called from
        /// MainMenuController.OnEnable, which fires both at app launch (Main Menu starts active)
        /// and every time the player navigates back to it.</summary>
        public void PlayLandingMusic() => PlayMusic(landingMusicClip);

        /// <summary>Crossfades to the given world's own gameplay track (falls back to
        /// backgroundMusicClip if that MazeType has no dedicated entry in worldMusicClips) — what
        /// GameManager.LoadLevel calls once a level actually begins, so the "Theme" landing track
        /// hands off to the world's own music at the exact moment gameplay starts.</summary>
        public void PlayWorldMusic(MazeType mazeType)
        {
            AudioClip clip = backgroundMusicClip;
            if (worldMusicClips != null)
            {
                foreach (var entry in worldMusicClips)
                {
                    if (entry.mazeType == mazeType && entry.clip != null)
                    {
                        clip = entry.clip;
                        break;
                    }
                }
            }
            _currentWorldMusicClip = clip;
            PlayMusic(clip);
        }

        /// <summary>Crossfades back to whichever world track PlayWorldMusic most recently started —
        /// the counterpart to PlayEatRobotMusic (PowerPelletManager calls this when a power
        /// pellet's effect ends). Falls back to backgroundMusicClip if no world track has played
        /// yet this session (e.g. Start()'s safety net in a context with no LoadLevel call at all).</summary>
        public void ResumeBackgroundMusic() => PlayMusic(_currentWorldMusicClip != null ? _currentWorldMusicClip : backgroundMusicClip);

        /// <summary>Silences whichever music source is currently playing — used by GameManager
        /// when leaving gameplay (EndLevel, QuitToLevelSelect) so background music plays only during
        /// an active run, never on Main Menu/World Map. Doesn't touch the SFX pool (unlike
        /// StopAllAudio), since those two concerns are independent.</summary>
        public void StopMusic()
        {
            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }
            if (musicSourceA != null) musicSourceA.Stop();
            if (musicSourceB != null) musicSourceB.Stop();
        }

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || sfxPool == null || sfxPool.Length == 0)
            {
                return;
            }
            if (SaveManager.Instance != null && !SaveManager.Instance.SfxOn)
            {
                return;
            }

            var source = sfxPool[_sfxPoolCursor];
            _sfxPoolCursor = (_sfxPoolCursor + 1) % sfxPool.Length;

            if (source == null)
            {
                return;
            }

            source.PlayOneShot(clip, Mathf.Clamp01(volume) * GetSfxVolume());
        }

        public void SetMusicVolume(float volume)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.MusicVolume = volume;
            }
            ApplyMusicVolume();
        }

        public void SetSFXVolume(float volume)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SfxVolume = volume;
            }
            ApplySfxVolume();
        }

        public void SetMusicMuted(bool muted)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.MusicOn = !muted;
            }
            ApplyMusicVolume();
        }

        public void SetSFXMuted(bool muted)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SfxOn = !muted;
            }
        }

        /// <summary>Diagnostic-only — lets Phase5Test confirm a PlaySFX call actually started audio
        /// playback on a pooled source, rather than just trusting that no exception was thrown.</summary>
        public bool IsAnySfxPlaying()
        {
            if (sfxPool == null)
            {
                return false;
            }
            foreach (var source in sfxPool)
            {
                if (source != null && source.isPlaying)
                {
                    return true;
                }
            }
            return false;
        }

        public void StopAllAudio()
        {
            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }

            if (musicSourceA != null) musicSourceA.Stop();
            if (musicSourceB != null) musicSourceB.Stop();

            if (sfxPool != null)
            {
                foreach (var source in sfxPool)
                {
                    if (source != null)
                    {
                        source.Stop();
                    }
                }
            }
        }

        private void ApplyMusicVolume()
        {
            float volume = MutedOrVolume(SaveManager.Instance != null && SaveManager.Instance.MusicOn, GetMusicVolume());
            // PlayMusic reads incoming/outgoing off _usingSourceA BEFORE flipping it, so after the
            // flip _usingSourceA actually names the source that just started playing (the old
            // "incoming") — musicSourceA when true, musicSourceB when false. This was inverted
            // (picked the silent/stopped source instead), which is why toggling music back on via
            // the Settings mute icon raised the volume on a source nobody could hear.
            var active = _usingSourceA ? musicSourceA : musicSourceB;
            if (active != null)
            {
                active.volume = volume;
            }
        }

        private void ApplySfxVolume()
        {
            // Per-clip volume is applied at PlaySFX time via GetSfxVolume(); nothing to do to the
            // pooled sources themselves ahead of time (PlayOneShot takes its volume as a param).
        }

        private static float MutedOrVolume(bool on, float volume) => on ? volume : 0f;
        private static float GetMusicVolume() => SaveManager.Instance != null ? SaveManager.Instance.MusicVolume : 1f;
        private static float GetSfxVolume() => SaveManager.Instance != null ? SaveManager.Instance.SfxVolume : 1f;
    }
}
