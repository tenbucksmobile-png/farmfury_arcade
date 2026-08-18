using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Loads every LevelData / CharacterData / RobotData ScriptableObject at startup and
    /// exposes lookup methods. Assets are loaded via Resources.LoadAll, so the
    /// ScriptableObjects/Resources/{Levels,Characters,Robots} folders are the source of truth.
    /// Phase 1 keeps this simple; a later phase can swap Resources for Addressables without
    /// touching the public API below.
    /// </summary>
    public class DataManager : Singleton<DataManager>
    {
        private readonly Dictionary<int, LevelData> _levelsByIndex = new Dictionary<int, LevelData>();
        private readonly Dictionary<CharacterType, CharacterData> _characters = new Dictionary<CharacterType, CharacterData>();
        private readonly Dictionary<RobotType, RobotData> _robots = new Dictionary<RobotType, RobotData>();
        private readonly Dictionary<string, CosmeticData> _cosmeticsById = new Dictionary<string, CosmeticData>();

        protected override void Awake()
        {
            base.Awake();
            LoadAllData();
        }

        private void LoadAllData()
        {
            _levelsByIndex.Clear();
            _characters.Clear();
            _robots.Clear();
            _cosmeticsById.Clear();

            var levels = Resources.LoadAll<LevelData>("Levels");
            foreach (var level in levels)
            {
                _levelsByIndex[level.levelNumber] = level;
            }

            var characters = Resources.LoadAll<CharacterData>("Characters");
            foreach (var character in characters)
            {
                _characters[character.characterType] = character;
            }

            var robots = Resources.LoadAll<RobotData>("Robots");
            foreach (var robot in robots)
            {
                _robots[robot.robotType] = robot;
            }

            var cosmetics = Resources.LoadAll<CosmeticData>("Cosmetics");
            foreach (var cosmetic in cosmetics)
            {
                if (string.IsNullOrEmpty(cosmetic.cosmeticId))
                {
                    Debug.LogWarning($"[DataManager] CosmeticData '{cosmetic.name}' has no cosmeticId set — skipped.");
                    continue;
                }
                _cosmeticsById[cosmetic.cosmeticId] = cosmetic;
            }

            Debug.Log($"[DataManager] Loaded {levels.Length} level data, {characters.Length} character data, {robots.Length} robot data, {cosmetics.Length} cosmetic data.");
        }

        public LevelData GetLevelData(int levelIndex)
        {
            return _levelsByIndex.TryGetValue(levelIndex, out var level) ? level : null;
        }

        public CharacterData GetCharacterData(CharacterType type)
        {
            return _characters.TryGetValue(type, out var character) ? character : null;
        }

        public RobotData GetRobotData(RobotType type)
        {
            return _robots.TryGetValue(type, out var robot) ? robot : null;
        }

        /// <summary>Ordered by CharacterType's declaration order (Cluck, Bessie, Percy, Woolly,
        /// Ducky, Horace, Gerald, Billy) rather than the backing Dictionary's insertion order —
        /// that order tracks Resources.LoadAll's own arbitrary asset-loading order, not anything
        /// deliberate, so ChooseCharacterScreen's carousel (and CharacterRosterScreen) rendered
        /// characters in a shuffled-looking sequence instead of Cluck/Bessie (the two starter,
        /// always-unlocked characters) coming first.</summary>
        public IEnumerable<CharacterData> GetAllCharacterData()
        {
            return _characters.Values.OrderBy(c => (int)c.characterType);
        }

        /// <summary>Ordered by levelNumber — LevelSelectController iterates this rather than
        /// assuming a fixed level count (only a couple of LevelData assets exist so far; the
        /// GDD's 100-level roadmap is a content-authoring task, not something this method
        /// should hardcode a range for).</summary>
        public IEnumerable<LevelData> GetAllLevelData()
        {
            return _levelsByIndex.Values.OrderBy(level => level.levelNumber);
        }

        public List<CharacterData> GetAllUnlockedCharacters()
        {
            return _characters.Values
                .Where(character => SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(character.characterType))
                .ToList();
        }

        public CosmeticData GetCosmeticData(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId))
            {
                return null;
            }
            return _cosmeticsById.TryGetValue(cosmeticId, out var cosmetic) ? cosmetic : null;
        }

        public IEnumerable<CosmeticData> GetAllCosmetics()
        {
            return _cosmeticsById.Values;
        }

        /// <summary>Hat/Skin cosmetics for one character — Store's per-character cosmetic tab and
        /// CharacterCosmeticRenderer's equip lookup both filter this way rather than scanning the
        /// full catalogue themselves.</summary>
        public IEnumerable<CosmeticData> GetCosmeticsForCharacter(CharacterType character, CosmeticType type)
        {
            return _cosmeticsById.Values.Where(c => c.cosmeticType == type && c.character == character);
        }

        public IEnumerable<CosmeticData> GetCosmeticsByType(CosmeticType type)
        {
            return _cosmeticsById.Values.Where(c => c.cosmeticType == type);
        }
    }
}
