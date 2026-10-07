# Builds the mod and creates a Thunderstore/r2modman-importable zip in .\dist
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build -c Release -p:Deploy=false
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$version = (Get-Content package\manifest.json -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $PSScriptRoot "dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\plugins" | Out-Null

Copy-Item package\manifest.json, package\README.md, package\CHANGELOG.md, package\icon.png $stage
Copy-Item bin\Release\ValheimSlots.dll "$stage\plugins\"

# Thunderstore naming: Author-Name-Version.zip
$zip = Join-Path $PSScriptRoot "dist\Lorcisen-ValheimSlots-$version.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
# Build the zip by hand: Compress-Archive (PowerShell 5.1) writes "plugins\x.dll" with backslashes,
# which Thunderstore and r2modman do not treat as a folder.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
} finally {
    $archive.Dispose()
}
Remove-Item $stage -Recurse -Force
Write-Host "Created $zip"
