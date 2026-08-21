using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Character Story overlay — "this is where we will tell a story about each
    /// character," no real story content exists yet, only the roster layout (see
    /// Phase5ProjectBuilder.BuildCharacterStoryPlaceholder's own doc comment). Populates the left
    /// column with one CharacterSelectCard per character, in the same Cluck-first hierarchy order
    /// DataManager.GetAllCharacterData() already returns (matches ChooseCharacterScreen's own
    /// ordering). Every card shows unlocked/non-active/non-interactive — this is a browsing list,
    /// not the swap gate ChooseCharacterScreen enforces, and tapping a card does nothing yet since
    /// there's no story to open.</summary>
    public class CharacterStoryScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button closeButton;

        private bool _populated;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }

        private void OnEnable()
        {
            PopulateIfNeeded();
        }

        private void PopulateIfNeeded()
        {
            if (_populated || cardContainer == null || cardPrefab == null || DataManager.Instance == null)
            {
                return;
            }

            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                var go = Instantiate(cardPrefab, cardContainer);
                var card = go.GetComponent<CharacterSelectCard>();
                if (card != null)
                {
                    card.Initialize(data, unlocked: true, isActive: false, onSelected: null);
                }
            }

            _populated = true;
        }
    }
}
