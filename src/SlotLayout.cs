using System.Collections.Generic;
using UnityEngine;

namespace ValheimSlots
{
    internal enum SlotKind
    {
        Helmet,
        Chest,
        Legs,
        Cape,
        Utility,
        Trinket,
        WeaponMain,
        WeaponOff,
        Ammo,
        Food,
        Mead,
    }

    internal sealed class SlotDef
    {
        public readonly int Index;
        public readonly SlotKind Kind;
        public readonly Vector2i GridPos;
        /// <summary>Cell (column, row) in the special panel.</summary>
        public readonly Vector2i PanelCell;
        private readonly string _labelEn;
        private readonly string _labelSv;
        /// <summary>Hotkey number (0-2) for food/mead/ammo, weapon set (0 = A, 1 = B) for weapons.</summary>
        public readonly int Group;

        /// <summary>Slot label in the game's language.</summary>
        public string Label => L.T(_labelEn, _labelSv);

        public SlotDef(int index, SlotKind kind, Vector2i gridPos, Vector2i panelCell, string labelEn, string labelSv, int group = 0)
        {
            Index = index;
            Kind = kind;
            GridPos = gridPos;
            PanelCell = panelCell;
            _labelEn = labelEn;
            _labelSv = labelSv;
            Group = group;
        }

        public bool IsRefillable => Kind == SlotKind.Ammo || Kind == SlotKind.Food || Kind == SlotKind.Mead;
        public bool IsArmor => Kind <= SlotKind.Trinket;
        public bool IsWeapon => Kind == SlotKind.WeaponMain || Kind == SlotKind.WeaponOff;
    }

    /// <summary>
    /// The whole mod lives in the player's normal inventory:
    ///   rows 0..MainRows-1   main inventory (row 0 is still the 1-8 hotbar)
    ///   rows MainRows..8     unused (reserved for vanilla's purchasable rows, max 9)
    ///   rows 9..11           special slots (equipment, weapons, ammo, food, mead)
    /// Because the special rows sit after vanilla's maximum of 9 rows they never move,
    /// even when the player buys more rows from the trader.
    /// </summary>
    internal static class SlotLayout
    {
        public const int Width = 8;
        public const int MaxVanillaRows = 9;
        public const int SpecialStartRow = MaxVanillaRows;
        public const int SpecialRows = 3;
        public const int TotalRows = SpecialStartRow + SpecialRows;

        /// <summary>Rows bought in vanilla ("invrows" player key). Updated whenever the game sets the size.</summary>
        public static int VanillaRows = 4;

        public static int MainRows => Mathf.Clamp(Mathf.Max(Plugin.MainRows?.Value ?? 6, VanillaRows), 1, MaxVanillaRows);

        public static readonly List<SlotDef> Slots = new List<SlotDef>();
        private static readonly Dictionary<Vector2i, SlotDef> ByPos = new Dictionary<Vector2i, SlotDef>();

        public static int LayoutVersion { get; private set; }

        static SlotLayout()
        {
            int r0 = SpecialStartRow, r1 = SpecialStartRow + 1, r2 = SpecialStartRow + 2;

            Add(SlotKind.Helmet, 0, r0, 0, 0, "Helmet", "Hjälm");
            Add(SlotKind.Chest, 1, r0, 1, 0, "Chest", "Bröst");
            Add(SlotKind.Legs, 2, r0, 2, 0, "Legs", "Ben");
            Add(SlotKind.Cape, 3, r0, 3, 0, "Cape", "Mantel");
            Add(SlotKind.Utility, 4, r0, 0, 1, "Belt", "Bälte");
            Add(SlotKind.Trinket, 5, r0, 1, 1, "Trinket", "Trinket");

            Add(SlotKind.WeaponMain, 0, r1, 0, 2, "A", "A", 0);
            Add(SlotKind.WeaponOff, 1, r1, 1, 2, "A", "A", 0);
            Add(SlotKind.WeaponMain, 2, r1, 2, 2, "B", "B", 1);
            Add(SlotKind.WeaponOff, 3, r1, 3, 2, "B", "B", 1);
            Add(SlotKind.Ammo, 4, r1, 0, 3, "", "", 0);
            Add(SlotKind.Ammo, 5, r1, 1, 3, "", "", 1);
            Add(SlotKind.Ammo, 6, r1, 2, 3, "", "", 2);

            Add(SlotKind.Food, 0, r2, 0, 4, "", "", 0);
            Add(SlotKind.Food, 1, r2, 1, 4, "", "", 1);
            Add(SlotKind.Food, 2, r2, 2, 4, "", "", 2);
            Add(SlotKind.Mead, 3, r2, 0, 5, "", "", 0);
            Add(SlotKind.Mead, 4, r2, 1, 5, "", "", 1);
            Add(SlotKind.Mead, 5, r2, 2, 5, "", "", 2);
        }

