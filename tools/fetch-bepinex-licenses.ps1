# Writes third_party/BepInEx/: the license of every component in the official BepInEx IL2CPP win-x64 build that
# the all-in-one package bundles, fetched at the exact version shipped, plus NOTICE.md listing them with sources.
# The official zip contains no license files, so these are required to redistribute it.
# Run again after changing BepInExBuild in UltraMode.csproj, and update $components to match the new zip
# (versions: right-click a dll in BepInEx/core -> Properties -> Details, or (Get-Item x.dll).VersionInfo).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root "third_party/BepInEx"
$licenses = Join-Path $dest "licenses"
$bepinex = "6.0.0-be.788+5b766a3"

# name, files in the zip, version, license, repository, ref of that version, license file (default: repo license)
$components = @(
    @{ Name = "BepInEx"; Files = "BepInEx/core/BepInEx.*.dll"; Version = "6.0.0-be.788"; License = "LGPL-2.1"; Repo = "BepInEx/BepInEx"; Ref = "5b766a3b7f6c164d4798924a93f3acf4db769d06" }
    @{ Name = "UnityDoorstop"; Files = "winhttp.dll, doorstop_config.ini"; Version = "4.5.0"; License = "LGPL-2.1"; Repo = "NeighTools/UnityDoorstop"; Ref = "v4.5.0" }
    @{ Name = "Il2CppInterop"; Files = "BepInEx/core/Il2CppInterop.*.dll"; Version = "1.5.3"; License = "LGPL-3.0"; Repo = "BepInEx/Il2CppInterop"; Ref = "dbda1cb353b0f4253345dc45136d170b9e50a5a0" }
    @{ Name = "HarmonyX"; Files = "BepInEx/core/0Harmony.dll"; Version = "2.10.2"; License = "MIT"; Repo = "BepInEx/HarmonyX"; Ref = "v2.10.2" }
    @{ Name = "MonoMod"; Files = "BepInEx/core/MonoMod.*.dll"; Version = "22.07.31.01 / Backports 1.1.2"; License = "MIT"; Repo = "MonoMod/MonoMod"; Ref = "v22.07.31.01" }
    @{ Name = "Cpp2IL"; Files = "BepInEx/core/Cpp2IL.Core.dll, LibCpp2IL.dll, StableNameDotNet.dll, WasmDisassembler.dll"; Version = "2022.1.0-development.1452"; License = "MIT"; Repo = "SamboyCoding/Cpp2IL"; Ref = "558ddd98642010897d54316b51fbaa7889fda093" }
    @{ Name = "Disarm"; Files = "BepInEx/core/Disarm.dll"; Version = "2022.1.0-master.99"; License = "MIT"; Repo = "SamboyCoding/Disarm"; Ref = "8574010015c8c56bb7faad7e407c50b0abe9cc72" }
    @{ Name = "AsmResolver"; Files = "BepInEx/core/AsmResolver*.dll"; Version = "6.0.0-beta.5"; License = "MIT"; Repo = "Washi1337/AsmResolver"; Ref = "124e161a9bc0ebb83b4d97276ae3da3c9cfe2fc5" }
    @{ Name = "AssetRipper.CIL"; Files = "BepInEx/core/AssetRipper.CIL.dll"; Version = "1.2.2"; License = "MIT"; Repo = "AssetRipper/AssetRipper.CIL"; Ref = "06216e69825ccd6536dc17e9ca81c4d539ce9321" }
    @{ Name = "AssetRipper.Primitives"; Files = "BepInEx/core/AssetRipper.Primitives.dll"; Version = "3.2.0"; License = "MIT"; Repo = "AssetRipper/AssetRipper.Primitives"; Ref = "d759b3db98bf2a1cb8c2181faf5d405610aac585" }
    @{ Name = "Mono.Cecil"; Files = "BepInEx/core/Mono.Cecil*.dll"; Version = "0.11.4"; License = "MIT"; Repo = "jbevain/cecil"; Ref = "0.11.4" }
    @{ Name = "Iced"; Files = "BepInEx/core/Iced.dll"; Version = "1.21.0"; License = "MIT"; Repo = "icedland/iced"; Ref = "601c40570d6c986c8e1a6a2e483dd2fdf5ade552" }
    @{ Name = "Capstone.NET"; Files = "BepInEx/core/Gee.External.Capstone.dll"; Version = "2.3.2"; License = "MIT"; Repo = "9ee1/Capstone.NET"; Ref = "b90e380c14e857865c94a355954236e678bd557c" }
    @{ Name = "SemanticVersioning"; Files = "BepInEx/core/SemanticVersioning.dll"; Version = "2.0.2"; License = "MIT"; Repo = "adamreeve/semver.net"; Ref = "" }
    @{ Name = "Dobby"; Files = "BepInEx/core/dobby.dll"; Version = "BepInEx fork"; License = "Apache-2.0"; Repo = "BepInEx/Dobby"; Ref = "" }
    @{ Name = ".NET Runtime"; Files = "dotnet/"; Version = "6.0.7"; License = "MIT"; Repo = "dotnet/runtime"; Ref = "v6.0.7"; LicenseFile = "dotnet-runtime.txt" }
)
$extra = @(
    @{ File = "dotnet-runtime-THIRD-PARTY-NOTICES.txt"; Url = "https://raw.githubusercontent.com/dotnet/runtime/v6.0.7/THIRD-PARTY-NOTICES.TXT" }
)

