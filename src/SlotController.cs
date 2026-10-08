using BepInEx.Configuration;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>Per-frame driver: hotkeys, weapon set swap, auto refill / auto eat and the HUD.</summary>
    internal sealed class SlotController : MonoBehaviour
    {
        private float _nextRefill;
        private float _nextEat;

        private void Update()
        {
            var player = Player.m_localPlayer;
            if (player == null)
                return;

            WeaponSets.Track(player);

            if (player.TakeInput())
                HandleHotkeys(player);
            PinnedRecipes.HandleBuildMenuInput(); // build menu is open, so TakeInput is not required

            float now = Time.time;
            if (now >= _nextRefill)
            {
                _nextRefill = now + 0.25f;
                AutoRefill.Tick(player);
            }
            if (now >= _nextEat)
            {
                _nextEat = now + 1f;
                AutoRefill.AutoEat(player);
            }

            CheatCleaner.Tick(player);
            AutoRepair.Tick(player);
            GraveTracker.Tick(player);
            StatusHud.Update(player);
            PinnedPanel.Update(player);
        }

        private static void HandleHotkeys(Player player)
        {
            if (IsDown(Plugin.WeaponSetKey.Value))
                WeaponSets.Swap(player);
            if (IsDown(Plugin.ClearCheatedKey.Value))
                CheatCleaner.ClearAround(player);
            if (IsDown(Plugin.ClearPinsKey.Value))
                PinnedRecipes.ClearAll();

            for (int i = 0; i < 3; i++)
            {
                if (IsDown(Plugin.FoodKeys[i].Value))
                    UseSlot(player, SlotKind.Food, i);
                if (IsDown(Plugin.MeadKeys[i].Value))
                    UseSlot(player, SlotKind.Mead, i);
                if (IsDown(Plugin.AmmoKeys[i].Value))
                    UseSlot(player, SlotKind.Ammo, i);
            }
        }

        private static void UseSlot(Player player, SlotKind kind, int group)
        {
            var def = SlotLayout.GetSlot(kind, group);
            if (def == null)
                return;
            var item = player.m_inventory.GetItemAt(def.GridPos.x, def.GridPos.y);
            if (item == null)
            {
                player.Message(MessageHud.MessageType.TopLeft, L.T("The slot is empty", "Platsen är tom"));
                return;
            }

            if (kind == SlotKind.Ammo)
            {
                // Select this ammo (never toggle it off by pressing twice).
                if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo && !player.IsItemEquiped(item))
                    player.EquipItem(item);
                return;
            }

            player.UseItem(player.m_inventory, item, fromInventoryGui: false);
        }

        /// <summary>Valheim uses the new input system, so read keys through ZInput rather than UnityEngine.Input.</summary>
        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, logWarning: false))
                return false;
            foreach (var mod in shortcut.Modifiers)
                if (!ZInput.GetKey(mod, logWarning: false))
                    return false;
            return true;
        }
    }
}
