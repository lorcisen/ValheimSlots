using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ValheimSlots
{
    /// <summary>
    /// Quick Stack - Store - Sort - Trash - Restock asks CompatibilitySupport whether a player-inventory position
    /// is an "equip or quick slot" before quick stacking, storing or sorting it. We answer yes for our special
    /// rows, the unused rows and locked slots. For restocking (IsEquipSlot) we answer yes only for
    /// armor/weapon slots, so QSS may still top up food, mead and ammo slots from chests.
    /// </summary>
    internal static class QuickStackStoreCompat
    {
        public const string Guid = "goldenrevolver.quick_stack_store";

        /// <summary>True when QSS is installed; its Alt+click "favorite slot" then doubles as our slot lock.</summary>
        public static bool Active { get; private set; }

        private static MethodInfo _getPlayerConfig;
        private static MethodInfo _isSlotFavorited;

        private static object _cachedConfig;
        private static int _cachedFrame = -1;
        private static readonly object[] Args = new object[1];

        public static bool IsFavoritedSlot(Vector2i pos)
        {
            if (_getPlayerConfig == null || _isSlotFavorited == null || Player.m_localPlayer == null)
                return false;
            try
            {
                if (_cachedFrame != UnityEngine.Time.frameCount)
                {
                    _cachedFrame = UnityEngine.Time.frameCount;
                    _cachedConfig = _getPlayerConfig.Invoke(null, new object[] { Player.m_localPlayer.GetPlayerID() });
                }
                if (_cachedConfig == null)
                    return false;
                Args[0] = pos;
                return (bool)_isSlotFavorited.Invoke(_cachedConfig, Args);
            }
            catch
            {
                _isSlotFavorited = null; // API changed – stop asking
                return false;
            }
        }

        public static void TryPatch(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
                return;
            try
            {
                var asm = info.Instance.GetType().Assembly;
                var userConfig = asm.GetType("QuickStackStore.UserConfig");
                if (userConfig != null)
                {
                    _getPlayerConfig = AccessTools.Method(userConfig, "GetPlayerConfig", new[] { typeof(long) });
                    _isSlotFavorited = AccessTools.Method(userConfig, "IsSlotFavorited", new[] { typeof(Vector2i) });
                }
                Active = _getPlayerConfig != null && _isSlotFavorited != null;

                var type = asm.GetType("QuickStackStore.CompatibilitySupport");
                var sig = new[] { typeof(int), typeof(int), typeof(Vector2i) };
                var equipOrQuick = type == null ? null : AccessTools.Method(type, "IsEquipOrQuickSlot", sig);
                var equipOnly = type == null ? null : AccessTools.Method(type, "IsEquipSlot", sig);
                if (equipOrQuick == null || equipOnly == null)
                {
                    Plugin.Log.LogWarning("QuickStackStore found but its API has changed - special slots are not protected from quick stacking.");
                    return;
                }
                harmony.Patch(equipOrQuick, postfix: new HarmonyMethod(typeof(QuickStackStoreCompat), nameof(EquipOrQuickPostfix)));
                harmony.Patch(equipOnly, postfix: new HarmonyMethod(typeof(QuickStackStoreCompat), nameof(EquipOnlyPostfix)));
                Plugin.Log.LogInfo("QuickStackStore compatibility active.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"QuickStackStore compatibility failed: {e}");
            }
        }

        private static bool IsOurPlayerInventory(int height, int width)
        {
            var p = Player.m_localPlayer;
            return p != null && p.m_inventory.GetHeight() == height && p.m_inventory.GetWidth() == width;
        }

        private static void EquipOrQuickPostfix(int inventoryHeight, int inventoryWidth, Vector2i itemPos, ref bool __result)
        {
            if (__result || !IsOurPlayerInventory(inventoryHeight, inventoryWidth))
                return;
            // QSS handles its own favorites; we only add our reserved rows and our own locks.
            __result = SlotLayout.IsReserved(itemPos) || LockedSlots.IsOwnLocked(itemPos);
        }

        private static void EquipOnlyPostfix(int inventoryHeight, int inventoryWidth, Vector2i itemPos, ref bool __result)
        {
            if (__result || !IsOurPlayerInventory(inventoryHeight, inventoryWidth))
                return;
            if (SlotLayout.IsMain(itemPos))
                return;
            var def = SlotLayout.GetSlot(itemPos);
            __result = def == null || !def.IsRefillable;
        }
    }
}
