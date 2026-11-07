# Copies Belmont UI (MIT, https://github.com/leandrobueno/BelmontUI) into vendor/BelmontUI.
# Usage: tools/sync-belmontui.ps1 [-Source <clone>]   (default: reference/BelmontUI, cloned if missing)
# Only mechanical edits are made, so the copy can be refreshed by running this again:
#   - namespace BelmontUi -> UltraModeVendor.BelmontUi (no clash with a standalone Belmont UI)
#   - #nullable disable (upstream isn't annotated; this project enables nullable)
#   - Plugin.cs is left out: the embedded copy is started by src/Menu/EmbeddedBelmontUi.cs, not by BepInEx
param([string]$Source = "")

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $Source) { $Source = Join-Path $root "reference/BelmontUI" }
if (-not (Test-Path $Source)) { git clone --depth 1 https://github.com/leandrobueno/BelmontUI.git $Source }

$dest = Join-Path $root "vendor/BelmontUI"
New-Item -ItemType Directory -Force $dest | Out-Null
Get-ChildItem $dest -Filter *.cs | Remove-Item

$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($file in Get-ChildItem $Source -Filter *.cs | Where-Object Name -ne "Plugin.cs") {
    $text = [IO.File]::ReadAllText($file.FullName)
    $text = $text -replace '(?m)^namespace BelmontUi;', 'namespace UltraModeVendor.BelmontUi;'
    [IO.File]::WriteAllText((Join-Path $dest $file.Name), "#nullable disable`r`n" + $text, $utf8)
}
Copy-Item (Join-Path $Source "LICENSE") (Join-Path $dest "LICENSE") -Force

$commit = git -C $Source log -1 --format="%H %cs"
$version = ([xml](Get-Content (Join-Path $Source "BelmontUI.csproj"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
[IO.File]::WriteAllText((Join-Path $dest "UPSTREAM"), "Belmont UI $version`r`nhttps://github.com/leandrobueno/BelmontUI $commit`r`n", $utf8)
Write-Host "Belmont UI $version ($commit) copied to $dest"
