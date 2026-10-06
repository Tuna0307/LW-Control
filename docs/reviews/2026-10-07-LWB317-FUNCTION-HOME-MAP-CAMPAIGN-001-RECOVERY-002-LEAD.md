# Home/Map RECOVERY-002 independent lead review — 2026-10-07

Decision: **CHANGES_REQUIRED**, reviewed worker HEAD
`59d3cde5192f03d39ae01580e05d1a6249158b20`, direct origin verified at the same
revision. Starting tree was clean. The parent Home/Map feature campaign remains
**PARTIAL**. RECOVERY-002 delivered substantial genuine corrections; do not repeat
RECOVERY-001 as though its original defects were unchanged.

New evidence: `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-review-2026-10-07/`.
Next bounded assignment: `docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-003.md`.
No production file was modified by this review.

## Independently verified progress

The coordinator read the integrated production/test diff and reviewed three bounded
read-only source reports. Isolated runtime/evidence/backup roots now reach the
named Home/package tests; inactive cleanup requires session/challenge and preserves
foreign sentinels. Active profile replacement refuses owned lifecycle work, retains
the shared bridge host, and fences late admission. Folder selection is pinned to
its request backend/generation and shares the profile-swap gate during save.

App now fences older paired status reads, keeps the global Auto Launch preference
on profile selection/poll, merges pre-hydration scalar edits over native settings,
and separates action/runtime Auto errors. The native scan provider has explicit
run-scoped activation/terminal/disposal; lease initialization releases its handle
on failure. New native acquisition/publication/reopen, due plunder worker,
multi-server Auto and all-eight synthetic DTO tests materially improve proof.
Host-owned issue history now survives document reload. These fixes remain credited.

Executed by the coordinator on this turn:

- Frontend `check`, fresh production `build`, `check:production-build`: PASS.
- Release Desktop.Checks compilation with `SkipCanonicalProductionUiBuild=true`:
  PASS, zero compiler warnings/errors. This build is not packaged-browser proof.
- Nine focused flags: profile owner, Home lifecycle, native Auto, native boundary,
  due plunder worker boundary, DTO matrix, campaign canonical, Map restart and root
  selection: all exit 0. Exact output and runner-results are in the new packet.
- Canonical Map check project: PASS (`LWB317_MAP_CHECKS_OK`).
- Full Release Desktop build with canonical frontend packaging: PASS, zero compiler
  warnings/errors. npm reported an esbuild install-script policy advisory; the
  build still completed. No policy setting was changed.
- Original target SHA-256: matched AGENTS.md exactly.
- Two exact-current frontend negative witnesses and three controlled native/I/O
  negative witnesses: all reproduce. Those cases distinguish holes absent from
  the passing general suites.

The read-only process inventory before execution found no LastWar/LWBridge process.
Native execution used the inspected isolated tests and temporary-root diagnostic
seams only. No game, real capture, helper process, updater or protected service was
launched. This does not claim owner's global files were exhaustively snapshotted.

## Acceptance-blocking findings

### R3-01 [P1] active cancellation/lease writes overwrite foreign runtime files

`OverviewLifecycleService.Close` calls `TryWriteStartCancellationMarker` for a
starting session (`:412–430`). `WriteStartCancellationMarker` unconditionally
overwrites shared `cancel-start.txt` (`:1519–1537`). `WriteLease` similarly overwrites
the lease (`:1490–1502`). Metadata checks on later deletion do not protect writes.

Coordinator proof installs a controlled starting identity in the actual lifecycle,
seeds foreign cancellation bytes in a temporary directory and calls actual Close.
The foreign bytes become the controlled owner's bytes. No helper/game is run.
The inactive-sentinel PASS is valid but does not cover this active case.
Enforce exact shared-file owner admission and preserve foreign destinations on
write/cancel/refresh. Retain current-client protocol paths; do not invent folders.

### R3-02 [P2] conditional deletion can delete a foreign replacement

`TryDeleteOwnedRuntimeFile` reads/validates bytes and then deletes the path
(`OverviewLifecycleService.cs:1628–1641`). Its instance-local lock does not serialize
a second owner replacing that shared path between the two operations.

Coordinator diagnostic drives actual Close and an existing DeleteFile test hook
that substitutes foreign bytes exactly between the successful metadata predicate
and the path deletion. The foreign replacement is deleted. This is a deterministic
permitted I/O interleaving, not evidence that any real owner file was deleted.
Protect the validated file identity/ownership transaction through mutation; add
contended-owner inverse proof. Rechecking a path without closing the race is not
an exact-ownership guarantee.

