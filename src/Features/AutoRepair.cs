using System.Collections.Generic;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>
    /// Repairs everything a nearby crafting station can repair as soon as you walk up to it
    /// (same rules as the vanilla repair button: right station type, high enough level, station usable).
    /// </summary>
    internal static class AutoRepair
    {
        private static float _next;
        private static readonly List<ItemDrop.ItemData> Worn = new List<ItemDrop.ItemData>();

        internal static void Tick(Player player)
        {
            if (!Plugin.AutoRepair.Value || Time.time < _next)
                return;
            _next = Time.time + 1f;
            if (player.IsDead() || player.IsTeleporting() || player.InCutscene())
                return;

            Worn.Clear();
            player.m_inventory.GetWornItems(Worn);
            if (Worn.Count == 0)
                return;

            var station = NearestStation(player.transform.position, Plugin.AutoRepairDistance.Value);
            if (station == null || !station.CheckUsable(player, showMessage: false))
                return;

            int repaired = 0;
            foreach (var item in Worn)
            {
                if (!CanRepair(item, station))
                    continue;
                player.RaiseSkill(Skills.SkillType.Crafting, 1f - item.m_durability / item.GetMaxDurability());
                item.m_durability = item.GetMaxDurability();
                repaired++;
            }
            if (repaired == 0)
                return;

            station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity);
            string where = Localization.instance.Localize(station.m_name);
            player.Message(MessageHud.MessageType.Center,
                repaired == 1 ? $"Reparerade 1 föremål vid {where}" : $"Reparerade {repaired} föremål vid {where}");
        }

        private static CraftingStation NearestStation(Vector3 pos, float maxDistance)
        {
            CraftingStation best = null;
            float bestDist = maxDistance;
            foreach (var station in CraftingStation.m_allStations)
            {
                if (station == null || !station.m_canRepair)
                    continue;
                float d = Vector3.Distance(pos, station.transform.position);
                if (d <= bestDist)
                {
                    best = station;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>Mirror of InventoryGui.CanRepair, but for a given station instead of the one the player is using.</summary>
        private static bool CanRepair(ItemDrop.ItemData item, CraftingStation station)
        {
            if (!item.m_shared.m_canBeReparied)
                return false;
            var recipe = ObjectDB.instance.GetRecipe(item);
            if (recipe == null || (recipe.m_craftingStation == null && recipe.m_repairStation == null))
                return false;
            bool rightStation = (recipe.m_repairStation != null && recipe.m_repairStation.m_name == station.m_name)
                                || (recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == station.m_name)
                                || item.m_worldLevel < Game.m_worldLevel;
            if (!rightStation)
                return false;
            return Mathf.Min(station.GetLevel(), 4) >= recipe.m_minStationLevel;
        }
    }
}
