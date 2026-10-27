# Inert mobile MarchData collection benchmark (2026-10-24)

This experiment isolates the repeated **native MarchData collection enumeration**
performed by the Truck/Railway scan. It exercises a deterministic Python model
of the source-shaped collection traversal. It neither runs the actual Lua
helper nor controls the game, native transport, scanner, installed scripts or
production settings. No scan speedup or native correctness qualification is
claimed from these numbers.

## Production anchoring and exact boundary

The source is `src/LWBridge.GameScripts/current_live_resource_probe.lua`:

- `train_march_aoi_records` near lines 3644–3829 obtains
  `WorldMarchDataManager.GetAllMarchesByCS`, enumerates current marches,
  rejects previously seen UUIDs and extracts typed Truck/Railway metadata.
- `_map015FastTruck.pump` near lines 7371–7413 currently invokes that collector
  every Update frame, including frames when `_lastGetBlockMsgTime` has not
  changed. Receipt attribution and fence ACK counts are tied to new stamps.
- The same pump near lines 7414–7487 issues 625 requests at 20/s, fences 25
  requests, and repairs all ambiguous members of an incomplete fence with
  only one request outstanding at a time (at most two bounded repair passes).
- `_map017CameraFreeMarchReady` near lines 3858–3901 separately verifies that
  `_lastHandleBlockMsgTime` is at or beyond the received marker. The March
  handler runs after receipt and before the actual cache population.

`source_guard()` checks these source anchors and hashes the current Lua before
running. A source change that removes the checked constructs fails closed.
The experiment's Python collector reproduces the **enumeration decision**, a
typed-record subset, per-UUID first-seen behavior and both positive arrival
deadlines; it does not execute the real xLua reflection or all train fields.
The machine had neither a Lua command-line interpreter nor `lupa` available,
so this is explicitly a semantic model and must not be called an extracted-
helper execution benchmark.

## Reproduction

From repository root, using Python 3.12 (only standard-library dependencies):

```powershell
python -m unittest tools/research/map_019_review/test_mobile_hotpath_bench.py -v
python tools/research/map_019_review/mobile_hotpath_bench.py --repeats 5 --output artifacts/map019/mobile-hotpath-inert-benchmark.json
```

The output JSON is ignored evidence, not a new tracked production artifact.
The benchmark alternates baseline/candidate execution order, builds the
immutable transport trace before taking measurements, recreates a fresh march
cache for each run, and measures CPU time (`process_time_ns`), elapsed wall time
(`perf_counter_ns`) and peak Python allocation (`tracemalloc`). The allocation
metric is **peak traced Python memory**, not native heap, process RSS or wire
bytes. These instrumented measurements include Python fixture-loop overhead.

## Fixture contract

- Synthetic 60-frame/s Update schedule with one paced send every 3 frames:
  **625 initial requests**, 20 sends/s and 25-send fences.
- Two different initial fences lose one push each. Because a native push lacks
  an echoed rectangle identity, **neither incomplete fence certifies any of
  its 25 rectangles**. The fixture then performs **50 separately attributed
  single-outstanding repairs**. It yields 675 sends, 673 observed pushes,
  exactly 625 certified rectangles and zero unproven rectangles.
- 400 unrelated background marches, transient typed Truck/Railway entries,
  reward/quality/power/owner fields and UUID deduplication. Cache population
  occurs one Update frame after the corresponding handler marker. Some Trucks
  expire at capture, and some expire by final managed-style publication. Railway
  is retained independently of Truck arrival expiry.
- Baseline enumerates the native-like dictionary every frame. Candidate
  enumerates initially, when the handler marker advances and on the following
  frame. All transport events and typed source rows are identical between
  policies. This does **not** modify send rates, fences, repairs, ordering or
  camera/session restoration behavior.

The positive fixture compares full canonical JSON for the modelled typed fields,
separate Truck/Railway counts and SHA-256, and checks every fence's exact ACK
and verified-rectangle ledger. The negative fixture inserts a brief cache-only
row without a new receipt/handle marker. The baseline catches it; the candidate
misses it. Therefore it is **unsafe to ship handled-marker gating** without
independent source proof about all possible cache mutation paths and row
lifetimes. The test is a counterexample, not evidence the real game emits that
event sequence.

## Five matched synthetic rounds

`artifacts/map019/mobile-hotpath-inert-benchmark.json` was produced on Windows
using source SHA-256 `1b311d402427c2682c18570298c3c959209a8706af11440bfeffec403fb13fba`.
The source file was being edited concurrently by a separate owner during this
investigation; the hash names only the inspected version. The benchmark source
itself remained isolated and does not measure those concurrent production edits.

| Metric (instrumented Python fixture) | Baseline per frame | Handler + 1 frame |
| --- | ---: | ---: |
| Median wall, 5 rounds | 1,588.810 ms | 794.352 ms |
| Median CPU, 5 rounds | 1,562.500 ms | 796.875 ms |
| Median peak traced allocations | 267.8 KiB | 267.8 KiB |
| Complete dictionary snapshots | 2,775 | 1,347 |
| Examined current March objects | 1,111,001 | 539,469 |
| Qualified final Truck / Railway rows | 116 / 9 | 116 / 9 |
| Final typed-row SHA-256 | `5b360218…b7077dca27` | identical |
| Verified rectangles / repairs | 625 / 50 | 625 / 50 |

The CPU ratio is 0.510; wall ratio 0.500 on this Python simulation. Equal peak
traced memory is unsurprising because both policies retain the same final rows
and rebuild the same synthetic cache. The gain measures fewer Python collection
iterations, not verified xLua cost or game/network wall time. The accepted real
all-ten direct evidence separately includes ~56.5–57 seconds for the typed
mobile sweep, with ~41–42 seconds recorded as reply waiting. The present
simulation cannot infer how many seconds a real scan would save.

Six distinguishing tests pass: production-anchor drift detection; full-fence
and selective repair accounting; exact typed field/expiry equivalence; a
counterexample for cache-only changes; seconds/millisecond expiry conversion;
and invalid input rejection. The existing
`test_march_idle_snapshot_hypothesis.py` also passes independently.

## Engineering decision

Treat this as a **diagnostic candidate only**. Before any production change,
acquire real per-frame evidence of received/handled stamps, current march cache
row lifetimes, same-stamp updates and amount of reflection time, with exact
owned native cleanup. The prime may then compare unchanged per-frame collection
and a source-proven alternative in the same session, verifying all 625 rectangles,
Truck/Railway identities, metadata and expiry against natural run-to-run drift.
Retain the qualified **20 sends/s, fence25**, typed admission and guarded
fallback. Any latency estimate must come from a later real matched test.
