using HarmonyLib;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>Adds the color-blind friendly check mark to equipped items on the vanilla 1-8 hotbar.</summary>
    [HarmonyPatch]
    internal static class HotkeyBarPatches
    {
        [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void HotkeyBar_UpdateIcons(HotkeyBar __instance)
        {
            foreach (var element in __instance.m_elements)
            {
                if (element?.m_go == null)
                    continue;
                bool equipped = element.m_used && element.m_equiped != null && element.m_equiped.activeSelf;
                EquippedMarker.Set(element.m_go.transform, equipped, 22f, new Vector2(2f, 6f));
            }
        }
    }
}
