using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Detects crop/power-pellet overlap via a trigger Collider2D, awards points, and
    /// tells GameManager one fewer item remains (both crops and power pellets count toward level
    /// completion, matching the original arcade convention that everything on the board must be
    /// cleared).</summary>
    public class CropCollector : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            var crop = other.GetComponent<CropPickup>();
            if (crop != null)
            {
                ScoreManager.Instance.AddCropPoints(crop.points);
                GameManager.Instance.NotifyCropCollected();
                if (crop.cropType == CropType.Corn)
                {
                    AudioManager.Instance?.PlayCornPickupSfx();
                }
                Destroy(other.gameObject);
                return;
            }

            var pellet = other.GetComponent<PowerPelletPickup>();
            if (pellet != null)
            {
                ScoreManager.Instance.AddCropPoints(pellet.points);
                GameManager.Instance.NotifyCropCollected();
                PowerPelletManager.Instance?.ActivatePower(PowerPelletManager.GetDuration(pellet.pelletType));
                pellet.SpawnCollectEffectIfRare();
                if (pellet.pelletType != PowerPelletType.Sunflower)
                {
                    AudioManager.Instance?.PlayRarePelletPickupSfx();
                }
                Destroy(other.gameObject);
            }
        }
    }
}
