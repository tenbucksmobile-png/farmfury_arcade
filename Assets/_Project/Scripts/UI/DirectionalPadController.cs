using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>On-screen D-pad (Gameplay HUD, right side) — an alternative to keyboard/swipe for
    /// directing the active character. Each button raises InputController.OnDirectionInput exactly
    /// like a keyboard press or swipe would, so GridMovement (which subscribes directly to that
    /// event) doesn't need to know this input source exists at all.
    ///
    /// Fires on PointerDown (touch/press), not Button.onClick — onClick only fires on release, which
    /// reads as sluggish for a directional control (the player expects the turn to register the
    /// instant they touch the button, not after they lift their finger back off it). Uses EventTrigger
    /// rather than implementing IPointerDownHandler directly on this component, since the existing
    /// Button references are built elsewhere (Phase5ProjectBuilder) and this only needs to add a
    /// PointerDown callback to each, not replace the Button/Image setup already there.</summary>
    public class DirectionalPadController : MonoBehaviour
    {
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        private void Awake()
        {
            WireImmediate(upButton, Direction.Up);
            WireImmediate(downButton, Direction.Down);
            WireImmediate(leftButton, Direction.Left);
            WireImmediate(rightButton, Direction.Right);
        }

        private static void WireImmediate(Button button, Direction direction)
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

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => InputController.RaiseDirectionInput(direction));
            trigger.triggers.Add(entry);
        }
    }
}
