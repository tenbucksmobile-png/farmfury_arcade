using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>3-star row; currently used by LevelCompleteController. Swaps each Image's sprite
    /// between real filled/empty star art (ScoreStar.png/ClearStar.png, wired by
    /// ArtWiringBuilder.WireMazeTiles) rather than tinting a shared placeholder shape — an earlier
    /// version tinted a solid-color square via `.color`, which (before PlaceholderSprite.GetStar
    /// existed) was itself a baked-color sprite, so the runtime tint multiplied on top of an
    /// already-grey pixel (0.35 grey * gold ~= dark olive/brown) instead of showing clean gold, the
    /// flat "brown box" look a 2026-08 screenshot review caught. Falls back to
    /// PlaceholderSprite.GetStar() (tinted via `.color`, which is safe for a plain white shape) if
    /// the real art hasn't been wired yet.</summary>
    public class StarDisplay : MonoBehaviour
    {
        private static readonly Color FilledPlaceholderTint = new Color(1f, 0.84f, 0f);
        private static readonly Color EmptyPlaceholderTint = new Color(0.35f, 0.35f, 0.35f);

        [SerializeField] private Image[] starImages;
        [SerializeField] private Sprite filledStarSprite;
        [SerializeField] private Sprite emptyStarSprite;

        public void SetStars(int count)
        {
            if (starImages == null)
            {
                return;
            }

            bool hasRealArt = filledStarSprite != null && emptyStarSprite != null;
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                {
                    continue;
                }

                bool filled = i < count;
                if (hasRealArt)
                {
                    starImages[i].sprite = filled ? filledStarSprite : emptyStarSprite;
                    starImages[i].color = Color.white;
                }
                else
                {
                    starImages[i].color = filled ? FilledPlaceholderTint : EmptyPlaceholderTint;
                }
            }
        }
    }
}
