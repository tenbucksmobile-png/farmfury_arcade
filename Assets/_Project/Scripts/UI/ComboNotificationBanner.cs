using System.Collections;
using UnityEngine;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Slides in (fades — no real art/animation to slide yet) on ComboSystem.OnComboTriggered
    /// and fades back out after visibleSeconds. Lives inside GameplayHUD.</summary>
    public class ComboNotificationBanner : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float visibleSeconds = 2f;
        [SerializeField] private float fadeSeconds = 0.3f;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (ComboSystem.Instance != null)
            {
                ComboSystem.Instance.OnComboTriggered += HandleComboTriggered;
            }
            canvasGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            if (ComboSystem.Instance != null)
            {
                ComboSystem.Instance.OnComboTriggered -= HandleComboTriggered;
            }
        }

        private void HandleComboTriggered(string comboName)
        {
            bannerText.text = $"COMBO! {comboName}";
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(ShowThenHide());
        }

        private IEnumerator ShowThenHide()
        {
            yield return Fade(0f, 1f);
            yield return new WaitForSeconds(visibleSeconds);
            yield return Fade(1f, 0f);
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
