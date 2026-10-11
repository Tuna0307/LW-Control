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
3. For native game integration, Python must be installed and available to the application.
4. Double-click Launch LWBridge.cmd or LWBridge.Desktop.exe.

IMPORTANT: The recovered Auto Launch Game preference is ON by default. Normal startup
may start Last War. Do not use the program against an active game unless authorized.

UI COVERAGE
Home, Map, Automation, Squads/Equipment, City Layout, Hotkeys, Mini Games,
Settings and the shared shell. English/light, Japanese/dark, and narrow
viewport were checked using the canonical production frontend.

MANUAL MAP SCAN (MAP-005 review candidate)
Manual City and Resource scanning now uses the actual packaged Windows Map controls,
authenticated official current client and current-server acquisition. A genuine
combined City/Resource Fast run on client content v24, server 2212, completed
2,500/2,500 blocks with 7,052 City / 8,002 Resource records. These are observations,
not expected counts or bundled private data. A separate active Resource Stop kept
those published records unchanged. The latest evidence, independent native/SQLite
query and City XLSX checks, and limitations are in docs/MAP_005_DELIVERY_RECEIPT_2026-10-11.md.
Standalone City Normal and Resource Fast runs subsequently completed 2,500/2,500
blocks each on server 2212, with 7,058 City / 8,008 Resource. The independent
native/SQLite audit, genuine City persistence after restart and reopened
12-column Excel results agree. Counts can change on later server scans.

ACCEPTED STORED MAP DATA (LWB317-MAP-DATA-DELIVERY-003)
City and Resource stored search, filters, sort, paging, tab retention, reopening
and City XLSX Save As remain supported. Historical archive checks used 7,000 City
and 8,008 Resource rows; archived data is NOT the MAP-005 scan witness.
A current live server context may still be needed to select saved records on
host restart. Genuine private datasets are NOT bundled in this ZIP.

KNOWN LIMITATIONS
This is a usable UI baseline, NOT a fully recovered native product.
Packaged Home Launch -> authenticated current-client Connected -> Close,
automatic startup and exact same-build host adoption have been demonstrated.
HOME-004 2026-10-11 live follow-up verified responsive game heartbeat after
actual task-owned authenticated transport loss: Reconnection OFF preserved
the game; ON restored a fresh authenticated Connected successor stable for
more than 15 seconds. Home Close with recovery ON prevented a later successor.
An isolated legitimate replacement-bridge journal/adoption failure was also
repaired through the actual Home Update-and-Launch control, with new genuine
authenticated Connected and exact restoration of the original game files.
The shared per-Windows-user mutable Lua package prevents the current
replacement from certifying two simultaneously independent official game
installations. Two local metadata profiles and two isolated RPC routes are
NOT two independently supported game sessions. HOME is still PARTIAL pending
this F-07 capability. Full original 0.3.17 licensed-runtime equivalence,
independent multi-game ownership, long external updater/maintenance paths,
other Map scan types, Map Auto execution, mini-game execution, protected services
and pixel parity are not certified. Original protected scanner internals and
exact traversal timing are not claimed recovered. See docs/MAP_005_CURRENT_STATUS.md
and docs/HOME_004_CURRENT_STATUS.md in source.
There is NO original login, licensing or subscription UI.

Build prerequisites (only for DEVELOPERS, not for this zip):
Windows x64, .NET 10 SDK and Node.js/npm. Optional UI browser test uses
installed Microsoft Edge and Playwright dev dependency.

Repository: https://github.com/Tuna0307/LW-Control
Released features and known limits: docs/FEATURE_STATUS.md in the source repository
Review PRs: https://github.com/Tuna0307/LW-Control/pulls
"@
if ($ReleaseLabel -eq 'MAP006-AVAILABLE') {
    $scopeStart = $readme.IndexOf('MANUAL MAP SCAN (MAP-005 review candidate)')
    $scopeEnd = $readme.IndexOf('Build prerequisites (only for DEVELOPERS', $scopeStart)
    if ($scopeStart -lt 0 -or $scopeEnd -le $scopeStart) { throw 'Could not identify the original MAP-005 release-scope text.' }
    $availableScope = @"
MAP-006 AVAILABLE MAP SCOPE — REVIEW CANDIDATE
City, Resource, Monster, Truck, Train/Railway, and Secret Task/Dispatch have
fresh, positive current-client full-world scan evidence. The latest server-2212
six-kind Fast run completed 2500/2500 blocks with zero failures and published
7060 City, 8003 Resource, 8850 Monster, 459 Truck, 2 Railway and 1710 Dispatch.
Counts are time-sensitive observations, NOT bundled private game data.

Available browsing includes saved City/Resource search/export, six-type tabs,
stored sort/page/filter/options, City mark/marked-only, and passive row coordinate
navigation. Genuine source-backed reads and controlled ownership tests supplement
the visible controls. Truck/Railway may show fewer unexpired rows than stored
counts. Game text metadata may be absent: a fallback Unknown resource/monster is
not a fabricated name.

Auto Scan supports bounded passive multi-server ordered scans and an explicit
Stop of the currently owned run. Native return-to-original-server was witnessed
on 2212/2213 and 2197 prior trials. A fresh cancelled first-server Truck run
retained all prior published six-type records, launched no second target, and
reported canceled without a false native return failure after the fix.

NOT INCLUDED IN THIS AVAILABLE-SCOPE ACCEPTANCE
Treasure seasonal/full eligibility and live claims are deferred by the owner;
Ghost positive acquisition is pending natural availability on 2026-10-15.
Treasure claim/status and Ghost plunder providers remain incomplete.
Live Dispatch/Truck plunder and alliance sharing are owner-only witnesses,
NOT completed worker tests. No live claims, plunder or sharing were attempted.
Earlier saved basic Treasure capture is retained but is not season/action proof.
The original encrypted scanner algorithm, fastest request method, full original
parity and independent simultaneous two-game support (Home F-07) are NOT claimed.
MAP-005/PR #7 remains a separate predecessor review. This ZIP is an unmerged
source-identified REVIEW CANDIDATE, not a published whole-Map release.

Owner action checklist: docs/MAP_006_OWNER_ONLY_VERIFICATION_CHECKLIST.md
in the source repository; do not use the packaged application for unverified
account-changing actions.

"@
    $readme = $readme.Substring(0, $scopeStart) + $availableScope + "`r`n" + $readme.Substring($scopeEnd)
}
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
    # Windows Compress-Archive may record backslash separators. Zip entry
    # identity is separator-insensitive for our Windows package, while the
    # executable and excluded-path checks must work on either spelling.
    $entries = @($zipEntries.Entries | ForEach-Object { $_.FullName.Replace([char]92, [char]47) })
    if (!$entries.Where({$_ -eq 'LW-Control-UI/LWBridge.Desktop.exe'}).Count) { throw 'Packaged executable missing from ZIP.' }
    if ($entries.Where({$_ -match '(^|/)(node_modules|tests|evidence)/'}).Count) { throw 'Fixture or research files included in runtime ZIP.' }
} finally { $zipEntries.Dispose() }
[pscustomobject]@{zip=$zip.FullName;bytes=$zip.Length;sha256=$sha;sourceCommit=$sourceCommit;fileCount=$fileCount;entries=$entries.Count} | ConvertTo-Json
Remove-Item -LiteralPath $stage -Force -Recurse
