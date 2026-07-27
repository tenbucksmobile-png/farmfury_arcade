using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Teleports the character (or a robot) to its paired warp tile on overlap.
    /// TileMapRenderer wires PairedWarp after instantiating both tunnel tiles for a row.</summary>
    public class WarpTunnel : MonoBehaviour
    {
        [SerializeField] private float reWarpCooldown = 0.1f;

        public WarpTunnel PairedWarp { get; set; }

        private float _cooldownTimer;

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_cooldownTimer > 0f || PairedWarp == null)
            {
                return;
            }

            bool isGridActor = other.GetComponent<GridMovement>() != null || other.GetComponent<RobotBase>() != null;
            if (!isGridActor)
            {
                return;
            }

            // Gerald is "too big" to fit through a warp tunnel while puffed up (Phase 4).
            var puff = other.GetComponent<PuffUpAbility>();
            if (puff != null && puff.IsPuffed)
            {
                return;
            }

            other.transform.position = PairedWarp.transform.position;
            PairedWarp.StartCooldown(reWarpCooldown);
            _cooldownTimer = reWarpCooldown;
        }

        public void StartCooldown(float duration)
        {
            _cooldownTimer = Mathf.Max(_cooldownTimer, duration);
        }
    }
}
