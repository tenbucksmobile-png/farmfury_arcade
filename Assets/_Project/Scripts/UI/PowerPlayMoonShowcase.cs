using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Sits over the moon baked into ChooseCharacterScreen's World1_Cornfield.png
    /// backdrop and auto-cycles through the combo ("power play") icon art — CrossFire.png/
    /// DoubleSlam.png/IronStampede.png/KicknRoll.png/SkipShatter.png, the same combo-icon assets
    /// ComboNotificationBanner shows in-maze — advertising that pairing characters unlocks combo
    /// power plays. Not the per-character {Name}_ability.png icons (those are a single
    /// character's own ability, not a "power play" between two); the icon set is assigned
    /// directly by Phase5ProjectBuilder rather than read from CharacterData, since combos aren't
    /// per-character data.</summary>
    public class PowerPlayMoonShowcase : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite[] icons;
        [SerializeField] private float displaySeconds = 2.5f;
        [SerializeField] private float fadeSeconds = 0.3f;

        private Coroutine _routine;

        private void OnEnable()
        {
            _routine = StartCoroutine(RotateRoutine());
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator RotateRoutine()
        {
            if (icons == null || icons.Length == 0)
            {
                targetImage.enabled = false;
                yield break;
            }

            targetImage.enabled = true;
            int index = 0;
            while (true)
            {
                targetImage.sprite = icons[index];
                yield return FadeTo(1f);
                yield return new WaitForSecondsRealtime(displaySeconds);
                yield return FadeTo(0f);
                index = (index + 1) % icons.Length;
            }
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            Color color = targetImage.color;
            float startAlpha = color.a;
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
                targetImage.color = color;
                yield return null;
            }
            color.a = targetAlpha;
            targetImage.color = color;
        }
    }
}
