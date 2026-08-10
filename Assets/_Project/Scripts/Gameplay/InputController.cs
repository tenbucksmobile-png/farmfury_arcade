using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Detects WASD/arrow keys (desktop), the on-screen D-pad, and swipe gestures (mobile touch, or
    /// mouse drag in the Editor for testing), and exposes "what direction is currently commanded"
    /// as a single static value (CurrentHeldDirection) plus a change event. GridMovement instances
    /// read this directly; there is exactly one active character at a time, so a static value is
    /// enough — Phase 4's character-swap system can route this through GameManager.CurrentCharacter
    /// instead if needed.
    ///
    /// Movement is hold-to-move: a character only moves while a direction is actively held, stops
    /// the instant it's released, and switching directions (including a full 180) is instantaneous
    /// with no cooldown — see GridMovement's own doc comment for how it consumes this. Keyboard and
    /// the on-screen D-pad both have true press/release semantics, tracked in a shared "currently
    /// held" stack (most-recently-pressed wins if two directions are held at once — e.g. tapping
    /// Down while still holding Right switches to Down immediately, and releasing Down reverts to
    /// Right if it's still held). A swipe has no natural "hold" — it sets the direction directly
    /// (bypassing the stack) and that direction persists until overridden by a keyboard/D-pad press
    /// or another swipe, same "flick and go" convention mobile Pac-Man-style games use.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public static event Action<Direction> OnHeldDirectionChanged;
        public static Direction CurrentHeldDirection { get; private set; } = Direction.None;

        private static readonly List<Direction> HeldStack = new List<Direction>();

        /// <summary>Space — activates whichever character's AbilityBase is currently active
        /// (Phase 4). Exactly one AbilityBase instance is ever enabled at a time, so a static
        /// event is safe here for the same reason it's safe on GridMovement.</summary>
        public static event Action OnAbilityActivateInput;

        /// <summary>Tab — toggles ChooseCharacterScreen.</summary>
        public static event Action OnSwapMenuToggleInput;

        [SerializeField] private float minSwipeDistancePixels = 50f;

        /// <summary>Starts commanding this direction — called on a keyboard key-down or the
        /// on-screen D-pad's PointerDown. A direction already held is a no-op.</summary>
        public static void PressDirection(Direction dir)
        {
            if (dir == Direction.None || HeldStack.Contains(dir))
            {
                return;
            }
            HeldStack.Insert(0, dir);
            RefreshHeldDirection();
        }

        /// <summary>Stops commanding this direction — called on a keyboard key-up or the
        /// on-screen D-pad's PointerUp/PointerExit. If another direction is still held, control
        /// falls back to whichever was pressed most recently among those still held.</summary>
        public static void ReleaseDirection(Direction dir)
        {
            if (HeldStack.Remove(dir))
            {
                RefreshHeldDirection();
            }
        }

        private static void RefreshHeldDirection()
        {
            Direction next = HeldStack.Count > 0 ? HeldStack[0] : Direction.None;
            if (next == CurrentHeldDirection)
            {
                return;
            }
            CurrentHeldDirection = next;
            OnHeldDirectionChanged?.Invoke(next);
        }

        /// <summary>A completed swipe sets the direction directly rather than going through the
        /// press/release stack — there's no physical "hold" to release. Persists until a keyboard/
        /// D-pad press or another swipe overrides it.</summary>
        private static void SetSwipeDirection(Direction dir)
        {
            if (dir == CurrentHeldDirection)
            {
                return;
            }
            CurrentHeldDirection = dir;
            OnHeldDirectionChanged?.Invoke(dir);
        }

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
            UpdateKeyboardHeld();
            HandlePointerSwipe();
            HandleAbilityAndSwapKeys();
        }

        private void UpdateKeyboardHeld()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            SyncKey(kb.upArrowKey.isPressed || kb.wKey.isPressed, Direction.Up);
            SyncKey(kb.downArrowKey.isPressed || kb.sKey.isPressed, Direction.Down);
            SyncKey(kb.leftArrowKey.isPressed || kb.aKey.isPressed, Direction.Left);
            SyncKey(kb.rightArrowKey.isPressed || kb.dKey.isPressed, Direction.Right);
        }

        private static void SyncKey(bool isPressed, Direction dir)
        {
            bool inStack = HeldStack.Contains(dir);
            if (isPressed && !inStack)
            {
                PressDirection(dir);
            }
            else if (!isPressed && inStack)
            {
                ReleaseDirection(dir);
            }
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
                    SetSwipeDirection(DirectionUtils.FromSwipeVector(delta));
                }
            }
        }
    }
}
