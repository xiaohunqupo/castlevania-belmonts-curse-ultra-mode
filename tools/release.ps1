# Builds, tags and publishes a GitHub release of Ultra Mode.
# Usage: tools/release.ps1 <version> [-NotesFile <file>] [-Draft] [-DryRun]
# Release notes default to docs/release-notes/v<version>.md, else GitHub generates them from the commits.
#   tools/release.ps1 0.2.0                  bump both version numbers, commit, tag, push, publish
#   tools/release.ps1 0.1.0                  version already set: only tag, push and publish
#   tools/release.ps1 0.2.0 -DryRun          run every check and the build, change nothing remote
# Needs the GitHub CLI logged in (gh auth login) and the game with BepInEx installed, because the build
# references its interop assemblies; that is also why this can't run on GitHub's hosted runners.
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$NotesFile = "",
    [switch]$Draft,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$repo = "xiaohunqupo/castlevania-belmonts-curse-ultra-mode"
$tag = "v$Version"
$csproj = Join-Path $root "UltraMode.csproj"
$pluginCs = Join-Path $root "src/Plugin.cs"
$zip = Join-Path $root "dist/UltraMode-$tag.zip"
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host "release: $text" -ForegroundColor Red; exit 1 }
function Run {
    $command, $rest = $args
    & $command @rest
    if ($LASTEXITCODE -ne 0) { Fail "'$($args -join ' ')' failed ($LASTEXITCODE)" }
}

Step "Checking tools and repository"
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail "GitHub CLI not found. Install it with: winget install GitHub.cli" }
gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Fail "GitHub CLI is not logged in. Run: gh auth login" }
if ((git branch --show-current) -ne "main") { Fail "releases are made from main" }
if (git status --porcelain) { Fail "working tree has uncommitted changes; commit or stash them first" }
Run git fetch origin main --tags --quiet
if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) { Fail "main differs from origin/main; pull or push first" }
if (git tag --list $tag) { Fail "tag $tag already exists" }
gh release view $tag -R $repo *> $null
if ($LASTEXITCODE -eq 0) { Fail "release $tag already exists on GitHub" }

Step "Version $Version"
$csprojText = [IO.File]::ReadAllText($csproj)
$pluginText = [IO.File]::ReadAllText($pluginCs)
$csprojVersion = [regex]::Match($csprojText, '<Version>([^<]+)</Version>').Groups[1].Value
$pluginVersion = [regex]::Match($pluginText, 'const string Version = "([^"]+)"').Groups[1].Value
$bumped = $false
if ($csprojVersion -ne $Version -or $pluginVersion -ne $Version) {
    Write-Host "    csproj $csprojVersion, Plugin.cs $pluginVersion -> $Version"
    [IO.File]::WriteAllText($csproj, ($csprojText -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"), $utf8)
    [IO.File]::WriteAllText($pluginCs, ($pluginText -replace 'const string Version = "[^"]+"', "const string Version = `"$Version`""), $utf8)
    $bumped = $true
}

Step "Building"
Run dotnet build -c Release -p:SkipDeploy=true -nologo -v:minimal -warnaserror
if (-not (Test-Path $zip)) { Fail "$zip was not produced" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try { $entries = $archive.Entries | ForEach-Object FullName } finally { $archive.Dispose() }
$prefix = "BepInEx/plugins/UltraMode/"
foreach ($required in "UltraMode.dll", "README.md", "README.zh-CN.md", "LICENSE-MIT", "LICENSE-MulanPSL2", "BelmontUI-LICENSE.txt") {
    if ($entries -notcontains "$prefix$required") { Fail "$required is missing from the zip" }
}
$entries | ForEach-Object { Write-Host "    $_" }

if ($DryRun) {
    if ($bumped) { Run git checkout -- $csproj $pluginCs }
    Step "Dry run finished; nothing was committed, tagged or published"
    exit 0
}

if ($bumped) {
    Step "Committing the version bump"
    Run git add $csproj $pluginCs
    Run git commit -m "Release $tag" --quiet
}

Step "Tagging and pushing $tag"
Run git tag -a $tag -m "Ultra Mode $tag"
Run git push origin main $tag --quiet

Step "Publishing the GitHub release"
if (-not $NotesFile) {
    $default = Join-Path $root "docs/release-notes/$tag.md"
    if (Test-Path $default) { $NotesFile = $default }
}
$ghArgs = @("release", "create", $tag, $zip, "-R", $repo, "--title", "Ultra Mode $tag", "--verify-tag")
if ($NotesFile) { $ghArgs += @("--notes-file", $NotesFile) } else { $ghArgs += "--generate-notes" }
if ($Draft) { $ghArgs += "--draft" }
Run gh @ghArgs
Step "Done: https://github.com/$repo/releases/tag/$tag"
