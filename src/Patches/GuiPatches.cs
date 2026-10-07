using HarmonyLib;
using UnityEngine;

namespace ValheimSlots
{
    [HarmonyPatch]
    internal static class GuiPatches
    {
        private static bool IsPlayerGrid(InventoryGrid grid)
            => InventoryGui.instance != null && grid == InventoryGui.instance.m_playerGrid && Player.m_localPlayer != null;

        // --- Layout ---------------------------------------------------------------------------

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void InventoryGrid_UpdateGui(InventoryGrid __instance, Player player)
        {
            if (player == null || !IsPlayerGrid(__instance) || __instance.m_inventory != player.m_inventory)
                return;
            SlotPanel.Layout(__instance, player);
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnDestroy))]
        [HarmonyPostfix]
        private static void InventoryGui_OnDestroy() => SlotPanel.Reset();

        // --- Drag & drop validation -----------------------------------------------------------

        /// <summary>
        /// Vanilla removes the dragged item before swapping, so an invalid swap could lose an item.
        /// Refuse the whole drop up front instead; the item stays on the cursor.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        [HarmonyPrefix]
        private static bool InventoryGui_OnSelectedItem_Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
        {
            AutoEquipMove.SuppressDepth++;
            if (__instance.m_dragGo == null || __instance.m_dragItem == null || Player.m_localPlayer == null)
                return true;

            if (!DropAllowed(grid.GetInventory(), __instance.m_dragInventory, __instance.m_dragItem, item, pos))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Det föremålet passar inte här");
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        [HarmonyFinalizer]
        private static void InventoryGui_OnSelectedItem_Finalizer() => AutoEquipMove.SuppressDepth--;

        /// <summary>Same check for anything else that drops onto the player grid directly.</summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        [HarmonyPrefix]
        private static bool InventoryGrid_DropItem(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
        {
            if (Player.m_localPlayer == null)
                return true;
            var target = __instance.m_inventory.GetItemAt(pos.x, pos.y);
            if (DropAllowed(__instance.m_inventory, fromInventory, item, target, pos))
                return true;
            __result = false;
            return false;
        }

        private static bool DropAllowed(Inventory targetInv, Inventory fromInv, ItemDrop.ItemData dragged, ItemDrop.ItemData target, Vector2i pos)
        {
            // 1. The dragged item must be allowed on the target position.
            if (Inv.IsPlayer(targetInv) && !SlotLayout.IsValidPlacement(dragged, pos))
                return false;

            // 2. If this becomes a swap, the item already there must be allowed where the dragged item came from.
            if (target != null && target != dragged && Inv.IsPlayer(fromInv) && IsSwap(dragged, target))
            {
                if (!SlotLayout.IsValidPlacement(target, dragged.m_gridPos))
                    return false;
            }
            return true;
        }

        /// <summary>Mirrors the swap condition in InventoryGrid.DropItem.</summary>
        private static bool IsSwap(ItemDrop.ItemData dragged, ItemDrop.ItemData target)
            => target.m_shared.m_name != dragged.m_shared.m_name
               || (dragged.m_shared.m_maxQuality > 1 && target.m_quality != dragged.m_quality)
               || target.m_shared.m_maxStackSize == 1;

        // --- Dropping items on the ground must not trigger auto refill ------------------------

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
        [HarmonyPrefix]
        private static void Humanoid_DropItem_Prefix() => AutoEquipMove.SuppressDepth++;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
        [HarmonyFinalizer]
        private static void Humanoid_DropItem_Finalizer() => AutoEquipMove.SuppressDepth--;

        // --- Alt + click locks a main slot ----------------------------------------------------

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftDown))]
        [HarmonyPrefix]
        private static bool InventoryGrid_OnLeftDown(InventoryGrid __instance, UIInputHandler clickHandler)
        {
            // With Quick Stack - Store - Sort installed, its own Alt+click favoriting is the lock.
            if (QuickStackStoreCompat.Active || !IsPlayerGrid(__instance) || InventoryGui.instance.m_dragGo != null)
                return true;
            var key = Plugin.LockModifier.Value;
            if (key == KeyCode.None || !ZInput.GetKey(key, logWarning: false))
                return true;

            var pos = __instance.GetButtonPos(clickHandler.gameObject);
            if (!SlotLayout.IsMain(pos))
                return true;
            bool locked = LockedSlots.Toggle(pos);
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, locked ? "Plats låst" : "Plats upplåst");
            return false;
        }

        // --- Gamepad: jump over the parked rows ------------------------------------------------

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
        [HarmonyPrefix]
        private static void InventoryGrid_UpdateGamepad_Prefix(InventoryGrid __instance, out Vector2i __state)
            => __state = __instance.m_selected;

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
        [HarmonyPostfix]
        private static void InventoryGrid_UpdateGamepad_Postfix(InventoryGrid __instance, Vector2i __state)
        {
            if (!IsPlayerGrid(__instance) || __instance.m_selected == __state)
                return;
            var sel = __instance.m_selected;
            if (SlotLayout.IsMain(sel) || SlotLayout.IsSpecial(sel))
                return;

            bool movingDown = sel.y > __state.y;
            if (sel.y >= SlotLayout.MainRows && sel.y < SlotLayout.SpecialStartRow)
                sel.y = movingDown ? SlotLayout.SpecialStartRow : SlotLayout.MainRows - 1;
            // Snap sideways onto the nearest real slot in a special row.
            if (sel.y >= SlotLayout.SpecialStartRow && !SlotLayout.IsSpecial(sel))
            {
                for (int x = sel.x; x >= 0; x--)
                    if (SlotLayout.IsSpecial(new Vector2i(x, sel.y))) { sel.x = x; break; }
            }
            __instance.m_selected = sel;
            var element = __instance.GetGamepadSelectedElement();
            if (element != null)
                element.GetComponent<UnityEngine.UI.Selectable>()?.Select();
        }
    }
}
