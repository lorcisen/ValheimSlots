namespace ValheimSlots
{
    /// <summary>Two weapon sets (main hand + off hand). One key swaps between them.</summary>
    internal static class WeaponSets
    {
        /// <summary>0 = A, 1 = B. Follows whatever set the equipped weapons are in.</summary>
        internal static int ActiveSet;

        internal static string SetName(int set) => set == 0 ? "A" : "B";

        /// <summary>Re-derive the active set from what is equipped (called every tick).</summary>
        internal static void Track(Player player)
        {
            foreach (var hand in new[] { player.m_rightItem, player.m_leftItem })
            {
                if (hand == null) continue;
                var def = SlotLayout.GetSlot(hand.m_gridPos);
                if (def != null && def.IsWeapon)
                {
                    ActiveSet = def.Group;
                    return;
                }
            }
        }

        internal static void Swap(Player player)
        {
            if (player.InAttack() || player.InDodge() || player.IsDead())
                return;

            int target = 1 - ActiveSet;
            var inv = player.m_inventory;
            var main = Get(inv, SlotKind.WeaponMain, target);
            var off = Get(inv, SlotKind.WeaponOff, target);
            if (main == null && off == null)
            {
                player.Message(MessageHud.MessageType.Center, $"Vapenset {SetName(target)} är tomt");
                return;
            }

            AutoEquipMove.SuppressDepth++;
            try
            {
                // Put away whatever is in the hands (including weapons hidden with R).
                if (player.m_rightItem != null) player.UnequipItem(player.m_rightItem, triggerEquipEffects: false);
                if (player.m_leftItem != null) player.UnequipItem(player.m_leftItem, triggerEquipEffects: false);
                if (player.m_hiddenRightItem != null) player.UnequipItem(player.m_hiddenRightItem, triggerEquipEffects: false);
                if (player.m_hiddenLeftItem != null) player.UnequipItem(player.m_hiddenLeftItem, triggerEquipEffects: false);

                if (main != null)
                    player.EquipItem(main);
                if (off != null && (main == null || !SlotLayout.IsTwoHanded(main)))
                    player.EquipItem(off);
            }
            finally
            {
                AutoEquipMove.SuppressDepth--;
            }

            ActiveSet = target;
            player.Message(MessageHud.MessageType.TopLeft, $"Vapenset {SetName(target)}");
        }

        private static ItemDrop.ItemData Get(Inventory inv, SlotKind kind, int set)
        {
            var def = SlotLayout.GetSlot(kind, set);
            return def == null ? null : inv.GetItemAt(def.GridPos.x, def.GridPos.y);
        }
    }
}