$headers = @{ "User-Agent" = "ultra-mode-license-fetch" }
if ($env:GITHUB_TOKEN) { $headers.Authorization = "Bearer $env:GITHUB_TOKEN" }
$utf8 = New-Object System.Text.UTF8Encoding($false)
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force $licenses | Out-Null

foreach ($c in $components) {
    $query = if ($c.Ref) { "?ref=$($c.Ref)" } else { "" }
    try { $info = Invoke-RestMethod "https://api.github.com/repos/$($c.Repo)/license$query" -Headers $headers }
    catch { Write-Warning "$($c.Name): no license at $($c.Ref), using the default branch"; $info = Invoke-RestMethod "https://api.github.com/repos/$($c.Repo)/license" -Headers $headers }
    $text = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($info.content))
    if (-not $c.LicenseFile) { $c.LicenseFile = "$($c.Name -replace '[^\w.]+', '-').txt" }
    $c.Source = if ($c.Ref) { "https://github.com/$($c.Repo)/tree/$($c.Ref)" } else { "https://github.com/$($c.Repo)" }
    [IO.File]::WriteAllText((Join-Path $licenses $c.LicenseFile), $text, $utf8)
    Write-Host "$($c.Name): $($info.license.spdx_id) ($($info.path))"
}
foreach ($e in $extra) {
    $text = (Invoke-WebRequest $e.Url -UseBasicParsing -Headers $headers).Content
    if ($text -is [byte[]]) { $text = [Text.Encoding]::UTF8.GetString($text) }
    [IO.File]::WriteAllText((Join-Path $licenses $e.File), $text, $utf8)
    Write-Host "$($e.File)"
}

$rows = $components | ForEach-Object { "| $($_.Name) | $($_.Version) | $($_.License) | ``$($_.Files)`` | [source]($($_.Source)) | [$($_.LicenseFile)](licenses/$($_.LicenseFile)) |" }
$notice = @"
# Third-party notices / 第三方组件声明

The all-in-one package (``UltraMode-v<version>-with-BepInEx.zip``) contains the official, unmodified
**BepInEx $bepinex** build for Unity IL2CPP win-x64, downloaded from
https://builds.bepinex.dev/projects/bepinex_be and checked against the SHA-256 in UltraMode.csproj.
Its components are listed below with their licenses and the source code of the exact version shipped.
None of them were modified. Ultra Mode itself only uses BepInEx's public API at run time.

整合包（``UltraMode-v<版本>-with-BepInEx.zip``）原样包含官方 **BepInEx $bepinex**（Unity IL2CPP win-x64），
未做任何修改。下表列出其中各组件的许可证，以及对应版本的源码地址。

| Component | Version | License | Files | Source | License text |
| --- | --- | --- | --- | --- | --- |
$($rows -join "`n")

LGPL-3.0 supplements GPL-3.0; Il2CppInterop's license file contains both texts.
The .NET runtime's own third-party notices: [dotnet-runtime-THIRD-PARTY-NOTICES.txt](licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt).

Under the LGPL you may replace these libraries with your own builds: every file listed above is a separate file
that can be swapped in place, and BepInEx loads whatever is there.
"@
[IO.File]::WriteAllText((Join-Path $dest "NOTICE.md"), $notice.Replace("`r`n", "`n"), $utf8)
Write-Host "Wrote $dest"
