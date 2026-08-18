using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>One purchasable/equippable cosmetic card in CosmeticStoreScreen. Modeled directly
    /// on CharacterSelectCard's shape (same Initialize(...)/button.interactable/
    /// RemoveAllListeners-then-AddListener idiom) — the "tap to act, disabled once already active"
    /// pattern there maps onto "tap to buy or equip, disabled once already equipped" here.</summary>
    public class CosmeticCardController : MonoBehaviour
    {
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonLabel;

        public void Initialize(CosmeticData data, bool owned, bool equipped, Action<CosmeticData> onTapped)
        {
            iconImage.sprite = data.previewSprite != null ? data.previewSprite : PlaceholderSprite.Get(new Color(0.5f, 0.5f, 0.55f));
            nameText.text = data.displayName;

            if (priceText != null)
            {
                priceText.gameObject.SetActive(!owned);
                priceText.text = owned ? string.Empty : data.coinCost.ToString();
            }

            if (equippedBadge != null)
            {
                equippedBadge.SetActive(equipped);
            }

            if (actionButtonLabel != null)
            {
                actionButtonLabel.text = !owned ? $"Buy {data.coinCost}" : (equipped ? "Equipped" : "Equip");
            }

            // Can't tap an already-equipped item — same "disabled once already active" convention
            // CharacterSelectCard uses for the currently-active character's own card.
            actionButton.interactable = !(owned && equipped);
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => onTapped?.Invoke(data));
        }
    }
}
