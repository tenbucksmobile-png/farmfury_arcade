using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Percy's ability. Arms a "phase" window: the next wall Percy hits in his current
    /// direction becomes temporarily walkable for 2 seconds (glowing while phaseable), then reverts.
    /// Earthquake Roll (Bessie -> Percy) and Kick and Roll (Horace -> Percy) both buff this to 3
    /// walls instead of 1 for the next activation.</summary>
    public class BounceRollAbility : AbilityBase
    {
        private const float PhaseDurationSeconds = 2f;
        private static readonly Color GlowColor = Color.cyan;

        [SerializeField] private GameObject trailPrefab;

        private bool _armed;
        private int _wallsRemaining;
        private GameObject _activeTrail;

        protected override void Execute()
        {
            bool tripleBuff = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeTripleWallPhase();
            _wallsRemaining = tripleBuff ? 3 : 1;
            _armed = true;

            if (trailPrefab != null)
            {
                if (_activeTrail != null)
                {
                    Destroy(_activeTrail);
                }
                _activeTrail = Instantiate(trailPrefab, transform);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!_armed || TileMap == null)
            {
                return;
            }

            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;
            Vector2Int aheadCell = Movement.CurrentGridPosition + DirectionUtils.ToVector(facing);

            if (!TileMap.IsWalkable(aheadCell))
            {
                StartCoroutine(PhaseWallTemporarily(aheadCell));
                _wallsRemaining--;
                if (_wallsRemaining <= 0)
                {
                    _armed = false;
                    if (_activeTrail != null)
                    {
                        Destroy(_activeTrail);
                        _activeTrail = null;
                    }
                }
            }
        }

        private IEnumerator PhaseWallTemporarily(Vector2Int cell)
        {
            var wallGO = TileMap.GetWallAt(cell);
            var wallSr = wallGO != null ? wallGO.GetComponent<SpriteRenderer>() : null;
            Color original = default;
            if (wallSr != null)
            {
                original = wallSr.color;
                wallSr.color = Color.Lerp(original, GlowColor, 0.5f);
            }

            TileMap.SetTemporaryWalkable(cell, true);
            yield return new WaitForSeconds(PhaseDurationSeconds);
            TileMap.SetTemporaryWalkable(cell, false);

            if (wallSr != null)
            {
                wallSr.color = original;
            }
        }
    }
}
