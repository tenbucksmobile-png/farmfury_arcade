using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Real uGUI "Choose Character" panel — replaces the old debug-style OnGUI CharacterSwapUI.
    /// Opened from PauseMenuController's Swap Character button, and still from Tab (via
    /// InputController.OnSwapMenuToggleInput, same event CharacterSwapUI used) for parity with
    /// the original shortcut. Not a SceneTransitionManager screen — like Pause/Settings, it's an
    /// overlay that temporarily takes Pause's place on top of Gameplay, then hands back to it.
    /// </summary>
    public class ChooseCharacterScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.18f;
        private const float HoldAfterSelectSeconds = 0.25f;
        private const float PopScale = 1.3f;

        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private GridLayoutGroup gridLayoutGroup;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject pauseMenuScreen;

        private bool _isBusy;

        private void Awake()
        {
            backButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            InputController.OnSwapMenuToggleInput += ToggleOpen;
        }

        private void OnDisable()
        {
            InputController.OnSwapMenuToggleInput -= ToggleOpen;
        }

        /// <summary>Tab's toggle target — mirrors CharacterSwapUI's old open/close-on-same-key
        /// behaviour, since this component is disabled while closed (OnEnable/OnDisable above
        /// only fire around the toggle itself, not this GameObject's own active state).</summary>
        public void ToggleOpen()
        {
            if (gameObject.activeSelf)
            {
                Close();
            }
            else
            {
                Show();
            }
        }

        public void Show()
        {
            if (pauseMenuScreen != null)
            {
                pauseMenuScreen.SetActive(false);
            }
            gameObject.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            if (pauseMenuScreen != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Paused)
            {
                pauseMenuScreen.SetActive(true);
            }
        }

        private void Refresh()
        {
            _isBusy = false;
            gridLayoutGroup.enabled = true;

            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }

            var activeType = CharacterManager.Instance != null
                ? CharacterManager.Instance.ActiveCharacter
                : (CharacterType?)null;

            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                var go = Instantiate(cardPrefab, cardContainer);
                var card = go.GetComponent<CharacterSelectCard>();
                bool unlocked = SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(data.characterType);
                bool isActive = activeType.HasValue && activeType.Value == data.characterType;
                card.Initialize(data, unlocked, isActive, HandleCardSelected);
            }
        }

        private void HandleCardSelected(CharacterSelectCard card)
        {
            if (_isBusy)
            {
                return;
            }
            _isBusy = true;
            StartCoroutine(SelectRoutine(card));
        }

        /// <summary>"Animates to the front": brought to the top of the sibling order (renders over
        /// its neighbours) and scaled up in place. Deliberately doesn't reposition to screen-center
        /// — the card sits inside a GridLayoutGroup, which would just snap any manual position
        /// change back on its next layout pass; scaling in place avoids that fight entirely while
        /// still reading clearly as "this is the one that got picked."</summary>
        private IEnumerator SelectRoutine(CharacterSelectCard card)
        {
            card.transform.SetAsLastSibling();
            var rect = (RectTransform)card.transform;
            Vector3 startScale = rect.localScale;
            Vector3 poppedScale = startScale * PopScale;

            float t = 0f;
            while (t < PopSeconds)
            {
                t += Time.unscaledDeltaTime;
                rect.localScale = Vector3.Lerp(startScale, poppedScale, Mathf.Clamp01(t / PopSeconds));
                yield return null;
            }
            rect.localScale = poppedScale;

            int cost = SaveManager.Instance != null && SaveManager.Instance.CoinBalance > 0 ? 1 : 0;
            if (cost > 0)
            {
                SaveManager.Instance.SpendCoins(cost);
            }
            CharacterManager.Instance.SwapCharacter(card.CharacterType);

            yield return new WaitForSecondsRealtime(HoldAfterSelectSeconds);

            Close();
        }
    }
}
