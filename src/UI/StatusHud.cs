using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// One compact panel above the bottom of the screen:
    ///   [weight] [durability warning]
    ///   food x3 | mead x3 | ammo x3   (icon, stack size, hotkey)
    /// </summary>
    internal static class StatusHud
    {
        private const float SlotSize = 48f;
        private const float Gap = 4f;
        private const float GroupGap = 14f;

        private sealed class SlotView
        {
            public SlotDef Def;
            public Image Frame;
            public Image Icon;
            public TextMeshProUGUI Amount;
            public TextMeshProUGUI Key;
        }

        private static RectTransform _root;
        private static TextMeshProUGUI _info;
        private static SlotView[] _views;
        private static Hud _builtFor;
        private static float _nextInfo;

        // Second row above the slots: [arrow] "Grav 240 m"      [no-portal icon] "3 ej portal"
        private static RectTransform _graveBadge;
        private static RectTransform _graveArrow;
        private static TextMeshProUGUI _graveText;
        private static RectTransform _portalBadge;
        private static Image _portalIcon;
        private static TextMeshProUGUI _portalText;

        private static readonly Color FrameColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color EquippedFrame = new Color(0.9f, 0.7f, 0.2f, 0.75f);

        public static void Update(Player player)
        {
            var hud = Hud.instance;
            if (hud == null)
                return;

            if (_builtFor != hud || _root == null)
                Build(hud);
            if (_root == null)
                return;

            bool show = Plugin.ShowHud.Value;
            if (_root.gameObject.activeSelf != show)
                _root.gameObject.SetActive(show);
            if (!show)
                return;

            _root.anchoredPosition = Plugin.HudPosition.Value;

            var inv = player.m_inventory;
            foreach (var v in _views)
            {
                var item = inv.GetItemAt(v.Def.GridPos.x, v.Def.GridPos.y);
                if (item == null)
                {
                    v.Icon.enabled = false;
                    v.Amount.text = "";
                    v.Frame.color = FrameColor;
                }
                else
                {
                    v.Icon.enabled = true;
                    v.Icon.sprite = item.GetIcon();
                    v.Amount.text = item.m_shared.m_maxStackSize > 1 ? item.m_stack.ToString() : "";
                    v.Frame.color = item.m_equipped ? EquippedFrame : FrameColor;
                }
                EquippedMarker.Set(v.Frame.transform, item != null && item.m_equipped, 18f, new Vector2(1f, 1f));
                v.Key.text = SlotLayout.KeyText(SlotLayout.HotkeyFor(v.Def));
            }

            UpdateGrave(player);

            if (Time.time >= _nextInfo)
            {
                _nextInfo = Time.time + 0.5f;
                _info.text = BuildInfo(player);
                UpdatePortal(player);
            }
        }

        private static string BuildInfo(Player player)
        {
            var sb = new StringBuilder();
            if (Plugin.ShowWeight.Value)
            {
                int w = Mathf.CeilToInt(player.GetInventory().GetTotalWeight());
                int max = Mathf.CeilToInt(player.GetMaxCarryWeight());
                string weight = L.T($"Weight {w}/{max}", $"Vikt {w}/{max}");
                sb.Append(w > max ? $"<color=#ff5050>{weight}</color>" : weight);
            }

            int warn = Plugin.DurabilityWarnPercent.Value;
            if (warn > 0)
            {
                ItemDrop.ItemData worst = null;
                float worstPct = 1f;
                foreach (var item in player.GetInventory().GetEquippedItems())
                {
                    if (!item.m_shared.m_useDurability)
                        continue;
                    float pct = item.GetDurabilityPercentage();
                    if (pct * 100f < warn && pct < worstPct)
                    {
                        worst = item;
                        worstPct = pct;
                    }
                }
                if (worst != null)
                {
                    if (sb.Length > 0) sb.Append("   ");
                    string name = Localization.instance.Localize(worst.m_shared.m_name);
                    sb.Append($"<color=#ffb040>! {name} {Mathf.RoundToInt(worstPct * 100f)}%</color>");
                }
            }
            return sb.ToString();
        }

        private static void Build(Hud hud)
        {
            _builtFor = hud;
            _root = null;
            var font = hud.m_hoverName != null ? hud.m_hoverName.font : null;
            if (font == null || hud.m_rootObject == null)
                return;

            _root = new GameObject("ValheimSlotsHud", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(hud.m_rootObject.transform, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);

            // food (3) | mead (3) | ammo (3)
            var order = new[] { SlotKind.Food, SlotKind.Mead, SlotKind.Ammo };
            _views = new SlotView[9];
            float totalWidth = 9 * SlotSize + 6 * Gap + 2 * GroupGap;
            _root.sizeDelta = new Vector2(totalWidth, SlotSize + 30f);
            float x = -totalWidth / 2f;
            int n = 0;
            for (int g = 0; g < order.Length; g++)
            {
                for (int i = 0; i < 3; i++)
                {
                    var def = SlotLayout.GetSlot(order[g], i);
                    _views[n++] = CreateSlot(def, font, new Vector2(x + SlotSize / 2f, SlotSize / 2f));
                    x += SlotSize + (i < 2 ? Gap : 0f);
                }
                x += GroupGap;
            }

            _info = CreateText("Info", _root, font, 16f, TextAlignmentOptions.Bottom);
            var infoRt = _info.rectTransform;
            infoRt.anchorMin = infoRt.anchorMax = new Vector2(0.5f, 0f);
            infoRt.pivot = new Vector2(0.5f, 0f);
            infoRt.sizeDelta = new Vector2(totalWidth + 200f, 24f);
            infoRt.anchoredPosition = new Vector2(0f, SlotSize + 4f);

            float badgeY = SlotSize + 32f;
            _graveBadge = CreateBadge("Grave", font, new Vector2(-totalWidth / 4f, badgeY), out var graveIcon, out _graveText);
            graveIcon.sprite = Icons.Arrow;
            _graveArrow = graveIcon.rectTransform;
            _graveBadge.gameObject.SetActive(false);

            _portalBadge = CreateBadge("Portal", font, new Vector2(totalWidth / 4f, badgeY), out _portalIcon, out _portalText);
            _portalBadge.gameObject.SetActive(false);
        }

        /// <summary>[icon] text, centered on <paramref name="center"/>.</summary>
        private static RectTransform CreateBadge(string name, TMP_FontAsset font, Vector2 center, out Image icon, out TextMeshProUGUI text)
        {
            const float iconSize = 24f;
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, iconSize);
            rt.anchoredPosition = center;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(rt, false);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.anchoredPosition = new Vector2(iconSize / 2f, 0f);
            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            text = CreateText("Text", rt, font, 16f, TextAlignmentOptions.MidlineLeft);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(iconSize + 6f, 0f);
            trt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void UpdateGrave(Player player)
        {
            if (_graveBadge == null)
                return;
            float dist = 0f;
            Vector3? grave = Plugin.ShowGraveArrow.Value ? GraveTracker.Nearest(player, out dist) : null;
            if (grave == null || dist < 3f)
            {
                if (_graveBadge.gameObject.activeSelf) _graveBadge.gameObject.SetActive(false);
                return;
            }
            if (!_graveBadge.gameObject.activeSelf) _graveBadge.gameObject.SetActive(true);

            // Direction relative to where the camera looks (top of screen = straight ahead).
            var cam = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            var to = Vector3.ProjectOnPlane(grave.Value - player.transform.position, Vector3.up);
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            _graveArrow.localEulerAngles = new Vector3(0f, 0f, -angle);
            string graveLabel = L.T("Grave", "Grav");
            _graveText.text = dist >= 1000f ? $"{graveLabel} {dist / 1000f:0.0} km" : $"{graveLabel} {Mathf.RoundToInt(dist)} m";
        }

        private static void UpdatePortal(Player player)
        {
            if (_portalBadge == null)
                return;
            int count = 0;
            if (Plugin.ShowPortalWarning.Value && ZoneSystem.instance != null && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll))
            {
                foreach (var item in player.m_inventory.m_inventory)
                    if (!item.m_shared.m_teleportable)
                        count++;
            }
            if (count == 0)
            {
                if (_portalBadge.gameObject.activeSelf) _portalBadge.gameObject.SetActive(false);
                return;
            }
            if (!_portalBadge.gameObject.activeSelf) _portalBadge.gameObject.SetActive(true);
            if (_portalIcon.sprite == null)
                _portalIcon.sprite = NoTeleportSprite();
            _portalIcon.enabled = _portalIcon.sprite != null;
            _portalText.text = count == 1
                ? L.T("1 item can't use portals", "1 föremål ej portal")
                : L.T($"{count} items can't use portals", $"{count} föremål ej portal");
        }

        /// <summary>The game's own "can't teleport" icon from the inventory slot prefab.</summary>
        private static Sprite NoTeleportSprite()
        {
            var prefab = InventoryGui.instance != null ? InventoryGui.instance.m_playerGrid?.m_elementPrefab : null;
            var element = prefab != null ? prefab.GetComponent<InventoryElement>() : null;
            return element != null && element.m_noteleport != null ? element.m_noteleport.sprite : null;
        }

        private static SlotView CreateSlot(SlotDef def, TMP_FontAsset font, Vector2 center)
        {
            var frameGo = new GameObject("Slot" + def.Index, typeof(RectTransform), typeof(Image));
            var frameRt = (RectTransform)frameGo.transform;
            frameRt.SetParent(_root, false);
            frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 0f);
            frameRt.sizeDelta = new Vector2(SlotSize, SlotSize);
            frameRt.anchoredPosition = center;
            var frame = frameGo.GetComponent<Image>();
            frame.color = FrameColor;
            frame.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(frameRt, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(5f, 5f);
            iconRt.offsetMax = new Vector2(-5f, -5f);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            var amount = CreateText("Amount", frameRt, font, 13f, TextAlignmentOptions.BottomRight);
            Stretch(amount.rectTransform, new Vector2(2f, 1f), new Vector2(-3f, -2f));

            var key = CreateText("Key", frameRt, font, 12f, TextAlignmentOptions.TopLeft);
            Stretch(key.rectTransform, new Vector2(3f, 1f), new Vector2(-2f, -2f));
            key.color = new Color(1f, 0.85f, 0.4f, 1f);

            return new SlotView { Def = def, Frame = frame, Icon = icon, Amount = amount, Key = key };
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            return text;
        }

        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