        private static void Add(SlotKind kind, int x, int y, int col, int row, string labelEn, string labelSv, int group = 0)
        {
            var def = new SlotDef(Slots.Count, kind, new Vector2i(x, y), new Vector2i(col, row), labelEn, labelSv, group);
            Slots.Add(def);
            ByPos[def.GridPos] = def;
        }

        public static void OnLayoutChanged() => LayoutVersion++;

        public static SlotDef GetSlot(Vector2i pos) => ByPos.TryGetValue(pos, out var def) ? def : null;

        public static SlotDef GetSlot(SlotKind kind, int group)
        {
            foreach (var s in Slots)
                if (s.Kind == kind && s.Group == group)
                    return s;
            return null;
        }

        public static bool IsMain(Vector2i pos) => pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < MainRows;

        public static bool IsSpecial(Vector2i pos) => ByPos.ContainsKey(pos);

        /// <summary>True for every position the main-inventory helpers (sort, quick stack, store) must leave alone.</summary>
        public static bool IsReserved(Vector2i pos) => !IsMain(pos);

        public static bool IsValidPlacement(ItemDrop.ItemData item, Vector2i pos)
        {
            if (IsMain(pos))
                return true;
            var def = GetSlot(pos);
            return def != null && item != null && Fits(def.Kind, item);
        }

        public static bool IsFood(ItemDrop.ItemData item)
        {
            var s = item.m_shared;
            return s.m_itemType == ItemDrop.ItemData.ItemType.Consumable && (s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f);
        }

        public static bool Fits(SlotKind kind, ItemDrop.ItemData item)
        {
            var t = item.m_shared.m_itemType;
            switch (kind)
            {
                case SlotKind.Helmet: return t == ItemDrop.ItemData.ItemType.Helmet;
                case SlotKind.Chest: return t == ItemDrop.ItemData.ItemType.Chest;
                case SlotKind.Legs: return t == ItemDrop.ItemData.ItemType.Legs;
                case SlotKind.Cape: return t == ItemDrop.ItemData.ItemType.Shoulder;
                case SlotKind.Utility: return t == ItemDrop.ItemData.ItemType.Utility;
                case SlotKind.Trinket: return t == ItemDrop.ItemData.ItemType.Trinket;
                case SlotKind.WeaponMain:
                    return t == ItemDrop.ItemData.ItemType.OneHandedWeapon
                        || t == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                        || t == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
                        || t == ItemDrop.ItemData.ItemType.Bow
                        || t == ItemDrop.ItemData.ItemType.Tool
                        || t == ItemDrop.ItemData.ItemType.Torch;
                case SlotKind.WeaponOff:
                    return t == ItemDrop.ItemData.ItemType.Shield || t == ItemDrop.ItemData.ItemType.Torch;
                case SlotKind.Ammo:
                    return t == ItemDrop.ItemData.ItemType.Ammo || t == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
                case SlotKind.Food: return IsFood(item);
                case SlotKind.Mead: return t == ItemDrop.ItemData.ItemType.Consumable && !IsFood(item);
            }
            return false;
        }

        /// <summary>The armor slot an equippable item belongs to, if any.</summary>
        public static SlotDef ArmorSlotFor(ItemDrop.ItemData item)
        {
            foreach (var s in Slots)
                if (s.IsArmor && Fits(s.Kind, item))
                    return s;
            return null;
        }

        public static bool IsTwoHanded(ItemDrop.ItemData item)
        {
            var t = item.m_shared.m_itemType;
            return t == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                || t == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
                || t == ItemDrop.ItemData.ItemType.Bow;
        }

        public static string KeyText(BepInEx.Configuration.KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return "";
            string main = shortcut.MainKey.ToString();
            if (main.StartsWith("Alpha")) main = main.Substring(5);
            foreach (var mod in shortcut.Modifiers)
            {
                string m = mod.ToString().Replace("Left", "").Replace("Right", "");
                main = m + "+" + main;
            }
            return main;
        }

        public static BepInEx.Configuration.KeyboardShortcut HotkeyFor(SlotDef def)
        {
            switch (def.Kind)
            {
                case SlotKind.Food: return Plugin.FoodKeys[def.Group].Value;
                case SlotKind.Mead: return Plugin.MeadKeys[def.Group].Value;
                case SlotKind.Ammo: return Plugin.AmmoKeys[def.Group].Value;
            }
            return BepInEx.Configuration.KeyboardShortcut.Empty;
        }
    }
}
