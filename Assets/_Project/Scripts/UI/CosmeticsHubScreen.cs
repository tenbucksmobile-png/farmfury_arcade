using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Cosmetics category picker (2026-08-20 redesign) — matches the new Cosmetics mockup: a
    /// "Cosmetics" wood sign and 2 icons (Hat, Trail/"Comet"). Reached via
    /// <see cref="ShopController"/>'s own Cosmetics button, layered on top of it the same way
    /// ChooseCharacterScreen layers on top of Pause.
    ///
    /// A 3rd "MazeTheme" (map) icon used to sit here — first non-interactive display-only, then
    /// briefly wired to a maze-reskin cosmetic purchase screen — but that idea was dropped in
    /// favor of purchasable whole new worlds instead (see the World Purchase screen, reached from
    /// Settings/Level Select). Removed entirely (2026-08-25 review), not just left unwired.
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
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }
    }
}
