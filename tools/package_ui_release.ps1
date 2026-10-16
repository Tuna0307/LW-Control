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
MAP-005/PR #7 is accepted, merged and published as manual-map-v0.5.0. This ZIP is an unmerged
source-identified REVIEW CANDIDATE, not a published whole-Map release.

MAP-006 R1 CROSS-SERVER AUTO STOP AND SCAN-METHOD MEASUREMENT
An independently reproduced cross-server Auto Stop race has been corrected:
the exact native scan Stop/lease is settled before return-to-origin. Genuine
client 2212 -> 2213 Stop -> 2212 returned successfully, with cancelled cycle,
zero published partial data and no return error. Held controlled failures,
retirement and successor ownership tests also pass.
Measured genuine Truck-only Fast baseline on server 2212: 97.887 seconds,
340 application-to-Lua region probes, 0 retries, exact 10000 AOIs, 178 unique
Truck records. These are NOT game network request counts. Reflected direct
SendAoiRequest timed out without fresh evidence; official fresh Train-list
refresh gave zero current-server 2212 Trucks. The full baseline remains in
use; a globally fastest or original encrypted-algorithm claim is NOT made.
See docs/proof/MAP-006-R1-STOP-EFFICIENCY-2026-10-11.md in source.

Owner action checklist: docs/MAP_006_OWNER_ONLY_VERIFICATION_CHECKLIST.md
in the source repository; do not use the packaged application for unverified
account-changing actions.

