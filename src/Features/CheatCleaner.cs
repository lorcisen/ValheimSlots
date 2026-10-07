using System.Collections.Generic;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>
    /// Removes Valheim's "summoned through cheating means" flag.
    ///
    /// Since 1.0 the flag spreads: a flagged tool flags what it hits (trees, rocks, creatures) and their drops,
    /// flagged materials flag what is crafted from them, and a flagged workbench flags everything crafted at it.
    /// In a modded game achievements are already disabled (Game.isModded), so the flag has no gameplay meaning
    /// here – it only spreads and clutters tooltips.
    ///
    ///  - Auto (config): clears flagged items in the player inventory.
    ///  - Hotkey: also clears nearby buildings/stations (and what is queued in cooking stations/fermenters)
    ///    and the items in nearby chests.
    /// </summary>
    internal static class CheatCleaner
    {
        private const float Radius = 30f;
        private const int MaxCookingSlots = 10;

        private static float _nextAuto;
        private static readonly List<Piece> Pieces = new List<Piece>();

        internal static void Tick(Player player)
        {
            if (!Plugin.AutoClearCheated.Value || Time.time < _nextAuto)
                return;
            _nextAuto = Time.time + 1f;
            ClearInventory(player.m_inventory);
            ClearProfile();
        }

        /// <summary>
        /// The character-level "has used cheats" flag (set when a cheat console command is run).
        /// The game writes it to the character file on its next save (logout / autosave).
        /// </summary>
        private static bool ClearProfile()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null || !profile.m_usedCheats)
                return false;
            profile.m_usedCheats = false;
            Plugin.Log.LogInfo("CheatCleaner: removed the character's used-cheats flag.");
            return true;
        }

        internal static void ClearAround(Player player)
        {
            int items = ClearInventory(player.m_inventory);
            bool profile = ClearProfile();
            int pieces = 0, chestItems = 0;

            Pieces.Clear();
            Piece.GetAllPiecesInRadius(player.transform.position, Radius, Pieces);
            foreach (var piece in Pieces)
            {
                var nview = piece.m_nview;
                if (nview == null || !nview.IsValid())
                    continue;
                var zdo = nview.GetZDO();

                bool flagged = zdo.GetBool(ZDOVars.s_cheated) || zdo.GetBool(ZDOVars.s_cheatedQueued);
                for (int i = 0; i < MaxCookingSlots && !flagged; i++)
                    flagged = zdo.GetBool(ZDOVars.s_cheatedQueued + i);

                var container = piece.GetComponent<Container>();
                bool chestFlagged = container != null && !container.IsInUse() && HasCheated(container.GetInventory());

                if (!flagged && !chestFlagged)
                    continue;

                if (!nview.IsOwner())
                    nview.ClaimOwnership();

                if (flagged)
                {
                    if (zdo.GetBool(ZDOVars.s_cheated)) zdo.Set(ZDOVars.s_cheated, false);
                    if (zdo.GetBool(ZDOVars.s_cheatedQueued)) zdo.Set(ZDOVars.s_cheatedQueued, false);
                    for (int i = 0; i < MaxCookingSlots; i++)
                        if (zdo.GetBool(ZDOVars.s_cheatedQueued + i))
                            zdo.Set(ZDOVars.s_cheatedQueued + i, false);
                    pieces++;
                }

                if (chestFlagged)
                {
                    // Changed() -> Container.OnContainerChanged() saves the inventory now that we own the chest.
                    chestItems += ClearInventory(container.GetInventory());
                }
            }

            player.Message(MessageHud.MessageType.Center,
                L.T($"Cheat flag removed: {items} items in inventory, {pieces} buildings, {chestItems} items in chests",
                    $"Fuskmarkering borttagen: {items} föremål i inventoryt, {pieces} byggnader, {chestItems} föremål i kistor")
                + (profile ? L.T(", character", ", karaktären") : ""));
            Plugin.Log.LogInfo($"CheatCleaner: inventory {items}, pieces {pieces}, chest items {chestItems} (radie {Radius} m).");
        }

        private static bool HasCheated(Inventory inv)
        {
            foreach (var item in inv.m_inventory)
                if (item.m_cheated)
                    return true;
            return false;
        }

        private static int ClearInventory(Inventory inv)
        {
            int n = 0;
            foreach (var item in inv.m_inventory)
            {
                if (!item.m_cheated)
                    continue;
                item.m_cheated = false;
                n++;
            }
            if (n > 0)
            {
                inv.m_cheatedPopup = false;
                Inv.Changed(inv);
            }
            return n;
        }
    }
}
