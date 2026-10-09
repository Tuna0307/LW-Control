# COMPLETION-010-R1 independent stopped-checkpoint review

**Disposition: PARTIAL / CHANGES_REQUIRED. Worker remains stopped by owner.**

Reviewed `faec6f61c3fe7ddd529d5dca0155512d2c439899`, including `d6dd7618`,
`54ed0613`, `9f8291d3` and `faec6f61`. The starting tree was clean.
This review performs static/headless/inert checks and passive file/process
inspection only. It does not initiate another live pilot or dispatch a worker.

## Credit for completed work

- **LEAD010-01 accepted for bounded controlled scope.** The captured run/server
  is checked under the serialized Start/Stop gate. My independent actual
  command/control/provider/SQLite replay leaves the successor running, with no
  additional provider Stop. Worker five-group status tests also run freshly.
- **LEAD010-02 substantially corrected, with a remaining proof defect below.**
  The pilot now requires durable completion, nonempty results, twelve export
  headers/cell comparisons, reopened counts, exact Stop/exit/restoration/cleanup,
  and a nonzero failure path. Twenty-one pure proof-gate cases are bounded
  validator tests, not an executed failure-injected live runner.
- Saved live receipts report two current-client pilots on server 2212: City
  6,780 in each; Resource 8,010 and 8,011. Both final gates have no errors;
  their isolated roots are absent. Current installed script hashes independently
  match both preflights. The second receipt observes City staging 404 before
  Stop, durable cancellation, and zero remaining staging. This is credited as
  historical current-client evidence, not a fresh lead-run/original scanner oracle.
- Actual source and new checks support bounded Home readiness cleanup,
  configured/active-root separation, per-profile ordering/error isolation,
  loose query handling, enter-world polling and Dispatch-only limit fan-out.
  Nonselected profile owners still return unavailable; the ordering wrapper is
  not simultaneous multi-profile runtime implementation.
- The follow-up malformed-`markedOnly` and stale Auto due-admission corrections
  are preserved. Fresh native-boundary/Auto checks exercise current code.

## Required corrections

### LEAD010R1-01 — level-range coercion changes query meaning (P2)

`Map317CommandService.cs:817` transforms f64 level bounds to nonnegative int
limits. It drops `minLevel:0` and clamps `maxLevel:-1` to zero. In an isolated
actual command/SQLite dataset with levels null, 0, 1 and 3:

| Input | Recovered comparison result | Current result |
|---|---:|---:|
| `maxLevel:-1` | 0 | 1 (level zero) |
| `minLevel:0` | 3 (null excluded) | 4 (null included) |

Fractional and inverted positive-range controls pass. Authority: hash-matched
original search f64 parsing/comparison/swap at `0x3E27F6`, the d-map-decode
recovery and native SQL `level >= ?` / `level <= ?` literals. The original
runtime was not executed. Preserve the exact comparison/null semantics;
nonnegative clamping is not an equivalent internal adaptation.

### LEAD010R1-02 — running server changes outside the world scene (P2)

`Map317CommandService.cs:641` overlays any positive live server regardless of
world state. My actual `map_scan_status` test starts a server-317 run, then
returns context `{isInWorld:false, serverId:318}`. The response still reads but
reports 318. Recovered original `0xF9567` retains the captured server while
reading outside the world. Correct both returned status and stored-overlay
consumers without weakening the accepted predecessor run/serial guard or the
legitimate same-run in-world server-change failure.

### LEAD010R1-03 — native page envelope is silently int32-capped (P3)

`Map317CommandService.cs:855` converts the recovered i64 page to int32.
`page:3000000000` returns `2147483647`, rather than the original page value.
Original search `0x3E1144` applies a lower bound only. Queries may return empty
rows for this page, but their returned page still must match. Recover/preserve
overflow behavior for SQL offset separately; do not invent another upper bound.

### LEAD010R1-04 — cancelled-run publication proof reads staging (P2)

`Completion010LivePilot.cs:347` calls `Search(... ScanRunId:run)` and labels its
count `publishedForRun`. `MapStore.Search` chooses `scan_records` whenever that
field is present, so both that number and `remainingStaged` inspect staging.
The saved zero cannot independently establish absence of publication into
`map_records`. The previous completed population also makes a total-only check
insufficient. Snapshot/hash the published table before the cancelled run and
after Stop using a source-supported invariant, preserve the evidence, and add
a distinguishing corruption inverse. Do not claim completed publication based
on two staging queries.

The runner deletes its DB and workbook with the isolated root. No R1 `.db` or
`.xlsx` artifact is present in its evidence packet. Cell-match/query booleans can
be inspected as receipts, but the deleted data cannot be independently reopened
now. Future useful pilots should preserve compact reproducible dataset/workbook
evidence or authoritative content manifests before cleanup; do not rerun a full
world scan merely to repeat an already credited positive observation.

## Remaining ready work and true dependencies

The 47 Home / 86 Map derivative retains all 133 distinct rows, but inherits old
`priorDifference`, `remainingWork`, and readiness classifications. Its updated
assessment is bounded and does not close everything. The lead summary in
`docs/PROJECT_STATUS.md` is current; older worker continuation is historical.

Ready/static implementation remains: original launch reservation/descriptor and
failure producers/finalizer consumers; actual multiple owner runtimes; remaining
scheduler/day/history/Truck/Auto lifetime semantics; the corrections above; and
the packaged WinForms/WebView2 crash diagnosis. Live repair/restart/reconnect and
genuine EN/light/JA/dark canonical control evidence are unfinished. Original
encrypted Map controllers, protected capacity/lease inputs and original-runtime
comparison are distinct unresolved input/access dependencies. No service bypass,
guessed producer or synthetic entitlement is justified.

## Verification and preservation

Fresh command results: **44/44 exit zero**, recorded in
`lead-review-r1-2026-10-09/replay/results.json` by `run-audit.py` (including
fresh Release build/publish, canonical frontend/package checks, mounted App,
native/Map/recovery/root/adoption and new worker proof gates).
Original d-map byte verification also passes **62 checks**.
The independent probe records seven cases: three passing controls/accepted
ownership cases and four mismatches across the first three findings. General
passing suites do not override these distinguishing negatives.

Reference EXE hash remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
No game/clone process was present at passive checks, and the two worker pilot
roots are gone. Older lead negatives, baseline obligation sources, worker R1
records and production files are unchanged by this lead audit. Fresh audit JSON
is compacted without changing its values; old records are not reformatted.

One prior inert SQLite test root remains:
`C:\Users\chimw\AppData\Local\Temp\lwb317-lead010-map-f440eaf16f494633abf72bb0825f51f5`.
An exact-path PowerShell cleanup attempt after this owner's tidy request was
again rejected by automatic approval review with **blocked by policy**.
No bypass or alternate deletion path was attempted; it is listed rather than
concealed as cleaned. See `cleanup-rejection.json` in the new audit packet.

**Continuation:** keep the worker stopped. Preserve this checkpoint and discuss
the owner's questions before issuing a new assignment. No full Home/Map or
original pixel/native gameplay parity is accepted by this review.