"@
    $readme = $readme.Substring(0, $scopeStart) + $availableScope + "`r`n" + $readme.Substring($scopeEnd)
}
if ($ReleaseLabel -eq 'MAP007-PROVIDERS') {
    $scopeStart = $readme.IndexOf('MANUAL MAP SCAN (MAP-005 review candidate)')
    $scopeEnd = $readme.IndexOf('Build prerequisites (only for DEVELOPERS', $scopeStart)
    if ($scopeStart -lt 0 -or $scopeEnd -le $scopeStart) { throw 'Could not identify older Map release text.' }
    $providerScope = @"
MAP-007 GHOST AND TREASURE PROVIDERS — CONTROLLED REVIEW CANDIDATE
This release candidate preserves accepted main: single-game Home, released
City/Resource, six-kind Map scanning/browse/export/marks and proven passive
cross-server Auto Stop/return. It is not a whole-Map functional release.

GHOST: Source-backed GhostreconPointInfo task preparation is available via
the Ghost tab's 'Prepare Ghost tasks' (not an armed plunder job). Positively
validated server/owner, source class, protection/expiry and stolen capacity
are prepared without any game attack. A dedicated job arm/result boundary
prevents Ghost reaching Dispatch transport, and correlates exact server,
owner server, Ghost UUID, attempt and launch session in inert native tests.
The current v24 Ghost attack/terminal-result game producer has NOT been proven:
actual Ghost execution remains unavailable; preview success is not plunder.

TREASURE: current-client read-only Treasure state inspection supports a status
snapshot, controlled 100-row batches and own player/server identity checks.
Single/Boxes/Season 'Preview' controls display tentative eligible and blocked
counts, with optional known Lucky ordering; they send NO claim request.
Daily caps, reset, cooldown, current v24 claim request/terminal reward and
actual inventory change are not verified. Real Treasure claim buttons are
not enabled. The owner has deferred full season-dependent Treasure testing.

LIVE OWNER-ONLY PENDING: Ghost positive population expected no earlier than
2026-10-15, actual Ghost plunder, Treasure claims/rewards, Dispatch and Truck
plunder, and alliance sharing. No live actions were performed by this worker.
See docs/MAP_007_ACTION_PROVIDER_HANDOFF.md and owner-only checklist in source.
The original licensed UI, two independent official games (Home F-07), full
original protected algorithm equivalence and full Map remain unaccepted.
This is an unmerged, source-identified review candidate. Do not publish.

"@
    $readme = $readme.Substring(0, $scopeStart) + $providerScope + "`r`n" + $readme.Substring($scopeEnd)
}
if ($ReleaseLabel -eq 'MAP008-SCAN') {
    $scopeStart = $readme.IndexOf('MANUAL MAP SCAN (MAP-005 review candidate)')
    $scopeEnd = $readme.IndexOf('Build prerequisites (only for DEVELOPERS', $scopeStart)
    if ($scopeStart -lt 0 -or $scopeEnd -le $scopeStart) { throw 'Could not identify older Map release text.' }
    $scanScope = @"
MAP-008 SCANNER METHOD AND MAP-007 LIFETIME CORRECTION — LEAD REVIEW CANDIDATE
Current-client v24 source-built passive Map acquisition supports City, Resource,
Monster, Truck, Railway, Dispatch, Ghost and Treasure selections. The scanner
now keeps the camera on the owned target until the correlated native response
is captured, including scans without Resource selected. An independently
confirmed current-client omission showed that earlier same-tick camera
restoration could report full AOI coverage while silently dropping known City,
Resource, Monster and Dispatch rows. Previous short non-Resource scan times
were therefore excluded from accuracy-qualified performance comparisons.

Normal conservatively targets native AOI gaps; Fast may take measured-wide
coverage steps when fresh response geometry permits. Camera mutations remain
serial and native fresh response ownership and 10,000-cell AOI union coverage
are enforced. All selected spatial kinds can share one native sweep, followed
by their necessary type-specific field hydration. Individual and mixed v24
Normal/Fast scans have been compared with real source timing, measured staging
and publication, bounded CPU/memory sampling and independent positive native
field witnesses. An additional four source-owned ALL-EIGHT scans used alternating
Normal/Fast/Normal/Fast selection: 210.651/207.424/207.905/206.860 seconds.
Each covered 10,000 fresh AOIs, published all 2,500 logical blocks and issued
340 serial native camera probes, with zero failures. On the second pair,
positive native source and stored results matched for all seven populated kinds,
including genuine Railway and ordinary Treasure; Ghost was not positive.
The second paired strict-source audits passed with zero field discrepancies.
Normal (n=2) median 209.278s; Fast (n=2) median 207.142s; only one pair had
strict witnesses for every positive kind, so no meaningful repeatable speed
advantage is established. Actual home-view restoration cost measured
89.981-90.460 seconds per scan, about 49.5 percent of native reply duration.
This observed cost is NOT a proven safe saving: alternative consecutive target
requests remain unqualified; exact serial native restoration stays enabled.
Lua AOI commands are not a measured count of network packets. Results vary
as moving objects expire or spawn; AOI coverage is not perfect world recall.

The Map's production Resource and Monster tables/filters now resolve actual
game-owned text keys from the selected admitted installed-game locale files,
with English fallback and bounded per-request load. A previous App handoff
mistook native game_root_status.root for path, silently disabling native names;
this source corrects that bug and shows an explicit unresolved-root error.
Current installed-game Resource and Monster names were confirmed in actual
Windows native Map UI in simplified Chinese, English and Japanese after
reopening stored scanner records. Current language-specific names, game build,
session ownership and fallback can still vary with the installed game.
Consult docs/MAP_008_SCAN_METHOD_CONTINUATION.md and the compact source proof
for exact current qualification, Auto cycles and open acceptance gates.

Ghost preparation and Treasure claim preview are read-only/non-executing.
MAP-007 retirement now releases only the deferred operation's own busy state,
without allowing an obsolete tab/server/API result to surface or to clear a
new owner's result. Live plunder, attack, sharing, marches and Treasure claims
have NOT been exercised. Do not interpret previews as executed rewards.

This candidate is for independent lead review and owner testing AFTER that
review. It is not a whole-map feature parity or original protected scanner
equivalence certification. For exact source limits, independent witnesses,
additional performance measurements, and open acceptance gates consult
docs/MAP_008_SCAN_METHOD_CONTINUATION.md in the source repository.
Only operate on one authorized official current-client session. Start/Stop
are exact-owner controls; keep Auto disabled when finished and verify
the current server and return-to-original after multi-server cycles.

"@
    $readme = $readme.Substring(0, $scopeStart) + $scanScope + "`r`n" + $readme.Substring($scopeEnd)
}
if ($ReleaseLabel -eq 'MAP010-BACKGROUND' -or $ReleaseLabel -eq 'MAP011-RECEIVER') {
    $scopeStart = $readme.IndexOf('MANUAL MAP SCAN (MAP-005 review candidate)')
    $scopeEnd = $readme.IndexOf('Build prerequisites (only for DEVELOPERS', $scopeStart)
    if ($scopeStart -lt 0 -or $scopeEnd -le $scopeStart) { throw 'MAP-010 release scope boundaries missing.' }
    $scope = @"
MAP-010 BACKGROUND ACQUISITION — UNMERGED LIMITED REVIEW CANDIDATE
This package preserves the earlier accepted single-game Home and the MAP-008
accuracy-preserving v24 Map baseline (eight selectable kinds, individual/mixed
scans, existing passive multi-server Auto Stop-before-return). The baseline
holds its temporary view until the source-owned target reply and confirms
restoration before requesting the next region. This is NOT a new faster scan.
The earlier source-led CPU grouping change is preserved, but its measured
effect on wall time has not been separately established.

Installed-game Resource and Monster labels are shown from the actual
current game's locale data. Loading, missing-game-root, failure and retry
feedback is translated in all nine supported clone languages. The normal
Map now permits browsing published saved servers when disconnected; it
does not invent an active game/server connection or freshness.
The exact isolated production-app EN/light and JA/dark native WebView
checks passed with genuine stored data. Neither test changed the official
Last War Chinese-language setting.

MAP-010 optimization trials were bounded and non-account-changing.
One existing coverage region proved a fresh 30-AOI target with 22 Resource
rows and exact home restoration. Direct SendAoiRequest and camera-independent
SendViewRequest did not return qualifying fresh data. A two-region/no-home
intermediate candidate captured the first target but the second reply did
not prove the requested AOI. Therefore no acquisition shortcut is enabled.
Do NOT infer the earlier ~90-second per-scan home cost is safely removable.
No paired full eight-kind/individual/mixed/two-server speedup benchmark was
performed for a qualifying MAP-010 candidate, because none passed the small
response/coverage/restoration admission. Normal/Fast have no new claim of
repeatable advantage or global optimality.

Ghost positive population, full seasonal Treasure and actual claim/plunder/
sharing remain deferred or owner-only. No claims, plunder, marches, sharing,
spending or relocation were performed. Whole Map, remote full-world acquisition
without switching server/world, and simultaneous multiple official game
processes are NOT accepted. Source/proof and exact follow-up dependencies:
docs/MAP_010_BACKGROUND_ACQUISITION_CONTINUATION.md. This is an unmerged
review candidate, not a published release.

"@
    if ($ReleaseLabel -eq 'MAP011-RECEIVER') {
        $scope += @"

MAP-011 LEAD CORRECTION AND ACTUAL RECEIVER TESTS — PARTIAL
LEADMAP010-01 is fixed: a change of selected native profile ID/generation
clears Map saved-server selection, options, rows and outstanding replies even
when the App reuses the same mapApi object. The stopped replacement owner
can browse only positive servers in its own published Map store. Current
offline server ID 0 is not replaced by an invented live connection.
Adversarial mounted A/B/A, held old reads, empty/failed store, same-server
owner changes and offline/online transitions pass.

LEADMAP010-02 R11 waited for the entire eight-second receiver deadline after
the second WorldScene camera tile settled. Its nine current AOIs never
included the required second target; old flags-ready and network activity
were rejected. The distinct read-only R12 known remote-server point-detail
request returned a point-ID match and unchanged active server/camera, but its
detail source server was 0, not the requested 2213. This is NOT proof of
remote server acceptance, still less unknown-object/full-map discovery.
No new camera-free, consecutive-region or remote-full-world production path
is enabled; the existing correct serial scanner is preserved.

R11/R12 private trial code/data is excluded. See
docs/MAP_010_BACKGROUND_ACQUISITION_CONTINUATION.md and
docs/proof/MAP-011-RECEIVER-RESULTS-2026-10-12.json in the source.
This remains an unmerged lead-review candidate. No merge/publication.

"@
    }
    $readme = $readme.Substring(0, $scopeStart) + $scope + "`r`n" + $readme.Substring($scopeEnd)
}
if ($ReleaseLabel -eq 'MAP014-OWNER') {
    $scopeStart = $readme.IndexOf('MANUAL MAP SCAN (MAP-005 review candidate)')
    $scopeEnd = $readme.IndexOf('Build prerequisites (only for DEVELOPERS', $scopeStart)
    if ($scopeStart -lt 0 -or $scopeEnd -le $scopeStart) { throw 'MAP-014 scope markers missing.' }
    $ownerScope = @"
MAP-014 OWNER FIXES — SCOPED FUNCTIONAL REVIEW CANDIDATE (OVERALL PARTIAL)
The production Map317 scanner retains the accuracy-guarded shared serial
camera-assisted LOD0 sweep and exact per-region camera restoration. Normal/Fast
controls are unchanged. Independent real game City/mixed scan snapshots
returned to the original camera geometry, as did a separate Stop during
active acquisition. City record counts (about 4,030 in the latest sample)
remain measured observations, NOT a proven complete world census.

Map Resource supports level, confirmed full/partial/unknown amount, and
black/normal/unknown terrain filters without fabricating unknown native facts.
Truck supports an inclusive maximum escort power in millions and AND matching
for multiple distinct actual reward item identities. Current client game-owned
images have rendered in both Truck rows and the reward filter menu. Secret
Task owners show source-backed matching names (50 of 50 in the positive
genuine saved-data test), with explicit UID fallback for unknown names.
Native Truck passive Follow and visible Stop, stable genuine warzone choices,
and ownership/retirement inverses passed the packaged WebView.

One entire genuine packaged Windows UI Auto Resource cycle started at 2212,
scanned 2213 (7,996 saved rows), returned to 2212, browsed a stored 2213
row, issued an exact-owned real server-2213 Jump to tile (987,900), returned
home, and cleared only the saved 2213 dataset from the Auto Clear control
while retaining all 7,975 original 2212 Resource rows. The destructive
Clear command is denied during scan/Auto activity and verifies the current
live owner and exact published saved server before deletion.

No new faster scanner is accepted: post-camera-correction consecutive
target/no-intermediate-restoration and ZoomMax candidates failed independently
qualified second-region replies. Direct AOI and per-kind source lists do not
establish full-world record completeness. Accuracy takes priority over speed;
no universal fastest-method or population completeness claim is made.
Seasonal Scattered Supplies (source WorldSuppliesPoint, suppliesType=1),
Celebration Party and moving Doom Walker require an actually spawned
source-correlated live row before claiming positive end-to-end behavior.
No plunder, marches, claims, sharing, spending or other account-changing
operations were performed.

Raw native evidence and actual game assets stay private in the controlled
workspace, not in this package. The detailed 13-issue and scanner-method
assessment is docs/MAP_014_SOLO_REVIEW_CANDIDATE.md in source. Prior
negative trials remain preserved, not reinterpreted as successes.
This is an unmerged, unpublished candidate for independent lead review.

"@
    $readme = $readme.Substring(0, $scopeStart) + $ownerScope + "`r`n" + $readme.Substring($scopeEnd)
}
if ($ReleaseLabel -eq 'MAP-SCANNER-TEST') {
    $readme = @"
LW-Control — current verified scanner test build
Source commit: $sourceCommit

Extract the WHOLE ZIP into a writable Windows x64 folder. Run Launch LWBridge.cmd.
Requires .NET 10 Desktop Runtime, Microsoft Edge WebView2 Runtime and Python
for game integration. Auto Launch Game is ON by default; normal startup can
launch Last War. Close another copy of LW-Control before starting this build.

SUPPORTED TEST SCOPE
Single-game Home and the established camera-assisted shared Map scan are retained.
City, Resource, Monster, Truck, Railway and Dispatch have genuine acquisition
evidence. Selected types share one discovery sweep with separate enrichment.
Spatial acquisition waits for the native response before restoring the camera.
Resource/Monster names use installed-game locale data. Saved server browsing
and profile changes retire obsolete responses. Auto Stop settles before return.

Normal and Fast remain available. Neither has a proven repeatable material speed
advantage. Prior full-world measurements were approximately 3–3.5 minutes/server;
this build does not claim a new speed improvement or perfect population recall.
Remote full-map discovery uses the supported server-view switch-and-return route.

NOT COMPLETED
Ghost positive acquisition and full seasonal Treasure qualification remain pending.
Ghost preparation and Treasure inspection/previews do not execute attacks or claims.
Native Ghost/Treasure terminal actions remain unavailable. Dispatch/Truck plunder
and alliance sharing have not received owner live-action acceptance. Do not treat
this scanner test build as certification of those actions or simultaneous games.

Camera-free and consecutive-region experimental alternatives remain disabled and
their diagnostic observer/runner is not shipped. No private database, profile,
game scripts, reverse-engineering tools or evidence is bundled.

Repository: https://github.com/Tuna0307/LW-Control
Review and limits: docs/MAP_SCANNER_TEST_ACCEPTANCE_2026-10-13.md
"@
}
if ($ReleaseLabel -eq 'MAP015') {
    $readme = @"
LW-Control MAP-015 — camera-free scanner review candidate
Source commit: $sourceCommit

Windows x64: extract the entire ZIP to a writable folder and start
LW-Control-UI/Launch LWBridge.cmd. Requires .NET 10 Desktop Runtime,
Microsoft Edge WebView2 Runtime and Python for live game integration.

CURRENT SCOPE
For complete scans of the current normal-world server, the scanner
attempts direct AOI collection without moving the camera. Truck uses a
separate 625-rectangle march-push sweep, initially at 20 requests/s,
with a 15 requests/s retry after a lost native push. Only game-owned
train.type=Truck records with production fields can enter Truck rows;
Railway remains separate. Camera scan is the fallback after failed,
empty or unverified direct acquisition with proven native cleanup.
Other existing Home, Map, saved filters, Stop and server-switch
behaviors remain available.

EVIDENCE AND OPEN TARGETS
Earlier exact-owned native R2 trials proved the 625-rectangle transport
at up to 20/s and the full eight-kind non-Truck scan in 46.36 s. The
Truck fast route is subject to current exact-session qualification and
source-stamped packaged UI acceptance, detailed in the worker report.
The <=20 s all-kind target and <=40 s non-lossy Truck target are NOT
certified without independent current receipts. Moving records can change
between two full-world sweeps.

This is a local review candidate for audit, not a merged or published
release. It includes no private game data or account credentials. Never
use Map actions such as claim, attack, march or plunder unintentionally.
Review source: docs/MAP_015_WORKER_REPORT.md
"@
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
