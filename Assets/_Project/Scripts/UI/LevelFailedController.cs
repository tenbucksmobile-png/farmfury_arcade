using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shown by GameplayHUD when GameManager.CurrentState becomes LevelFailed (a timer expiry or
    /// exhausting the respawn cap — see GameManager.MaxRespawns/LevelTimeLimitSeconds). Rebuilt to a
    /// 2026-08-01 Canva mockup: World1_Cornfield-style night backdrop (Bg_LevelSelect.png) with the
    /// square LevelFailed.png "TRY AGAIN!" card as an aspect-locked PanelArt child (same
    /// square-art-on-landscape-overlay fix Pause/Level Complete already have), and two buttons:
    /// Restart (replays the same level) and Quit (returns to Level Select, not Main Menu — one step
    /// back to where the player picked this level from).
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject levelSelectScreen;

        private int _levelIndex;

        private void Awake()
        {
            restartButton.onClick.AddListener(Restart);
            quitButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
        }

        private void OnEnable()
        {
            _levelIndex = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.levelNumber : 0;
        }

        private void Restart()
        {
            SceneTransitionManager.Instance.TransitionTo(() =>
            {
                gameObject.SetActive(false);
                gameplayScreen.SetActive(true);
            });
            GameManager.Instance.LoadLevel(_levelIndex);
        }
    }
}
