using UnityEngine;

namespace ValheimSlots
{
    /// <summary>Small runtime-drawn icons (white shape, black outline) that do not rely on color or font glyphs.</summary>
    internal static class Icons
    {
        private const int Size = 64;
        private static Sprite _arrow;

        /// <summary>An arrow pointing up (rotate the Image to aim it).</summary>
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = CreateArrow());

        private static Sprite CreateArrow()
        {
            // Arrow head (triangle) + shaft (rectangle), y up.
            var tip = new Vector2(32f, 60f);
            var left = new Vector2(8f, 30f);
            var right = new Vector2(56f, 30f);
            var shaft = new Rect(23f, 4f, 18f, 28f);
            const float outline = 3.5f;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ValheimSlotsArrow",
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // Signed distance: negative inside the arrow.
                    float d = Mathf.Min(TriangleDistance(p, tip, left, right), RectDistance(p, shaft));
                    Color32 c;
                    if (d <= 0f) c = new Color32(255, 255, 255, 255);
                    else if (d <= outline) c = new Color32(0, 0, 0, (byte)(255f * Mathf.Clamp01(outline - d + 0.5f)));
                    else c = new Color32(0, 0, 0, 0);
                    pixels[y * Size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float RectDistance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, p.y - r.yMax);
            if (dx <= 0f && dy <= 0f) return Mathf.Max(dx, dy);
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        }

        private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
            bool inside = Side(p, a, b) == Side(p, b, c) && Side(p, b, c) == Side(p, c, a);
            return inside ? -d : d;
        }

        private static bool Side(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) >= 0f;

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
