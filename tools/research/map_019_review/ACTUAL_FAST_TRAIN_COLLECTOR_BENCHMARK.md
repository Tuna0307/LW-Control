# Actual Lua collector old/new: six-run inert benchmark (2026-10-24)

**Scope:** PASS for canonical typed-record parity; not game-backed performance qualification. No app, game, package, installed script, service or remote tests invoked. No production edit or commit by worker-2.

Command: python tools/research/map_019_review/bench_actual_fast_train_collectors.py

Complete run/snapshot metrics: tools/research/map_019_review/actual_fast_train_collector_benchmark_20261024.json (UTF-8).

Old collector is extracted from git show 3da98523:src/LWBridge.GameScripts/current_live_resource_probe.lua; SHA256 c9f65ad3898895d53d1ccf0d6057dc2686c5b432bc996c5373f86637818f85a4.
Candidate is the actual current worktree collector SHA256 511647cfb09af777a37fe794a7dbb981eb6c555878d1e07c5e1ceb5e5a3bcb26.
Source remained unchanged during the benchmark and post-run hash inspection.

Uses actual unmodified old/new Lua collector fragments plus the actual production Truck expiry helper, via local Lua 5.3 and the existing FAKE_GAME from tools/live-harness/test_fast_train_seen_gate.py. Shared deterministic mock TrainData is extended with Truck/Railway typed ownership, alliance, goods and rewards. Six ordered-pair iterations/version/scenario, alternate AB/BA execution order, 30 synthetic moving frames/run. Timers exclude Lua loading/parsing and record canonicalization. Every returned typed field and record across all frames is recursively compared and SHA256 matched between versions and across repeats, including Truck arrival rejection, Railway inclusion, seen/unseen moved UUIDs, null/fallback types and reward structures.

| Mock case | Marches | Old 30-frame median wall | New median | Reduction | Type reads old -> new |
|---|---:|---:|---:|---:|---:|
| Truck low seen | 200 | 13.989 ms | 11.963 ms | 14.48% | 6,502 -> 600 |
| Combined high seen | 850 | 122.639 ms | 105.881 ms | 13.66% | 28,547 -> 3,180 |
| Truck very high seen | 1,800 | 177.052 ms | 128.290 ms | 27.54% | 55,635 -> 2,160 |
| Camera no seen | 160 | 133.421 ms | 126.894 ms | 4.89% | 9,529 -> 4,800 |

First/last full typed rows: 17/17, 101/102, 55/55, 158/158. Post-expiry admitted rows: 11/11, 74/76, 35/35, 117/117. All versions and repetitions had identical canonical typed hashes.

CPU time uses Windows Python process_time_ns with coarse 15.625 ms quantum: old -> new 30-frame median CPU (ms) 15.625 -> 0 (low-seen), 117.188 -> 93.750 (combined), 171.875 -> 125 (very-high-seen), 85.938 -> 125 (no-seen). These CPU estimates, particularly small/no-seen, are not reliable precise estimates. Raw optional Lua GC KiB changes in JSON vary across collector cycles and do not represent retained memory.

Supplemental tests: python -m unittest test_fast_train_seen_gate test_map015_fast_truck_r4 -q (run from tools/live-harness) -- **28 tests pass**. git diff --check clean.

Only a genuine controlled 625-rectangle, 20/s, fence25, typed-complete game/package benchmark can qualify reducing the ~57-second Truck capture. Inert collector percentages **must not** be extrapolated to full scan wall time.
