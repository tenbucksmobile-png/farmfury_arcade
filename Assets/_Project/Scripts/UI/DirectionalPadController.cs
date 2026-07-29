using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>On-screen D-pad (Gameplay HUD, right side) — an alternative to keyboard/swipe for
    /// directing the active character. Each button raises InputController.OnDirectionInput exactly
    /// like a keyboard press or swipe would, so GridMovement (which subscribes directly to that
    /// event) doesn't need to know this input source exists at all.</summary>
    public class DirectionalPadController : MonoBehaviour
    {
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        private void Awake()
        {
            upButton.onClick.AddListener(() => InputController.RaiseDirectionInput(Direction.Up));
            downButton.onClick.AddListener(() => InputController.RaiseDirectionInput(Direction.Down));
            leftButton.onClick.AddListener(() => InputController.RaiseDirectionInput(Direction.Left));
            rightButton.onClick.AddListener(() => InputController.RaiseDirectionInput(Direction.Right));
        }
    }
}
