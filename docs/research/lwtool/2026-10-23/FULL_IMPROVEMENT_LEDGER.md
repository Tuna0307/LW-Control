# LwTool full comparison and LW-Control improvement programme ledger

Date: 2026-10-23

Implementation branch: `codex/lwtool-full-improvement`

Assignment base: `85c9fc1b6e6f9a284f18fae216d69abb84900e81`

Released production baseline: `main@45e151c87350c8b8cf4d046a889ac6a735c12556`

## Starting state

- Branch created from the research/assignment checkpoint; released `src/`,
  `tests/` and `tools/` were unchanged from the production baseline.
- Pre-existing `.claude/` remains unrelated and untouched.
- Recovered LwTool backend/UI/native DLL are reused from the ignored research
  workspace. No private payload/key/token material is copied into Git.
- No Last War or LwTool game session has been launched by this programme. This is
  intentional: there was no task-owned connected game, and opening a new visible
  competitor/game session would conflict with the assignment's background-only
  boundary while the owner uses the PC.

## Baseline regression receipts

After repairing only the lockfile-pinned UI dependency directory with
`npm.cmd ci` (no package-version or source change):

- Desktop delivery suite:
  `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release`
  -> PASS, `HOME_FEATURE_DELIVERY_CHECKS_OK`, 98.7 s.
- Full UI suite:
  `npm.cmd run check` from `src/LWBridge.UI-0.3.17`
  -> PASS, 56.469 s.

The desktop suite includes the existing exact-owner lifecycle, recovery,
stale-successor, route, FEATURE-018 and Map safety checks. The UI suite includes
the current Home, Map, Automation, AFK, City, Hotkeys, Mini Games, Settings and
responsive/localization checks.

## Measurement infrastructure selected

Reusable existing tools:

- `tools/r29_part_c_sampler.py`: passive CPU, working set, private bytes,
  I/O, handles/threads and runtime-file activity sampling.
- `tools/r29_measure_phases.py`: host descendant/process-tree phase capture,
  with game cost separated from controller/helper cost.
- `tools/home_004_pipe_recovery_probe`: inert production named-pipe admission,
  malformed-frame, idle, reconnect, concurrent RPC and shutdown checks.
- `tools/feature018_r30_package.py`: strong source-pinned package fencing,
  manifest/hash/private-data rejection patterns.

Known harness limitations that must remain explicit:

- refresh descendant identity during a long sample rather than snapshotting it
  only once;
- report p50/p95/p99 for RTT/start timing;
- process "other I/O" is not equivalent to named-pipe traffic and must not be
  labelled as IPC bytes without explicit counters.

## Whole-product decision ledger

This table is updated as each workstream closes.

| Area | Current decision | Reason / next decisive evidence |
| --- | --- | --- |
| Home launch/ownership/Stop | RETAIN | LW-Control exact PID/path/start/session/challenge Stop/restoration is stronger than LwTool normal image-wide Stop. |
| Home slow readiness | DEFER | LwTool has useful late-connect recovery. Safe LW adoption needs a new exact-owner pending journal state, Stop/restoration semantics and evidence that current bounded readiness actually fails on slow legitimate logins. |
| Host pipe authentication | RETAIN | Stronger explicit OS PID/image/token/profile/session admission; no reason to copy a looser shared-memory authority. |
| Adapter lease validation | IMPLEMENTED (`73b510ca`, `5a1fdc5f`) | Successful exact-session parse results are cached for <=250 ms, but each hit still checks current file identity and original 5 s timestamp freshness. Delete/rewrite/instance change invalidate immediately; due contention fails closed. |
| Adapter/Lua disk mailbox | DEFER | LwTool ordinary mmap path is structurally leaner, but xLua game-thread ownership is not yet proven for a direct callback path. A replacement needs bounded correlation/overflow/retirement and one authenticated command authority. |
| UI periodic status/config reads | IMPLEMENTED (`ccfd924f`) | Five-second status/proxy watchdog remains, while redundant persisted-config reads are skipped. Hidden retained Map pages no longer poll aggregate DB summary. |
| Map scanner | RETAIN | Current MAP-019 scanner has stronger source/coverage/freshness proof; LwTool normal/fast alternatives are filtered, subset or externally sourced and are not equivalent census methods. |
| Automation scheduler | RETAIN | Current R30 inactive zero-work gate, exact ownership, pending/uncertain tombstones and Stop-before-save are stronger than LwTool's broad reconnect/config-repair AFK loop. |
| Squads/AFK | IMPLEMENTED one-Pump snapshot (`26eb5255`); otherwise RETAIN | Current reservations/send-edge revalidation/no-replay semantics remain. Shared `available_monsters()` snapshot cuts repeated enumeration within one Pump; cross-engine squad reservation stays deferred pending an actual-adapter delayed-visibility fixture. |
| City Layout | RETAIN | Current snapshot/draft/validate/apply/status/cancel/restore pipeline has stronger completion semantics. |
| Hotkeys | RETAIN | Current exact game/window/session/account/text-input admission is stronger than competitor physical binding exposure alone. |
| Mini Games | RETAIN accepted paths | Current request causality/account fencing and completion checks are stronger. Removed/locked features remain removed. |
| Settings/persistence | RETAIN | LW-Control has cross-process lock, backup recovery, schema validation and atomic replace; competitor broad load failure can fall back to defaults. |
| Localization | RETAIN | Current full UI suite validates nine locales and no competitor method offers stronger accepted consistency. |
| Packaging | IMPLEMENT programme-specific source pin | `tools/lwtool_full_improvement_package.py` reuses the proven production fingerprint/bridge verification and adds exact pushed-HEAD, tracked source digest, privacy/test-UID gates, per-entry hashes, ZIP CRC replay and external receipt. |

