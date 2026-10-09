param(
    [string]$PublishDirectory,
    [string]$OutputDirectory,
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$ReleaseLabel = 'UI'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (!$PublishDirectory) { $PublishDirectory = Join-Path $repo 'artifacts/application' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/release' }
$PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$sourceCommit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Valid Git source commit is required.' }
$dirty = & git -C $repo status --porcelain
if ($LASTEXITCODE -ne 0 -or $dirty) { throw "Source tree has uncommitted changes. Commit and verify first: $dirty" }
foreach ($required in @('LWBridge.Desktop.exe','LWBridge.Desktop.dll','LWBridge.Desktop.runtimeconfig.json','ProductionUi/index.html','OverviewBridge','LiveResourceProbe','runtimes','Microsoft.Web.WebView2.Core.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $PublishDirectory $required))) { throw "Publish dependency missing: $required" }
}
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PublishDirectory 'LWBridge.Desktop.exe')).ProductVersion
if ($version -notmatch ('\+' + [regex]::Escape($sourceCommit) + '$')) { throw "Published executable does not identify current source commit: $version" }
& node (Join-Path $repo 'src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs') (Join-Path $PublishDirectory 'ProductionUi')
if ($LASTEXITCODE -ne 0) { throw 'Published production UI does not match current source.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$stage = [IO.Path]::GetFullPath((Join-Path $outputRoot '_stage'))
if ([IO.Path]::GetDirectoryName($stage) -ne $outputRoot) { throw 'Staging path escaped the selected output directory.' }
if (Test-Path -LiteralPath $stage) { throw "Existing staging directory preserved: $stage. Choose a fresh output directory." }
$appRoot = Join-Path $stage 'LW-Control-UI'
New-Item -ItemType Directory -Force -Path $appRoot | Out-Null
# Stage all publish children explicitly (LiteralPath does not expand wildcards).
Get-ChildItem -LiteralPath $PublishDirectory -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $appRoot -Recurse -Force
}
Get-ChildItem -LiteralPath $appRoot -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.xml') } | Remove-Item -Force
$launcher = '@echo off' + "`r`n" + 'setlocal' + "`r`n" + 'set "APP=%~dp0LWBridge.Desktop.exe"' + "`r`n" + 'if not exist "%APP%" (echo Missing LWBridge.Desktop.exe & exit /b 2)' + "`r`n" + 'start "" "%APP%" %*' + "`r`n"
[IO.File]::WriteAllText((Join-Path $appRoot 'Launch LWBridge.cmd'), $launcher, [Text.Encoding]::ASCII)
$readme = @"
LW-Control $ReleaseLabel release candidate
================================
Build source commit: $sourceCommit
Target UI behavior: LWBridge 0.3.17 (post-auth only)
Reference executable SHA-256: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783

INSTALL
1. Extract the WHOLE ZIP into a writable Windows x64 folder. Keep all files/subfolders together.
2. Install Microsoft .NET 10 Desktop Runtime (x64) and Microsoft Edge WebView2 Runtime if not already installed.
3. Double-click Launch LWBridge.cmd or LWBridge.Desktop.exe.

IMPORTANT: The recovered Auto Launch Game preference is ON by default. Normal startup
may start Last War. Do not use the program against an active game unless authorized.

UI COVERAGE
Home, Map, Automation, Squads/Equipment, City Layout, Hotkeys, Mini Games,
Settings and the shared shell. English/light, Japanese/dark, and narrow
viewport were checked using the canonical production frontend.

KNOWN LIMITATIONS
This is a usable UI baseline, NOT a fully recovered native product.
One bounded Home Launch -> authenticated current-client Connected -> Close
has been demonstrated through real packaged native controls, with exact PID
exit and verified script restoration on a single isolated profile.
Full original 0.3.17 licensed-runtime equivalence, adverse recovery/retry,
multi-owner entitlements, Map scanners, mini-game execution, updater,
protected original service and full original runtime pixel parity are NOT
certified. Some actions require later native work.
There is NO original login, licensing or subscription UI.

Build prerequisites (only for DEVELOPERS, not for this zip):
Windows x64, .NET 10 SDK and Node.js/npm. Optional UI browser test uses
installed Microsoft Edge and Playwright dev dependency.

Repository: https://github.com/Tuna0307/LW-Control
Feature branch: codex/home-launch-delivery-002
Review PRs: https://github.com/Tuna0307/LW-Control/pulls
"@
[IO.File]::WriteAllText((Join-Path $appRoot 'README-RELEASE.txt'), $readme, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $appRoot 'SOURCE-COMMIT.txt'), "$sourceCommit`r`n", [Text.Encoding]::ASCII)
$destination = Join-Path $OutputDirectory ("LW-Control-" + $ReleaseLabel + "-RC-" + $sourceCommit.Substring(0,12) + ".zip")
if (Test-Path -LiteralPath $destination) { throw "ZIP already exists: $destination. Remove only an owned older ZIP before rerunning." }
Compress-Archive -Path $appRoot -DestinationPath $destination -CompressionLevel Optimal
$zip = Get-Item -LiteralPath $destination
$sha = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
$fileCount = @(Get-ChildItem -LiteralPath $appRoot -Recurse -File).Count
$zipEntries = [IO.Compression.ZipFile]::OpenRead($destination)
try {
    $entries = @($zipEntries.Entries | ForEach-Object FullName)
    if (!$entries.Where({$_ -eq 'LW-Control-UI/LWBridge.Desktop.exe'}).Count) { throw 'Packaged executable missing from ZIP.' }
    if ($entries.Where({$_ -match '(^|/)(node_modules|tests|evidence)/'}).Count) { throw 'Fixture or research files included in runtime ZIP.' }
} finally { $zipEntries.Dispose() }
[pscustomobject]@{zip=$zip.FullName;bytes=$zip.Length;sha256=$sha;sourceCommit=$sourceCommit;fileCount=$fileCount;entries=$entries.Count} | ConvertTo-Json
Remove-Item -LiteralPath $stage -Force -Recurse
