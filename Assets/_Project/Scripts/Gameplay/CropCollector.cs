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
                // CornPickup.mp3 doubles as the generic crop-pickup cue now — plays for
                // vegetables (carrots) too, not just corn kernels, per feedback that carrots were
                // silent.
                AudioManager.Instance?.PlayCornPickupSfx();
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
                return;
            }

            var coin = other.GetComponent<CoinPickup>();
            if (coin != null)
            {
                // Deliberately does NOT call GameManager.NotifyCropCollected — see CoinPickup's
                // doc comment for why it's excluded from LevelData.totalCropsRequired.
                SaveManager.Instance?.AddCoins(coin.coinValue);
                AudioManager.Instance?.PlayCornPickupSfx(); // no dedicated coin SFX uploaded yet
                Destroy(other.gameObject);
            }
        }
    }
}
