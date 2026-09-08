using System.Collections;
using UnityEngine;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>One fading "afterimage" left behind by a character's equipped Trail cosmetic —
    /// spawned by CharacterCosmeticRenderer using the trail's own real CosmeticData.previewSprite
    /// (the actual uploaded art, e.g. EmberTrail.png's glowing ember cluster) rather than the flat
    /// procedural TrailRenderer line that runs when no art exists at all. Self-destroys once fully
    /// faded — same "self-contained, Destroy(gameObject) when done" convention PelletCollectBurst
    /// already uses.</summary>
    public class CosmeticTrailGhost : MonoBehaviour
    {
        /// <summary>Fraction of the lifetime spent at full opacity before fading starts — a
        /// straight linear fade from the moment of spawn read as "too light/sparse" (feedback
        /// 2026-09-08) since every ghost was already dimming the instant it appeared. Holding
        /// solid for the first chunk of its life, then fading only over what's left, keeps the
        /// trail reading dense/dark for its visible length instead of uniformly faint.</summary>
        private const float HoldFraction = 0.45f;

        /// <summary>Multiplies the sprite's own colour down before any fade — darkens the trail
        /// overall (same feedback) without needing new art; 1 = no darkening.</summary>
        private const float DarkenMultiplier = 0.7f;

        public void Configure(Sprite sprite, int sortingLayerID, int sortingOrder, float scale, float lifetimeSeconds)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(DarkenMultiplier, DarkenMultiplier, DarkenMultiplier, 1f);
            transform.localScale = Vector3.one * scale;
            StartCoroutine(FadeAndDestroy(sr, lifetimeSeconds));
        }

        private IEnumerator FadeAndDestroy(SpriteRenderer sr, float lifetimeSeconds)
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < lifetimeSeconds)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / lifetimeSeconds);
                float fadeProgress = Mathf.Clamp01((progress - HoldFraction) / (1f - HoldFraction));
                Color c = sr.color;
                c.a = 1f - fadeProgress;
                sr.color = c;
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.75f, progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