### R3-03 [P1] Stop cancellation after local commit can skip capture termination

`MapScanStateMachine.StopAsync` first cancels the durable local run, calls provider
Stop with the same caller token, swallows provider exceptions and publishes idle
(`MapScanStateMachine.cs:355–397`). Concrete provider Stop now throws on a canceled
caller token before signaling active capture (`CurrentClientMap317ScanProvider.cs:
174–186`). A caller canceled after local commit therefore leaves capture untouched;
a repeated Stop sees idle and skips the provider.

The coordinator executes actual state machine and actual current-client Stop with
an inert adapter, controlled capture token/task and local-sink cancellation barrier.
It records idle, an uncanceled capture token and no second provider Stop. Reflection
only installs diagnostic identities/handles; it does not release a lease or repair
the service. No real capture is claimed. The actual production interleaving is
source-confirmed; the instrumented token boundary is executed.
After committed local Stop, own exact-run terminalization independently of retired
caller cancellation. Preserve lease ownership until terminalization and prove
coherent subsequent Stop/Start through the actual native service boundary.

### R3-04 [P2] failed global Auto Launch edit rolls back to a differing profile gate

App correctly leaves global/local preference unchanged during polling, but failure
rollback uses `autoLaunchNativeCommittedByOwnerRef` (`App.jsx:857–860`). That mirror
can differ from global intent. With global=false and B native=true, enabling then
rejecting the write leaves global/storage=true rather than restoring false.

Coordinator executes the exact current updateAutoLaunch callback with controlled
rejection and owner refs; both visible and stored values reproduce the defect.
The package rejection case uses agreeing A values and does not distinguish this.
Keep last confirmed global intent separate from native profile gates; retain
serialized successful acknowledgements, concurrent edits and generation fencing.

### R3-05 [P1] pre-hydration array edits still discard saved configuration

The scalar patch correction is valid. However actual Add still builds an absolute
array from visible defaults (`MapDataPage.jsx:841–845`). Type toggles/removals do
the same (`:847–853`, remove at `:1034`). Coordinator hydration shallow-merges those
arrays over native state (`autoScanNativeCoordinator.js:53–64`, App merge `:468–473`).

Exact current Add callback + current App initializer/coordinator proof: native
saved targets=[317], visible defaults=[], user adds9, submitted targets=[9]. Source
original Add appends existing targets (`index-BVfnK1wp.js`, `wi` bytes359234–359284;
panel Add40544–40620); its profile config is synchronously available. The clone's
async adaptation must rebase operation intent, not erase unknown entries. Cover
ordered/deduplicated Adds, removals and type toggles during hydration/failure/retry/
profile retirement. Preserve immediate editability and the Auto last-type rule.

## Package/evidence assessment and limits

Both saved v4 PNGs were decoded and visually inspected by the coordinator: correct
EN/light1120×720 and JA/dark900×720, stopped/offline final lifecycle, durable inert
Map rows. The coordinator rebuilt identical UI source/artifact fingerprints
`cd86aec04987bf5683d38e27d05af5a4c26a773e3be65ea3dc5133463be2d150` /
`1ae836263c78106f014fcc44d23f9df5b6f2b22453f0462c9e968094419390c5`.
Both packets record zero **unexpected** issues; two expected history sentinels are
present. This is stronger proof than the earlier per-document-only assertion.

The coordinator's rebuild stamps AssemblyInformationalVersion with current
`59d3cde5`; the captured implementation package was built at `b2e6089f` before docs
closeout. The resulting current EXE/DLL hashes differ from historical captures.
That is not a demonstrated worker source/package mismatch. Captured records are
preserved; no complete desktop session was independently rerun this turn.

Native suites are isolated/inert proof, not live algorithms or current-client
successful acquisition. DTO fixtures are honestly synthetic source-derived cases;
some default/formula locators remain broad and should be tightened if those cases
are used as exact-contract authority. New native Auto cases do not establish every
possible replacement-Manual timeout boundary. These are bounded proof limits,
not excuses to invent results or start unassigned live research.

Protected Treasure claim/status, Ghost preparation, positive live Railway/Ghost/
Treasure populations, updater/OS execution and protected-original pixels remain
separate dependencies. Historical recovered-source/local UI acceptance is preserved,
with the current preference/config regressions requiring correction. No full
feature/whole-clone/LIVE_PROVEN acceptance is granted.
