using System;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>One tappable card in ChooseCharacterScreen. Uses CharacterData.selectCardArt (a
    /// framed "animal card" image with its own wood-frame border baked in) when available, falling
    /// back to a plain placeholder square for any character without dedicated card art yet — same
    /// fallback convention used everywhere else uploaded art is partial.</summary>
    public class CharacterSelectCard : MonoBehaviour
    {
        [SerializeField] private Image cardImage;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject activeHighlight;
        [SerializeField] private Button button;

        public CharacterType CharacterType { get; private set; }

        public void Initialize(CharacterData data, bool unlocked, bool isActive, Action<CharacterSelectCard> onSelected)
        {
            CharacterType = data.characterType;

            bool hasCardArt = data.selectCardArt != null;
            cardImage.sprite = hasCardArt ? data.selectCardArt : PlaceholderSprite.Get(new Color(0.5f, 0.5f, 0.55f));
            cardImage.color = hasCardArt ? Color.white : new Color(0.5f, 0.5f, 0.55f);

            if (lockIcon != null)
            {
                lockIcon.SetActive(!unlocked);
            }
            if (activeHighlight != null)
            {
                activeHighlight.SetActive(isActive);
            }

            button.interactable = unlocked && !isActive;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(this));
        }
    }
}
