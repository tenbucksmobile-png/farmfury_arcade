using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Generates a single solid-colour 1x1 sprite, cached per colour, so Phase 1 can
    /// render maze/character/robot placeholders before real art exists.</summary>
    public static class PlaceholderSprite
    {
        private static readonly System.Collections.Generic.Dictionary<Color, Sprite> Cache =
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
    }
}
