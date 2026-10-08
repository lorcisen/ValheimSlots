# Changelog

## 1.0.0
- First version: 8x6 inventory, equipment/weapon-set/ammo/food/mead slots, hotkeys, auto refill, auto eat, locked slots, HUD, EAQS migration, QuickStackStore compatibility.

## 1.0.1
- Panel moved right so Quick Stack - Store - Sort buttons are visible; shorter slot labels with row headers.
- Color-blind friendly check mark on equipped items (inventory, hotbar, HUD).
- HUD moved above the stamina bar.
- Author set to Lorcisen; plugin id is now lorcisen.valheimslots (old settings are moved automatically).

## 1.0.2
- Removes Valheim's "summoned through cheating means" flag: automatically in your inventory, and with Ctrl+F9 also on nearby buildings, workbenches, cooking stations, fermenters and chests (30 m).

## 1.0.3
- The cheat flag cleaner also clears the character's "has used cheats" flag (saved to the character file on the next save).

## 1.0.4
- Grave arrow: HUD arrow + distance to your grave; the grave and Valheim's death pin are removed once the grave is emptied.
- Portal warning: HUD shows how many carried items can't go through a portal (with the game's no-portal icon).
- Auto repair: walking up to a usable workbench/forge/etc. repairs everything it can (same rules as the repair button).
- Recipe search: search field above the recipe list; start with ! to search by ingredient.

## 1.0.5
- Pinned recipes: right-click a recipe in the crafting list to pin it. Pinned recipes are listed on the right side with ingredient counts from your inventory (check mark when you have enough). They are removed automatically after crafting, with the X (inventory open) or with Ctrl+F11.

## 1.0.6
- All texts follow the game language: Swedish when Valheim runs in Swedish, English otherwise. Switching language in game updates the texts right away.
- Config descriptions are written in the game language; log messages are in English.
- Fixed garbled Swedish characters in the config descriptions.

## 1.0.7
- Build pieces can be pinned too: middle-click a piece in the build menu (hammer, cultivator, ...). Works with the vanilla build menu and Legacy Build Menu. The pin is removed when you place the piece.
