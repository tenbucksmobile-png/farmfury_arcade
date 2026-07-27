using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>One character's entry in CharacterRosterScreen — unlocked or its unlock
    /// requirement.</summary>
    public class RosterCard : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statusText;

        public void Initialize(CharacterData data)
        {
            bool unlocked = SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(data.characterType);
            nameText.text = data.displayName;
            statusText.text = unlocked ? "Unlocked" : $"Unlocks at {data.unlockLevel} mazes completed";
            portrait.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f);
        }
    }
}
