using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Monetisation Build Plan Phase 4's cosmetics purchase/equip surface — Hat/Trail/MazeTheme
    /// category tabs, a scrolling row of CosmeticCardController cards, buy-with-coins then
    /// auto-equip on tap. Reached via ShopController's own "Cosmetics" button, layered on top of
    /// the Shop overlay the same way ChooseCharacterScreen layers on top of Pause. Overlay
    /// convention (shown/hidden via Show()/SetActive, not SceneTransitionManager), same as
    /// ShopController/SettingsPanel.
    ///
    /// Skin isn't a tab yet — no Skin CosmeticData assets exist. MazeTheme's tab renders an empty
    /// card row until MazeTheme assets exist (its tab/icon are wired now since the art already
    /// landed, so no further Editor-tool work will be needed when that content does).
    /// </summary>
    public class CosmeticStoreScreen : MonoBehaviour
    {
        [Serializable]
        private struct TabButton
        {
            public CosmeticType type;
            public Button button;
            public Image icon;
        }

        [SerializeField] private TabButton[] tabButtons;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TextMeshProUGUI coinBalanceText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button closeButton;

        /// <summary>Dims a tab's icon when it isn't the active tab — no dedicated "selected" art
        /// exists yet, same placeholder-tint convention LockedTint/LockedWorldTint use elsewhere.</summary>
        private static readonly Color InactiveTabTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        private CosmeticType _activeTab = CosmeticType.Hat;
        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }

            if (tabButtons == null)
            {
                return;
            }

            foreach (var tab in tabButtons)
            {
                if (tab.button == null)
                {
                    continue;
                }
                CosmeticType type = tab.type;
                tab.button.onClick.AddListener(() => SelectTab(type));
            }
        }

        private void OnEnable()
        {
            RefreshCoinBalanceText();
            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
            SelectTab(_activeTab);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        private void SelectTab(CosmeticType type)
        {
            _activeTab = type;

            if (tabButtons != null)
            {
                foreach (var tab in tabButtons)
                {
                    if (tab.icon != null)
                    {
                        tab.icon.color = tab.type == type ? Color.white : InactiveTabTint;
                    }
                }
            }

            PopulateCards(type);
        }

        private void PopulateCards(CosmeticType type)
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                {
                    Destroy(card);
                }
            }
            _spawnedCards.Clear();

            if (cardContainer == null || cardPrefab == null || DataManager.Instance == null || SaveManager.Instance == null)
            {
                return;
            }

            IEnumerable<CosmeticData> cosmetics = type == CosmeticType.Hat || type == CosmeticType.Skin
                ? DataManager.Instance.GetCosmeticsForCharacter(CharacterManager.Instance.ActiveCharacter, type)
                : DataManager.Instance.GetCosmeticsByType(type);

            foreach (var data in cosmetics)
            {
                var cardGO = Instantiate(cardPrefab, cardContainer);
                var card = cardGO.GetComponent<CosmeticCardController>();
                bool owned = SaveManager.Instance.IsCosmeticOwned(data.cosmeticId);
                bool equipped = IsEquipped(data);
                card.Initialize(data, owned, equipped, HandleCardTapped);
                _spawnedCards.Add(cardGO);
            }
        }

        private bool IsEquipped(CosmeticData data)
        {
            switch (data.cosmeticType)
            {
                case CosmeticType.Hat:
                case CosmeticType.Skin:
                    return SaveManager.Instance.GetEquippedCosmetic(data.cosmeticType, CharacterManager.Instance.ActiveCharacter) == data.cosmeticId;
                case CosmeticType.Trail:
                    return SaveManager.Instance.GetEquippedTrail() == data.cosmeticId;
                case CosmeticType.MazeTheme:
                    return SaveManager.Instance.GetEquippedMazeTheme(data.mazeType) == data.cosmeticId;
                default:
                    return false;
            }
        }

        private void HandleCardTapped(CosmeticData data)
        {
            if (SaveManager.Instance == null)
            {
                return;
            }

            if (!SaveManager.Instance.IsCosmeticOwned(data.cosmeticId))
            {
                if (!SaveManager.Instance.PurchaseCosmetic(data))
                {
                    if (statusText != null)
                    {
                        statusText.text = "Not enough coins!";
                    }
                    return;
                }
            }

            EquipCosmetic(data);

            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
            RefreshCoinBalanceText();
            SelectTab(_activeTab);
        }

        private void EquipCosmetic(CosmeticData data)
        {
            switch (data.cosmeticType)
            {
                case CosmeticType.Hat:
                case CosmeticType.Skin:
                    SaveManager.Instance.SetEquippedCosmetic(data.cosmeticType, CharacterManager.Instance.ActiveCharacter, data.cosmeticId);
                    CharacterManager.Instance.ActiveCharacterObject?.GetComponent<CharacterCosmeticRenderer>()?.Refresh();
                    break;
                case CosmeticType.Trail:
                    SaveManager.Instance.SetEquippedTrail(data.cosmeticId);
                    break;
                case CosmeticType.MazeTheme:
                    SaveManager.Instance.SetEquippedMazeTheme(data.mazeType, data.cosmeticId);
                    break;
            }
        }

        private void RefreshCoinBalanceText()
        {
            if (coinBalanceText != null && SaveManager.Instance != null)
            {
                coinBalanceText.text = SaveManager.Instance.CoinBalance.ToString();
            }
        }
    }
}
