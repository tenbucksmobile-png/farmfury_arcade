using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>On-screen D-pad (Gameplay HUD) — an alternative to keyboard/swipe for directing
    /// the active character. True press/release semantics: PointerDown calls
    /// InputController.PressDirection (starts commanding this direction), PointerUp/PointerExit
    /// calls InputController.ReleaseDirection (stops) — matching a physical key's down/up, so
    /// releasing a finger from the button stops the character exactly like releasing a keyboard
    /// key does. GridMovement never needs to know this input source exists at all; it only reads
    /// InputController.CurrentHeldDirection.
    ///
    /// Uses EventTrigger rather than implementing IPointerDownHandler/IPointerUpHandler directly on
    /// this component, since the existing Button references are built elsewhere
    /// (Phase5ProjectBuilder) and this only needs to add callbacks to each, not replace the
    /// Button/Image setup already there. PointerExit is wired alongside PointerUp so dragging a
    /// finger off the button while still pressed also releases it — otherwise a direction could
    /// stay "held" forever if the release happens off the button's bounds.
    ///
    /// Real bug found 2026-09-14 (iPhone 11 playtest, see project_testing_device memory): Percy kept
    /// moving right with nothing touching the screen, resolved only once a swipe overrode
    /// CurrentHeldDirection directly (SetSwipeDirection bypasses HeldStack entirely — see
    /// InputController's own doc comment). This is a distinct issue from the 2026-09-14 keyboard-
    /// sync bug (dpad_keyboard_sync_bug memory) — that one made the D-pad never move the character
    /// at all; this one is the opposite, a direction getting stuck ON. Root cause is almost
    /// certainly a missed PointerUp/PointerExit callback for a specific touch — a known intermittent
    /// gotcha on real iOS devices (a fast lift, or an OS-level gesture briefly stealing/cancelling
    /// the touch, can mean uGUI's EventSystem never delivers the matching release event for that
    /// press). Fixed with a per-frame watchdog: if this pad believes a direction is still held but
    /// no pointer (mouse or any touch) is actually pressed anywhere on screen, force-release it —
    /// a safety net on top of the event-based release, not a replacement for it.</summary>
    public class DirectionalPadController : MonoBehaviour
    {
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        private readonly HashSet<Direction> _heldDirections = new HashSet<Direction>();

        private void Awake()
        {
            WirePressRelease(upButton, Direction.Up);
            WirePressRelease(downButton, Direction.Down);
            WirePressRelease(leftButton, Direction.Left);
            WirePressRelease(rightButton, Direction.Right);
        }

        private void Update()
        {
            if (_heldDirections.Count == 0 || IsAnyPointerCurrentlyPressed())
            {
                return;
            }

            // Nothing is actually touching the screen (or holding the mouse button down), yet this
            // pad still thinks it's commanding a direction — the touch that pressed it ended without
            // uGUI ever delivering the matching PointerUp/PointerExit. Force-release everything this
            // pad believes is held so the character can't keep moving on a phantom input.
            foreach (var dir in _heldDirections)
            {
                InputController.ReleaseDirection(dir);
            }
            _heldDirections.Clear();
        }

        private static bool IsAnyPointerCurrentlyPressed()
        {
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed)
                    {
                        return true;
                    }
                }
            }
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private void WirePressRelease(Button button, Direction direction)
        {
            if (button == null)
            {
                return;
            }

            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            AddEntry(trigger, EventTriggerType.PointerDown, () =>
            {
                _heldDirections.Add(direction);
                InputController.PressDirection(direction);
            });
            AddEntry(trigger, EventTriggerType.PointerUp, () =>
            {
                _heldDirections.Remove(direction);
                InputController.ReleaseDirection(direction);
            });
            AddEntry(trigger, EventTriggerType.PointerExit, () =>
            {
                _heldDirections.Remove(direction);
                InputController.ReleaseDirection(direction);
            });
        }

        private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
