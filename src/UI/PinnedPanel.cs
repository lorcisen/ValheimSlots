using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// Pinned recipes on the right side of the screen:
    ///   [icon] Bronze sword            [✓] [X]
    ///      [icon] Bronze        2/8
    ///      [icon] Wood          4/2   [✓]
    /// Counts come from the player inventory. A check mark (shape, not color) marks what you have enough of.
    /// The X buttons are only shown while the inventory is open (that is when the mouse cursor is free);
    /// the panel then moves to the left of the crafting panel so it never covers it.
    /// </summary>
    internal static class PinnedPanel
    {
        private const float Width = 270f;
        private const float HeaderHeight = 28f;
        private const float RowHeight = 22f;
        private const float BlockGap = 8f;
        private const float Pad = 6f;

        private sealed class IngredientRow
        {
            public string ItemName;
            public int Need;
            public TextMeshProUGUI Text;
            public Image Check;
            public string Label;
        }

        private sealed class Block
        {
            public PinnedRecipes.Pin Pin;
            public Image HeaderCheck;
            public GameObject RemoveButton;
            public readonly List<IngredientRow> Rows = new List<IngredientRow>();
        }

        private static RectTransform _root;
        private static Hud _builtFor;
        private static TMP_FontAsset _font;
        private static int _builtVersion = -1;
        private static bool _builtSwedish;
        private static readonly List<Block> Blocks = new List<Block>();
        private static float _nextCount;
        private static bool _buttonsShown;

        private static readonly Color BlockBg = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Vector3[] Corners = new Vector3[4];

        public static void Update(Player player)
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return;
            if (_builtFor != hud || _root == null)
                CreateRoot(hud);
            if (_root == null)
                return;

            var pins = PinnedRecipes.All;
            bool show = Plugin.PinnedEnabled.Value && pins.Count > 0;
            if (_root.gameObject.activeSelf != show)
                _root.gameObject.SetActive(show);
            if (!show)
                return;

            bool swedish = L.Swedish;
            if (_builtVersion != PinnedRecipes.Version || _builtSwedish != swedish)
            {
                _builtSwedish = swedish;
                Rebuild(pins); // names and labels follow the game language
            }

            bool inventoryOpen = InventoryGui.IsVisible();
            if (_buttonsShown != inventoryOpen)
            {
                _buttonsShown = inventoryOpen;
                foreach (var b in Blocks)
                    b.RemoveButton.SetActive(inventoryOpen);
            }

            Position(inventoryOpen);

            if (Time.time >= _nextCount)
            {
                _nextCount = Time.time + 0.25f;
                UpdateCounts(player);
            }
        }

        private static void Position(bool inventoryOpen)
        {
            _root.anchoredPosition = Plugin.PinnedPosition.Value;
            var crafting = InventoryGui.instance != null ? InventoryGui.instance.m_crafting : null;
            if (!inventoryOpen || crafting == null || !crafting.gameObject.activeInHierarchy)
                return;
            // Sit just left of the crafting panel while the inventory is open.
            crafting.GetWorldCorners(Corners);
            float left = Corners[0].x;
            float gap = 10f * _root.lossyScale.x;
            var p = _root.position;
            if (p.x > left - gap)
                _root.position = new Vector3(left - gap, p.y, p.z);
        }

        private static void UpdateCounts(Player player)
        {
            var inv = player.m_inventory;
            foreach (var block in Blocks)
            {
                bool all = true;
                foreach (var row in block.Rows)
                {
                    int have = inv.CountItems(row.ItemName);
                    bool enough = have >= row.Need;
                    all &= enough;
                    row.Text.text = $"{row.Label}  {have}/{row.Need}";
                    row.Text.color = enough ? Color.white : Dim;
                    row.Check.enabled = enough;
                }
                block.HeaderCheck.enabled = all && block.Rows.Count > 0;
            }
        }

        // --- Building -------------------------------------------------------------------------------

        private static void CreateRoot(Hud hud)
        {
            _builtFor = hud;
            _root = null;
            _font = hud.m_hoverName != null ? hud.m_hoverName.font : null;
            if (_font == null)
                return;

            var go = new GameObject("ValheimSlotsPinned", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _root = (RectTransform)go.transform;
            _root.SetParent(hud.m_rootObject.transform, false);
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 0.5f);
            _root.pivot = new Vector2(1f, 1f); // top-right corner is the anchor point
            _root.sizeDelta = new Vector2(Width, 10f);
            // Own canvas so the X buttons receive clicks; drawn above the HUD.
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            _builtVersion = -1;
            _buttonsShown = false;
        }

        private static void Rebuild(IReadOnlyList<PinnedRecipes.Pin> pins)
        {
            _builtVersion = PinnedRecipes.Version;
            foreach (Transform child in _root)
                Object.Destroy(child.gameObject);
            Blocks.Clear();
            _buttonsShown = false;

            float y = 0f;
            int shown = 0;
            foreach (var pin in pins)
            {
                if (pin.Recipe == null || pin.Recipe.m_item == null)
                    continue;
                if (shown++ >= Plugin.PinnedMaxShown.Value)
                    break;
                y = BuildBlock(pin, y);
                y -= BlockGap;
            }
            _root.sizeDelta = new Vector2(Width, Mathf.Max(10f, -y));
            _nextCount = 0f;
        }

        private static float BuildBlock(PinnedRecipes.Pin pin, float top)
        {
            var block = new Block { Pin = pin };
            var item = pin.Recipe.m_item.m_itemData;

            var reqs = new List<Piece.Requirement>();
            foreach (var req in pin.Recipe.m_resources)
                if (req?.m_resItem != null && req.GetAmount(pin.Quality) > 0)
                    reqs.Add(req);

            float height = Pad * 2f + HeaderHeight + reqs.Count * RowHeight;
            var bg = NewRect("Pin", _root, new Vector2(0f, top), new Vector2(Width, height));
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = BlockBg;
            bgImg.raycastTarget = false;

            // Header: icon, name (+ level), all-ingredients check, remove button.
            float y = -Pad;
            NewIcon(bg, item.GetIcon(), new Vector2(Pad, y), 24f);
            string title = Localization.instance.Localize(item.m_shared.m_name);
            if (pin.Quality > 1) title += L.T($" (level {pin.Quality})", $" (nivå {pin.Quality})");
            var titleText = NewText(bg, title, 17f, new Vector2(Pad + 30f, y), new Vector2(Width - Pad * 2f - 30f - 52f, HeaderHeight));
            titleText.fontStyle = FontStyles.Bold;
            block.HeaderCheck = NewIcon(bg, EquippedMarker.Sprite, new Vector2(Width - Pad - 50f, y - 2f), 22f);
            block.RemoveButton = NewRemoveButton(bg, new Vector2(Width - Pad - 24f, y), pin);
            block.RemoveButton.SetActive(false);

            // Ingredients.
            y -= HeaderHeight;
            foreach (var req in reqs)
            {
                var res = req.m_resItem.m_itemData;
                NewIcon(bg, res.GetIcon(), new Vector2(Pad + 14f, y - 1f), 20f);
                var text = NewText(bg, "", 15f, new Vector2(Pad + 40f, y), new Vector2(Width - Pad * 2f - 40f - 26f, RowHeight));
                var check = NewIcon(bg, EquippedMarker.Sprite, new Vector2(Width - Pad - 24f, y - 1f), 18f);
                check.enabled = false;
                block.Rows.Add(new IngredientRow
                {
                    ItemName = res.m_shared.m_name,
                    Need = req.GetAmount(pin.Quality),
                    Text = text,
                    Check = check,
                    Label = Localization.instance.Localize(res.m_shared.m_name),
                });
                y -= RowHeight;
            }

            Blocks.Add(block);
            return top - height;
        }

        private static GameObject NewRemoveButton(RectTransform parent, Vector2 topLeft, PinnedRecipes.Pin pin)
        {
            var rt = NewRect("Remove", parent, topLeft, new Vector2(24f, 24f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.25f, 0.05f, 0.05f, 0.85f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => PinnedRecipes.Remove(pin));
            var x = NewText(rt, "X", 16f, Vector2.zero, new Vector2(24f, 24f));
            x.alignment = TextAlignmentOptions.Center;
            x.fontStyle = FontStyles.Bold;
            return rt.gameObject;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 topLeft, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = topLeft;
            return rt;
        }

        private static Image NewIcon(RectTransform parent, Sprite sprite, Vector2 topLeft, float size)
        {
            var rt = NewRect("Icon", parent, topLeft, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(RectTransform parent, string text, float size, Vector2 topLeft, Vector2 box)
        {
            var rt = NewRect("Text", parent, topLeft, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.fontSize = size;
            t.text = text;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }
    }
}