## Implemented checkpoints

1. `73b510ca` — bounded adapter lease parse cache + inert benchmark/check.
2. `ccfd924f` — retained-UI periodic config/hidden-Map poll reduction.
3. `26eb5255` — one-Pump AFK cleaned-monster snapshot reuse.
4. `5a1fdc5f` — preserve immediate normal lease delete/rewrite revocation
   visibility and stable-snapshot parse caching.
5. Direct adapter/Lua queue and long late-connect were evaluated and deferred;
   Map scanner/City/Hotkeys/Mini/Settings accepted methods were retained.

Final source/package/document commit, exact remote push, clean package smoke and
draft PR are the remaining completion steps at this ledger checkpoint.

## Measured candidate evidence

- Corrected lease benchmark, 3 x 40,000 fresh validations: median exact
  baseline full-parse wall time 1435.799 ms versus candidate 817.607 ms
  (~1.76x).
  The production cadence model drops full text parses from an upper bound of
  ~100/s to ~4/s (96%); cache hits still perform file metadata identity checks.
- AFK 4 profiles x 4 squads, no matching target: shared world-march enumeration
  16 -> 1 in one Pump (93.75% fewer enumerations), with zero sends and unchanged
  send-edge native revalidation.
- UI status watchdog: 3 -> 2 native bridge reads per 5-second tick, i.e. 36 ->
  24/minute per selected profile (33.3% fewer watchdog bridge calls). Hidden Map
  summary polling: 12 -> 0/minute while another retained route is visible.
- Inert production authenticated-pipe benchmark: 500 read-only echoes P50
  0.211 ms, P95 0.347 ms, P99 0.478 ms. A 60-second heartbeat-only soak sent
  12 heartbeats with zero failed sessions; private bytes -8 KiB, working set
  +1,982,464 bytes, handles +1 and threads -4 between endpoint snapshots.
  This is host/named-pipe evidence only, not a connected-game measurement.

## Measurement boundary

No competitor connected-idle benchmark was run. Therefore the prerequisite
"prove competitor automation disabled before idle benchmarking" was never
bypassed: LwTool source shows connect/late-connect can auto-start AFK/chat, so a
live competitor idle run would first have required supported-setting shutdown
and verification. Because an equivalent task-owned background-safe connected
session was unavailable, cross-product Start-to-connected, connected-idle
CPU/RAM/disk/IPC and game/account-ready RTT remain unresolved. This programme
uses matched internal candidate/baseline evidence instead, as allowed by the
assignment when safe equivalent competitor execution is unavailable.

## Safety / authority checkpoint

- Background only; no owner-window control or global input.
- One primary live controller only if a later live test becomes necessary.
- No gameplay/account-changing action is currently needed for the selected
  shared-infrastructure improvements.
- Exact owner/auth/readiness/cancellation/restoration/account restrictions are
  release invariants, not optimization targets.
