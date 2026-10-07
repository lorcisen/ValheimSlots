using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ValheimSlots
{
    /// <summary>
    /// A search field above the crafting/upgrade recipe list.
    ///   "plank"  – recipes whose name contains "plank"
    ///   "!iron"  – recipes that use an ingredient containing "iron"
    /// The field is cloned from the game's own build-menu search field so it looks native.
    /// While you type, the game treats it like the chat box, so E/Tab don't close the window and
    /// other mods' hotkeys (Quick Stack etc.) don't fire.
    /// </summary>
    [HarmonyPatch]
    internal static class CraftSearch
    {
        private const float FieldHeight = 30f;
        private const float Gap = 4f;

        private static TMP_InputField _field;
        private static string _query = "";

        internal static bool IsFocused => _field != null && _field.isFocused;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        [HarmonyPostfix]
        private static void InventoryGui_Show(InventoryGui __instance) => Ensure(__instance);

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        [HarmonyPostfix]
        private static void InventoryGui_Hide()
        {
            _query = "";
            if (_field != null)
            {
                _field.SetTextWithoutNotify("");
                _field.DeactivateInputField();
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnDestroy))]
        [HarmonyPostfix]
        private static void InventoryGui_OnDestroy()
        {
            _field = null;
            _query = "";
        }

        /// <summary>Filter the recipes before the list is built (works for both the craft and upgrade tabs).</summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipeList))]
        [HarmonyPrefix]
        private static void InventoryGui_UpdateRecipeList(List<Recipe> recipes)
        {
            if (!Plugin.CraftSearch.Value || string.IsNullOrWhiteSpace(_query) || recipes == null)
                return;
            string q = _query.Trim().ToLowerInvariant();
            bool byIngredient = q.StartsWith("!");
            if (byIngredient)
                q = q.Substring(1).Trim();
            if (q.Length == 0)
                return;
            recipes.RemoveAll(r => !Matches(r, q, byIngredient));
        }

        /// <summary>Report "typing" while the search field has focus.</summary>
        [HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
        [HarmonyPostfix]
        private static void Chat_HasFocus(ref bool __result)
        {
            if (!__result && IsFocused)
                __result = true;
        }

        private static bool Matches(Recipe recipe, string q, bool byIngredient)
        {
            if (recipe == null || recipe.m_item == null)
                return false;
            if (!byIngredient)
                return Localize(recipe.m_item.m_itemData.m_shared.m_name).Contains(q);
            if (recipe.m_resources == null)
                return false;
            foreach (var req in recipe.m_resources)
                if (req?.m_resItem != null && Localize(req.m_resItem.m_itemData.m_shared.m_name).Contains(q))
                    return true;
            return false;
        }

        private static string Localize(string token) => Localization.instance.Localize(token).ToLowerInvariant();

        private static void Ensure(InventoryGui gui)
        {
            if (!Plugin.CraftSearch.Value)
            {
                if (_field != null) _field.gameObject.SetActive(false);
                return;
            }
            if (_field != null)
            {
                if (!_field.gameObject.activeSelf) _field.gameObject.SetActive(true);
                SetPlaceholder();
                return;
            }

            var template = Hud.instance != null && Hud.instance.m_buildUi != null ? Hud.instance.m_buildUi.m_searchField : null;
            var list = gui.m_recipeListRoot;
            if (template == null || list == null)
            {
                Plugin.Log.LogWarning("Recipe search: could not find the build menu search field or the recipe list - the search field is not shown.");
                return;
            }

            // The recipe list sits in a scroll view; make room at its top for the field.
            var scroll = list.GetComponentInParent<ScrollRect>();
            var view = (RectTransform)(scroll != null ? scroll.transform : list.parent);
            view.offsetMax -= new Vector2(0f, FieldHeight + Gap);

            var go = Object.Instantiate(template.gameObject, view.parent, false);
            go.name = "ValheimSlotsCraftSearch";
            go.SetActive(true);
            _field = go.GetComponent<TMP_InputField>();
            DisablePersistentListeners(_field.onValueChanged);
            DisablePersistentListeners(_field.onSelect);
            DisablePersistentListeners(_field.onDeselect);
            DisablePersistentListeners(_field.onEndEdit);
            DisablePersistentListeners(_field.onSubmit);
            _field.SetTextWithoutNotify("");
            SetPlaceholder();

            // Place the field in the strip freed above the list (same horizontal anchors as the list).
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(view.anchorMin.x, view.anchorMax.y);
            rt.anchorMax = new Vector2(view.anchorMax.x, view.anchorMax.y);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(view.offsetMin.x, view.offsetMax.y + Gap);
            rt.offsetMax = new Vector2(view.offsetMax.x, view.offsetMax.y + Gap + FieldHeight);
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            _field.onValueChanged.AddListener(OnQueryChanged);
        }

        private static void SetPlaceholder()
        {
            if (_field != null && _field.placeholder is TMP_Text placeholder)
                placeholder.text = L.T("Search recipes…  (!iron = ingredient)", "Sök recept…  (!järn = ingrediens)");
        }

        private static void OnQueryChanged(string value)
        {
            _query = value ?? "";
            var gui = InventoryGui.instance;
            if (gui != null && Player.m_localPlayer != null && InventoryGui.IsVisible())
                gui.UpdateCraftingPanel();
        }

        /// <summary>The cloned field still has the build menu's inspector-assigned callbacks; switch them off.</summary>
        private static void DisablePersistentListeners(UnityEventBase evt)
        {
            if (evt == null)
                return;
            evt.RemoveAllListeners();
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                evt.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }
}
