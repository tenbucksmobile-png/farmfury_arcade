using System.Collections;
using UnityEngine;
using TMPro;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Small auto-dismissing popup shown when the player taps a locked Level Select tile. Not a
    /// SceneTransitionManager screen and not a Pause/Settings-style persistent overlay — it's a
    /// transient toast that shows itself, waits, and hides itself again. LevelSelectController just
    /// calls Show(message) and never has to manage its visibility directly.
    /// </summary>
    public class LockedHintPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float autoDismissSeconds = 2f;

        private Coroutine _dismissRoutine;

        public void Show(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            gameObject.SetActive(true);

            if (_dismissRoutine != null)
            {
                StopCoroutine(_dismissRoutine);
            }
            _dismissRoutine = StartCoroutine(DismissAfterDelay());
        }

        private IEnumerator DismissAfterDelay()
        {
            // Unscaled so a locked-level tap right before/after a pause still dismisses on a real
            // 2-second wall-clock timer, same convention SceneTransitionManager's fade uses.
            yield return new WaitForSecondsRealtime(autoDismissSeconds);
            gameObject.SetActive(false);
            _dismissRoutine = null;
        }
    }
}
