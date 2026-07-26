using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Tags a power pellet GameObject with its type/value so CropCollector can read it on overlap.
    /// Phase 2 awards flat points only; activating the power state (robots vulnerable/fleeing)
    /// is Phase 3 scope per spec.</summary>
    public class PowerPelletPickup : MonoBehaviour
    {
        public PowerPelletType pelletType;
        public int points = 500;
    }
}
