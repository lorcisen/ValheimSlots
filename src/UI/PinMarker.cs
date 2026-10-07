using UnityEngine;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>Pin-head marker on the left edge of a pinned recipe in the crafting list.</summary>
    internal static class PinMarker
    {
        private const string ObjectName = "ValheimSlotsPinMarker";

        public static void Set(Transform element, bool pinned)
        {
            var existing = element.Find(ObjectName);
            if (existing == null)
            {
                if (!pinned)
                    return;
                var go = new GameObject(ObjectName, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(element, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(14f, 14f);
                rt.anchoredPosition = new Vector2(4f, 0f);
                var img = go.GetComponent<Image>();
                img.sprite = Icons.Pin;
                img.raycastTarget = false;
                existing = rt;
            }
            existing.gameObject.SetActive(pinned);
            if (pinned)
                existing.SetAsLastSibling();
        }
    }
}
