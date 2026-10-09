using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// A toggle in the pause menu (Esc), just above Logout:
    ///   "Achievements: OFF"  /  "Achievements: ON (restart)"
    /// Cloned from the Settings button so it looks native (the same way Mod Configs adds its button).
    /// </summary>
    [HarmonyPatch]
    internal static class AchievementsMenuButton
    {
        private const string ObjectName = "ValheimSlotsAchievements";
        private static Button _button;

        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        [HarmonyPostfix]
        private static void Menu_Start() => Ensure();

        [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
        [HarmonyPostfix]
        private static void Menu_Show()
        {
            Ensure();
            UpdateLabel();
        }

        private static void Ensure()
        {
            if (_button != null)
                return;
            var menu = Menu.instance;
            if (menu == null || menu.m_settingsButton == null || menu.m_logoutButton == null)
                return;

            var parent = menu.m_logoutButton.transform.parent;
            var existing = parent.Find(ObjectName);
            var go = existing != null
                ? existing.gameObject
                : Object.Instantiate(menu.m_settingsButton.gameObject, parent, false);
            go.name = ObjectName;
            go.transform.SetSiblingIndex(menu.m_logoutButton.transform.GetSiblingIndex());

            _button = go.GetComponent<Button>();
            if (_button == null)
                return;
            // Drop the cloned "open settings" behaviour.
            _button.onClick = new Button.ButtonClickedEvent();
            foreach (var trigger in go.GetComponentsInChildren<EventTrigger>(true))
                trigger.triggers?.RemoveAll(e => e != null && (e.eventID == EventTriggerType.PointerClick || e.eventID == EventTriggerType.Submit));
            _button.onClick.AddListener(Toggle);

            var nav = _button.navigation;
            nav.mode = Navigation.Mode.Automatic;
            _button.navigation = nav;
            go.SetActive(true);
            UpdateLabel();
        }

        private static void Toggle()
        {
            Plugin.AchievementsWhenModded.Value = !Plugin.AchievementsWhenModded.Value; // SettingChanged shows the restart message
            UpdateLabel();
        }

        internal static void UpdateLabel()
        {
            if (_button == null)
                return;
            bool on = Plugin.AchievementsWhenModded.Value;
            // Short, like the other menu entries; the restart notice is shown in the middle of the screen.
            string text = L.T($"Achievements: {(on ? "ON" : "OFF")}", $"Prestationer: {(on ? "PÅ" : "AV")}");
            if (on != ModdedAchievements.ActiveThisSession)
                text += L.T(" (restart)", " (starta om)");

            var tmp = _button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                // Same size as the Settings entry next to it.
                var reference = Menu.instance != null && Menu.instance.m_settingsButton != null
                    ? Menu.instance.m_settingsButton.GetComponentInChildren<TMP_Text>(true)
                    : null;
                tmp.enableAutoSizing = reference != null && reference.enableAutoSizing;
                if (reference != null)
                {
                    tmp.fontSize = reference.fontSize;
                    tmp.fontSizeMin = reference.fontSizeMin;
                    tmp.fontSizeMax = reference.fontSizeMax;
                }
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
            }
            var legacy = _button.GetComponentInChildren<Text>(true);
            if (legacy != null)
                legacy.text = text;
        }
    }
}
