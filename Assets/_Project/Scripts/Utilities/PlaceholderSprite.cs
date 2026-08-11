using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Generates a single solid-colour 1x1 sprite, cached per colour, so Phase 1 can
    /// render maze/character/robot placeholders before real art exists.</summary>
    public static class PlaceholderSprite
    {
        private static readonly System.Collections.Generic.Dictionary<Color, Sprite> Cache =
            new System.Collections.Generic.Dictionary<Color, Sprite>();
        private static readonly System.Collections.Generic.Dictionary<Color, Sprite> CircleCache =
            new System.Collections.Generic.Dictionary<Color, Sprite>();
        private static Sprite _starSprite;

        public static Sprite Get(Color color)
        {
            if (Cache.TryGetValue(color, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, color);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            Cache[color] = sprite;
            return sprite;
        }

        /// <summary>Same solid-colour placeholder convention as Get(), but filled as a circle
        /// (transparent outside the radius) rather than a solid square — for spots that need a round
        /// background with no dedicated round art yet (e.g. the Gameplay HUD's character portrait).
        /// Needs real pixel resolution (unlike Get()'s 1x1) since the circle edge has to actually be
        /// drawn.</summary>
        public static Sprite GetCircle(Color color)
        {
            if (CircleCache.TryGetValue(color, out var cached) && cached != null)
            {
                return cached;
            }

            const int size = 128;
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
            float radius = size / 2f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // 1px anti-aliased edge rather than a hard/jagged circle boundary.
                    float alpha = Mathf.Clamp01(radius - dist);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            CircleCache[color] = sprite;
            return sprite;
        }

        /// <summary>A single cached white 5-point star shape (alpha-only outside the points), meant
        /// to be tinted via the consuming Image's own `color` field rather than baked pre-colored
        /// like Get()/GetCircle() — StarDisplay.SetStars already does exactly that (gold vs dim
        /// grey). UIBuilderHelpers.CreateStarDisplay used to build each star from CreateImage's
        /// baked-color square instead, which meant StarDisplay's runtime tint multiplied on top of
        /// an already-grey baked pixel (0.35 grey * gold ≈ dark olive/brown) instead of showing clean
        /// gold — the flat "brown box" look a 2026-08 screenshot review caught. A 2x2-supersampled
        /// point-in-polygon rasterization gives a reasonably anti-aliased edge without needing a
        /// dedicated star icon asset.</summary>
        public static Sprite GetStar()
        {
            if (_starSprite != null)
            {
                return _starSprite;
            }

            const int size = 128;
            const int outerPoints = 5;
            float outerRadius = size * 0.5f;
            float innerRadius = outerRadius * 0.4f;
            var center = new Vector2(size / 2f, size / 2f);

            var vertices = new Vector2[outerPoints * 2];
            for (int i = 0; i < vertices.Length; i++)
            {
                // Start pointing straight up (-90 degrees), alternating outer/inner radius.
                float angle = -Mathf.PI / 2f + i * Mathf.PI / outerPoints;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            var texture = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
            const int supersample = 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < supersample; sy++)
                    {
                        for (int sx = 0; sx < supersample; sx++)
                        {
                            var sample = new Vector2(
                                x + (sx + 0.5f) / supersample,
                                y + (sy + 0.5f) / supersample);
                            if (IsPointInPolygon(sample, vertices))
                            {
                                hits++;
                            }
                        }
                    }
                    float alpha = (float)hits / (supersample * supersample);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            _starSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _starSprite;
        }

        /// <summary>Standard ray-casting point-in-polygon test (even-odd rule) — used by GetStar()'s
        /// rasterizer.</summary>
        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses)
                {
                    float xIntersect = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                    if (point.x < xIntersect)
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }
    }
}
