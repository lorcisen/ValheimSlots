using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// Re-arranges the player InventoryGrid: main rows stay in the grid, the unused rows are parked,
    /// and the special rows are moved into a separate panel beside the inventory.
    /// The grid keeps owning all elements, so clicking, dragging, tooltips and other mods' element
    /// decorations (EpicLoot rarity, BetterUI stars, QuickStackStore borders) keep working.
    /// </summary>
    internal static class SlotPanel
    {
        private const float Cell = 74f;
        private static readonly Vector2 Origin = new Vector2(25.5f, -27f);

        private static RectTransform _slotRoot;
        private static RectTransform _hiddenRoot;
        private static RectTransform _background;
        private static Image _inventoryBkg;

        private static int _appliedMainRows = -1;
        private static bool _colorsCaptured;
        private static ColorBlock _defaultColors;

        private static readonly Color DenyNormal = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        private static readonly Color DenyHighlight = new Color(0.9f, 0.3f, 0.3f, 0.7f);
        private static readonly Color AllowNormal = new Color(0.3f, 0.8f, 0.3f, 0.45f);
        private static readonly Color LockedNormal = new Color(0.35f, 0.55f, 0.95f, 0.55f);
        private static readonly Color LockedHighlight = new Color(0.45f, 0.65f, 1f, 0.75f);
        private static readonly Color ActiveSetLabel = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color InactiveLabel = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color HotkeyLabel = new Color(1f, 0.85f, 0.4f, 1f);

        private static readonly Vector3[] Corners = new Vector3[4];

        public static void Reset()
        {
            _slotRoot = null;
            _hiddenRoot = null;
            _background = null;
            _inventoryBkg = null;
            _colorsCaptured = false;
            _appliedMainRows = -1;
        }

        public static void Layout(InventoryGrid grid, Player player)
        {
            var gui = InventoryGui.instance;
            if (gui == null || grid.m_elements.Count == 0)
                return;

            var inv = grid.m_inventory;
            int width = inv.GetWidth();
            int mainRows = SlotLayout.MainRows;
            float space = grid.m_elementSpace;

            grid.m_gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, mainRows * space);
            if (mainRows != _appliedMainRows)
            {
                _appliedMainRows = mainRows;
                gui.SetInventorySize(mainRows); // resize the player panel (e.g. after changing the config in game)
            }

            EnsureRoots(gui, grid);
            CaptureColors(grid.m_elements[0]);

            var dragItem = gui.m_dragItem;
            bool draggingOwn = dragItem != null;

            // Vanilla element origin (see InventoryGrid.UpdateGui).
            var gridRect = (RectTransform)grid.transform;
            var widget = grid.GetWidgetSize();
            Vector2 origin = new Vector2(gridRect.rect.width / 2f, 0f) - new Vector2(widget.x, 0f) * 0.5f;

            bool anySpecial = false;
            for (int i = 0; i < grid.m_elements.Count; i++)
            {
                var element = grid.m_elements[i];
                if (element == null)
                    continue;
                var go = element.gameObject;
                var pos = new Vector2i(i % width, i / width);
                EquippedMarker.Set(go.transform, element.m_used && element.m_equiped != null && element.m_equiped.enabled);

                if (pos.y < mainRows)
                {
                    if (go.transform.parent != grid.m_gridRoot)
                    {
                        go.transform.SetParent(grid.m_gridRoot, false);
                        ((RectTransform)go.transform).anchoredPosition = origin + new Vector2(pos.x * space, -pos.y * space);
                    }
                    if (!go.activeSelf)
                        go.SetActive(true);
                    bool locked = LockedSlots.IsOwnLocked(pos); // QSS draws its own border for favorites
                    SetColors(go, locked ? LockedNormal : (Color?)null, locked ? LockedHighlight : (Color?)null);
                    continue;
                }

                var def = SlotLayout.GetSlot(pos);
                if (def == null)
                {
                    Park(go);
                    continue;
                }

                anySpecial = true;
                if (go.transform.parent != _slotRoot)
                    go.transform.SetParent(_slotRoot, false);
                if (!go.activeSelf)
                    go.SetActive(true);
                ((RectTransform)go.transform).anchoredPosition =
                    Plugin.PanelPosition.Value + Origin + new Vector2(def.PanelCell.x * Cell, -def.PanelCell.y * Cell);

                SetLabel(go, def);

                if (draggingOwn)
                {
                    bool fits = SlotLayout.Fits(def.Kind, dragItem);
                    SetColors(go, fits ? AllowNormal : DenyNormal, fits ? (Color?)null : DenyHighlight);
                }
                else
                {
                    SetColors(go, null, null);
                }
            }

            UpdateBackground(grid, anySpecial);
        }

        private static void EnsureRoots(InventoryGui gui, InventoryGrid grid)
        {
            var playerPanel = gui.m_player;
            var gridRoot = grid.m_gridRoot;

            if (_slotRoot == null)
            {
                _slotRoot = new GameObject("ValheimSlotsRoot", typeof(RectTransform)).GetComponent<RectTransform>();
                _slotRoot.SetParent(playerPanel, false);
                _slotRoot.SetAsLastSibling();
            }
            // Align the slot root with the grid root so slot positions are relative to the inventory grid.
            _slotRoot.anchorMin = gridRoot.anchorMin;
            _slotRoot.anchorMax = gridRoot.anchorMax;
            _slotRoot.pivot = gridRoot.pivot;
            _slotRoot.sizeDelta = gridRoot.rect.size;
            _slotRoot.position = gridRoot.position;
            _slotRoot.localScale = Vector3.one;

            if (_hiddenRoot == null)
            {
                _hiddenRoot = new GameObject("ValheimSlotsHidden", typeof(RectTransform)).GetComponent<RectTransform>();
                _hiddenRoot.SetParent(playerPanel, false);
                _hiddenRoot.gameObject.SetActive(false);
            }

            if (_inventoryBkg == null)
                _inventoryBkg = playerPanel.Find("Bkg")?.GetComponent<Image>();

            if (_background == null && _inventoryBkg != null)
            {
                _background = Object.Instantiate(_inventoryBkg.rectTransform, _slotRoot, false);
                _background.name = "ValheimSlotsBkg";
                foreach (Transform child in _background)
                    Object.Destroy(child.gameObject);
                _background.anchorMin = _background.anchorMax = new Vector2(0.5f, 0.5f);
                _background.pivot = new Vector2(0.5f, 0.5f);
                _background.localScale = Vector3.one;
                var img = _background.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
            }
            if (_background != null)
                _background.SetAsFirstSibling();
        }

        private static void UpdateBackground(InventoryGrid grid, bool visible)
        {
            if (_background == null)
                return;
            _background.gameObject.SetActive(visible);
            if (!visible)
                return;

            // Fit the background around the placed slot elements (in slot-root space).
            bool any = false;
            Vector2 min = default, max = default;
            foreach (Transform child in _slotRoot)
            {
                if (child == _background || !child.gameObject.activeSelf)
                    continue;
                ((RectTransform)child).GetWorldCorners(Corners);
                for (int c = 0; c < 4; c++)
                {
                    Vector2 p = _slotRoot.InverseTransformPoint(Corners[c]);
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                }
            }
            if (!any)
                return;
            const float pad = 11f;
            _background.sizeDelta = (max - min) + new Vector2(pad * 2f, pad * 2f);
            _background.localPosition = (min + max) / 2f;
        }

        private static void Park(GameObject go)
        {
            if (go.activeSelf)
                go.SetActive(false);
            if (_hiddenRoot != null && go.transform.parent != _hiddenRoot)
                go.transform.SetParent(_hiddenRoot, false);
        }

        private static void SetLabel(GameObject go, SlotDef def)
        {
            var binding = go.transform.Find("binding")?.GetComponent<TMP_Text>();
            if (binding == null)
                return;
            binding.enabled = true;
            binding.gameObject.SetActive(true);
            binding.textWrappingMode = TextWrappingModes.NoWrap;
            binding.overflowMode = TextOverflowModes.Overflow;

            string text;
            Color color = InactiveLabel;
            switch (def.Kind)
            {
                case SlotKind.Food:
                case SlotKind.Mead:
                case SlotKind.Ammo:
                    // Only the hotkey here – the row gets its own header to the right.
                    text = SlotLayout.KeyText(SlotLayout.HotkeyFor(def));
                    color = HotkeyLabel;
                    if (def.Group == 2)
                        SetRowHeader(go, binding, def.Kind == SlotKind.Food ? L.T("Food", "Mat")
                            : def.Kind == SlotKind.Mead ? L.T("Mead", "Mjöd") : L.T("Ammo", "Ammo"));
                    break;
                case SlotKind.WeaponMain:
                    text = def.Label + "1";
                    if (def.Group == WeaponSets.ActiveSet) color = ActiveSetLabel;
                    break;
                case SlotKind.WeaponOff:
                    text = def.Label + "2";
                    if (def.Group == WeaponSets.ActiveSet) color = ActiveSetLabel;
                    break;
                default:
                    text = def.Label;
                    break;
            }
            binding.text = text;
            binding.color = color;
        }

        /// <summary>A header ("Food", "Mead", "Ammo") in the free fourth cell, right of the row's last slot.</summary>
        private static void SetRowHeader(GameObject lastSlotInRow, TMP_Text template, string text)
        {
            const string name = "ValheimSlotsRowHeader";
            var header = lastSlotInRow.transform.Find(name)?.GetComponent<TMP_Text>();
            if (header == null)
            {
                var go = Object.Instantiate(template.gameObject, lastSlotInRow.transform, false);
                go.name = name;
                header = go.GetComponent<TMP_Text>();
                var rt = header.rectTransform;
                var slotRt = (RectTransform)lastSlotInRow.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); // right edge, vertical middle of the slot
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(Cell, slotRt.rect.height > 0 ? slotRt.rect.height : 64f);
                rt.anchoredPosition = new Vector2(Cell - slotRt.rect.width + 6f, 0f);
                header.alignment = TextAlignmentOptions.MidlineLeft;
                header.fontSize = template.fontSize * 1.25f;
                header.raycastTarget = false;
            }
            header.enabled = true;
            header.gameObject.SetActive(true);
            header.text = text;
            header.color = InactiveLabel;
        }

        private static void CaptureColors(InventoryElement element)
        {
            if (_colorsCaptured)
                return;
            var button = element.GetComponent<Button>();
            if (button == null)
                return;
            _defaultColors = button.colors;
            _colorsCaptured = true;
        }

        private static void SetColors(GameObject go, Color? normal, Color? highlight)
        {
            if (!_colorsCaptured)
                return;
            var button = go.GetComponent<Button>();
            if (button == null)
                return;
            var colors = _defaultColors;
            if (normal.HasValue) colors.normalColor = normal.Value;
            if (highlight.HasValue) colors.highlightedColor = highlight.Value;
            if (button.colors != colors)
                button.colors = colors;
        }
    }
}
