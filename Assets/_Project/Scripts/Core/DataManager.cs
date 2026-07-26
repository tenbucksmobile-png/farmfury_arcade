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

            Debug.Log($"[DataManager] Loaded {levels.Length} level data, {characters.Length} character data, {robots.Length} robot data.");
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

        public List<CharacterData> GetAllUnlockedCharacters()
        {
            return _characters.Values
                .Where(character => SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(character.characterType))
                .ToList();
        }
    }
}
