# Copies the mods and configs from the r2modman "Default" profile to the Valheim dedicated server.
# Run again whenever you add/update/remove mods in r2modman.
#
#   powershell -ExecutionPolicy Bypass -File sync-server-mods.ps1
#
# The server's BepInEx\plugins folder is managed by this script: plugin folders that are not in the
# profile (or that are client-only) are removed from the server.

param(
    [string]$ProfileDir = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\Default",
    [string]$ServerDir  = "F:\SteamLibrary\steamapps\common\Valheim dedicated server"
)

$ErrorActionPreference = "Stop"

# Client-only mods (UI/HUD/visual). They do nothing useful on a headless server and some touch UI that
# does not exist there. Matched against the plugin folder name.
$ClientOnly = @(
    "Azumatt-AzuHoverStats",
    "Elg-AzuHoverStats_PTBR",
    "Biozip-HoverCompare",
    "KompjoeFriek-ShowPlantProgress",
    "JamesJonesTV-Legacy_Build_Menu",
    "Lorcisen-ValheimSlots",
    "MainStreetGaming-BetterDiving",
    "orfox-ValheimClock",
    "OverDrive-DungeonMaps",
    "Zenox-BetterUI"
)

$srcPlugins = Join-Path $ProfileDir "BepInEx\plugins"
$srcConfig  = Join-Path $ProfileDir "BepInEx\config"
$dstPlugins = Join-Path $ServerDir  "BepInEx\plugins"
$dstConfig  = Join-Path $ServerDir  "BepInEx\config"

foreach ($p in @($srcPlugins, $srcConfig, (Join-Path $ServerDir "BepInEx\core"), (Join-Path $ServerDir "valheim_server.exe"))) {
    if (-not (Test-Path $p)) { throw "Saknas: $p" }
}
if (Get-Process valheim_server -ErrorAction SilentlyContinue) {
    throw "Servern körs – stäng den först (Ctrl+C i serverfönstret)."
}
New-Item -ItemType Directory -Force $dstPlugins, $dstConfig | Out-Null

# --- Plugins -------------------------------------------------------------------------------
$wanted = @()
foreach ($dir in Get-ChildItem $srcPlugins -Directory) {
    $enabledDlls = Get-ChildItem $dir.FullName -Recurse -Filter *.dll -File
    if ($ClientOnly -contains $dir.Name) { Write-Host "  hoppar över (klient) $($dir.Name)" -ForegroundColor DarkGray; continue }
    if (-not $enabledDlls -and (Get-ChildItem $dir.FullName -Recurse -Filter *.dll.old -File)) {
        Write-Host "  hoppar över (avstängd i r2modman) $($dir.Name)" -ForegroundColor DarkGray; continue
    }
    $wanted += $dir.Name
    robocopy $dir.FullName (Join-Path $dstPlugins $dir.Name) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy misslyckades för $($dir.Name)" }
    Write-Host "  kopierad $($dir.Name)" -ForegroundColor Green
}
# Loose DLLs directly in plugins\ (rare)
Get-ChildItem $srcPlugins -File -Filter *.dll | ForEach-Object { Copy-Item $_.FullName $dstPlugins -Force; $wanted += $_.Name }

foreach ($old in Get-ChildItem $dstPlugins) {
    if ($wanted -notcontains $old.Name) {
        Remove-Item $old.FullName -Recurse -Force
        Write-Host "  borttagen från servern $($old.Name)" -ForegroundColor Yellow
    }
}

# --- Configs -------------------------------------------------------------------------------
# Server configs win for ServerSync mods, so the server gets your current settings.
# BepInEx.cfg is the server's own (console/logging) and is never overwritten. Per-player data files are skipped.
robocopy $srcConfig $dstConfig /E /XF BepInEx.cfg *.dat /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy misslyckades för config" }
Write-Host "  configs kopierade" -ForegroundColor Green

Write-Host ""
Write-Host "Klart: $($wanted.Count) mods på servern. Starta med start_server_modded.bat i serverns mapp." -ForegroundColor Cyan
