using System;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>
    /// Base class for every character's unique ability component. Exactly one instance exists at
    /// a time (whichever character is currently active — CharacterManager destroys the previous
    /// one on swap), so subscribing directly to InputController's static
    /// OnAbilityActivateInput event here is safe, same reasoning as GridMovement.
    /// </summary>
    [RequireComponent(typeof(GridMovement))]
    public abstract class AbilityBase : MonoBehaviour
    {
        [SerializeField] protected float totalCooldown = 15f;

        public float TotalCooldown => totalCooldown;
        public float CooldownRemaining { get; private set; }
        public bool IsReady => CooldownRemaining <= 0f;
        public CharacterBase Owner { get; private set; }

        /// <summary>(remaining, total) — for a future cooldown UI to subscribe to.</summary>
        public event Action<float, float> OnCooldownChanged;

        protected GridMovement Movement { get; private set; }
        protected TileMapRenderer TileMap { get; private set; }

        protected virtual void Awake()
        {
            Movement = GetComponent<GridMovement>();
            Owner = GetComponent<CharacterBase>();
        }

        protected virtual void Start()
        {
            TileMap = FindFirstObjectByType<TileMapRenderer>();
        }

        protected virtual void OnEnable()
        {
            InputController.OnAbilityActivateInput += HandleActivateInput;
        }

        protected virtual void OnDisable()
        {
            InputController.OnAbilityActivateInput -= HandleActivateInput;
        }

        protected virtual void Update()
        {
            UpdateCooldown(Time.deltaTime);
        }

        private void HandleActivateInput()
        {
            TryActivate();
        }

        public void UpdateCooldown(float deltaTime)
        {
            if (CooldownRemaining <= 0f)
            {
                return;
            }

            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            OnCooldownChanged?.Invoke(CooldownRemaining, totalCooldown);

            // This only executes while CooldownRemaining was still >0 on entry (the early-return
            // above skips it every frame after), so the moment it lands on exactly 0 is the single
            // "ability just became usable again" transition — not every frame it happens to be
            // ready, and not the initial "never used yet" state either.
            if (CooldownRemaining <= 0f)
            {
                AudioManager.Instance?.PlayPowerReadySfx();
            }
        }

        public bool TryActivate()
        {
            if (!IsReady)
            {
                return false;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return false;
            }

            Execute();
            CooldownRemaining = totalCooldown;
            OnCooldownChanged?.Invoke(CooldownRemaining, totalCooldown);
            return true;
        }

        protected abstract void Execute();
    }
}
