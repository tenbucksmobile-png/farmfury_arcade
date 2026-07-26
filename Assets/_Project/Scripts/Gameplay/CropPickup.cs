using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Tags a crop GameObject with its type/value so CropCollector can read it on overlap.</summary>
    public class CropPickup : MonoBehaviour
    {
        public CropType cropType;
        public int points = 10;
    }
}
