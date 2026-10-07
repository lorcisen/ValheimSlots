using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ValheimSlots
{
    /// <summary>
    /// Recipes the player has pinned (right-click in the recipe list). Shown by <see cref="PinnedPanel"/>.
    /// A pin is a recipe + target quality (1 = craft, 2+ = upgrade to that level). Each recipe can be pinned once.
    /// Pins are removed automatically when that recipe is successfully crafted/upgraded.
    /// Stored per character in Player.m_customData.
    /// </summary>
    [HarmonyPatch]
    internal static class PinnedRecipes
    {
        internal sealed class Pin
        {
            public Recipe Recipe;
            public int Quality;
        }

        private const string Key = "ValheimSlots.pins";

        private static readonly List<Pin> Pins = new List<Pin>();
        private static Player _owner;

        /// <summary>Bumped on every change so the panel knows when to rebuild.</summary>
        internal static int Version { get; private set; }

        internal static IReadOnlyList<Pin> All
        {
            get { EnsureLoaded(); return Pins; }
        }

        internal static bool IsPinned(Recipe recipe)
        {
            EnsureLoaded();
            return Find(recipe) != null;
        }

        internal static void Toggle(Recipe recipe, int quality)
        {
            EnsureLoaded();
            if (recipe == null || _owner == null)
                return;
            var existing = Find(recipe);
            string name = Localization.instance.Localize(recipe.m_item.m_itemData.m_shared.m_name);
            if (existing != null)
            {
                Pins.Remove(existing);
                _owner.Message(MessageHud.MessageType.TopLeft, $"{name}: pin borttagen");
            }
            else
            {
                Pins.Add(new Pin { Recipe = recipe, Quality = Mathf.Max(1, quality) });
                _owner.Message(MessageHud.MessageType.TopLeft, $"{name}: pinnad");
            }
            Changed();
        }

        internal static void Remove(Pin pin)
        {
            if (Pins.Remove(pin))
                Changed();
        }

        internal static void ClearAll()
        {
            EnsureLoaded();
            if (Pins.Count == 0)
                return;
            Pins.Clear();
            Changed();
            _owner?.Message(MessageHud.MessageType.TopLeft, "Alla pinnade recept borttagna");
        }

        private static Pin Find(Recipe recipe)
        {
            foreach (var p in Pins)
                if (p.Recipe == recipe || (p.Recipe != null && recipe != null && p.Recipe.name == recipe.name))
                    return p;
            return null;
        }

        private static void Changed()
        {
            Version++;
            Save();
            // Refresh the pin markers in the recipe list.
            if (InventoryGui.instance != null && InventoryGui.IsVisible() && Player.m_localPlayer != null)
                InventoryGui.instance.UpdateCraftingPanel();
        }

        // --- Persistence -----------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            var player = Player.m_localPlayer;
            if (player == _owner)
                return;
            _owner = player;
            Pins.Clear();
            Version++;
            if (player == null || ObjectDB.instance == null || !player.m_customData.TryGetValue(Key, out var raw) || string.IsNullOrEmpty(raw))
                return;
            foreach (var part in raw.Split(';'))
            {
                var bits = part.Split('@');
                if (bits.Length != 2 || !int.TryParse(bits[1], out int quality))
                    continue;
                var recipe = ObjectDB.instance.m_recipes.Find(r => r != null && r.name == bits[0]);
                if (recipe != null && recipe.m_item != null && Find(recipe) == null)
                    Pins.Add(new Pin { Recipe = recipe, Quality = quality });
            }
        }

        private static void Save()
        {
            if (_owner == null)
                return;
            var sb = new StringBuilder();
            foreach (var p in Pins)
            {
                if (p.Recipe == null) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(p.Recipe.name.Replace(";", "").Replace("@", "")).Append('@').Append(p.Quality);
            }
            if (sb.Length == 0)
                _owner.m_customData.Remove(Key);
            else
                _owner.m_customData[Key] = sb.ToString();
        }

        // --- Right-click in the recipe list ---------------------------------------------------------

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.AddRecipeToList))]
        [HarmonyPostfix]
        private static void InventoryGui_AddRecipeToList(InventoryGui __instance, Recipe recipe, ItemDrop.ItemData item)
        {
            if (!Plugin.PinnedEnabled.Value || __instance.m_availableRecipes.Count == 0)
                return;
            var element = __instance.m_availableRecipes[__instance.m_availableRecipes.Count - 1].InterfaceElement;
            if (element == null)
                return;
            var handler = element.GetComponent<RecipePinHandler>() ?? element.AddComponent<RecipePinHandler>();
            handler.Recipe = recipe;
            handler.Quality = item == null ? 1 : item.m_quality + 1;
            PinMarker.Set(element.transform, IsPinned(recipe));
        }

        internal sealed class RecipePinHandler : MonoBehaviour, IPointerClickHandler
        {
            public Recipe Recipe;
            public int Quality;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                    Toggle(Recipe, Quality);
            }
        }

        // --- Auto-remove after a successful craft -------------------------------------------------------

        private static Recipe _crafting;
        private static bool _crafted;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        [HarmonyPrefix]
        private static void InventoryGui_DoCrafting_Prefix(InventoryGui __instance)
        {
            _crafting = __instance.m_craftRecipe;
            _crafted = false;
        }

        /// <summary>The crafted (or upgraded) item is added through this overload; a non-null result means success.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
            typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        private static void Inventory_AddItem_Crafted(string name, ItemDrop.ItemData __result)
        {
            if (_crafting != null && __result != null && _crafting.m_item != null && name == _crafting.m_item.gameObject.name)
                _crafted = true;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        [HarmonyFinalizer]
        private static void InventoryGui_DoCrafting_Finalizer()
        {
            var recipe = _crafting;
            bool crafted = _crafted;
            _crafting = null;
            _crafted = false;
            if (!crafted || recipe == null)
                return;
            EnsureLoaded();
            var pin = Find(recipe);
            if (pin == null)
                return;
            Pins.Remove(pin);
            Changed();
            _owner?.Message(MessageHud.MessageType.TopLeft,
                $"{Localization.instance.Localize(recipe.m_item.m_itemData.m_shared.m_name)}: klar, pin borttagen");
        }
    }
}
