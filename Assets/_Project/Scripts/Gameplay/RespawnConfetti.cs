using System.Collections;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>World-space respawn celebration — a burst of twinkling stars pops outward from the
    /// character the instant they fade back in after a death, so a respawn reads as an exciting
    /// "you're back!" beat rather than a plain silent fade-in.
    ///
    /// Reworked 2026-09-11 from flat coloured confetti squares to stars, per direct feedback
    /// ("make it more prominent and magical") — three changes drive that: (1) real star shapes
    /// (PlaceholderSprite.GetStar(), the same anti-aliased star rasterization StarDisplay already
    /// uses for score stars, tinted via SpriteRenderer.color the same way that convention does)
    /// instead of plain solid squares, (2) a warm gold/white/pale-cyan "magic sparkle" palette
    /// instead of the old full rainbow confetti spread, plus a brief bright flash at the burst
    /// origin (PlaceholderSprite.GetCircle, white, fast fade) so the moment itself reads as a
    /// magical pop rather than just particles appearing, and (3) each star twinkles — a sine-wave
    /// scale pulse layered on top of its outward flight and fade, same "pulse" convention
    /// GameplayHUD's ability-ready flash and TitleScreenController's PRESS START prompt both
    /// already use — rather than flying out rigid and flat. Gravity was also lightened
    /// significantly (stars drift and hang rather than dropping hard like confetti), matching a
    /// "sparkling magic" feel over a "falling paper" one.
    ///
    /// Still the same procedural "no dedicated VFX art yet" convention PelletCollectBurst uses for
    /// world-space bursts (all built from placeholder SpriteRenderers, not dedicated art) — this
    /// stays that same kind of stand-in, just redesigned to read as magical rather than confetti.</summary>
    public static class RespawnConfetti
    {
        // Warm gold/white/pale-cyan "magic sparkle" palette — replaces the old full rainbow
        // confetti spread, which read as a party-popper rather than a magical shimmer.
        private static readonly Color[] Palette =
        {
            new Color(1f, 0.85f, 0.3f),    // gold
            new Color(1f, 0.95f, 0.6f),    // pale gold
            new Color(1f, 1f, 1f),         // white
            new Color(0.75f, 0.95f, 1f),   // pale cyan sparkle
            new Color(1f, 0.75f, 0.9f),    // soft pink sparkle
        };

        private const int PieceCount = 26;
        private const float Lifetime = 1.1f;
        // Lightened significantly from confetti's own 6 — stars drift and hang rather than
        // dropping hard, reading as magical sparkle instead of falling paper.
        private const float Gravity = 1.5f;
        private const float TwinkleCyclesPerSecond = 3f;
        private const float TwinkleDepth = 0.35f; // fraction of base scale the twinkle pulses by

        /// <summary>Bright, fast-fading flash at the burst's own origin — sells the "magical pop"
        /// moment itself, separate from the stars flying outward from it.</summary>
        private const float FlashLifetime = 0.35f;
        private const float FlashMaxScale = 1.4f;

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
                StartCoroutine(AnimateFlash());
                for (int i = 0; i < PieceCount; i++)
                {
                    StartCoroutine(AnimatePiece());
                }
                Destroy(gameObject, Mathf.Max(Lifetime, FlashLifetime) + 0.1f);
            }

            private IEnumerator AnimateFlash()
            {
                var flashGO = new GameObject("Flash");
                flashGO.transform.SetParent(transform, false);
                var sr = flashGO.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSprite.GetCircle(Color.white);
                sr.sortingOrder = 19; // just behind the stars (order 20) so it reads as a backing glow

                float t = 0f;
                while (t < FlashLifetime)
                {
                    t += Time.deltaTime;
                    float lifeFraction = t / FlashLifetime;
                    float scale = Mathf.Lerp(0.1f, FlashMaxScale, Mathf.Sqrt(lifeFraction));
                    flashGO.transform.localScale = Vector3.one * scale;
                    var c = Color.white;
                    c.a = Mathf.Clamp01(1f - lifeFraction);
                    sr.color = c;
                    yield return null;
                }
                Destroy(flashGO);
            }

            private IEnumerator AnimatePiece()
            {
                var pieceGO = new GameObject("Star");
                pieceGO.transform.SetParent(transform, false);
                var sr = pieceGO.AddComponent<SpriteRenderer>();
                Color color = Palette[Random.Range(0, Palette.Length)];
                sr.sprite = PlaceholderSprite.GetStar();
                sr.color = color;
                sr.sortingOrder = 20; // above the character sprite, same reasoning EggHazard uses

                // Larger than the old confetti pieces (0.14-0.22) per "make it more prominent."
                float baseScale = Random.Range(0.22f, 0.34f);

                // Mostly-upward launch with a wide spread (60-120 degrees, centred on straight up
                // at 90) so it reads as a celebratory pop rather than a directional spray.
                float speed = Random.Range(2.2f, 4.2f);
                float angle = Random.Range(60f, 120f) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float angularVelocity = Random.Range(-220f, 220f); // gentler spin than confetti's own 540 — reads as twinkling, not tumbling
                float maxLife = Lifetime * Random.Range(0.8f, 1.15f);
                float twinklePhase = Random.Range(0f, Mathf.PI * 2f);

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

                    // Twinkle — a sine-wave scale pulse layered on top of the flight, so each star
                    // shimmers in place rather than flying out as a rigid, flat shape.
                    float twinkle = 1f + Mathf.Sin(t * TwinkleCyclesPerSecond * Mathf.PI * 2f + twinklePhase) * TwinkleDepth;
                    pieceGO.transform.localScale = Vector3.one * (baseScale * twinkle);

                    float lifeFraction = t / maxLife;
                    if (lifeFraction > 0.55f)
                    {
                        Color c = color;
                        c.a = Mathf.Clamp01(1f - (lifeFraction - 0.55f) / 0.45f);
                        sr.color = c;
                    }
                    yield return null;
                }
            }
        }
    }
}
