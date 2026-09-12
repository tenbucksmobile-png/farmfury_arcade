using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>Per-world Leaderboard detail page (2026-09-12) — opened from LeaderboardsScreen's
    /// world-select collage, one instance reused for all 7 worlds (Show(int world) repopulates it
    /// live, same "generic reusable component" convention CosmeticPurchaseScreen already uses for
    /// Hats/Trails/World Purchase). Layers on top of LeaderboardsScreen without hiding it — same
    /// "layers on top, never hidden" convention ChooseCharacterScreen uses over Pause — so its own
    /// generic close button (a plain SetActive(false)) reveals the world-select collage again
    /// automatically.
    ///
    /// Every stat here is read live from LeaderboardManager's per-world rollups every time Show()
    /// runs, so it always reflects "where the player's at" at the moment the page is opened — no
    /// separate refresh/save step needed, since SaveManager/ScoreManager already persist the
    /// underlying data the instant it changes during real gameplay (see LeaderboardManager's own
    /// per-world methods for exactly what's summed/counted).</summary>
    public class WorldLeaderboardDetailScreen : MonoBehaviour
    {
        [Tooltip("Index-aligned with UnlockProgression's own world numbering — same 7 banner " +
                 "sprites LeaderboardsScreen's world-select collage uses, reused here as this " +
                 "page's own header so the banner a player tapped keeps reading as \"this is where " +
                 "I am.\"")]
        [SerializeField] private Sprite[] worldBannerSprites;
        [SerializeField] private Image headerImage;

        [SerializeField] private Image bestCharacterImage;

        [Tooltip("Index-aligned with CharacterType (Cluck, Bessie, Percy, Woolly, Ducky, Horace, " +
                 "Gerald, Billy) — each character's own ThumbsUp portrait for \"BestFarmFury.\"")]
        [SerializeField] private Sprite[] characterThumbsUpSprites;

        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private TextMeshProUGUI fastestTimeText;
        [SerializeField] private TextMeshProUGUI oneStarCountText;
        [SerializeField] private TextMeshProUGUI twoStarCountText;
        [SerializeField] private TextMeshProUGUI threeStarCountText;

        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }

        public void Show(int world)
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh(world);
        }

        private void Refresh(int world)
        {
            if (headerImage != null && worldBannerSprites != null && world >= 0 && world < worldBannerSprites.Length)
            {
                headerImage.sprite = worldBannerSprites[world];
            }

            var leaderboard = LeaderboardManager.Instance;
            if (leaderboard == null)
            {
                return;
            }

            if (highScoreText != null)
            {
                highScoreText.text = leaderboard.GetWorldTotalScore(world).ToString("N0");
            }

            if (fastestTimeText != null)
            {
                float fastest = leaderboard.GetWorldFastestTime(world);
                fastestTimeText.text = fastest > 0f ? FormatTime(fastest) : "--:--";
            }

            var (oneStar, twoStar, threeStar) = leaderboard.GetWorldStarCounts(world);
            if (oneStarCountText != null) oneStarCountText.text = oneStar.ToString();
            if (twoStarCountText != null) twoStarCountText.text = twoStar.ToString();
            if (threeStarCountText != null) threeStarCountText.text = threeStar.ToString();

            if (bestCharacterImage != null && characterThumbsUpSprites != null)
            {
                CharacterType bestCharacter = leaderboard.GetWorldBestCharacter(world);
                int index = (int)bestCharacter;
                bestCharacterImage.sprite = index >= 0 && index < characterThumbsUpSprites.Length
                    ? characterThumbsUpSprites[index]
                    : null;
                bestCharacterImage.enabled = bestCharacterImage.sprite != null;
            }
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes}:{secs:00}";
        }
    }
}
