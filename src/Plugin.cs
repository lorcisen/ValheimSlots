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
        public const string Version = "1.0.4";

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

        // Cheat flag
        internal static ConfigEntry<bool> AutoClearCheated;
        internal static ConfigEntry<KeyboardShortcut> ClearCheatedKey;

        // Convenience
        internal static ConfigEntry<bool> AutoRepair;
        internal static ConfigEntry<float> AutoRepairDistance;
        internal static ConfigEntry<bool> CraftSearch;
        internal static ConfigEntry<bool> ShowGraveArrow;
        internal static ConfigEntry<bool> ShowPortalWarning;

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
                Log.LogInfo("Dedikerad server upptÃ¤ckt â€“ ValheimSlots Ã¤r en klientmod och gÃ¶r ingenting hÃ¤r.");
                return;
            }

            MigrateOldConfigFile();
            BindConfig();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            QuickStackStoreCompat.TryPatch(_harmony);

            gameObject.AddComponent<SlotController>();
            Log.LogInfo($"{Name} {Version} laddad. Huvudinventory: {MainRows.Value} rader, specialplatser pÃ¥ rad {SlotLayout.SpecialStartRow}-{SlotLayout.TotalRows - 1}.");
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
                Log.LogInfo($"Flyttade instÃ¤llningar frÃ¥n {OldGuid}.cfg till {Guid}.cfg.");
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"Kunde inte flytta gamla instÃ¤llningar: {e.Message}");
            }
        }

        private void BindConfig()
        {
            MainRows = Config.Bind("1 - Inventory", "Main rows", 6,
                new ConfigDescription("Antal rader i huvudinventoryt (vanilla Ã¤r 4). Om du kÃ¶pt fler rader i spelet anvÃ¤nds det hÃ¶gre vÃ¤rdet.",
                    new AcceptableValueRange<int>(4, SlotLayout.MaxVanillaRows)));

            PanelPosition = Config.Bind("2 - Panel", "Panel position", new Vector2(740f, 28f),
                "Specialpanelens position relativt inventoryts Ã¶vre vÃ¤nstra hÃ¶rn. Ã–ka X om panelen tÃ¤cker andra knappar.");
            ShowEquippedMarker = Config.Bind("2 - Panel", "Show equipped check mark", true,
                "Visa en vit bock pÃ¥ utrustade fÃ¶remÃ¥l (lÃ¤ttare att se Ã¤n fÃ¤rgmarkeringen, t.ex. vid fÃ¤rgblindhet).");

            // Defaults avoid vanilla keys (V auto pickup, G radial, T emote, X sit, C walk, Q autorun, E, R, F, M)
            // and keys used by common mods (EpicLoot G/H/J, QuickStackStore P/O/L, BetterUI J, AzuHoverStats H).
            FoodKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Food 1", new KeyboardShortcut(KeyCode.Z), "Ã„t maten i matplats 1."),
                Config.Bind("3 - Hotkeys", "Food 2", new KeyboardShortcut(KeyCode.B), "Ã„t maten i matplats 2."),
                Config.Bind("3 - Hotkeys", "Food 3", new KeyboardShortcut(KeyCode.N), "Ã„t maten i matplats 3."),
            };
            MeadKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Mead 1", new KeyboardShortcut(KeyCode.Alpha9), "Drick mjÃ¶d/dryck i plats 1."),
                Config.Bind("3 - Hotkeys", "Mead 2", new KeyboardShortcut(KeyCode.Alpha0), "Drick mjÃ¶d/dryck i plats 2."),
                Config.Bind("3 - Hotkeys", "Mead 3", new KeyboardShortcut(KeyCode.K), "Drick mjÃ¶d/dryck i plats 3."),
            };
            AmmoKeys = new[]
            {
                Config.Bind("3 - Hotkeys", "Ammo 1", new KeyboardShortcut(KeyCode.U), "VÃ¤lj ammo i ammoplats 1."),
                Config.Bind("3 - Hotkeys", "Ammo 2", new KeyboardShortcut(KeyCode.I), "VÃ¤lj ammo i ammoplats 2."),
                Config.Bind("3 - Hotkeys", "Ammo 3", new KeyboardShortcut(KeyCode.Y), "VÃ¤lj ammo i ammoplats 3."),
            };
            WeaponSetKey = Config.Bind("3 - Hotkeys", "Swap weapon set", new KeyboardShortcut(KeyCode.CapsLock),
                "Byt mellan vapenset A och B.");
            LockModifier = Config.Bind("3 - Hotkeys", "Lock slot modifier", KeyCode.LeftAlt,
                "HÃ¥ll in denna tangent och vÃ¤nsterklicka pÃ¥ en plats fÃ¶r att lÃ¥sa den. Om Quick Stack - Store - Sort Ã¤r installerat anvÃ¤nds dess favoritmarkering (ocksÃ¥ Alt+klick) som lÃ¥s i stÃ¤llet.");
            AutoMoveWeapons = Config.Bind("3 - Hotkeys", "Auto move weapons to set", true,
                "Flytta vapen till aktivt vapenset nÃ¤r du utrustar dem frÃ¥n huvudinventoryt (inte frÃ¥n hotbaren 1-8).");

            AutoRefill = Config.Bind("4 - Auto", "Auto refill", true,
                "Fyll pÃ¥ mat-, mjÃ¶d- och ammoplatser frÃ¥n inventoryt nÃ¤r en stack tar slut.");
            TopUpFromInventory = Config.Bind("4 - Auto", "Top up stacks", true,
                "Fyll pÃ¥ ofullstÃ¤ndiga stackar i mat-, mjÃ¶d- och ammoplatser frÃ¥n inventoryt (nÃ¤r inventoryt Ã¤r stÃ¤ngt).");
            AutoEat = Config.Bind("4 - Auto", "Auto eat", false,
                "Ã„t automatiskt frÃ¥n matplatserna nÃ¤r en matbuff hÃ¥ller pÃ¥ att ta slut eller en matplats i magen Ã¤r ledig.");
            AutoEatSecondsLeft = Config.Bind("4 - Auto", "Auto eat seconds left", 60,
                new ConfigDescription("Ã„t igen nÃ¤r sÃ¥ hÃ¤r mÃ¥nga sekunder Ã¥terstÃ¥r av en matbuff.", new AcceptableValueRange<int>(5, 1200)));

            AutoClearCheated = Config.Bind("6 - Cheat flag", "Auto clear inventory", true,
                "Ta automatiskt bort \"summoned through cheating means\" frÃ¥n fÃ¶remÃ¥l i ditt inventory. " +
                "I ett moddat spel Ã¤r prestationer redan avstÃ¤ngda, sÃ¥ markeringen har ingen funktion â€“ den sprider sig bara.");
            ClearCheatedKey = Config.Bind("6 - Cheat flag", "Clear nearby key", new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Ta bort markeringen frÃ¥n inventoryt, byggnader/arbetsbÃ¤nkar och kistor inom 30 m.");

            AutoRepair = Config.Bind("7 - Convenience", "Auto repair", true,
                "Reparera automatiskt allt som en närliggande arbetsbänk, smedja osv. kan reparera (samma regler som reparationsknappen).");
            AutoRepairDistance = Config.Bind("7 - Convenience", "Auto repair distance", 5f,
                new ConfigDescription("Hur nära stationen du måste vara (meter).", new AcceptableValueRange<float>(1f, 20f)));
            CraftSearch = Config.Bind("7 - Convenience", "Recipe search", true,
                "Visa ett sökfält ovanför receptlistan. Börja med ! för att söka på ingrediens (t.ex. !järn).");
            ShowGraveArrow = Config.Bind("7 - Convenience", "Grave arrow", true,
                "Visa en pil och avståndet till din grav. Grav- och kartmarkeringen tas bort när graven är tömd.");
            ShowPortalWarning = Config.Bind("7 - Convenience", "Portal warning", true,
                "Visa hur många föremål du bär som inte får tas genom en portal (malm, metall m.m.).");

            ShowHud = Config.Bind("5 - HUD", "Show quick slot HUD", true, "Visa mat-, mjÃ¶d- och ammoplatserna pÃ¥ skÃ¤rmen.");
            HudPosition = Config.Bind("5 - HUD", "HUD position", new Vector2(0f, 230f),
                "Position fÃ¶r HUD:en relativt skÃ¤rmens nederkant (mitten).");
            ShowWeight = Config.Bind("5 - HUD", "Show weight", true, "Visa vikt (nu/max) ovanfÃ¶r snabbplatserna.");
            DurabilityWarnPercent = Config.Bind("5 - HUD", "Durability warning percent", 20,
                new ConfigDescription("Varna nÃ¤r utrustad utrustning har mindre hÃ¥llbarhet Ã¤n sÃ¥ hÃ¤r (0 = av).", new AcceptableValueRange<int>(0, 100)));

            // 1.0.0 defaults overlapped QuickStackStore's buttons and the stamina bar: move untouched values.
            if (PanelPosition.Value == new Vector2(615f, 28f))
                PanelPosition.Value = (Vector2)PanelPosition.DefaultValue;
            if (HudPosition.Value == new Vector2(0f, 150f))
                HudPosition.Value = (Vector2)HudPosition.DefaultValue;

            MainRows.SettingChanged += (_, __) => SlotLayout.OnLayoutChanged();
        }
    }
}
