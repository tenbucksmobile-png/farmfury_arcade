using System;
using System.Collections;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Single global power-state countdown (Pac-Man convention: only one "frightened" timer runs
    /// at a time; eating a second pellet while one is active just refreshes the duration). Every
    /// RobotBase subscribes to OnPowerStateChanged and flips to/from Vulnerable; ChaseScoreManager
    /// resets its chain when the state turns off.
    /// </summary>
    public class PowerPelletManager : Singleton<PowerPelletManager>
    {
        public event Action<bool> OnPowerStateChanged;

        public bool IsPowerActive { get; private set; }
        public float TimeRemaining { get; private set; }

        /// <summary>The duration passed to the most recent ActivatePower call — lets GameplayHUD
        /// normalize TimeRemaining into a 0-1 fill for the power-pellet timer bar.</summary>
        public float ActivatedDuration { get; private set; }

        /// <summary>Per-maze flag for DailyChallengeManager's "No Power" objective — reset by
        /// SceneController.LoadLevelContent.</summary>
        public bool WasActivatedThisMaze { get; private set; }

        private Coroutine _countdownRoutine;

        public void ResetForNewMaze()
        {
            WasActivatedThisMaze = false;
        }

        /// <summary>Real bug found and fixed (2026-09-12): leaving a maze mid-power-pellet-effect
        /// (level complete, level failed, or a deliberate Pause > Quit) never stopped this
        /// countdown — PowerPelletManager is a persistent singleton on GameManagers, untouched by
        /// scene/content teardown, so CountDown() kept ticking down in the background using real
        /// Time.deltaTime even after the player had already left gameplay and Theme had started
        /// playing again (GameManager.EndLevel/QuitToLevelSelect both call PlayLandingMusic
        /// immediately). Once the stale countdown reached zero — up to 17s later for a Rainbow-tier
        /// pellet — its own tail-end ResumeBackgroundMusic() call yanked the music back to whichever
        /// world track was last playing, mid-menu-browsing, with no level actually running. Reported
        /// as "plays theme when outside then all of a sudden will begin the world level music."
        /// GameManager.EndLevel/QuitToLevelSelect now call this immediately on leaving a maze, so the
        /// countdown can never outlive the run that started it. Deliberately does NOT fire
        /// OnPowerStateChanged or touch audio itself — every robot that cared is about to be
        /// destroyed by the next level load anyway, and the caller already owns the correct music
        /// transition (PlayLandingMusic) for this moment.</summary>
        public void StopAndReset()
        {
            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
                _countdownRoutine = null;
            }
            IsPowerActive = false;
            TimeRemaining = 0f;
        }

        /// <summary>Eating a pellet while the power state is already active only ever EXTENDS the
        /// countdown (Mathf.Max against whatever's left), never shortens it. Previously this
        /// unconditionally overwrote TimeRemaining with the new pellet's own duration — harmless
        /// when a stronger pellet followed a weaker one, but a maze typically has several plain
        /// Sunflower pellets (5s) alongside its one capped rare pellet (GoldenWheat 9.5s / Rainbow
        /// 17s, see GetDuration); eating a Sunflower partway through an already-running rare-tier
        /// window reset the timer down to a flat 5s, cutting the rare pellet's real duration short
        /// well before it should have expired.</summary>
        public void ActivatePower(float duration)
        {
            TimeRemaining = IsPowerActive ? Mathf.Max(TimeRemaining, duration) : duration;
            ActivatedDuration = duration;
            WasActivatedThisMaze = true;

            if (!IsPowerActive)
            {
                IsPowerActive = true;
                OnPowerStateChanged?.Invoke(true);
                AudioManager.Instance?.PlayEatRobotMusic();
            }

            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
            }
            _countdownRoutine = StartCoroutine(CountDown());
        }

        /// <summary>Duration in seconds per the GDD's power pellet tiers — halved from the original
        /// spec (8/15/30 -> 4/7.5/15) per feedback that the window a robot stays catchable/vulnerable
        /// was too long. The two rare tiers (GoldenWheat/Rainbow — same "rare" gate CropCollector's
        /// PlayRarePelletPickupSfx uses) then got +2s back on top of that, per a later gameplay
        /// pass, so a rare pellet still reads as meaningfully more valuable than a Sunflower one.
        /// Sunflower's own base window was extended 4s -> 5s in a later pass, still well short of
        /// the two rare tiers.</summary>
        public static float GetDuration(PowerPelletType type)
        {
            return type switch
            {
                PowerPelletType.GoldenWheat => 7.5f + 2f,
                PowerPelletType.Rainbow => 15f + 2f,
                _ => 5f // Sunflower
            };
        }

        private IEnumerator CountDown()
        {
            while (TimeRemaining > 0f)
            {
                TimeRemaining -= Time.deltaTime;
                yield return null;
            }

            TimeRemaining = 0f;
            IsPowerActive = false;
            _countdownRoutine = null;
            OnPowerStateChanged?.Invoke(false);
            ChaseScoreManager.Instance?.ResetChain();
            AudioManager.Instance?.ResumeBackgroundMusic();
        }
    }
}
