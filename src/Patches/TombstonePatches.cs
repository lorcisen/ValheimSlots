using HarmonyLib;

namespace ValheimSlots
{
    /// <summary>
    /// Vanilla already handles big graves (MoveInventoryToGrave copies the size, Container.UpdateRows grows it),
    /// and "take all" puts items back on their original positions. Only the quick "does it fit" check
    /// needs to know that grave items can go straight back into their special slots.
    /// </summary>
    [HarmonyPatch]
    internal static class TombstonePatches
    {
        [HarmonyPatch(typeof(TombStone), nameof(TombStone.EasyFitInInventory))]
        [HarmonyPostfix]
        private static void TombStone_EasyFitInInventory(TombStone __instance, Player player, ref bool __result)
        {
            if (__result || player == null || player != Player.m_localPlayer)
                return;

            var grave = __instance.m_container.GetInventory();
            var inv = player.GetInventory();
            if (inv.GetTotalWeight() + grave.GetTotalWeight() > player.GetMaxCarryWeight())
                return;

            int needMainSlots = 0;
            foreach (var item in grave.GetAllItems())
            {
                var pos = item.m_gridPos;
                bool backToOwnSlot = SlotLayout.IsValidPlacement(item, pos) && inv.GetItemAt(pos.x, pos.y) == null
                                     && !LockedSlots.IsLocked(pos);
                if (!backToOwnSlot && inv.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) < item.m_stack)
                    needMainSlots++;
            }
            __result = needMainSlots <= Inv.CountFreeMainSlots(inv);
        }
    }
}
