using UnityEngine;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Persists for lifetimeSeconds; stuns any robot that walks over it. Reusable within
    /// its lifetime (not consumed on first hit) — nothing in the spec says an egg breaks after one
    /// stun, and letting it linger as a hazard matches "eggs persist for 15 seconds" literally.
    /// Also dropped mid-walk by WoollyClone when the Feather Storm combo buff is active.</summary>
    public class EggHazard : MonoBehaviour
    {
        private const float StunDuration = 3f;

        [SerializeField] private float lifetimeSeconds = 15f;

        private void Start()
        {
            Destroy(gameObject, lifetimeSeconds);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var robot = other.GetComponent<RobotBase>();
            robot?.Stun(StunDuration);
        }
    }
}
