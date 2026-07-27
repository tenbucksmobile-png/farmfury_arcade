using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>3 placeholder Image children (gold vs dim grey) — reused by LevelMarker and
    /// LevelCompleteController rather than each building its own star row.</summary>
    public class StarDisplay : MonoBehaviour
    {
        private static readonly Color FilledColor = new Color(1f, 0.84f, 0f);
        private static readonly Color EmptyColor = new Color(0.35f, 0.35f, 0.35f);

        [SerializeField] private Image[] starImages;

        public void SetStars(int count)
        {
            if (starImages == null)
            {
                return;
            }

            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                {
                    starImages[i].color = i < count ? FilledColor : EmptyColor;
                }
            }
        }
    }
}
