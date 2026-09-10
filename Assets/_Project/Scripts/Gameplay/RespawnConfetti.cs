using System.Collections;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>World-space respawn celebration — a small burst of coloured placeholder squares
    /// pops outward from the character the instant they fade back in after a death, so a respawn
    /// reads as an exciting "you're back!" beat rather than a plain silent fade-in. Same procedural
    /// "no dedicated VFX art yet" convention PelletCollectBurst already uses for world-space bursts
    /// (a ring of placeholder-coloured squares flying outward and fading) — this is a denser,
    /// gravity-affected variant matching ConfettiBurst's UI-canvas palette/feel, just built from
    /// SpriteRenderers instead of RectTransform/Image since it needs to sit in world space at the
    /// character's own grid position (not a full-screen canvas overlay).
    ///
    /// Enlarged 2026-09-11 per direct feedback ("I can hardly see it") — the original piece scale
    /// (0.06-0.11 world units) was tiny relative to a 1-unit grid cell, especially at this game's
    /// fixed per-tile screen-height ratio (CameraFollow.CellScreenHeightFraction, ~10.5% of screen
    /// height per tile — see CLAUDE.md's "Camera" section), and the short 0.7s lifetime meant it was
    /// gone again almost as soon as it appeared. Piece scale roughly doubled, piece count and
    /// lifetime both increased, and launch speed scaled up to match the bigger pieces so the burst
    /// still reads as a tight celebratory pop rather than a slow-drifting cloud.</summary>
    public static class RespawnConfetti
    {
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.25f, 0.25f), // red
            new Color(0.98f, 0.62f, 0.15f), // orange
            new Color(0.98f, 0.85f, 0.2f),  // gold/yellow
            new Color(0.35f, 0.75f, 0.35f), // green
            new Color(0.3f, 0.55f, 0.95f),  // blue
            new Color(0.75f, 0.4f, 0.85f),  // purple
            new Color(0.95f, 0.5f, 0.75f),  // pink
        };

        private const int PieceCount = 30; // was 20
        private const float Lifetime = 1.0f; // was 0.7f — stays on screen noticeably longer
        private const float Gravity = 6f; // world units/sec^2

        /// <summary>Spawns a self-destroying burst rooted at worldPosition. Fire-and-forget — the
        /// host GameObject (and its runner MonoBehaviour) clean themselves up once every piece has
        /// faded out, same convention PelletCollectBurst.Configure uses.</summary>
        public static void Spawn(Vector3 worldPosition)
        {
            var go = new GameObject("RespawnConfetti");
            go.transform.position = worldPosition;
            var runner = go.AddComponent<ConfettiRunner>();
            runner.Run();
        }

        private class ConfettiRunner : MonoBehaviour
        {
            public void Run()
            {
                for (int i = 0; i < PieceCount; i++)
                {
                    StartCoroutine(AnimatePiece());
                }
                Destroy(gameObject, Lifetime + 0.1f);
            }

            private IEnumerator AnimatePiece()
            {
                var pieceGO = new GameObject("Piece");
                pieceGO.transform.SetParent(transform, false);
                var sr = pieceGO.AddComponent<SpriteRenderer>();
                Color color = Palette[Random.Range(0, Palette.Length)];
                sr.sprite = PlaceholderSprite.Get(color);
                sr.sortingOrder = 20; // above the character sprite, same reasoning EggHazard uses

                // Roughly doubled (was 0.06-0.11) — the old size read as barely-visible specks
                // against a full 1-unit grid cell at this game's fixed per-tile screen size.
                float scale = Random.Range(0.14f, 0.22f);
                pieceGO.transform.localScale = Vector3.one * scale;

                // Mostly-upward launch with a wide spread (60-120 degrees, centred on straight up
                // at 90) so it reads as a celebratory pop rather than a directional spray. Speed
                // bumped up to match the larger pieces so the burst still pops outward briskly
                // rather than looking like it's merely drifting.
                float speed = Random.Range(2.8f, 5.2f);
                float angle = Random.Range(60f, 120f) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float angularVelocity = Random.Range(-540f, 540f);
                float maxLife = Lifetime * Random.Range(0.75f, 1.1f);

                float t = 0f;
                Vector3 localPos = Vector3.zero;
                while (t < maxLife)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    velocity += Vector2.down * Gravity * dt;
                    localPos += (Vector3)(velocity * dt);
                    pieceGO.transform.localPosition = localPos;
                    pieceGO.transform.Rotate(0f, 0f, angularVelocity * dt);

                    float lifeFraction = t / maxLife;
                    if (lifeFraction > 0.6f)
                    {
                        Color c = color;
                        c.a = Mathf.Clamp01(1f - (lifeFraction - 0.6f) / 0.4f);
                        sr.color = c;
                    }
                    yield return null;
                }
            }
        }
    }
}
