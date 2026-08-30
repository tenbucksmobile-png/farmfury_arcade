using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Slides in (fades — no real art/animation to slide yet) on ComboSystem.OnComboTriggered
    /// and fades back out after visibleSeconds. Lives inside GameplayHUD.</summary>
    public class ComboNotificationBanner : MonoBehaviour
    {
        [Serializable]
        private struct ComboIconEntry
        {
            public string comboName;
            public Sprite icon;
        }

        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float visibleSeconds = 2f;
        [SerializeField] private float fadeSeconds = 0.3f;

        // Icon art only exists for 3 of the 8 combos so far (Crossfire/Double Slam/Kick and Roll) —
        // a combo with no matching entry here just hides the icon and shows text only, same
        // "wire whatever art has landed, fall back gracefully for the rest" convention this project
        // already uses for character/robot art.
        [SerializeField] private Image comboIcon;
        [SerializeField] private ComboIconEntry[] comboIcons;

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

            if (comboIcon != null)
            {
                Sprite icon = null;
                if (comboIcons != null)
                {
                    foreach (var entry in comboIcons)
                    {
                        if (entry.comboName == comboName)
                        {
                            icon = entry.icon;
                            break;
                        }
                    }
                }
                comboIcon.sprite = icon;
                comboIcon.gameObject.SetActive(icon != null);
            }

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
