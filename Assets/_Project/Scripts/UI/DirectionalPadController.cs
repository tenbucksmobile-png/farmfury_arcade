using UnityEngine;
using UnityEngine.EventSystems;
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
    /// stay "held" forever if the release happens off the button's bounds.</summary>
    public class DirectionalPadController : MonoBehaviour
    {
        [SerializeField] private Button upButton;
        [SerializeField] private Button downButton;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        private void Awake()
        {
            WirePressRelease(upButton, Direction.Up);
            WirePressRelease(downButton, Direction.Down);
            WirePressRelease(leftButton, Direction.Left);
            WirePressRelease(rightButton, Direction.Right);
        }

        private static void WirePressRelease(Button button, Direction direction)
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

            AddEntry(trigger, EventTriggerType.PointerDown, () => InputController.PressDirection(direction));
            AddEntry(trigger, EventTriggerType.PointerUp, () => InputController.ReleaseDirection(direction));
            AddEntry(trigger, EventTriggerType.PointerExit, () => InputController.ReleaseDirection(direction));
        }

        private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
