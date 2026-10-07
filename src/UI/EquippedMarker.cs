using UnityEngine;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// A white check mark with a black outline, shown on equipped items.
    /// Shape-based instead of color-based so it is readable for color-blind players.
    /// The sprite is drawn at runtime so it does not depend on font glyphs.
    /// </summary>
    internal static class EquippedMarker
    {
        private const string ObjectName = "ValheimSlotsEquipped";
        private const int TexSize = 64;

        private static Sprite _sprite;

        public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = CreateSprite());

        /// <summary>Show or hide the check mark on a slot element (created on first use).</summary>
        public static void Set(Transform element, bool equipped, float size = 24f, Vector2? offset = null)
        {
            if (element == null)
                return;
            var existing = element.Find(ObjectName);
            if (existing == null)
            {
                if (!equipped || !Plugin.ShowEquippedMarker.Value)
                    return;
                existing = Create(element, size, offset ?? new Vector2(2f, 8f));
            }
            bool show = equipped && Plugin.ShowEquippedMarker.Value;
            if (existing.gameObject.activeSelf != show)
                existing.gameObject.SetActive(show);
            if (show)
                existing.SetAsLastSibling();
        }

        private static Transform Create(Transform parent, float size, Vector2 offset)
        {
            var go = new GameObject(ObjectName, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero; // bottom-left corner of the slot
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = offset;
            var img = go.GetComponent<Image>();
            img.sprite = Sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return rt;
        }

        private static Sprite CreateSprite()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = ObjectName,
            };

            // Check mark polyline in texture space (y up): left-middle -> bottom -> top-right.
            var a = new Vector2(10f, 32f);
            var b = new Vector2(26f, 14f);
            var c = new Vector2(54f, 50f);
            const float inner = 5.5f;  // white stroke half-width
            const float outer = 9.5f;  // black outline half-width

            var pixels = new Color32[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                    Color32 col;
                    if (d <= inner)
                        col = new Color32(255, 255, 255, 255);
                    else if (d <= outer)
                    {
                        // soft outer edge for anti-aliasing
                        byte alpha = (byte)(255f * Mathf.Clamp01(outer - d + 0.5f));
                        col = new Color32(0, 0, 0, alpha);
                    }
                    else
                        col = new Color32(0, 0, 0, 0);
                    pixels[y * TexSize + x] = col;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
