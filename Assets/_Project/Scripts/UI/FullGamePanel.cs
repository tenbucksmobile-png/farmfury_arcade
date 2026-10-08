using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Web demo: the "this is in the full game" panel. Two ways in:
    /// - Show(false): the player tapped one of the 6 locked full-game world shields on Level Select.
    /// - Show(true): the player finished the demo's last level (Corn Field 25). YouTube Playables
    ///   requires the game to say clearly when there is no more content, so this is that message.
    ///
    /// The store button only appears where external links are allowed (the website build).
    /// YouTube Playables forbids external links and store URLs, so there it shows text only.
    /// Overlay convention, same as SettingsPanel: shown/hidden directly, Esc closes it
    /// (AndroidBackButtonHandler).</summary>
    public class FullGamePanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI noLinkText;
        [SerializeField] private Button googlePlayButton;
        [SerializeField] private Button closeButton;

        /// <summary>UTM-tagged so installs from the web demo show separately in Play Console.</summary>
        public const string GooglePlayUrl =
            "https://play.google.com/store/apps/details?id=com.farmfury.arcade" +
            "&referrer=utm_source%3Dfarmfurygames.com%26utm_medium%3Dweb_demo";

        private const string LockedWorldHeader = "MORE TO EXPLORE!";
        private const string LockedWorldBody =
            "This world is in the full game: 6 more worlds and 150 more levels, plus Gerald and Billy, " +
            "machines, hats and trails.";
        private const string DemoCompleteHeader = "YOU CLEARED CORN FIELD!";
        private const string DemoCompleteBody =
            "That's the end of the free demo - well played! The full game has 6 more worlds, " +
            "150 more levels, Gerald and Billy, and lots more to unlock.";
        private const string NoLinkMessage = "Get Farm Fury: Arcade free on Google Play.";

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (googlePlayButton != null)
            {
                googlePlayButton.onClick.AddListener(() =>
                {
                    if (Platform.CanOpenExternalLinks)
                    {
                        Application.OpenURL(GooglePlayUrl);
                    }
                });
            }
        }

        public void Show(bool demoComplete)
        {
            if (headerText != null) headerText.text = demoComplete ? DemoCompleteHeader : LockedWorldHeader;
            if (bodyText != null) bodyText.text = demoComplete ? DemoCompleteBody : LockedWorldBody;

            bool links = Platform.CanOpenExternalLinks;
            if (googlePlayButton != null) googlePlayButton.gameObject.SetActive(links);
            if (noLinkText != null)
            {
                noLinkText.gameObject.SetActive(!links);
                noLinkText.text = NoLinkMessage;
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }
    }
}
