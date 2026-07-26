using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmFuryArcade.Gameplay
{
    public static class DirectionUtils
    {
        public static Vector2Int ToVector(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return new Vector2Int(0, 1);
                case Direction.Down: return new Vector2Int(0, -1);
                case Direction.Left: return new Vector2Int(-1, 0);
                case Direction.Right: return new Vector2Int(1, 0);
                default: return Vector2Int.zero;
            }
        }

        public static Direction Opposite(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                case Direction.Right: return Direction.Left;
                default: return Direction.None;
            }
        }

        /// <summary>Ignores diagonal swipes — whichever axis has the larger absolute delta wins.</summary>
        public static Direction FromSwipeVector(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x > 0 ? Direction.Right : Direction.Left;
            }

            return delta.y > 0 ? Direction.Up : Direction.Down;
        }

        /// <summary>Arrow keys or WASD via the Input System package. Returns None if no relevant key
        /// was pressed this frame (or no keyboard is present).</summary>
        public static Direction FromKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return Direction.None;
            }

            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) return Direction.Up;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return Direction.Down;
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) return Direction.Left;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) return Direction.Right;

            return Direction.None;
        }
    }
}
