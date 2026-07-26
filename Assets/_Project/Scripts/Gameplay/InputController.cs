using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Detects WASD/arrow keys (desktop) and swipe gestures (mobile touch, or mouse drag in the
    /// Editor for testing) and raises OnDirectionInput. GridMovement instances subscribe directly;
    /// there is exactly one active character in Phase 2, so a static event is enough — Phase 4's
    /// character-swap system can route this through GameManager.CurrentCharacter instead if needed.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public static event Action<Direction> OnDirectionInput;

        [SerializeField] private float minSwipeDistancePixels = 50f;

        private Vector2 _pointerDownPosition;
        private bool _isPressed;

        private void Update()
        {
            Direction keyboardDirection = DirectionUtils.FromKeyboard();
            if (keyboardDirection != Direction.None)
            {
                OnDirectionInput?.Invoke(keyboardDirection);
            }

            HandlePointerSwipe();
        }

        private void HandlePointerSwipe()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _pointerDownPosition = pointer.position.ReadValue();
                _isPressed = true;
            }
            else if (pointer.press.wasReleasedThisFrame && _isPressed)
            {
                _isPressed = false;
                Vector2 releasePosition = pointer.position.ReadValue();
                Vector2 delta = releasePosition - _pointerDownPosition;

                if (delta.magnitude >= minSwipeDistancePixels)
                {
                    OnDirectionInput?.Invoke(DirectionUtils.FromSwipeVector(delta));
                }
            }
        }
    }
}
