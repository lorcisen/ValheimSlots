# ValheimSlots

*By Lorcisen*

Client-side inventory mod for Valheim 1.0.x. Only you need it. Servers and other players do not.

## Features
- **Bigger inventory:** 8x6 by default (configurable 4–9 rows). Rows bought from the trader are respected.
- **Equipment slots:** helmet, chest, legs, cape, utility and trinket. Equipping an item moves it into its slot.
- **Two weapon sets (A/B):** main hand + off hand each. Swap with `Caps Lock`.
- **Ammo slots (3):** select ammo with `U` / `I` / `Y`.
- **Food slots (3):** eat with `Z` / `B` / `N`.
- **Mead slots (3):** drink with `9` / `0` / `K`.
- **Auto refill:** a food, mead or ammo slot that runs out is refilled from your inventory, and partial stacks are topped up.
- **Auto eat (off by default):** eats from the food slots when a buff is about to run out.
- **Locked slots:** hold `Left Alt` and click a slot. Locked slots are never auto-filled, stacked into chests, quick-stacked or sorted. If Quick Stack - Store - Sort is installed, its favorited slots (also Alt+click) act as the lock.
- **Cheat flag cleaner:** removes Valheim's spreading "summoned through cheating means" flag. It is cleared automatically from your inventory. `Ctrl+F9` also cleans buildings, workbenches, cooking stations, fermenters and chests within 30 m. Achievements are already disabled in a modded game, so the flag has no other effect.
- **Achievements when modded (optional, off by default):** lets you earn achievements in a modded game. Only the modded state is ignored. Cheat commands, cheat world modifiers and cheated items still block achievements. Toggle it with the "Achievements (modded)" button in the pause menu (Esc). It takes effect after restarting the game.
- **Auto repair:** walk up to a workbench, forge or other station (5 m) and everything it can repair is repaired.
- **Recipe search:** a search field above the crafting list. Type a name, or `!ingredient` (e.g. `!iron`) to find recipes that use it.
- **Pinned recipes:** right-click a recipe in the crafting list to pin it, or middle-click a piece in the build menu (hammer, cultivator, ...). It is shown on the right side with how much of each ingredient you carry, and a check mark when you have enough. It disappears once you have crafted it. Remove it yourself with the X (when the inventory is open) or clear all with `Ctrl+F11`.
- **Grave arrow:** the HUD points to your grave with the distance. The grave and Valheim's death pin are removed once you have emptied it.
- **Portal warning:** the HUD shows how many items you carry that can't go through a portal.
- **HUD:** quick-slot bar with icons, stack sizes and hotkeys, carry weight, and a low-durability warning.

All keys and options are in `BepInEx/config/lorcisen.valheimslots.cfg`.

**Languages:** English and Swedish. Texts follow the game's language setting.

## Compatibility
- **EpicLoot:** magic items keep all their data, and equipped items count as normal.
- **Multiplayer:** everything is stored in your normal character inventory. Graves work for everyone, including players without the mod.
- **Quick Stack - Store - Sort - Trash - Restock:** leaves the special and locked slots alone, and can still restock food, mead and ammo.
- **Not compatible with EquipmentAndQuickSlots:** ValheimSlots replaces it. Uninstall EAQS first. Your items are moved into the new slots automatically the first time you load the character.

## Uninstalling
Items outside vanilla's rows are dropped at your feet the next time you spawn without the mod. Pick them up again, or move them into the main grid before uninstalling.
