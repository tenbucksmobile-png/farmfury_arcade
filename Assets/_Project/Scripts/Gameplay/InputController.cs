using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

        /// <summary>Space — activates whichever character's AbilityBase is currently active
        /// (Phase 4). Exactly one AbilityBase instance is ever enabled at a time, so a static
        /// event is safe here for the same reason it's safe on GridMovement.</summary>
        public static event Action OnAbilityActivateInput;

        /// <summary>Tab — toggles ChooseCharacterScreen.</summary>
        public static event Action OnSwapMenuToggleInput;

        [SerializeField] private float minSwipeDistancePixels = 50f;

        /// <summary>Raises OnDirectionInput exactly like a keyboard press or swipe would — the
        /// on-screen directional pad (GameplayHUD's DirectionalPadController) calls this from each
        /// button's onClick instead of duplicating the event/subscription mechanics GridMovement
        /// already listens to.</summary>
        public static void RaiseDirectionInput(Direction dir) => OnDirectionInput?.Invoke(dir);

        /// <summary>Raises OnAbilityActivateInput exactly like Space would — the Gameplay HUD's
        /// character portrait (doubling as the on-screen ability button, since Space has no touch
        /// equivalent) calls this from its own onClick instead of duplicating the event mechanics
        /// AbilityBase already listens to.</summary>
        public static void RaiseAbilityActivateInput() => OnAbilityActivateInput?.Invoke();

        private Vector2 _pointerDownPosition;
        private bool _isPressed;
        private bool _pointerDownOverUI;

        private void Update()
        {
            Direction keyboardDirection = DirectionUtils.FromKeyboard();
            if (keyboardDirection != Direction.None)
            {
                OnDirectionInput?.Invoke(keyboardDirection);
            }

            HandlePointerSwipe();
            HandleAbilityAndSwapKeys();
        }

        private void HandleAbilityAndSwapKeys()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.spaceKey.wasPressedThisFrame)
            {
                OnAbilityActivateInput?.Invoke();
            }
            if (kb.tabKey.wasPressedThisFrame)
            {
                OnSwapMenuToggleInput?.Invoke();
            }
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
                // A press that starts on a UI control (the on-screen D-pad, Pause button, etc.)
                // must not also be reinterpreted as a swipe — without this, dragging a thumb
                // across the D-pad could raise both the button's own direction AND a conflicting
                // swipe-derived direction from the same gesture.
                _pointerDownOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            }
            else if (pointer.press.wasReleasedThisFrame && _isPressed)
            {
                _isPressed = false;
                if (_pointerDownOverUI)
                {
                    return;
                }

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
