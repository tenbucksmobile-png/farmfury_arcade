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
    /// Defeated and resumed from where it left off). PowerPelletManager broadcasts power on/off;
    /// every enabled robot listens and flips to/from Vulnerable. A hit while Vulnerable decrements
    /// health (RegisterHit); health reaching zero triggers a brief Defeated pause, then the robot
    /// permanently vanishes for the rest of the maze (see Disappear) — there is no respawn-back-
    /// to-Chase path (an earlier "pathfind back to the factory, then respawn" Returning state was
    /// removed; RobotState no longer has that value at all).
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
        /// existing no-U-turn rule).
        ///
        /// Widened from 6 to 16 (2026-09-13, real bug found by reasoning through the maze generator
        /// rather than guessing): this game's mazes are only 12x9, and the generator deliberately
        /// adds 5-8 extra loop edges on top of its spanning tree — genuine cycles a robot can
        /// legitimately walk all the way around. At a memory window of 6, a robot could fully
        /// traverse a loop longer than 6 cells (easy on even a single row of a 12-wide maze) and
        /// arrive back at the same junction with its own earlier path no longer counted as
        /// "recent," re-committing to the same equally-good choice and circling indefinitely — this
        /// is what read as "gets caught in a loop, remaining in one row." 16 covers a
        /// single-row-scale loop comfortably while staying well under this maze's ~100-cell total,
        /// so it doesn't meaningfully constrain normal long-distance pathing.</summary>
        private const int RecentCellHistory = 16;

        /// <summary>Real cycle-detection, not just a wider memory window — even a 16-cell window
        /// only reduces the CHANCE of looping, it doesn't guarantee escaping one (a big enough loop,
        /// or an unlucky tie sequence, could still repeat). If a robot arrives at a cell it already
        /// has in its own recent history this many times, that's direct proof it's retracing ground
        /// rather than making progress — StuckRevisitThreshold gates a one-shot forced random
        /// escape (PickRandomEscapeDirection, bypassing BFS-optimal targeting entirely for that one
        /// decision) which structurally cannot repeat the same deterministic pattern, guaranteeing
        /// the cycle actually breaks rather than just becoming less likely.</summary>
        private const int StuckRevisitThreshold = 2;
        private int _stuckRevisitCount;

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

        /// <summary>Audit finding C4.2: this used to be a genuinely live GetComponent lookup on
        /// every single access — read from GetTargetPosition on the same hot path RobotAI's own
        /// per-frame pathing lives on, up to 8x/frame per robot while blocked (RobotBase.
        /// UpdateMovement's own guard), times up to 5 robots. Cached now, but self-healing rather
        /// than relying on an event: CharacterManager.OnCharacterChanged only fires on an explicit
        /// SwapCharacter call, NOT on SpawnInitialCharacter (which is what actually runs on every
        /// level load/reload per SceneController.LoadLevelContent) — an event-only cache would have
        /// gone stale after the first level and silently never refreshed again. Comparing against
        /// CharacterManager's own live ActiveCharacterObject reference on every access instead (a
        /// cheap reference check, not a GetComponent call) means GetComponent only actually runs
        /// again when that reference genuinely changed — correct on every path that changes it, not
        /// just the swap one. Static/shared across all robot instances since there's only ever one
        /// active character for all of them to reference. Same identifier as the old Phase 3 field
        /// so every subclass's existing `playerMovement.CurrentGridPosition`-style code keeps
        /// compiling unchanged.</summary>
        private static GameObject _cachedForCharacterObject;
        private static GridMovement _cachedPlayerMovement;

        protected GridMovement playerMovement
        {
            get
            {
                var current = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
                if (current != _cachedForCharacterObject)
                {
                    _cachedForCharacterObject = current;
                    _cachedPlayerMovement = current != null ? current.GetComponent<GridMovement>() : null;
                }
                return _cachedPlayerMovement;
            }
        }

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
            _stuckRevisitCount = 0;
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
            _stuckRevisitCount = 0;

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
        /// duration seconds. Only ComboSystem's Full Fury combo calls this today (a direct,
        /// immediate stun on every robot in the maze) — Does nothing to a robot that's already
        /// Defeated. Extends rather than resets if already stunned with more time remaining.</summary>
        public virtual void Stun(float duration)
        {
            if (CurrentState == RobotState.Defeated)
            {
                return;
            }

            IsStunned = true;
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>Slides this robot up to tiles cells in direction (stopping early at a wall),
        /// freezing its AI for the slide, then defeats it on landing (ForceDefeat, bypassing the
        /// Vulnerable requirement — same convention as PuffUpAbility).
        ///
        /// Currently unused — its only caller (Horace's old RearKickAbility, "find the nearest robot
        /// and yank it") was reworked 2026-09-16 into HorseshoeThrowAbility, a launched projectile
        /// that ForceDefeats on contact directly (ThrownProjectileEffect) with no knockback slide at
        /// all. Left in place as a generic, already-working robot mechanic rather than deleted, in
        /// case a future ability wants a knockback-then-defeat effect again.</summary>
        public virtual void KnockBack(Vector2Int direction, int tiles)
        {
            if (CurrentState == RobotState.Defeated)
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
            if (CurrentState == RobotState.Defeated)
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
            bool isNewArrival = _lastRecentCell != cell;
            // Checked BEFORE enqueueing, and only on a genuine new arrival — otherwise a robot
            // merely stationary on the same cell across several stationary re-checks would always
            // find itself "in" its own history (it enqueued itself on the first check) and falsely
            // read as stuck.
            bool isRevisit = isNewArrival && _recentCells.Contains(cell);

            if (isNewArrival)
            {
                _recentCells.Enqueue(cell);
                _lastRecentCell = cell;
                while (_recentCells.Count > RecentCellHistory)
                {
                    _recentCells.Dequeue();
                }

                // Genuine forward progress onto ground not in recent memory resets the counter;
                // a revisit increments it. A stationary re-check (isNewArrival false) touches
                // neither, so it can't reset progress made just before it nor fake one out.
                _stuckRevisitCount = isRevisit ? _stuckRevisitCount + 1 : 0;
            }

            Direction desired = _stuckRevisitCount >= StuckRevisitThreshold
                ? PickRandomEscapeDirection(cell)
                : ComputeDesiredDirection(cell);
            if (_stuckRevisitCount >= StuckRevisitThreshold)
            {
                _stuckRevisitCount = 0;
            }

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

        /// <summary>One-shot forced escape once EvaluateArrivalAndDirection's own cycle detection
        /// fires — picks uniformly among every currently-valid (non-reverse-unless-dead-end)
        /// direction, deliberately ignoring BFS-optimal targeting for this single decision. Bypassing
        /// the deterministic distance-based choice is the point: a robot stuck retracing its own
        /// steps got there BECAUSE the deterministic algorithm kept recommitting to the same
        /// "optimal" choice around a loop (see RecentCellHistory's own doc comment for the maze-
        /// loop-edge topology that causes this) — reusing that same logic here would just repeat the
        /// cycle instead of breaking it.
        ///
        /// Built from this robot's own IsWalkableForThisRobot (virtual) rather than calling
        /// RobotAI.GetValidDirections directly — that helper is hardwired to the maze's raw
        /// IsWalkable and would ignore DroneRobot's own wall-phasing override during exactly this
        /// one decision, the only real behavioural gap that would have introduced.</summary>
        private Direction PickRandomEscapeDirection(Vector2Int cell)
        {
            Direction reverse = DirectionUtils.Opposite(CurrentDirection);
            _escapeCandidatesScratch.Clear();
            foreach (var dir in AllEscapeDirections)
            {
                if (CurrentDirection != Direction.None && dir == reverse)
                {
                    continue;
                }
                if (IsWalkableForThisRobot(cell + DirectionUtils.ToVector(dir)))
                {
                    _escapeCandidatesScratch.Add(dir);
                }
            }
            if (_escapeCandidatesScratch.Count == 0 && CurrentDirection != Direction.None
                && IsWalkableForThisRobot(cell + DirectionUtils.ToVector(reverse)))
            {
                _escapeCandidatesScratch.Add(reverse);
            }
            return _escapeCandidatesScratch.Count == 0
                ? Direction.None
                : _escapeCandidatesScratch[Random.Range(0, _escapeCandidatesScratch.Count)];
        }

        private static readonly Direction[] AllEscapeDirections =
        {
            Direction.Up, Direction.Down, Direction.Left, Direction.Right
        };
        private readonly List<Direction> _escapeCandidatesScratch = new List<Direction>(4);

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

            // Some per-robot Chase targets (Scout's "4 tiles ahead of the player," Patrol's flanking
            // vector) are raw, unclamped projections that can land on a wall or off the maze grid —
            // see RobotAI.ClampToWalkable's own doc comment for why that silently reintroduces the
            // exact corridor-oscillation bug the BFS-distance rework was supposed to have fixed.
            // Snapping every resolved target to a real walkable cell here, centrally, means no
            // current or future robot subclass needs to remember to validate its own target.
            Vector2Int target = RobotAI.ClampToWalkable(ResolveTarget(), tileMap);
            return RobotAI.GetNextDirection(cell, target, CurrentDirection, tileMap, _recentCells);
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
                if (CurrentState == RobotState.Defeated)
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
            HighlightMarkers.Mark("robot_defeated",
                $"robot={(robotData != null ? robotData.robotType.ToString() : "Unknown")} " +
                $"chain={(ChaseScoreManager.Instance != null ? ChaseScoreManager.Instance.ChainCount : 0)} " +
                $"power={(PowerPelletManager.Instance != null && PowerPelletManager.Instance.IsPowerActive)}");
            // Single funnel for both kill paths (RegisterHit's power-pellet chain-kill and every
            // ForceDefeat ability-triggered instant-kill), so this fires exactly once per robot
            // defeated regardless of which route got it here — see AudioManager.PlayRobotDamageSfx's
            // own doc comment for why this is distinct from PlayEatRobotMusic/each ability's cast SFX.
            AudioManager.Instance?.PlayRobotDamageSfx();
            StartCoroutine(DefeatedThenDisappear());
        }

        private IEnumerator DefeatedThenDisappear()
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
