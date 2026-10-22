# LWB317-MAP-SCAN-METHOD-REVIEW-019: is the current Map scanner really the best method? (2026-10-23)

Assigned by the project lead (Claude). Owner (CEO) question, in their words: "find out if this is really the
best method to scan the map for the Map tab". This is an **investigation with measured proof**, not a rewrite.
Answer the question with numbers; prototype anything promising; change production only in phase 2, after the
lead accepts your phase-1 report.

## Start here
- Base: branch `codex/feature-recovery-018` at `2b37bfc4` or later (it carries the current scanner plus all
  later fixes). Create **`codex/map-scan-method-019`** from it and open a **draft** PR to main. Never merge,
  publish or force-push. Push only at checkpoints.
- Read first, in this order:
  1. `docs/MAP_015_CAMERA_FREE_SCAN_FINDINGS.md` — the measured facts behind today's method.
  2. `docs/reviews/2026-10-16-MAP-015-LEAD-AUDIT.md` (R4 section) — accepted numbers for the current method.
  3. `docs/MAP_013_METHOD_SELECTION_FINISH.md` and `docs/MAP_013_NATIVE_CAUSAL_TRACE_CONTINUATION.md` — methods
     already tried and why they were rejected. **Do not repeat a rejected trial unless you change the reason it
     failed, and say what changed.**
- Current production path: `tools/current_live_resource_probe.lua` + `src/LWBridge.Desktop/CurrentClientMapBlockSource*.cs`.
- Tools you can reuse: `tools/map_014_lead_native/` (owned native runner, `run_owned.py`, `compare_ab_rounds.py`),
  `tools/map_014_il_decode/rgmd_disasm.py` (game C# IL), `tools/feature018_mini_lua.py` (game Lua),
  `tools/map_009_offline/rgmd_il.py` (metadata).

## The current method (what you are judging)
Camera-free `WorldGetBlockMessage` requests for the current server: one request in flight, up to 160 AOI areas
per request, 63 linear chunks cover the world (~16 s for point kinds), records read as each reply lands. Trucks
come from per-request march pushes using 625 small 40×40-tile rectangles, overlapped, with missing rectangles
re-sent (accepted: Truck-only 72 s, all nine kinds 159 s). Remote servers still switch server first, then scan.
Camera scan stays as fallback.

## Questions to answer (each with measured evidence)
1. **Speed**: for each kind (City, Resource, Monster, Zombie Boss, Secret Task, Ghost, Treasure, Railway, Truck),
   for "all nine", and for a typical mixed selection: time now vs the best alternative you can make work.
2. **Accuracy/completeness**: same-session A/B against the current method and against a camera scan (two
   back-to-back rounds to measure natural drift). Loss must be no worse than drift. Check fields too
   (names, levels, owners, rewards, remaining amount, protection, arrival times), not only counts.
3. **Alternatives to evaluate** (skip one only with a source-backed reason):
   - request shape: more/fewer areas per request, larger rectangles, `lod` 1/2 or "fold-up" points
     (`WorldPointManager.ParseWorldPointFoldUp`, `GetServerLod`, `UpdateLWAoi_Big3000`) — does a coarser LOD carry
     the fields we need?
   - safe parallelism: is >1 in flight really lossy for every kind, or only because of how replies are read?
   - kind-specific server queries the game itself uses (search/"find" messages, alliance/world lists, radar,
     minimap, event/activity managers) that return a kind's objects without sweeping the whole map.
   - Trucks: any list or push that gives all Trucks without 625 rectangles.
   - remote servers without switching server (re-check the `ParseWorldGetBlock` serverId drop at source level).
   - scanning only what changed since the last scan (incremental refresh) vs full sweeps.
4. **Load and risk**: requests per scan, bytes, game FPS/memory impact, and anything that looks unusual to the
   server (rate, request shape). The owner accepted camera-free requests; do not add new risk silently — report it.
5. **Verdict**: per kind, "current method is best" or "X is better by N s / N % with equal accuracy". A finite set of
   trials cannot prove no better method exists; say what you tried and what remains untried.

## Rules for live work (unchanged project rules)
- The owner is using the PC: **background only** — no mouse, keyboard, focus forcing, minimizing or moving owner
  windows. Start/stop only your own task-owned app/game sessions, with exact session identity and cleanup.
- Map scanning is read-only. **No game actions** of any kind (no marches, attacks, claims, plunder, sharing,
  purchases, relocation). Server 2212 MAIN account (owner's) is read-only; the test sub-account is UID
  4799432499002212 on 2212 — prefer it for live sessions. Never put UIDs in ZIPs or public text.
- Preserve the game language (Chinese), installed scripts (restore exactly after each owned session), and the
  current production scanner until phase 2 is accepted. Respect any tool denial or owner stop; do not bypass it.
- Subagents are allowed; only one may control live game/app sessions at a time.

## Deliverables
**Phase 1 (this assignment)** — report `docs/MAP_019_SCAN_METHOD_REVIEW.md`:
- one table per question above with real numbers and the session/receipt each number came from;
- prototypes kept as harness/opt-in code only (not the default), with how to run them;
- negative results recorded with their exact reason;
- a clear recommendation: keep current method, or adopt specific changes (expected gain, risk, effort).
Status line: `READY_FOR_LEAD_AUDIT` (or `PARTIAL` with the exact blocker). Push, update the draft PR, stop.

**Phase 2 (only after the lead accepts phase 1)** — implement the accepted changes as the default with the
current method as automatic fallback, full A/B proof in the packaged app, one ZIP from the final commit.

## Acceptance (lead will audit)
- Every claim in the report has a reproducible receipt (log/SQLite/JSON under `artifacts/`, ignored) and the
  command that produced it. Same-session A/B for accuracy claims. No fixture-only "positives".
- Existing gates still pass: `python tools/feature018_all_lua_checks.py ...`, `npm.cmd run check` in
  `src/LWBridge.UI-0.3.17`, `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj`,
  and the map_014/map_015 tests you touch.
- No production behavior change in phase 1.
