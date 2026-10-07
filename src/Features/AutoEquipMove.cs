using HarmonyLib;

namespace ValheimSlots
{
    /// <summary>
    /// When the player equips armor or a weapon from the main grid, move it into its dedicated slot.
    /// The item that was in the slot (just unequipped by vanilla) takes the old position, so nothing is lost.
    /// </summary>
    [HarmonyPatch]
    internal static class AutoEquipMove
    {
        /// <summary>Set while the inventory GUI is moving items itself (drag &amp; drop) or while we swap weapon sets.</summary>
        internal static int SuppressDepth;

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Humanoid_EquipItem(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || SuppressDepth > 0 || item == null)
                return;
            var player = __instance as Player;
            if (player == null || player != Player.m_localPlayer)
                return;
            var inv = player.m_inventory;
            if (!inv.ContainsItem(item))
                return;

            var current = SlotLayout.GetSlot(item.m_gridPos);
            if (current != null && SlotLayout.Fits(current.Kind, item))
                return; // already in a fitting slot (e.g. weapon set B)

            SlotDef target = SlotLayout.ArmorSlotFor(item);
            // Weapons used from the 1-8 hotbar stay there, otherwise the hotbar key would stop working.
            if (target == null && Plugin.AutoMoveWeapons.Value && item.m_gridPos.y != 0)
            {
                int set = WeaponSets.ActiveSet;
                if (item == player.m_rightItem && SlotLayout.Fits(SlotKind.WeaponMain, item))
                    target = SlotLayout.GetSlot(SlotKind.WeaponMain, set);
                else if (item == player.m_leftItem && SlotLayout.Fits(SlotKind.WeaponOff, item))
                    target = SlotLayout.GetSlot(SlotKind.WeaponOff, set);
                else if (item == player.m_leftItem && SlotLayout.Fits(SlotKind.WeaponMain, item))
                    target = SlotLayout.GetSlot(SlotKind.WeaponMain, set); // bows sit in the left hand
            }
            if (target == null)
                return;

            MoveInto(inv, item, target.GridPos);
        }

        /// <summary>Move <paramref name="item"/> to <paramref name="target"/>, swapping with any occupant that fits the old position.</summary>
        internal static bool MoveInto(Inventory inv, ItemDrop.ItemData item, Vector2i target)
        {
            if (!SlotLayout.IsValidPlacement(item, target))
                return false;
            var occupant = inv.GetItemAt(target.x, target.y);
            var oldPos = item.m_gridPos;
            if (occupant != null)
            {
                if (occupant == item)
                    return true;
                if (occupant.m_equipped || !SlotLayout.IsValidPlacement(occupant, oldPos))
                    return false;
                occupant.m_gridPos = oldPos;
            }
            item.m_gridPos = target;
            Inv.Changed(inv);
            return true;
        }
    }
}
