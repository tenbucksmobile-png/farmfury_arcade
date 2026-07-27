using System.Collections;
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
        private const float AlignmentEpsilon = 0.02f;
        private const float ChaseDurationSeconds = 20f;
        private const float ScatterDurationSeconds = 5f;
        private const float DefeatedPauseSeconds = 0.5f;
        private const float FactoryArriveEpsilonTiles = 0.5f;
        private const float FleeDistanceTiles = 10f;

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

        public RobotState CurrentState { get; protected set; } = RobotState.Chase;
        public Vector2Int CurrentGridPosition { get; protected set; }
        public Direction CurrentDirection { get; protected set; } = Direction.None;
        public RobotData Data => robotData;
        public bool IsStunned { get; private set; }
        public bool IsKnockedBack { get; private set; }

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
        protected virtual float VulnerableSpeedMultiplier => 0.5f;
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
            _initialized = true;
        }

        /// <summary>Resets this robot to its spawn cell and Chase state. Called by RobotSpawner
        /// when the player dies (per the GDD's "reset all robots to factory" death sequence).</summary>
        public virtual void ResetToFactory()
        {
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
        /// freezing its AI for the slide, then stuns it for stunDurationAfterLanding. Used by
        /// RearKickAbility.</summary>
        public virtual void KnockBack(Vector2Int direction, int tiles, float stunDurationAfterLanding)
        {
            if (CurrentState == RobotState.Defeated || CurrentState == RobotState.Returning)
            {
                return;
            }

            StartCoroutine(KnockBackRoutine(direction, tiles, stunDurationAfterLanding));
        }

        private IEnumerator KnockBackRoutine(Vector2Int direction, int tiles, float stunAfter)
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
            Stun(stunAfter);
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

        protected virtual void UpdateMovement()
        {
            if (CurrentState == RobotState.Defeated)
            {
                return;
            }

            Vector2Int cell = tileMap.WorldToGrid(transform.position);
            Vector3 cellCenter = tileMap.GridToWorld(cell);
            bool atCenter = Vector3.Distance(transform.position, cellCenter) < AlignmentEpsilon;

            if (atCenter)
            {
                transform.position = cellCenter;
                CurrentGridPosition = cell;

                if (CurrentState == RobotState.Returning &&
                    Vector2Int.Distance(cell, factoryPosition) <= FactoryArriveEpsilonTiles)
                {
                    ArriveAtFactory();
                    return;
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
            }

            if (CurrentDirection != Direction.None)
            {
                Vector2Int dirVector = DirectionUtils.ToVector(CurrentDirection);
                transform.position += new Vector3(dirVector.x, dirVector.y, 0f) * CurrentSpeed * Time.deltaTime;
            }
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

            return RobotAI.GetNextDirection(cell, ResolveTarget(), CurrentDirection, tileMap);
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
                return baseSpeed * SpeedMultiplier * stateMultiplier;
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

        protected virtual Vector2Int GetFleeTarget()
        {
            if (playerMovement == null)
            {
                return CurrentGridPosition;
            }

            Vector2Int away = CurrentGridPosition - playerMovement.CurrentGridPosition;
            if (away == Vector2Int.zero)
            {
                away = new Vector2Int(1, 0);
            }

            return CurrentGridPosition + away * Mathf.RoundToInt(FleeDistanceTiles);
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
            CurrentState = RobotState.Returning;
        }

        private void ArriveAtFactory()
        {
            CurrentDirection = Direction.None;
            CurrentState = RobotState.Chase;
            _healthPoints = InitialHealthPoints;
            _chaseScatterTimer = 0f;
            _exitingFactory = true;
        }
    }
}
