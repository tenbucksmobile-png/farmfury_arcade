using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Tags a power pellet GameObject with its type/value so CropCollector can read it on
    /// overlap and activate PowerPelletManager for the duration matching pelletType.</summary>
    public class PowerPelletPickup : MonoBehaviour
    {
        public PowerPelletType pelletType;
        public int points = 500;
    }
}
