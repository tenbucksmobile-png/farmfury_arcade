using System.Collections;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// No audio clips exist yet (see CLAUDE.md "No art or audio") — this is the full playback API
    /// and pooling/volume/mute plumbing, exercised in Phase5Test with synthetic AudioClips rather
    /// than real content. Wire real music/SFX clips into CharacterData/LevelData/RobotData/UI
    /// prefabs and call these same methods once art/audio lands; nothing here should need to
    /// change shape.
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource[] sfxPool;
        [SerializeField] private float musicCrossfadeSeconds = 0.6f;

        private int _sfxPoolCursor;
        private bool _usingSourceA = true;
        private Coroutine _musicFadeRoutine;

        protected override void Awake()
        {
            base.Awake();
            ApplyMusicVolume();
            ApplySfxVolume();
        }

        public void PlayMusic(AudioClip clip, bool fade = true)
        {
            if (clip == null)
            {
                return;
            }

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
            var active = _usingSourceA ? musicSourceB : musicSourceA; // the one currently playing (see PlayMusic's swap)
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
