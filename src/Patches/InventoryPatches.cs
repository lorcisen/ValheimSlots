using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimSlots
{
    internal static class Inv
    {
        public static bool IsPlayer(Inventory inv)
        {
            var p = Player.m_localPlayer;
            return p != null && ReferenceEquals(inv, p.m_inventory);
        }

        public static bool IsFreeMainSlot(Inventory inv, int x, int y)
            => !LockedSlots.IsLocked(new Vector2i(x, y)) && inv.GetItemAt(x, y) == null;

        public static Vector2i FindFreeMainSlot(Inventory inv, bool topFirst)
        {
            int rows = SlotLayout.MainRows;
            int width = inv.GetWidth();
            if (topFirst)
            {
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < width; x++)
                        if (IsFreeMainSlot(inv, x, y))
                            return new Vector2i(x, y);
            }
            else
            {
                for (int y = rows - 1; y >= 0; y--)
                    for (int x = 0; x < width; x++)
                        if (IsFreeMainSlot(inv, x, y))
                            return new Vector2i(x, y);
            }
            return new Vector2i(-1, -1);
        }

        public static int CountFreeMainSlots(Inventory inv)
        {
            int free = 0;
            int rows = SlotLayout.MainRows;
            int width = inv.GetWidth();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < width; x++)
                    if (IsFreeMainSlot(inv, x, y))
                        free++;
            return free;
        }

        /// <summary>Raises the inventory's change callbacks so the GUI and weight refresh.</summary>
        public static void Changed(Inventory inv) => inv.Changed();
    }

    [HarmonyPatch]
    internal static class InventoryPatches
    {
        // --- Size --------------------------------------------------------------------------

        [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
        [HarmonyPostfix]
        private static void Player_Awake(Player __instance)
        {
            // Make room for the special rows before the profile is loaded into this inventory.
            __instance.m_inventory.SetHeight(SlotLayout.TotalRows);
        }

        /// <summary>
        /// Vanilla sets the height to the purchased row count and then drops everything below it.
        /// We keep vanilla's bookkeeping ("invrows") untouched but size the inventory for our layout.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.SetInventorySize))]
        [HarmonyPrefix]
        private static bool Player_SetInventorySize(Player __instance, int rows)
        {
            rows = Mathf.Clamp(rows, 0, SlotLayout.MaxVanillaRows);
            SlotLayout.VanillaRows = rows;
            __instance.m_inventory.SetHeight(SlotLayout.TotalRows);
            __instance.AddUniqueKeyValue(Player.InventoryRowsKey, rows.ToString());
            if (InventoryGui.instance != null)
                InventoryGui.instance.SetInventorySize(SlotLayout.MainRows);
            SlotLayout.OnLayoutChanged();

            if (__instance == Player.m_localPlayer)
                Migration.Sanitize(__instance);
            __instance.DropInvalidItems();
            return false;
        }

        // --- Where new items may go ----------------------------------------------------------

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindEmptySlot))]
        [HarmonyPrefix]
        private static bool Inventory_FindEmptySlot(Inventory __instance, bool topFirst, ref Vector2i __result)
        {
            if (!Inv.IsPlayer(__instance))
                return true;
            __result = Inv.FindFreeMainSlot(__instance, topFirst);
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        [HarmonyPrefix]
        private static bool Inventory_HaveEmptySlot(Inventory __instance, ref bool __result)
        {
            if (!Inv.IsPlayer(__instance))
                return true;
            __result = Inv.FindFreeMainSlot(__instance, true).x >= 0;
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        [HarmonyPrefix]
        private static bool Inventory_GetEmptySlots(Inventory __instance, ref int __result)
        {
            if (!Inv.IsPlayer(__instance))
                return true;
            __result = Inv.CountFreeMainSlots(__instance);
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
        [HarmonyPrefix]
        private static bool Inventory_CanAddItem(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
        {
            if (!Inv.IsPlayer(__instance))
                return true;
            if (stack <= 0)
                stack = item.m_stack;
            __result = __instance.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel)
                       + Inv.CountFreeMainSlots(__instance) * item.m_shared.m_maxStackSize >= stack;
            return false;
        }

        /// <summary>Positional add (drag/drop, take-all, MoveItemToThis). Reject positions the item may not occupy.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
            typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool))]
        [HarmonyPrefix]
        private static bool Inventory_AddItemAt(Inventory __instance, ItemDrop.ItemData item, int x, int y, bool skipValidPositionCheck, ref bool __result)
        {
            // Loading a save uses skipValidPositionCheck: never reject there or items would be lost.
            if (skipValidPositionCheck || !Inv.IsPlayer(__instance))
                return true;
            if (SlotLayout.IsValidPlacement(item, new Vector2i(x, y)))
                return true;
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData), typeof(Vector2i))]
        [HarmonyPrefix]
        private static bool Inventory_AddItemAtPos(Inventory __instance, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
        {
            if (!Inv.IsPlayer(__instance) || SlotLayout.IsValidPlacement(item, pos))
                return true;
            __result = __instance.AddItem(item);
            return false;
        }

        // --- Keep special and locked slots out of "Stack all" -------------------------------

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        [HarmonyPrefix]
        private static void Inventory_StackAll_Prefix(Inventory fromInventory, out List<ItemDrop.ItemData> __state)
        {
            __state = null;
            if (!Inv.IsPlayer(fromInventory))
                return;
            foreach (var item in fromInventory.m_inventory)
            {
                if (SlotLayout.IsReserved(item.m_gridPos) || LockedSlots.IsLocked(item.m_gridPos))
                    (__state ??= new List<ItemDrop.ItemData>()).Add(item);
            }
            if (__state != null)
                fromInventory.m_inventory.RemoveAll(__state.Contains);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
        [HarmonyFinalizer]
        private static void Inventory_StackAll_Finalizer(Inventory fromInventory, List<ItemDrop.ItemData> __state)
        {
            if (__state == null)
                return;
            fromInventory.m_inventory.AddRange(__state);
            Inv.Changed(fromInventory);
        }

        // --- Detect a refill slot running empty (eating / shooting) --------------------------

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
        [HarmonyPrefix]
        private static void Inventory_RemoveOneItem(Inventory __instance, ItemDrop.ItemData item)
        {
            if (item != null && item.m_stack <= 1 && Inv.IsPlayer(__instance))
                AutoRefill.NotifyEmptying(item);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(ItemDrop.ItemData), typeof(int))]
        [HarmonyPrefix]
        private static void Inventory_RemoveItemAmount(Inventory __instance, ItemDrop.ItemData item, int amount)
        {
            if (item != null && amount >= item.m_stack && Inv.IsPlayer(__instance))
                AutoRefill.NotifyEmptying(item);
        }
    }
}
