using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>
    /// Grid-based AI movement and state machine shared by every robot type. Movement mirrors
    /// GridMovement's continuous-move-to-next-cell-centre algorithm, but the direction each robot
    /// wants next comes from AI (RobotAI.GetNextDirection / ComputeDesiredDirection) instead of a
    /// queued player input, so it deliberately does not reuse GridMovement itself — GridMovement
    /// subscribes to InputController's static OnDirectionInput event, and giving robots that
    /// component too would make every robot obey player input.
    ///
    /// State machine: Chase and Scatter alternate on a 20s/5s cycle (paused while Vulnerable/
    /// Defeated/Returning and resumed from where it left off). PowerPelletManager broadcasts
    /// power on/off; every enabled robot listens and flips to/from Vulnerable. A hit while
    /// Vulnerable decrements health (RegisterHit); health reaching zero triggers a brief Defeated
    /// pause, then Returning (fast pathfind to the factory), then a respawn back to Chase once the
    /// factory cell is reached.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public abstract class RobotBase : MonoBehaviour
    {
        private const float ChaseDurationSeconds = 20f;
        private const float ScatterDurationSeconds = 5f;
        private const float DefeatedPauseSeconds = 0.5f;

        /// <summary>How many of this robot's most-recently-occupied cells RobotAI.GetNextDirection
        /// discourages re-entering — see that method's doc comment for why (breaks short
        /// greedy-heuristic loops between two similarly-distant intersections, distinct from the
        /// existing no-U-turn rule). Short enough that a robot happily re-enters ground it left a
        /// few turns ago rather than ever getting hard-blocked in a small pocket.</summary>
        private const int RecentCellHistory = 6;

        [SerializeField] protected RobotData robotData;

        protected TileMapRenderer tileMap;
        protected Vector2Int spawnGridPosition;
        protected Vector2Int factoryPosition;
        protected Vector2Int scatterCornerPosition;

        private int _healthPoints = 1;
        private RobotState _stateBeforeVulnerable = RobotState.Chase;
        private float _chaseScatterTimer;
        private bool _exitingFactory = true;
        private bool _initialized;
        private float _stunTimer;
        private readonly Queue<Vector2Int> _recentCells = new Queue<Vector2Int>();
        private Vector2Int? _lastRecentCell;

        public RobotState CurrentState { get; protected set; } = RobotState.Chase;
        public Vector2Int CurrentGridPosition { get; protected set; }
        public Direction CurrentDirection { get; protected set; } = Direction.None;
        public RobotData Data => robotData;
        public bool IsStunned { get; private set; }
        public bool IsKnockedBack { get; private set; }

        /// <summary>Set once a defeated robot finishes its brief eyes-only pause and disappears (see
        /// Disappear()) — it stays gone for the rest of THIS maze rather than pathfinding back to the
        /// factory and respawning into Chase, per playtest feedback that "floating eyes" walking back
        /// through the maze read as a bug, not a feature. RobotSpawner.SpawnLevelRobots already
        /// destroys and recreates every robot fresh on the next LoadLevel, so a new level/stage is the
        /// only thing that brings a defeated robot back. RobotSpawner.ResetAllRobotsToFactory also
        /// checks this (a permanently-defeated robot should stay gone if it's ever called) — it's no
        /// longer invoked on player death, though (see PlayerHealth.DeathSequence): only the
        /// character respawns now, robots stay wherever they are.</summary>
        public bool IsPermanentlyDefeated { get; private set; }

        /// <summary>Live lookup of whichever character is currently active, via CharacterManager
        /// (Phase 4) — NOT cached, because character-swapping destroys and recreates the player
        /// GameObject, which would leave a cached reference stale. Same identifier as the old
        /// Phase 3 field so every subclass's existing `playerMovement.CurrentGridPosition`-style
        /// code keeps compiling unchanged.</summary>
        protected GridMovement playerMovement =>
            CharacterManager.Instance != null && CharacterManager.Instance.ActiveCharacterObject != null
                ? CharacterManager.Instance.ActiveCharacterObject.GetComponent<GridMovement>()
                : null;

        protected virtual float SpeedMultiplier => 1f;

        /// <summary>Runtime difficulty knob, distinct from the per-subclass SpeedMultiplier override
        /// above (Heavy's 0.7x, Drone's 0.5x, etc.) — this one is set from the outside, once, at
        /// spawn time (RobotSpawner.SpawnRobot, right after Initialize), so it stacks on top of
        /// whichever per-type multiplier already applies rather than replacing it. 1f for a normal
        /// level; DailyChallengeManager.RobotDifficultySpeedMultiplier for a Daily Challenge run —
        /// see RobotSpawner.DifficultyMultiplier's doc comment for how it gets here.</summary>
        private float _difficultyMultiplier = 1f;

        public void SetDifficultyMultiplier(float multiplier)
        {
            _difficultyMultiplier = multiplier;
        }

        /// <summary>Fraction of THIS ROBOT'S OWN normal (Chase/Scatter) RobotData.movementSpeed a
        /// Vulnerable robot flees at. 0.85 is a mild reduction ("slightly slower") rather than the
        /// old 0.5 (half speed) — a previous pass tried keying this off the ACTIVE CHARACTER's speed
        /// instead (character 4.0 * 0.85 = 3.4), which backfired: since robots chase at a much lower
        /// base speed (2.0) than any character, that made a fleeing robot move faster than it does
        /// while hunting, the opposite of the intent. Keying it off the robot's own speed keeps
        /// "slightly slower" meaning what it says — still comfortably outrun by any character (all
        /// unified to 4.0, see Phase4ProjectBuilder), but no longer faster than the robot's own
        /// normal pace.</summary>
        protected virtual float VulnerableSpeedMultiplier => 0.85f;
        protected virtual float ReturningSpeedMultiplier => 2f;
        protected virtual int InitialHealthPoints => robotData != null ? Mathf.Max(1, robotData.healthPoints) : 1;

        /// <summary>Called once by RobotSpawner right after Instantiate.</summary>
        public virtual void Initialize(RobotData data, TileMapRenderer maze, Vector2Int spawnPosition, Vector2Int corner)
        {
            robotData = data;
            tileMap = maze;
            spawnGridPosition = spawnPosition;
            factoryPosition = spawnPosition;
            scatterCornerPosition = corner;

            CurrentGridPosition = spawnPosition;
            transform.position = tileMap.GridToWorld(spawnPosition);

            _healthPoints = InitialHealthPoints;
            CurrentState = RobotState.Chase;
            CurrentDirection = Direction.None;
            _chaseScatterTimer = 0f;
            _exitingFactory = true;
            IsStunned = false;
            IsKnockedBack = false;
            _recentCells.Clear();
            _lastRecentCell = null;
            _initialized = true;
        }

        /// <summary>Resets this robot to its spawn cell and Chase state. Called by RobotSpawner
        /// when the player dies (per the GDD's "reset all robots to factory" death sequence).</summary>
        public virtual void ResetToFactory()
        {
            if (IsPermanentlyDefeated)
            {
                return;
            }

            StopAllCoroutines();
            CurrentDirection = Direction.None;
            CurrentState = RobotState.Chase;
            CurrentGridPosition = spawnGridPosition;
            transform.position = tileMap.GridToWorld(spawnGridPosition);
            _healthPoints = InitialHealthPoints;
            _chaseScatterTimer = 0f;
            _exitingFactory = true;
            IsStunned = false;
            IsKnockedBack = false;
            _recentCells.Clear();
            _lastRecentCell = null;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
            }
        }

        protected virtual void OnEnable()
        {
            if (PowerPelletManager.Instance != null)
            {
                PowerPelletManager.Instance.OnPowerStateChanged += HandlePowerStateChanged;
            }
        }

        protected virtual void OnDisable()
        {
            if (PowerPelletManager.Instance != null)
            {
                PowerPelletManager.Instance.OnPowerStateChanged -= HandlePowerStateChanged;
            }
        }

        protected virtual void Start()
        {
            if (tileMap == null)
            {
                tileMap = FindFirstObjectByType<TileMapRenderer>();
            }
        }

        protected virtual void Update()
        {
            if (!_initialized || tileMap == null)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            // KnockBackRoutine drives transform.position directly — don't let normal AI movement
            // fight it mid-slide.
            if (IsKnockedBack)
            {
                return;
            }

            if (IsStunned)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f)
                {
                    IsStunned = false;
                }
                return;
            }

            UpdateStateTimer();
            UpdateMovement();
        }

        /// <summary>Freezes this robot in place (no state-cycle progress, no movement) for
        /// duration seconds. Used by EggDropAbility, GroundSlamAbility, and RearKickAbility's
        /// knockback landing. Does nothing to a robot that's already "eyes" (Defeated/Returning).
        /// Extends rather than resets if already stunned with more time remaining.</summary>
        public virtual void Stun(float duration)
        {
            if (CurrentState == RobotState.Defeated || CurrentState == RobotState.Returning)
            {
                return;
            }

            IsStunned = true;
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>Slides this robot up to tiles cells in direction (stopping early at a wall),
        /// freezing its AI for the slide, then defeats it on landing (ForceDefeat, bypassing the
        /// Vulnerable requirement — same convention as PuffUpAbility). Used by RearKickAbility.
        /// Was a stun; changed per a gameplay rule that a deployed ability hazard should kill a
        /// robot that runs through it, not just incapacitate it.</summary>
        public virtual void KnockBack(Vector2Int direction, int tiles)
        {
            if (CurrentState == RobotState.Defeated || CurrentState == RobotState.Returning)
            {
                return;
            }

            StartCoroutine(KnockBackRoutine(direction, tiles));
        }

        private IEnumerator KnockBackRoutine(Vector2Int direction, int tiles)
        {
            const float slideSecondsPerTile = 0.08f;
            IsKnockedBack = true;

            Vector2Int cell = CurrentGridPosition;
            for (int moved = 0; moved < tiles; moved++)
            {
                Vector2Int next = cell + direction;
                if (!IsWalkableForThisRobot(next))
                {
                    break;
                }

                Vector3 from = tileMap.GridToWorld(cell);
                Vector3 to = tileMap.GridToWorld(next);
                float t = 0f;
                while (t < slideSecondsPerTile)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / slideSecondsPerTile));
                    yield return null;
                }
                transform.position = to;
                cell = next;
                CurrentGridPosition = cell;
            }

            IsKnockedBack = false;
            ForceDefeat();
        }

        /// <summary>Defeats this robot regardless of state (unlike RegisterHit, which requires
        /// Vulnerable) — used by PuffUpAbility, which bypasses the power-pellet requirement
        /// entirely while Gerald is puffed up.</summary>
        public virtual void ForceDefeat()
        {
            if (CurrentState == RobotState.Defeated || CurrentState == RobotState.Returning)
            {
                return;
            }

            TransitionToDefeated();
        }

        private void UpdateStateTimer()
        {
            if (CurrentState != RobotState.Chase && CurrentState != RobotState.Scatter)
            {
                return;
            }

            _chaseScatterTimer += Time.deltaTime;
            float duration = CurrentState == RobotState.Chase ? ChaseDurationSeconds : ScatterDurationSeconds;
            if (_chaseScatterTimer < duration)
            {
                return;
            }

            _chaseScatterTimer = 0f;
            CurrentState = CurrentState == RobotState.Chase ? RobotState.Scatter : RobotState.Chase;
        }

        /// <summary>Same crossing/clamp movement approach as GridMovement (see its doc comment for
        /// why the old fixed-epsilon "am I at the cell center" check was unreliable at real
        /// per-frame speeds — it applies here identically, since RobotBase mirrors that algorithm).
        /// Snaps exactly onto any cell boundary crossed this frame and carries over leftover
        /// distance, instead of only sometimes sampling a narrow center window once per frame.</summary>
        protected virtual void UpdateMovement()
        {
            if (CurrentState == RobotState.Defeated)
            {
                return;
            }

            float remaining = CurrentSpeed * TileMapRenderer.CellSize * Time.deltaTime;
            int guard = 0;
            while (remaining > 0f && guard++ < 8)
            {
                if (CurrentDirection == Direction.None)
                {
                    Vector2Int cell = tileMap.WorldToGrid(transform.position);
                    transform.position = tileMap.GridToWorld(cell);
                    CurrentGridPosition = cell;

                    if (EvaluateArrivalAndDirection(cell))
                    {
                        return;
                    }
                    if (CurrentDirection == Direction.None)
                    {
                        break;
                    }
                    continue;
                }

                Vector2Int fromCell = tileMap.WorldToGrid(transform.position);
                Vector2Int dirVector = DirectionUtils.ToVector(CurrentDirection);
                Vector2Int nextCell = fromCell + dirVector;

                if (!IsWalkableForThisRobot(nextCell))
                {
                    transform.position = tileMap.GridToWorld(fromCell);
                    CurrentGridPosition = fromCell;
                    CurrentDirection = Direction.None;
                    break;
                }

                Vector3 targetCenter = tileMap.GridToWorld(nextCell);
                float distToTarget = Vector3.Distance(transform.position, targetCenter);

                if (remaining >= distToTarget)
                {
                    transform.position = targetCenter;
                    CurrentGridPosition = nextCell;
                    remaining -= distToTarget;

                    if (EvaluateArrivalAndDirection(nextCell))
                    {
                        return;
                    }
                }
                else
                {
                    transform.position += new Vector3(dirVector.x, dirVector.y, 0f) * remaining;
                    remaining = 0f;
                }
            }
        }

        /// <summary>Runs on every cell-center arrival (and every loop iteration while stationary,
        /// matching the old code's per-frame atCenter re-evaluation): ComputeDesiredDirection picks
        /// (or fails to pick) a new heading. Returns true if a caller must stop immediately (unused
        /// now that defeat simply disappears the robot instead of pathfinding back to the factory —
        /// kept as a bool so this method's signature doesn't need to change if that ever comes back).</summary>
        private bool EvaluateArrivalAndDirection(Vector2Int cell)
        {
            // Guards against re-pushing the same cell on every stationary re-evaluation (this method
            // runs once per real cell arrival, but also once per loop iteration while CurrentDirection
            // stays None — see the doc comment above) — _lastRecentCell tracks the most recently
            // pushed cell directly, since Queue<T> only exposes Peek() on its FRONT (oldest) element.
            if (_lastRecentCell != cell)
            {
                _recentCells.Enqueue(cell);
                _lastRecentCell = cell;
                while (_recentCells.Count > RecentCellHistory)
                {
                    _recentCells.Dequeue();
                }
            }

            Direction desired = ComputeDesiredDirection(cell);
            if (desired != Direction.None && IsWalkableForThisRobot(cell + DirectionUtils.ToVector(desired)))
            {
                CurrentDirection = desired;
            }
            else if (CurrentDirection != Direction.None && !IsWalkableForThisRobot(cell + DirectionUtils.ToVector(CurrentDirection)))
            {
                CurrentDirection = Direction.None;
            }
            return false;
        }

        protected virtual Direction ComputeDesiredDirection(Vector2Int cell)
        {
            if (_exitingFactory)
            {
                if (IsWalkableForThisRobot(cell + DirectionUtils.ToVector(Direction.Up)))
                {
                    return Direction.Up;
                }
                _exitingFactory = false;
            }

            return RobotAI.GetNextDirection(cell, ResolveTarget(), CurrentDirection, tileMap, _recentCells);
        }

        protected virtual bool IsWalkableForThisRobot(Vector2Int cell) => tileMap.IsWalkable(cell);

        protected virtual float CurrentSpeed
        {
            get
            {
                float baseSpeed = robotData != null ? robotData.movementSpeed : 3f;
                float stateMultiplier = CurrentState switch
                {
                    RobotState.Vulnerable => VulnerableSpeedMultiplier,
                    RobotState.Returning => ReturningSpeedMultiplier,
                    _ => 1f
                };
                return baseSpeed * SpeedMultiplier * stateMultiplier * _difficultyMultiplier;
            }
        }

        /// <summary>Chase-state target — the one bit every subclass makes its own.</summary>
        protected abstract Vector2Int GetTargetPosition();

        protected virtual Vector2Int ResolveTarget()
        {
            return CurrentState switch
            {
                RobotState.Scatter => scatterCornerPosition,
                RobotState.Vulnerable => GetFleeTarget(),
                RobotState.Returning => factoryPosition,
                _ => GetTargetPosition()
            };
        }

        /// <summary>The maze's actual farthest-from-the-player reachable cell (a real BFS result,
        /// not a straight-line projection — see RobotAI.FindFarthestCell's doc comment for why the
        /// old projection approach fed the same straight-line bias that caused robots to get stuck
        /// oscillating in one row/column instead of genuinely fleeing).</summary>
        protected virtual Vector2Int GetFleeTarget()
        {
            if (playerMovement == null || tileMap == null)
            {
                return CurrentGridPosition;
            }

            return RobotAI.FindFarthestCell(playerMovement.CurrentGridPosition, tileMap);
        }

        protected virtual void HandlePowerStateChanged(bool active)
        {
            if (active)
            {
                if (CurrentState == RobotState.Defeated || CurrentState == RobotState.Returning)
                {
                    return;
                }

                _stateBeforeVulnerable = CurrentState;
                CurrentState = RobotState.Vulnerable;

                // Classic "frightened" cue: reverse on the spot when power activates.
                if (CurrentDirection != Direction.None)
                {
                    CurrentDirection = DirectionUtils.Opposite(CurrentDirection);
                }
            }
            else if (CurrentState == RobotState.Vulnerable)
            {
                CurrentState = _stateBeforeVulnerable;
            }
        }

        /// <summary>Called by PlayerHealth when Cluck touches this robot while it's Vulnerable.
        /// Default behaviour: any hit defeats a 1-health robot. HeavyRobot overrides to add its
        /// glitch effect while keeping the same decrement-then-check contract.</summary>
        public virtual void RegisterHit()
        {
            if (CurrentState != RobotState.Vulnerable)
            {
                return;
            }

            _healthPoints--;
            if (_healthPoints <= 0)
            {
                TransitionToDefeated();
            }
        }

        protected virtual void TransitionToDefeated()
        {
            CurrentState = RobotState.Defeated;
            CurrentDirection = Direction.None;
            ChaseScoreManager.Instance?.OnRobotDefeated();
            StartCoroutine(DefeatedThenReturn());
        }

        private IEnumerator DefeatedThenReturn()
        {
            yield return new WaitForSeconds(DefeatedPauseSeconds);
            Disappear();
        }

        /// <summary>Replaces the old "eyes pathfind back to the factory, then respawn into Chase"
        /// Returning flow — the robot simply vanishes (renderer + collider off) and stays gone for
        /// the rest of this maze. See IsPermanentlyDefeated's doc comment for why.</summary>
        private void Disappear()
        {
            IsPermanentlyDefeated = true;
            CurrentDirection = Direction.None;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }
}
