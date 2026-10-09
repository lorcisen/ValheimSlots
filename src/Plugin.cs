using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ValheimSlots
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInIncompatibility("randyknapp.mods.equipmentandquickslots")]
    [BepInDependency(QuickStackStoreCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Author = "Lorcisen";
        public const string Guid = "lorcisen.valheimslots";
        public const string Name = "ValheimSlots";
        private const string OldGuid = "pelle.valheimslots";
        public const string Version = "1.0.8";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // Inventory
        internal static ConfigEntry<int> MainRows;

        // Panel
        internal static ConfigEntry<Vector2> PanelPosition;
        internal static ConfigEntry<bool> ShowEquippedMarker;

        // Hotkeys
        internal static ConfigEntry<KeyboardShortcut>[] FoodKeys;
        internal static ConfigEntry<KeyboardShortcut>[] MeadKeys;
        internal static ConfigEntry<KeyboardShortcut>[] AmmoKeys;
        internal static ConfigEntry<KeyboardShortcut> WeaponSetKey;
        internal static ConfigEntry<KeyCode> LockModifier;
        internal static ConfigEntry<bool> AutoMoveWeapons;

        // Auto refill / auto eat
        internal static ConfigEntry<bool> AutoRefill;
        internal static ConfigEntry<bool> TopUpFromInventory;
        internal static ConfigEntry<bool> AutoEat;
        internal static ConfigEntry<int> AutoEatSecondsLeft;

        // Cheat flag / achievements
        internal static ConfigEntry<bool> AchievementsWhenModded;
        internal static ConfigEntry<bool> AutoClearCheated;
        internal static ConfigEntry<KeyboardShortcut> ClearCheatedKey;

        // Convenience
        internal static ConfigEntry<bool> AutoRepair;
        internal static ConfigEntry<float> AutoRepairDistance;
        internal static ConfigEntry<bool> CraftSearch;
        internal static ConfigEntry<bool> ShowGraveArrow;
        internal static ConfigEntry<bool> ShowPortalWarning;

        // Pinned recipes
        internal static ConfigEntry<bool> PinnedEnabled;
        internal static ConfigEntry<Vector2> PinnedPosition;
        internal static ConfigEntry<int> PinnedMaxShown;
        internal static ConfigEntry<KeyboardShortcut> ClearPinsKey;
        internal static ConfigEntry<KeyboardShortcut> PinPieceKey;

        // HUD
        internal static ConfigEntry<bool> ShowHud;
        internal static ConfigEntry<Vector2> HudPosition;
        internal static ConfigEntry<bool> ShowWeight;
        internal static ConfigEntry<int> DurabilityWarnPercent;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Log.LogInfo("Dedicated server detected - ValheimSlots is a client-side mod and does nothing here.");
                return;
            }

            MigrateOldConfigFile();
            BindConfig();
            ModdedAchievements.CaptureAtStartup();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            QuickStackStoreCompat.TryPatch(_harmony);

            gameObject.AddComponent<SlotController>();
            ModdedAchievements.LogState();
            Log.LogInfo($"{Name} {Version} loaded. Main inventory: {MainRows.Value} rows, special slots on rows {SlotLayout.SpecialStartRow}-{SlotLayout.TotalRows - 1}.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        /// <summary>Keep the settings from before the plugin id changed to lorcisen.valheimslots.</summary>
        private void MigrateOldConfigFile()
        {
            try
            {
                string oldPath = System.IO.Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
                if (!System.IO.File.Exists(oldPath) || System.IO.File.Exists(Config.ConfigFilePath))
                    return;
                System.IO.File.Move(oldPath, Config.ConfigFilePath);
                Config.Reload();
                Log.LogInfo($"Moved settings from {OldGuid}.cfg to {Guid}.cfg.");
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"Could not move old settings: {e.Message}");
            }
        }

        /// <summary>
        /// Descriptions are written in the language the game is set to when it starts (Swedish or English).
        /// Section and key names stay English so existing config files keep working.
        /// </summary>
        private void BindConfig()
        {
            MainRows = Config.Bind("1 - Inventory", "Main rows", 6,
                new ConfigDescription(L.T(
                        "Number of rows in the main inventory (vanilla is 4). If you bought more rows in game, the higher value is used.",
                        "Antal rader i huvudinventoryt (vanilla är 4). Om du köpt fler rader i spelet används det högre värdet."),
                    new AcceptableValueRange<int>(4, SlotLayout.MaxVanillaRows)));

            PanelPosition = Config.Bind("2 - Panel", "Panel position", new Vector2(740f, 28f), L.T(
                "Position of the special slot panel relative to the inventory's top-left corner. Increase X if the panel covers other buttons.",
                "Specialpanelens position relativt inventoryts övre vänstra hörn. Öka X om panelen täcker andra knappar."));
            ShowEquippedMarker = Config.Bind("2 - Panel", "Show equipped check mark", true, L.T(
                "Show a white check mark on equipped items (easier to see than the color highlight, e.g. for color blindness).",
                "Visa en vit bock på utrustade föremål (lättare att se än färgmarkeringen, t.ex. vid färgblindhet)."));

            // Defaults avoid vanilla keys (V auto pickup, G radial, T emote, X sit, C walk, Q autorun, E, R, F, M)
            // and keys used by common mods (EpicLoot G/H/J, QuickStackStore P/O/L, BetterUI J, AzuHoverStats H).
            FoodKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Food 1", new KeyboardShortcut(KeyCode.Z), L.T("Eat the food in food slot 1.", "Ät maten i matplats 1.")),
                Config.Bind("3 - Hotkeys", "Food 2", new KeyboardShortcut(KeyCode.B), L.T("Eat the food in food slot 2.", "Ät maten i matplats 2.")),
                Config.Bind("3 - Hotkeys", "Food 3", new KeyboardShortcut(KeyCode.N), L.T("Eat the food in food slot 3.", "Ät maten i matplats 3.")),
            };
            MeadKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Mead 1", new KeyboardShortcut(KeyCode.Alpha9), L.T("Drink the mead/potion in slot 1.", "Drick mjöd/dryck i plats 1.")),
                Config.Bind("3 - Hotkeys", "Mead 2", new KeyboardShortcut(KeyCode.Alpha0), L.T("Drink the mead/potion in slot 2.", "Drick mjöd/dryck i plats 2.")),
                Config.Bind("3 - Hotkeys", "Mead 3", new KeyboardShortcut(KeyCode.K), L.T("Drink the mead/potion in slot 3.", "Drick mjöd/dryck i plats 3.")),
            };
            AmmoKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Ammo 1", new KeyboardShortcut(KeyCode.U), L.T("Select the ammo in ammo slot 1.", "Välj ammo i ammoplats 1.")),
                Config.Bind("3 - Hotkeys", "Ammo 2", new KeyboardShortcut(KeyCode.I), L.T("Select the ammo in ammo slot 2.", "Välj ammo i ammoplats 2.")),
                Config.Bind("3 - Hotkeys", "Ammo 3", new KeyboardShortcut(KeyCode.Y), L.T("Select the ammo in ammo slot 3.", "Välj ammo i ammoplats 3.")),
            };
            WeaponSetKey = Config.Bind("3 - Hotkeys", "Swap weapon set", new KeyboardShortcut(KeyCode.CapsLock), L.T(
                "Swap between weapon set A and B.",
                "Byt mellan vapenset A och B."));
            LockModifier = Config.Bind("3 - Hotkeys", "Lock slot modifier", KeyCode.LeftAlt, L.T(
                "Hold this key and left-click a slot to lock it. If Quick Stack - Store - Sort is installed, its favorite marking (also Alt+click) is used as the lock instead.",
                "Håll in denna tangent och vänsterklicka på en plats för att låsa den. Om Quick Stack - Store - Sort är installerat används dess favoritmarkering (också Alt+klick) som lås i stället."));
            AutoMoveWeapons = Config.Bind("3 - Hotkeys", "Auto move weapons to set", true, L.T(
                "Move weapons into the active weapon set when you equip them from the main inventory (not from the 1-8 hotbar).",
                "Flytta vapen till aktivt vapenset när du utrustar dem från huvudinventoryt (inte från hotbaren 1-8)."));

            AutoRefill = Config.Bind("4 - Auto", "Auto refill", true, L.T(
                "Refill food, mead and ammo slots from the inventory when a stack runs out.",
                "Fyll på mat-, mjöd- och ammoplatser från inventoryt när en stack tar slut."));
            TopUpFromInventory = Config.Bind("4 - Auto", "Top up stacks", true, L.T(
                "Top up partial stacks in food, mead and ammo slots from the inventory (while the inventory is closed).",
                "Fyll på ofullständiga stackar i mat-, mjöd- och ammoplatser från inventoryt (när inventoryt är stängt)."));
            AutoEat = Config.Bind("4 - Auto", "Auto eat", false, L.T(
                "Eat automatically from the food slots when a food buff is running out or a food slot in your stomach is free.",
                "Ät automatiskt från matplatserna när en matbuff håller på att ta slut eller en matplats i magen är ledig."));
            AutoEatSecondsLeft = Config.Bind("4 - Auto", "Auto eat seconds left", 60,
                new ConfigDescription(L.T(
                        "Eat again when this many seconds of a food buff remain.",
                        "Ät igen när så här många sekunder återstår av en matbuff."),
                    new AcceptableValueRange<int>(5, 1200)));

            AchievementsWhenModded = Config.Bind("6 - Cheat flag", "Achievements when modded", false, L.T(
                "Allow achievements even though the game is modded. Only the modded state is ignored - cheat commands, cheat world modifiers and cheated items still block achievements. Off = normal Valheim behaviour. Can also be toggled in the pause menu (Esc). Takes effect after restarting the game.",
                "Tillåt prestationer trots att spelet är moddat. Bara moddningen ignoreras - fuskkommandon, fusk-världsinställningar och fuskmarkerade föremål spärrar fortfarande. Av = vanligt Valheim-beteende. Kan också slås på/av i pausmenyn (Esc). Gäller efter omstart av spelet."));
            // Takes effect after a restart; tell the player in the middle of the screen.
            AchievementsWhenModded.SettingChanged += (_, __) => ModdedAchievements.OnSettingChanged();

            AutoClearCheated = Config.Bind("6 - Cheat flag", "Auto clear inventory", true, L.T(
                "Automatically remove \"summoned through cheating means\" from items in your inventory. Achievements are already disabled in a modded game, so the flag has no function - it only spreads.",
                "Ta automatiskt bort \"summoned through cheating means\" från föremål i ditt inventory. I ett moddat spel är prestationer redan avstängda, så markeringen har ingen funktion - den sprider sig bara."));
            ClearCheatedKey = Config.Bind("6 - Cheat flag", "Clear nearby key", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl), L.T(
                "Remove the flag from your inventory, your character, and buildings/workbenches and chests within 30 m.",
                "Ta bort markeringen från inventoryt, karaktären samt byggnader/arbetsbänkar och kistor inom 30 m."));

            AutoRepair = Config.Bind("7 - Convenience", "Auto repair", true, L.T(
                "Automatically repair everything a nearby workbench, forge etc. can repair (same rules as the repair button).",
                "Reparera automatiskt allt som en närliggande arbetsbänk, smedja osv. kan reparera (samma regler som reparationsknappen)."));
            AutoRepairDistance = Config.Bind("7 - Convenience", "Auto repair distance", 5f,
                new ConfigDescription(L.T(
                        "How close to the station you need to be (meters).",
                        "Hur nära stationen du måste vara (meter)."),
                    new AcceptableValueRange<float>(1f, 20f)));
            CraftSearch = Config.Bind("7 - Convenience", "Recipe search", true, L.T(
                "Show a search field above the recipe list. Start with ! to search by ingredient (e.g. !iron).",
                "Visa ett sökfält ovanför receptlistan. Börja med ! för att söka på ingrediens (t.ex. !järn)."));
            ShowGraveArrow = Config.Bind("7 - Convenience", "Grave arrow", true, L.T(
                "Show an arrow and the distance to your grave. The grave and map marker are removed once the grave is emptied.",
                "Visa en pil och avståndet till din grav. Grav- och kartmarkeringen tas bort när graven är tömd."));
            ShowPortalWarning = Config.Bind("7 - Convenience", "Portal warning", true, L.T(
                "Show how many carried items can't go through a portal (ore, metal etc.).",
                "Visa hur många föremål du bär som inte får tas genom en portal (malm, metall m.m.)."));

            PinnedEnabled = Config.Bind("8 - Pinned recipes", "Enabled", true, L.T(
                "Right-click a recipe in the crafting list to pin it. Pinned recipes are shown on the right with the ingredients you need.",
                "Högerklicka på ett recept i tillverkningslistan för att pinna det. Pinnade recept visas till höger med ingredienserna du behöver."));
            PinnedPosition = Config.Bind("8 - Pinned recipes", "Position", new Vector2(-20f, 120f), L.T(
                "Top-right corner of the list, relative to the right edge of the screen (vertical middle). Negative X = in from the edge, positive Y = up.",
                "Listans övre högra hörn, relativt skärmens högra kant (mitten i höjdled). Negativt X = in från kanten, positivt Y = uppåt."));
            PinnedMaxShown = Config.Bind("8 - Pinned recipes", "Max shown", 5,
                new ConfigDescription(L.T(
                        "How many pinned recipes are shown at once.",
                        "Hur många pinnade recept som visas samtidigt."),
                    new AcceptableValueRange<int>(1, 15)));
            ClearPinsKey = Config.Bind("8 - Pinned recipes", "Clear all key", new KeyboardShortcut(KeyCode.F11, KeyCode.LeftControl), L.T(
                "Remove all pinned recipes.",
                "Ta bort alla pinnade recept."));
            PinPieceKey = Config.Bind("8 - Pinned recipes", "Pin build piece key", new KeyboardShortcut(KeyCode.Mouse2), L.T(
                "Press while hovering a piece in the build menu (hammer, cultivator, ...) to pin or unpin it. Default: middle mouse button.",
                "Tryck medan du håller musen över en byggbit i byggmenyn (hammare, kultivator, ...) för att pinna eller ta bort den. Standard: mittenklick (scrollhjulet)."));

            ShowHud = Config.Bind("5 - HUD", "Show quick slot HUD", true, L.T(
                "Show the food, mead and ammo slots on screen.",
                "Visa mat-, mjöd- och ammoplatserna på skärmen."));
            HudPosition = Config.Bind("5 - HUD", "HUD position", new Vector2(0f, 230f), L.T(
                "Position of the HUD relative to the bottom center of the screen.",
                "Position för HUD:en relativt skärmens nederkant (mitten)."));
            ShowWeight = Config.Bind("5 - HUD", "Show weight", true, L.T(
                "Show carry weight (current/max) above the quick slots.",
                "Visa vikt (nu/max) ovanför snabbplatserna."));
            DurabilityWarnPercent = Config.Bind("5 - HUD", "Durability warning percent", 20,
                new ConfigDescription(L.T(
                        "Warn when equipped gear has less durability than this (0 = off).",
                        "Varna när utrustad utrustning har mindre hållbarhet än så här (0 = av)."),
                    new AcceptableValueRange<int>(0, 100)));

            // 1.0.0 defaults overlapped QuickStackStore's buttons and the stamina bar: move untouched values.
            if (PanelPosition.Value == new Vector2(615f, 28f))
                PanelPosition.Value = (Vector2)PanelPosition.DefaultValue;
            if (HudPosition.Value == new Vector2(0f, 150f))
                HudPosition.Value = (Vector2)HudPosition.DefaultValue;

            // Rewrite the file so descriptions follow the current game language.
            Config.Save();

            MainRows.SettingChanged += (_, __) => SlotLayout.OnLayoutChanged();
        }
    }
}
