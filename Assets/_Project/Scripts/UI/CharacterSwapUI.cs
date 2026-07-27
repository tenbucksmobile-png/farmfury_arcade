using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Functional-not-polished swap panel per Phase 4 spec — Phase 5 replaces this with a real
    /// uGUI screen. Toggled by InputController.OnSwapMenuToggleInput (Tab). Shows every
    /// CharacterType as a button (disabled + annotated with its unlock requirement if locked),
    /// highlights the active one, and a Confirm/Cancel pair once a target is picked. Cost is 1
    /// coin normally, free if the player currently has 0 (per spec) — CharacterManager.CanSwapTo
    /// never blocks on affordability, only on unlock status.
    /// </summary>
    public class CharacterSwapUI : MonoBehaviour
    {
        private bool _isOpen;
        private CharacterType? _pendingSwap;

        private void OnEnable()
        {
            InputController.OnSwapMenuToggleInput += ToggleOpen;
        }

        private void OnDisable()
        {
            InputController.OnSwapMenuToggleInput -= ToggleOpen;
        }

        /// <summary>Public so GameplayHUD's Swap button (Phase 5) can trigger the same toggle a
        /// Tab press does, without InputController needing a second, button-specific event.</summary>
        public void ToggleOpen()
        {
            _isOpen = !_isOpen;
            if (!_isOpen)
            {
                _pendingSwap = null;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen || CharacterManager.Instance == null || DataManager.Instance == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 160f, 320f, 320f), GUI.skin.box);
            GUILayout.Label("Swap Character (Tab to close)");

            foreach (CharacterType type in System.Enum.GetValues(typeof(CharacterType)))
            {
                DrawCharacterButton(type);
            }

            if (_pendingSwap.HasValue)
            {
                DrawConfirmPanel(_pendingSwap.Value);
            }

            GUILayout.EndArea();
        }

        private void DrawCharacterButton(CharacterType type)
        {
            bool unlocked = SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(type);
            bool isActive = CharacterManager.Instance.ActiveCharacter == type;

            string label = type.ToString();
            if (!unlocked)
            {
                var data = DataManager.Instance.GetCharacterData(type);
                label += data != null ? $"  (locked — {data.unlockLevel} mazes)" : "  (locked)";
            }
            if (isActive)
            {
                label += "  [ACTIVE]";
            }

            GUI.enabled = unlocked && !isActive;
            if (GUILayout.Button(label))
            {
                _pendingSwap = type;
            }
            GUI.enabled = true;
        }

        private void DrawConfirmPanel(CharacterType target)
        {
            int cost = SaveManager.Instance != null && SaveManager.Instance.CoinBalance > 0 ? 1 : 0;
            GUILayout.Space(8f);
            GUILayout.Label($"Swap to {target}? Cost: {cost} coin(s)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirm Swap"))
            {
                if (cost > 0)
                {
                    SaveManager.Instance.SpendCoins(cost);
                }
                CharacterManager.Instance.SwapCharacter(target);
                _pendingSwap = null;
                _isOpen = false;
            }
            if (GUILayout.Button("Cancel"))
            {
                _pendingSwap = null;
            }
            GUILayout.EndHorizontal();
        }
    }
}
