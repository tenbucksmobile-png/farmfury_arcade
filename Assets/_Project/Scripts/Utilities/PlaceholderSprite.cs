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
    }
}
