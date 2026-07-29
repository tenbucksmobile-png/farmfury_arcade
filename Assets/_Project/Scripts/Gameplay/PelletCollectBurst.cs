using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Procedural collect flourish for GoldenWheat/Rainbow power pellets — no dedicated
    /// sparkle/particle art exists yet (see CLAUDE.md "Art status"), so this spawns a small ring of
    /// placeholder-coloured squares that fly outward from the pellet's position and fade out over
    /// their lifetime, instead. Swap for a real ParticleSystem or sprite-sheet burst once dedicated
    /// VFX art is uploaded — Configure's tier argument is the only thing a replacement would need
    /// to keep reading.</summary>
    public class PelletCollectBurst : MonoBehaviour
    {
        private const float Lifetime = 0.5f;
        private const int RayCount = 8;

        public void Configure(PowerPelletType tier)
        {
            float travelDistance = 0.45f * TileMapRenderer.CellSize;
            float startScale = 0.125f * TileMapRenderer.CellSize;

            for (int i = 0; i < RayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / RayCount;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Color color = tier == PowerPelletType.Rainbow
                    ? Color.HSVToRGB((float)i / RayCount, 1f, 1f)
                    : new Color(1f, 0.84f, 0.2f); // GoldenWheat tier colour
                StartCoroutine(AnimateRay(dir, color, travelDistance, startScale));
            }

            Destroy(gameObject, Lifetime + 0.05f);
        }

        private IEnumerator AnimateRay(Vector2 dir, Color color, float travelDistance, float startScale)
        {
            var go = new GameObject("Ray");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            sr.sortingOrder = 8;

            float t = 0f;
            while (t < Lifetime)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / Lifetime);
                go.transform.localPosition = dir * travelDistance * progress;
                go.transform.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 0.2f, progress);
                Color c = color;
                c.a = 1f - progress;
                sr.color = c;
                yield return null;
            }
        }
    }
}
