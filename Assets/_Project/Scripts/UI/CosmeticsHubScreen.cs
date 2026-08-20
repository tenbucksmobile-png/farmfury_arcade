using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Cosmetics category picker (2026-08-20 redesign) — matches the new Cosmetics mockup: a
    /// "Cosmetics" wood sign and 3 icons (Hat, Trail/"Comet", MazeTheme). Reached via
    /// <see cref="ShopController"/>'s own Cosmetics button, layered on top of it the same way
    /// ChooseCharacterScreen layers on top of Pause.
    ///
    /// The MazeTheme icon is shown (matching the mockup) but deliberately non-interactive — no
    /// maze-theme cosmetic content exists yet (per explicit instruction: "for mapicon - dont
    /// create, I have not done this"), so it has no destination screen to open.
    /// </summary>
    public class CosmeticsHubScreen : MonoBehaviour
    {
        [SerializeField] private Button hatButton;
        [SerializeField] private Button trailButton;
        [SerializeField] private Button closeButton;

        [SerializeField] private CosmeticPurchaseScreen hatPurchaseScreen;
        [SerializeField] private CosmeticPurchaseScreen trailPurchaseScreen;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (hatButton != null && hatPurchaseScreen != null)
            {
                hatButton.onClick.AddListener(() => hatPurchaseScreen.Show());
            }
            if (trailButton != null && trailPurchaseScreen != null)
            {
                trailButton.onClick.AddListener(() => trailPurchaseScreen.Show());
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
