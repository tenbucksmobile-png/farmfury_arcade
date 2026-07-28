using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// A character card (current/default character) and up to 3 robot cards for this level's
    /// distinct robot types — overlaid directly on the two wood-frame slots baked into
    /// matchup.png's background art — plus star rating from previous attempts and the 3-2-1-GO
    /// countdown into gameplay. GameManager.LoadLevel is deliberately NOT called until the
    /// countdown finishes — tapping a World Map marker only shows this screen, matching the
    /// spec's "On level marker tap: show Matchup Screen" / "Countdown sequence on play tap".
    /// Level number/name/objective text was removed in the matchup-screen cleanup (see
    /// Phase5ProjectBuilder.BuildMatchup) — level identity is already established by the World
    /// Map marker the player just tapped.
    /// </summary>
    public class MatchupScreenController : MonoBehaviour
    {
        private static readonly string[] CountdownSteps = { "3", "2", "1", "GO!" };
        private const float CountdownStepSeconds = 0.6f;

        private static readonly Dictionary<RobotType, Color> RobotColors = new Dictionary<RobotType, Color>
        {
            { RobotType.Harvester, new Color(0.86f, 0.16f, 0.16f) },
            { RobotType.Scout, new Color(0.98f, 0.55f, 0.75f) },
            { RobotType.Patrol, new Color(0.20f, 0.80f, 0.85f) },
            { RobotType.Drifter, new Color(0.95f, 0.55f, 0.15f) },
            { RobotType.Heavy, new Color(0.55f, 0.55f, 0.58f) },
            { RobotType.Drone, new Color(0.62f, 0.20f, 0.86f) },
        };

        [SerializeField] private StarDisplay starDisplay;
        [SerializeField] private Image characterCardImage;
        [SerializeField] private Image[] robotCardImages;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject worldMapScreen;

        private int _levelIndex;

        private void Awake()
        {
            playButton.onClick.AddListener(() => StartCoroutine(CountdownThenPlay()));
            backButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(worldMapScreen));
        }

        public void ShowForLevel(int levelIndex)
        {
            _levelIndex = levelIndex;
            var level = DataManager.Instance.GetLevelData(levelIndex);

            starDisplay.SetStars(SaveManager.Instance.GetLevelStars(levelIndex));
            countdownText.text = string.Empty;
            playButton.interactable = true;

            SetCharacterCard();
            SetRobotCards(level);

            SceneTransitionManager.Instance.ShowOnly(gameObject);
        }

        private void SetCharacterCard()
        {
            if (characterCardImage == null)
            {
                return;
            }

            // Matchup happens before the player GameObject spawns, so this reflects whichever
            // character was last selected (GameManager.CurrentCharacter), falling back to Cluck.
            var data = GameManager.Instance.CurrentCharacter ?? DataManager.Instance.GetCharacterData(CharacterType.Cluck);
            var portrait = data != null ? data.portraitSprite : null;
            characterCardImage.sprite = portrait;
            // Only tint the placeholder square when there's no real portrait yet — tinting a real
            // sprite would wash it out the same way RobotVisual's colour tint used to (see
            // RobotVisual.BaseTintColor).
            characterCardImage.color = portrait != null ? Color.white : (data != null ? new Color(1f, 0.84f, 0f) : Color.white);
        }

        private void SetRobotCards(LevelData level)
        {
            if (robotCardImages == null)
            {
                return;
            }

            var distinctTypes = level != null && level.robotSpawns != null
                ? level.robotSpawns.Select(s => s.robotType).Distinct().Take(robotCardImages.Length).ToList()
                : new List<RobotType>();

            for (int i = 0; i < robotCardImages.Length; i++)
            {
                if (robotCardImages[i] == null)
                {
                    continue;
                }

                bool hasRobot = i < distinctTypes.Count;
                robotCardImages[i].gameObject.SetActive(hasRobot);
                if (hasRobot)
                {
                    var robotData = DataManager.Instance.GetRobotData(distinctTypes[i]);
                    var portrait = robotData != null ? robotData.portraitSprite : null;
                    robotCardImages[i].sprite = portrait;
                    robotCardImages[i].color = portrait != null
                        ? Color.white
                        : (RobotColors.TryGetValue(distinctTypes[i], out var color) ? color : Color.grey);
                }
            }
        }

        private IEnumerator CountdownThenPlay()
        {
            playButton.interactable = false;

            foreach (var step in CountdownSteps)
            {
                countdownText.text = step;
                yield return new WaitForSecondsRealtime(CountdownStepSeconds);
            }
            countdownText.text = string.Empty;

            GameManager.Instance.LoadLevel(_levelIndex);
            SceneTransitionManager.Instance.ShowOnly(gameplayScreen);
        }
    }
}
