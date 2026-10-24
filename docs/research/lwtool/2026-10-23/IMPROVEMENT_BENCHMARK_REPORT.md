# LW-Control improvement benchmark and qualification report

Date: 2026-10-23

Branch: `codex/lwtool-full-improvement`

Assignment base: `85c9fc1b6e6f9a284f18fae216d69abb84900e81`

This report distinguishes measurements made in this programme from recovered
competitor facts and older accepted project measurements. It does not relabel
an unauthenticated launcher or an inert host as a connected game.

## Pre-change regression baseline

Before production edits, after restoring only lockfile-pinned UI dependencies
with `npm.cmd ci`:

| Gate | Result |
| --- | --- |
| `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release` | PASS, `HOME_FEATURE_DELIVERY_CHECKS_OK`, 98.7 s |
| `npm.cmd run check` in `src/LWBridge.UI-0.3.17` | PASS, 56.469 s |

The baseline covers existing lifecycle ownership/recovery/stale-successor,
transport, FEATURE-018, Map and all current UI tabs/locales.

## Lease parse cache

Changed production path: `src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs`.

The pre-change adapter calls `LeaseIsFresh()` inside a 10 ms worker loop. Each
call fully opens, decodes and parses `lease.txt`. The candidate caches only a
successful exact-session parse for up to 250 ms. Every cache hit still checks:

- the original `updatedAt` against the existing +/-5 s freshness rule; and
- current lease-file existence, length and last-write timestamp against the
  file snapshot that was completely parsed.

Delete/rewrite, instance change or due revalidation invalidate the cache. A
sharing-contended due parse still fails closed; no old cache value substitutes
for a failed current read.

`tools/lwtool_improvement_bench/LeaseCacheBench.csproj`, 3 trials x 40,000
validations, warm OS/JIT state, no game:

| Metric | Baseline full parse | Candidate | Change |
| --- | ---: | ---: | ---: |
| Median wall time | 1435.799 ms | 817.607 ms | ~1.76x faster |
| Median process CPU | 1406.250 ms | 796.875 ms | ~1.76x lower in this run |
| Source-derived full parses/s at 10 ms idle loop | <=100 | <=4 | 96% fewer |

Process CPU is Windows-accounting data and is secondary to wall time/call-count
evidence. The production cache-hit path still performs metadata checks, so this
programme does **not** claim 96% less total lease filesystem activity.

Distinguishing safety check:
`GamePipeAdapterLeaseChecks` proves initial exact-session admission, cache use,
immediate same-instance delete rejection, immediate stale replacement rejection,
successor identity fence and fail-closed due contention.

## Retained UI background work

Changed path: `src/LWBridge.UI-0.3.17/src/App.jsx`.

Before the change the five-second connection watchdog performed status, proxy
status and `local_config_get`. Config already has explicit owner admission/save
paths, so the periodic watchdog now performs status + proxy only. The config
poll generation increments only when a config-bearing read is actually issued,
so status-only watchdogs cannot retire an overlapping manual/owner config read.

Deterministic cadence change per selected profile:

- watchdog native calls: 36/minute -> 24/minute, 33.3% fewer;
- periodic `local_config_get`: 12/minute -> 0/minute;
- hidden retained Map aggregate summary polls: 12/minute -> 0/minute while a
  non-Map route is visible.

Map still refreshes on Map entry, profile change and scan completion. Native
status/proxy events remain the fast path and their watchdog remains five seconds.

## AFK one-Pump monster snapshot

Changed path: `tools/feature018_afk_native.lua`.

The AFK scheduler can evaluate multiple profiles across multiple free squads in
one one-second Pump. Previously each `Targets` call rebuilt the same
`available_monsters()` cleaned snapshot from `GetAllMarches` and native monster
metadata. The candidate lazily shares only that cleaned snapshot for the current
`engine.Pump()` and clears it immediately afterward.

Inert distinguishing fixture: 4 enabled profiles x 4 free squads, no matching
target:

| Metric | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| World-march enumerations in one Pump | 16 | 1 | 93.75% fewer |
| Sends | 0 | 0 | unchanged |

Per-profile `seen`/filter state remains live. Ordinary attack/rally paths still
re-read exact native target data, owner state and squad state immediately before
mutation, and uncertain/no-replay behavior is unchanged.

## Inert production named-pipe RTT and soak

`tools/lwtool_improvement_bench/PipeRttBench.csproj` uses the real production
`LWBridgeControlPipeHostState`, real Windows named pipes, production handshake,
registry, framing, request correlation and shutdown. The client is a task-owned
inert echo responder; there is no game, Lua bridge, adapter or automation.

500 authenticated read-only echo samples:

- P50: 0.211 ms
- P95: 0.3472 ms
- P99: 0.478 ms
- min: 0.1443 ms
- max: 27.3928 ms

Then the same authenticated route was held for 60 seconds with one heartbeat
every five seconds:

- 12 heartbeats;
- zero failed RPC sessions;
- process CPU: 328.125 ms over the 60-second observation;
- private bytes: 18,362,368 -> 18,354,176 (-8,192);
- working set: 43,941,888 -> 45,924,352 (+1,982,464);
- handles: 299 -> 300 (+1);
- threads: 19 -> 15 (-4).

These endpoint deltas are a bounded stability signal, not proof of zero leak;
the harness does not include a real game or adapter and therefore cannot replace
the assignment's desired connected-game soak.

## Cross-product performance limitation

No connected LwTool idle/start benchmark was performed. Active recovered LwTool
source proves connect and late-connect can auto-start AFK/chat engines, so a fair
idle run first requires disabling those through supported controls and verifying
the resulting admission. No task-owned background-safe equivalent connected
game session was available in this programme, and opening visible owner-facing
game/app windows only to collect a benchmark would violate the operational
boundary.

Therefore these remain **UNKNOWN cross-product** rather than inferred winners:

- cold/warm application Start-to-native-ready;
- Start-to-game/account-final-connected;
- 60-second equivalent connected-idle CPU/private/working set/disk/IPC;
- real adapter/game command RTT;
- matched LwTool versus LW-Control game process cost.

The programme still proceeds because the assignment explicitly permits
source-based cross-product comparison plus matched internal candidate/baseline
measurements when safe equivalent competitor execution is unavailable.

## Retained historical Map evidence

No Map scanner timing is re-labelled as a fresh programme measurement. Existing
accepted MAP-019 evidence remains the basis for retaining the current scanner:
camera-free point kinds ~16-24 s versus ~189-201 s camera; Truck ~52-79 s versus
~198 s; Railway ~60-61 s versus ~205 s; all-nine ~114-120 s. Rejected coarse and
parallel-poll variants lost recall, so this programme did not trade accuracy for
another timer improvement.
