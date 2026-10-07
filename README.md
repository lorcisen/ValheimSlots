# ValheimSlots

Client-side BepInEx mod for Valheim 1.0.x by **Lorcisen**. It adds a bigger inventory, dedicated equipment, weapon-set, ammo, food and mead slots with hotkeys, plus quality-of-life features: auto refill, auto repair, recipe search, a grave arrow and a portal warning. Only the player needs it. It works on any server and with EpicLoot.

The player-facing feature list is in [package/README.md](package/README.md). Version history is in [package/CHANGELOG.md](package/CHANGELOG.md).

## Building

Requirements:
- .NET SDK 8 (builds a `net462` assembly)
- Valheim with BepInExPack 5.4.2351 installed through r2modman

Build, and copy the DLL into your r2modman profile:

```
dotnet build -c Release
```

Paths default to `F:\SteamLibrary\steamapps\common\Valheim` and the r2modman `Default` profile. Override them with:

```
dotnet build -c Release -p:GameDir="D:\Games\Valheim" -p:ProfileDir="%APPDATA%\r2modmanPlus-local\Valheim\profiles\MyProfile"
```

Add `-p:Deploy=false` to build without copying into the profile.

Create a Thunderstore / r2modman-importable zip in `dist/`:

```
powershell -ExecutionPolicy Bypass -File pack.ps1
```

## How it works

Everything lives in the player's normal inventory:

| Rows | Content |
|---|---|
| 0–(main−1) | Main inventory (row 0 is the vanilla 1–8 hotbar) |
| main–8 | Reserved for Valheim's purchasable rows (max 9) |
| 9–11 | Special slots: equipment, weapon sets, ammo, food, mead |

Because of this, saving, death and graves, other mods' item data (EpicLoot) and multiplayer all work without any networking of their own. The special rows are drawn in a separate panel by moving the grid's own elements, so other mods' slot decorations keep working.

## Source layout

- `src/Plugin.cs`: plugin entry point and config.
- `src/SlotLayout.cs`: slot definitions and placement rules.
- `src/Patches/`: Harmony patches for the inventory, GUI, hotbar and tombstones.
- `src/Features/`: auto refill, weapon sets, auto repair, grave tracker and the cheat-flag cleaner.
- `src/UI/`: special slot panel, HUD, recipe search and runtime-drawn icons.
- `src/Compat/`: Quick Stack - Store - Sort - Trash - Restock integration.
- `server/sync-server-mods.ps1`: copies an r2modman profile's mods and configs to a dedicated server.

## License

[MIT](LICENSE)
