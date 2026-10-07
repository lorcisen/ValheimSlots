using System.Collections.Generic;

namespace ValheimSlots
{
    /// <summary>
    /// Keeps food, mead and ammo slots stocked from the main inventory, and optionally eats automatically.
    /// A slot is only refilled after it was emptied by eating/drinking/shooting – never after the player
    /// dragged the item out themselves.
    /// </summary>
    internal static class AutoRefill
    {
        private struct Pending
        {
            public SlotDef Slot;
            public string Name;
            public int Quality;
        }

        private static readonly List<Pending> PendingRefills = new List<Pending>();

        /// <summary>Called right before the last item of a stack is removed from the player inventory.</summary>
        internal static void NotifyEmptying(ItemDrop.ItemData item)
        {
            if (!Plugin.AutoRefill.Value || AutoEquipMove.SuppressDepth > 0)
                return;
            var def = SlotLayout.GetSlot(item.m_gridPos);
            if (def == null || !def.IsRefillable)
                return;
            PendingRefills.Add(new Pending { Slot = def, Name = item.m_shared.m_name, Quality = item.m_quality });
        }

        internal static void Tick(Player player)
        {
            var inv = player.m_inventory;

            if (PendingRefills.Count > 0)
            {
                foreach (var p in PendingRefills)
                {
                    var pos = p.Slot.GridPos;
                    if (inv.GetItemAt(pos.x, pos.y) != null)
                        continue;
                    var source = FindInMain(inv, p.Name, p.Quality);
                    if (source != null)
                    {
                        source.m_gridPos = pos;
                        Inv.Changed(inv);
                    }
                }
                PendingRefills.Clear();
            }

            if (Plugin.TopUpFromInventory.Value && !InventoryGui.IsVisible())
                TopUp(inv);
        }

        private static void TopUp(Inventory inv)
        {
            bool changed = false;
            foreach (var def in SlotLayout.Slots)
            {
                if (!def.IsRefillable)
                    continue;
                var slotItem = inv.GetItemAt(def.GridPos.x, def.GridPos.y);
                if (slotItem == null || slotItem.m_stack >= slotItem.m_shared.m_maxStackSize)
                    continue;
                var source = FindInMain(inv, slotItem.m_shared.m_name, slotItem.m_quality, slotItem.m_worldLevel);
                if (source == null)
                    continue;
                int amount = System.Math.Min(slotItem.m_shared.m_maxStackSize - slotItem.m_stack, source.m_stack);
                slotItem.m_stack += amount;
                source.m_stack -= amount;
                if (source.m_stack <= 0)
                    inv.m_inventory.Remove(source);
                changed = true;
            }
            if (changed)
                Inv.Changed(inv);
        }

        private static ItemDrop.ItemData FindInMain(Inventory inv, string name, int quality, int worldLevel = -1)
        {
            ItemDrop.ItemData best = null;
            foreach (var item in inv.m_inventory)
            {
                if (item.m_shared.m_name != name || item.m_quality != quality)
                    continue;
                if (worldLevel >= 0 && item.m_worldLevel != worldLevel)
                    continue;
                if (!SlotLayout.IsMain(item.m_gridPos) || LockedSlots.IsLocked(item.m_gridPos) || item.m_equipped)
                    continue;
                if (item.m_gridPos.y == 0)
                    continue; // leave the hotbar alone
                // Take the smallest stack first so the main inventory gets tidier.
                if (best == null || item.m_stack < best.m_stack)
                    best = item;
            }
            if (best != null)
                return best;
            // Fall back to the hotbar if nothing else is available.
            foreach (var item in inv.m_inventory)
                if (item.m_shared.m_name == name && item.m_quality == quality && item.m_gridPos.y == 0
                    && !LockedSlots.IsLocked(item.m_gridPos) && (worldLevel < 0 || item.m_worldLevel == worldLevel))
                    return item;
            return null;
        }

        // --- Auto eat ----------------------------------------------------------------------

        internal static void AutoEat(Player player)
        {
            if (!Plugin.AutoEat.Value || player.IsDead() || player.InAttack() || player.IsTeleporting()
                || InventoryGui.IsVisible() || player.InCutscene())
                return;

            var inv = player.m_inventory;
            var foods = player.GetFoods();
            float threshold = Plugin.AutoEatSecondsLeft.Value;

            foreach (var def in SlotLayout.Slots)
            {
                if (def.Kind != SlotKind.Food)
                    continue;
                var item = inv.GetItemAt(def.GridPos.x, def.GridPos.y);
                if (item == null || item.m_shared.m_food <= 0f && item.m_shared.m_foodStamina <= 0f && item.m_shared.m_foodEitr <= 0f)
                    continue;

                Player.Food active = null;
                foreach (var f in foods)
                    if (f.m_item.m_shared.m_name == item.m_shared.m_name)
                        active = f;

                bool wantEat = active == null
                    ? foods.Count < 3
                    : active.m_time <= threshold && active.CanEatAgain();
                if (!wantEat || !player.CanEat(item, showMessages: false))
                    continue;

                player.UseItem(inv, item, fromInventoryGui: true);
                return; // one bite per tick
            }
        }
    }
}
