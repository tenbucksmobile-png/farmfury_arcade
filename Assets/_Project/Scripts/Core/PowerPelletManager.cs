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

        public void ActivatePower(float duration)
        {
            TimeRemaining = duration;
            ActivatedDuration = duration;
            WasActivatedThisMaze = true;

            if (!IsPowerActive)
            {
                IsPowerActive = true;
                OnPowerStateChanged?.Invoke(true);
                AudioManager.Instance?.PlayPowerReadySfx();
                AudioManager.Instance?.PlayEatRobotMusic();
            }

            if (_countdownRoutine != null)
            {
                StopCoroutine(_countdownRoutine);
            }
            _countdownRoutine = StartCoroutine(CountDown());
        }

        /// <summary>Duration in seconds per the GDD's power pellet tiers.</summary>
        public static float GetDuration(PowerPelletType type)
        {
            return type switch
            {
                PowerPelletType.GoldenWheat => 15f,
                PowerPelletType.Rainbow => 30f,
                _ => 8f // Sunflower
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
