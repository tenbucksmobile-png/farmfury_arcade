using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFuryArcade.UI
{
    /// <summary>Lightweight, code-driven confetti particle burst — dozens of small coloured
    /// squares/streamers rain down from the top of the screen, spinning and arcing under a simple
    /// constant "gravity", fading out near the end of their life. No external tween/particle
    /// library, same "procedural placeholder burst" convention PelletCollectBurst already uses for
    /// world-space VFX (a ring of placeholder-coloured squares that fly outward and fade) — this is
    /// the ScreenSpaceOverlay-Canvas equivalent, built from plain UI Images positioned via
    /// anchoredPosition rather than a world-space ParticleSystem, so it can layer on top of a UI
    /// celebration screen (New Character Unlock) and draws above everything else on the canvas
    /// (parented last-sibling by whoever builds it).</summary>
    public class ConfettiBurst : MonoBehaviour
    {
        private struct Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float angularVelocity;
            public float life;
            public float maxLife;
        }

        [Tooltip("Full-screen RectTransform particles are parented under and measured against for " +
                 "spawn spread — must stretch to the Canvas' own screen size.")]
        [SerializeField] private RectTransform particlesRoot;

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

        private const float Gravity = 900f; // canvas px/sec^2 (unscaled)

        private readonly List<Particle> _particles = new List<Particle>();
        private Coroutine _routine;

        /// <summary>Spawns count confetti pieces from a spread of origin points across the top of
        /// the screen (not a single point — reads as "raining down" rather than one firework), each
        /// launched outward/downward with randomised speed, angle, spin, and lifetime. Safe to call
        /// again before a previous burst finishes — clears any still-alive particles from the last
        /// call first, same "stop and restart cleanly" convention every other repeatable coroutine
        /// effect in this project uses (ComboHypeScreen, ChaseScoreManager's chain, etc.).</summary>
        public void Burst(int count = 70, float durationSeconds = 2f)
        {
            if (particlesRoot == null)
            {
                return;
            }

            ClearParticles();

            float halfWidth = particlesRoot.rect.width * 0.5f;
            float topY = particlesRoot.rect.height * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(particlesRoot, false);
                var rect = (RectTransform)go.transform;
                float size = Random.Range(14f, 26f);
                rect.sizeDelta = new Vector2(size, size * Random.Range(0.4f, 1f)); // mix of squares/streamers
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                float originX = Random.Range(-halfWidth * 0.9f, halfWidth * 0.9f);
                rect.anchoredPosition = new Vector2(originX, topY + Random.Range(0f, 100f));
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = Palette[Random.Range(0, Palette.Length)];

                // Mostly downward with a wide left/right spread (200-340 degrees, i.e. centred on
                // straight down at 270) so it reads as confetti raining/scattering over the whole
                // screen rather than a single upward firework burst.
                float speed = Random.Range(250f, 550f);
                float angle = Random.Range(200f, 340f) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

                _particles.Add(new Particle
                {
                    rect = rect,
                    image = image,
                    velocity = velocity,
                    angularVelocity = Random.Range(-360f, 360f),
                    life = 0f,
                    maxLife = durationSeconds * Random.Range(0.7f, 1.15f),
                });
            }

            _routine = StartCoroutine(RunParticles());
        }

        private IEnumerator RunParticles()
        {
            while (_particles.Count > 0)
            {
                float dt = Time.unscaledDeltaTime;
                for (int i = _particles.Count - 1; i >= 0; i--)
                {
                    var p = _particles[i];
                    if (p.rect == null)
                    {
                        _particles.RemoveAt(i);
                        continue;
                    }

                    p.life += dt;
                    if (p.life >= p.maxLife)
                    {
                        Destroy(p.rect.gameObject);
                        _particles.RemoveAt(i);
                        continue;
                    }

                    p.velocity += Vector2.down * Gravity * dt;
                    p.rect.anchoredPosition += p.velocity * dt;
                    p.rect.Rotate(0f, 0f, p.angularVelocity * dt);

                    float lifeFraction = p.life / p.maxLife;
                    if (lifeFraction > 0.7f)
                    {
                        var c = p.image.color;
                        c.a = Mathf.Clamp01(1f - (lifeFraction - 0.7f) / 0.3f);
                        p.image.color = c;
                    }

                    _particles[i] = p;
                }
                yield return null;
            }
            _routine = null;
        }

        private void ClearParticles()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            foreach (var p in _particles)
            {
                if (p.rect != null)
                {
                    Destroy(p.rect.gameObject);
                }
            }
            _particles.Clear();
        }

        private void OnDisable()
        {
            ClearParticles();
        }
    }
}
