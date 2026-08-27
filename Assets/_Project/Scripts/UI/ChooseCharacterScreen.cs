using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Real uGUI "Choose Character" panel — replaces the old debug-style OnGUI CharacterSwapUI.
    /// Opened via Tab (InputController.OnSwapMenuToggleInput, same event CharacterSwapUI used) —
    /// PauseMenuController's own Swap Character button is gone as of its 2026-08-27 mockup redesign
    /// (this screen, its Tab shortcut, and its pauseMenuScreen back-reference below are all still
    /// fully intact; only that one entry point went away, pending a move onto Gameplay HUD itself).
    /// Not a SceneTransitionManager screen — like Pause/Settings, it's an overlay that temporarily
    /// takes Pause's place on top of Gameplay, then hands back to it.
    /// Rebuilt to a Canva mockup (2026-07-31): cards sit in a CardCarouselController (the same
    /// component Level Select's world picker uses) instead of a static GridLayoutGroup — flick to
    /// cycle which card is centred/full-scale, tap the centred card to swap into it.
    /// </summary>
    public class ChooseCharacterScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.18f;
        private const float HoldAfterSelectSeconds = 0.25f;
        private const float PopScale = 1.3f;

        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private CardCarouselController carousel;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject pauseMenuScreen;

        private readonly List<CharacterSelectCard> _cards = new List<CharacterSelectCard>();
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

            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }
            carousel.enabled = true;
            carousel.ClearItems();
            _cards.Clear();

            var activeType = CharacterManager.Instance != null
                ? CharacterManager.Instance.ActiveCharacter
                : (CharacterType?)null;

            int startIndex = 0;
            int index = 0;
            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                var go = Instantiate(cardPrefab, cardContainer);
                var card = go.GetComponent<CharacterSelectCard>();
                bool unlocked = SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(data.characterType);
                bool isActive = activeType.HasValue && activeType.Value == data.characterType;
                // No direct onSelected here — the carousel is the sole trigger for a swap (see
                // HandleCarouselCenterTapped), so a card doesn't dive in on tap unless it's already
                // the centred one. Initialize still needs a non-null delegate shape, so this passes
                // null rather than duplicating carousel's own tap gating in a second listener.
                card.Initialize(data, unlocked, isActive, null);
                _cards.Add(card);
                if (isActive)
                {
                    startIndex = index;
                }
                index++;
            }

            var rects = new List<RectTransform>(_cards.Count);
            var buttons = new List<Button>(_cards.Count);
            foreach (var card in _cards)
            {
                rects.Add((RectTransform)card.transform);
                buttons.Add(card.GetComponent<Button>());
            }
            carousel.SetItems(rects, buttons, startIndex, HandleCarouselCenterTapped);
        }

        private void HandleCarouselCenterTapped(int index)
        {
            if (_isBusy || index < 0 || index >= _cards.Count)
            {
                return;
            }
            _isBusy = true;
            StartCoroutine(SelectRoutine(_cards[index]));
        }

        /// <summary>Pop-scales the tapped card in place so the player can see which one was picked,
        /// then swaps and auto-resumes gameplay directly — deliberately skips landing back on the
        /// Pause menu (unlike the Back button, which does return there): picking a character is
        /// itself the "I'm done, let's go" action. Doesn't reposition to screen-centre — the card is
        /// already centred (only the centred card's tap reaches this point at all, per
        /// CardCarouselController's gating) — scaling in place is enough to read as "this one got
        /// picked" without fighting the carousel's own per-frame layout pass.</summary>
        private IEnumerator SelectRoutine(CharacterSelectCard card)
        {
            // Carousel's own Update() re-applies every card's scale from its distance-to-centre
            // every frame — left enabled, it would fight this routine's scale tween for the same
            // RectTransform.localScale and the pop would never visibly happen. Re-enabled at the
            // top of Refresh() on next open, not here — this screen closes right after anyway.
            carousel.enabled = false;
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

            gameObject.SetActive(false);
            if (pauseMenuScreen != null)
            {
                pauseMenuScreen.SetActive(false);
            }
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.ResumeGame();
            }
        }
    }
}
