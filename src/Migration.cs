using System.Collections.Generic;
using HarmonyLib;

namespace ValheimSlots
{
    /// <summary>
    /// Puts every item on a position it is allowed to occupy. Runs when the character spawns, which covers:
    ///  - characters coming from EquipmentAndQuickSlots (its slots are extra rows right after the vanilla rows),
    ///  - characters coming from other inventory mods,
    ///  - lowering "Main rows" in the config.
    /// Items are moved, never deleted. Only if the inventory is completely full is an item dropped at the player's feet.
    /// </summary>
    [HarmonyPatch]
    internal static class Migration
    {
        private const string VersionKey = "ValheimSlots.version";

        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        [HarmonyPostfix]
        private static void Player_Load(Player __instance) => RefreshVanillaRows(__instance);

        [HarmonyPatch(typeof(Player), nameof(Player.EquipInventoryItems))]
        [HarmonyPrefix]
        private static void Player_EquipInventoryItems(Player __instance) => RefreshVanillaRows(__instance);

        private static void RefreshVanillaRows(Player player)
        {
            if (player != Player.m_localPlayer)
                return;
            SlotLayout.VanillaRows = player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out var v) && int.TryParse(v, out int rows)
                ? rows
                : 4;
        }

        public static void Sanitize(Player player)
        {
            var inv = player.m_inventory;
            var occupied = new HashSet<Vector2i>();
            var stray = new List<ItemDrop.ItemData>();

            // Valid items in main rows / matching special slots stay. The first item on a position wins.
            foreach (var item in inv.m_inventory)
            {
                if (SlotLayout.IsValidPlacement(item, item.m_gridPos) && occupied.Add(item.m_gridPos))
                    continue;
                stray.Add(item);
            }

            if (stray.Count == 0)
            {
                player.m_customData[VersionKey] = Plugin.Version;
                return;
            }

            // Equipped items and armor first so they get their dedicated slots.
            stray.Sort((a, b) => Priority(b).CompareTo(Priority(a)));

            var dropped = new List<ItemDrop.ItemData>();
            int moved = 0;
            foreach (var item in stray)
            {
                var target = PreferredSpecialSlot(item, occupied);
                if (target.x < 0)
                    target = FirstFreeMain(inv, occupied);
                if (target.x < 0)
                {
                    dropped.Add(item);
                    continue;
                }
                Plugin.Log.LogInfo($"Moving {item.m_shared.m_name} from ({item.m_gridPos.x},{item.m_gridPos.y}) to ({target.x},{target.y}).");
                item.m_gridPos = target;
                occupied.Add(target);
                moved++;
            }

            foreach (var item in dropped)
            {
                Plugin.Log.LogWarning($"Inventory full - dropping {item.m_shared.m_name} at your feet.");
                player.DropItem(inv, item, item.m_stack);
            }

            if (moved > 0 || dropped.Count > 0)
            {
                Inv.Changed(inv);
                player.Message(MessageHud.MessageType.Center,
                    dropped.Count > 0
                        ? L.T($"ValheimSlots: moved {moved} items, {dropped.Count} dropped on the ground (inventory full).",
                              $"ValheimSlots: flyttade {moved} föremål, {dropped.Count} släpptes på marken (fullt).")
                        : L.T($"ValheimSlots: moved {moved} items to new slots.",
                              $"ValheimSlots: flyttade {moved} föremål till nya platser."));
            }
            player.m_customData[VersionKey] = Plugin.Version;
        }

        private static int Priority(ItemDrop.ItemData item)
        {
            if (item.m_equipped) return 2;
            return SlotLayout.ArmorSlotFor(item) != null ? 1 : 0;
        }

        private static Vector2i PreferredSpecialSlot(ItemDrop.ItemData item, HashSet<Vector2i> occupied)
        {
            foreach (var def in SlotLayout.Slots)
            {
                if (occupied.Contains(def.GridPos) || !SlotLayout.Fits(def.Kind, item))
                    continue;
                // Armor always goes to its slot; weapons only when equipped; consumables/ammo stay in the main grid.
                if (def.IsArmor || (def.IsWeapon && item.m_equipped && def.Group == 0))
                    return def.GridPos;
            }
            return new Vector2i(-1, -1);
        }

        private static Vector2i FirstFreeMain(Inventory inv, HashSet<Vector2i> occupied)
        {
            int rows = SlotLayout.MainRows;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < inv.GetWidth(); x++)
                {
                    var p = new Vector2i(x, y);
                    if (!occupied.Contains(p))
                        return p;
                }
            return new Vector2i(-1, -1);
        }
    }
}
