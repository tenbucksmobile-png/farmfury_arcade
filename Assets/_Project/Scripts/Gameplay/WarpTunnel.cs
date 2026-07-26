using UnityEngine;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Teleports the character to its paired warp tile on overlap. TileMapRenderer wires
    /// PairedWarp after instantiating both tunnel tiles for a row.</summary>
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

            var movement = other.GetComponent<GridMovement>();
            if (movement == null)
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
