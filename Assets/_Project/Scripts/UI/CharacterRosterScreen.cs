using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Main Menu's "Character Roster" — unlock progress for all 8 characters.</summary>
    public class CharacterRosterScreen : MonoBehaviour
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject mainMenuScreen;

        private void Awake()
        {
            backButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnEnable()
        {
            foreach (Transform child in cardContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var data in DataManager.Instance.GetAllCharacterData())
            {
                var go = Instantiate(cardPrefab, cardContainer);
                go.GetComponent<RosterCard>().Initialize(data);
            }
        }
    }
}
