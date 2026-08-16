using System;
using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Tracks the order of characters used within the current maze (reset by SceneController on
    /// LoadLevelContent) and detects the 8 GDD combos on each character swap. Combo effects that
    /// "modify next ability use" are stored here as one-shot Pending* flags rather than on the
    /// ability instances themselves, because swapping destroys and recreates character GameObjects
    /// — a flag on e.g. BounceRollAbility would be lost the moment Percy is swapped away and back.
    /// Each affected ability calls the matching Consume* method at the top of its own Execute().
    /// </summary>
    public class ComboSystem : Singleton<ComboSystem>
    {
        public event Action<string> OnComboTriggered;

        private readonly List<CharacterType> _usedOrder = new List<CharacterType>();
        private readonly HashSet<CharacterType> _usedDistinct = new HashSet<CharacterType>();
        private int _bessieActivations;
        private bool _fullFuryFired;

        /// <summary>DailyChallengeManager's "Combo Hunt" objective, and DistinctCharactersUsedCount
        /// for its "Character Locked" objective.</summary>
        public bool AnyComboTriggeredThisMaze { get; private set; }
        public int DistinctCharactersUsedCount => _usedDistinct.Count;

        private readonly List<string> _combosTriggeredThisMaze = new List<string>();
        /// <summary>Names of every combo triggered this maze, in order — LevelCompleteController's
        /// "combo achievements this run" list.</summary>
        public IReadOnlyList<string> CombosTriggeredThisMaze => _combosTriggeredThisMaze;

        // One-shot buffs consumed by the named ability's Execute() the next time it activates.
        // Earthquake Roll, Kick and Roll -> BounceRollAbility. Name/field predate BounceRollAbility's
        // rework from wall-phasing to a forward roll-and-kill — now buffs roll DISTANCE (3 tiles ->
        // 9) for the next activation instead of wall count, same "one buffed use" one-shot contract.
        public bool PendingTripleWallPhase { get; private set; }
        public bool PendingEggDropClones { get; private set; }    // Feather Storm -> TripleCloneAbility
        public bool PendingDoubleWoolClones { get; private set; } // Skip Shatter -> SkipShotAbility
        public bool PendingDoubleSlamRadius { get; private set; } // Double Slam -> GroundSlamAbility
        public bool PendingDoubleKnockback { get; private set; }  // Crossfire -> RearKickAbility
        public bool PendingWallDestroyPuff { get; private set; }  // Iron Stampede -> PuffUpAbility

        public void ResetForNewMaze()
        {
            _usedOrder.Clear();
            _usedDistinct.Clear();
            _bessieActivations = 0;
            _fullFuryFired = false;
            AnyComboTriggeredThisMaze = false;
            _combosTriggeredThisMaze.Clear();
            PendingTripleWallPhase = false;
            PendingEggDropClones = false;
            PendingDoubleWoolClones = false;
            PendingDoubleSlamRadius = false;
            PendingDoubleKnockback = false;
            PendingWallDestroyPuff = false;
        }

        /// <summary>Records the maze's starting character (no "previous" to pair-check against).</summary>
        public void RegisterInitialCharacter(CharacterType type)
        {
            _usedOrder.Add(type);
            _usedDistinct.Add(type);
            if (type == CharacterType.Bessie)
            {
                _bessieActivations++;
            }
        }

        public void RegisterCharacterSwap(CharacterType previous, CharacterType next)
        {
            _usedOrder.Add(next);
            _usedDistinct.Add(next);
            if (next == CharacterType.Bessie)
            {
                _bessieActivations++;
            }

            CheckPairCombos(previous, next);
            CheckFullFury();
        }

        private void CheckPairCombos(CharacterType previous, CharacterType next)
        {
            if (previous == CharacterType.Cluck && next == CharacterType.Woolly)
            {
                Trigger("Feather Storm", () => PendingEggDropClones = true);
            }
            else if (previous == CharacterType.Bessie && next == CharacterType.Percy)
            {
                Trigger("Earthquake Roll", () => PendingTripleWallPhase = true);
            }
            else if (previous == CharacterType.Ducky && next == CharacterType.Woolly)
            {
                Trigger("Skip Shatter", () => PendingDoubleWoolClones = true);
            }
            else if (next == CharacterType.Bessie && _bessieActivations >= 2)
            {
                Trigger("Double Slam", () => PendingDoubleSlamRadius = true);
            }
            else if (previous == CharacterType.Billy && next == CharacterType.Horace)
            {
                Trigger("Crossfire", () => PendingDoubleKnockback = true);
            }
            else if (previous == CharacterType.Bessie && next == CharacterType.Gerald)
            {
                Trigger("Iron Stampede", () => PendingWallDestroyPuff = true);
            }
            else if (previous == CharacterType.Horace && next == CharacterType.Percy)
            {
                Trigger("Kick and Roll", () => PendingTripleWallPhase = true);
            }
        }

        private void CheckFullFury()
        {
            if (_fullFuryFired || _usedDistinct.Count < 5)
            {
                return;
            }

            _fullFuryFired = true;
            Trigger("Full Fury", () =>
            {
                foreach (var robot in UnityEngine.Object.FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
                {
                    robot.Stun(6f); // was 5f — extended per the same gameplay pass as the other stun-based abilities
                }
            });
        }

        private void Trigger(string comboName, Action effect)
        {
            effect();
            AnyComboTriggeredThisMaze = true;
            _combosTriggeredThisMaze.Add(comboName);
            SaveManager.Instance?.IncrementTotalCombosTriggered();
            Debug.Log($"[ComboSystem] Combo triggered: {comboName}");
            OnComboTriggered?.Invoke(comboName);
        }

        public bool ConsumeTripleWallPhase()
        {
            bool value = PendingTripleWallPhase;
            PendingTripleWallPhase = false;
            return value;
        }

        public bool ConsumeEggDropClones()
        {
            bool value = PendingEggDropClones;
            PendingEggDropClones = false;
            return value;
        }

        public bool ConsumeDoubleWoolClones()
        {
            bool value = PendingDoubleWoolClones;
            PendingDoubleWoolClones = false;
            return value;
        }

        public bool ConsumeDoubleSlamRadius()
        {
            bool value = PendingDoubleSlamRadius;
            PendingDoubleSlamRadius = false;
            return value;
        }

        public bool ConsumeDoubleKnockback()
        {
            bool value = PendingDoubleKnockback;
            PendingDoubleKnockback = false;
            return value;
        }

        public bool ConsumeWallDestroyPuff()
        {
            bool value = PendingWallDestroyPuff;
            PendingWallDestroyPuff = false;
            return value;
        }
    }
}
