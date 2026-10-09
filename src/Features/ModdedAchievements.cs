using HarmonyLib;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>
    /// Optional (off by default): earn achievements in a modded game.
    ///
    /// Valheim blocks achievements in Achievements.IsCheatedAtAll(), which is true if the character has used cheat
    /// commands, the world has cheat modifiers, you carry cheated items – or Game.isModded is set (mods set it).
    /// With the option on, only the "modded" part is ignored; real cheating still blocks achievements.
    /// Game.isModded itself is left untouched, so the main menu still says "modded" and logs stay honest.
    /// </summary>
    [HarmonyPatch]
    internal static class ModdedAchievements
    {
        /// <summary>
        /// The setting as it was when the game started. Changes take effect after a restart,
        /// so achievements can't be toggled on and off in the middle of a session.
        /// </summary>
        internal static bool ActiveThisSession { get; private set; }

        internal static void CaptureAtStartup() => ActiveThisSession = Plugin.AchievementsWhenModded.Value;

        [HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
        [HarmonyPrefix]
        private static bool Achievements_IsCheatedAtAll(ref bool __result)
        {
            if (!ActiveThisSession)
                return true; // vanilla behaviour

            if (Time.frameCount == Achievements.m_cheatCheckFrame)
            {
                __result = Achievements.m_cheatCheckCache;
                return false;
            }
            Achievements.m_cheatCheckFrame = Time.frameCount;
            bool usedCheats = Game.instance != null && Game.instance.GetPlayerProfile().m_usedCheats;
            bool worldCheated = Achievements.IsWorldCheated();
            bool cheatedItems = Player.m_localPlayer != null && Player.m_localPlayer.GetInventory().AnyCheatedItem();
            Achievements.m_cheatCheckCache = usedCheats || worldCheated || cheatedItems; // Game.isModded deliberately ignored
            __result = Achievements.m_cheatCheckCache;
            return false;
        }

        /// <summary>Tell the player once per session what the setting does, so it is never a surprise.</summary>
        internal static void LogState()
        {
            Plugin.Log.LogInfo(ActiveThisSession
                ? "Achievements in modded game: ON (modded state ignored; cheats still block achievements)."
                : "Achievements in modded game: OFF (vanilla behaviour).");
        }

        /// <summary>Big centered notice after the setting has been changed.</summary>
        internal static void OnSettingChanged()
        {
            bool on = Plugin.AchievementsWhenModded.Value;
            Plugin.Log.LogInfo($"Achievements in modded game set to {(on ? "ON" : "OFF")} - takes effect after a restart.");
            AchievementsMenuButton.UpdateLabel();

            string text = on == ActiveThisSession
                ? L.T($"Achievements when modded: {(on ? "ON" : "OFF")}", $"Prestationer i moddat spel: {(on ? "PÅ" : "AV")}")
                : on
                    ? L.T("Achievements when modded: ON\nRestart the game for the setting to take effect",
                          "Prestationer i moddat spel: PÅ\nStarta om spelet för att inställningen ska börja gälla")
                    : L.T("Achievements when modded: OFF\nRestart the game for the setting to take effect",
                          "Prestationer i moddat spel: AV\nStarta om spelet för att inställningen ska börja gälla");
            if (MessageHud.instance != null)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
        }
    }
}
