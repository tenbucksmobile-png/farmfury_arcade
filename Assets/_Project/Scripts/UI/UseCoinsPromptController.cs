using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// "Use Coins?" confirmation modal (2026-09-13) shown by CosmeticPurchaseScreen when a tapped
    /// item's coin-purchase alternative is actually affordable — see
    /// CosmeticPurchaseScreen.HandleItemTapped for the branching logic this is only one half of.
    /// Generic Show(onYes, onNo) callback shape rather than knowing about IAPManager/productIds
    /// itself, so this same component could back any future "spend coins or do X instead" prompt.
    /// Layers on top of whatever's already showing (the Hats/Trails purchase screen) the same
    /// "overlay, not a scene swap" convention every other in-app popup in this project uses.
    /// </summary>
    public class UseCoinsPromptController : MonoBehaviour
    {
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action _onYes;
        private Action _onNo;

        private void Awake()
        {
            // Wired here, not by the editor-script builder — a listener added directly from
            // editor-script code doesn't survive a scene save/reload (UnityEvent's non-persistent
            // listeners aren't serialized), same pitfall RevivePromptController/SimpleClosePanel
            // exist to work around elsewhere in this project.
            if (yesButton != null)
            {
                yesButton.onClick.AddListener(HandleYes);
            }
            if (noButton != null)
            {
                noButton.onClick.AddListener(HandleNo);
            }
        }

        public void Show(Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void HandleYes()
        {
            gameObject.SetActive(false);
            _onYes?.Invoke();
        }

        private void HandleNo()
        {
            gameObject.SetActive(false);
            _onNo?.Invoke();
        }
    }
}
