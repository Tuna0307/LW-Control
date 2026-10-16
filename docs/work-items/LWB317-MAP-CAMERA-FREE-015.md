# LWB317-MAP-CAMERA-FREE-015: fast camera-free Map scanner (owner assignment, 2026-10-16)

Assigned by the project lead. Owner (CEO) goal: **each Map scan fast and accurate**. Owner target: 10–15 s per
category, and Trucks as fast as reliably possible. Owner accepted the anti-cheat risk of camera-free requests.

## Start here
- Branch `codex/map-owner-fixes-015` (pushed, HEAD 8add6064 or later). Do not use main or the merged PR #10 branch.
  Open a **draft** PR from this branch to main if you do not have one yet. Never merge or publish.
- Read `docs/MAP_015_CAMERA_FREE_SCAN_FINDINGS.md` first (all measured facts). Read
  `docs/MAP_014_OWNER_DEFECTS_HANDOFF_2026-10-15.md` for the current production scanner.
- Prototype (harness only, already working): `tools/map_014_lead_native/map014_fullscan_fixture.lua`, scenario
  `direct-full` in `tools/map_014_lead_native/Program.cs`, run via
  `LWBRIDGE_MAP014_FULLSCAN_FIXTURE=1 LWBRIDGE_MAP014_FULL_CONFIGS=... python tools/map_014_lead_native/run_owned.py direct-full`
  (rebuild `Native.csproj -c Release` after C# edits; the fixture plus wrapper must stay ≤ 24000 bytes).
- Decoder for the game's encrypted C# IL: `tools/map_014_il_decode/rgmd_disasm.py --type X --method Y`.

## Known facts (do not re-research)
- Request: build `CS.WorldGetBlockMessage.Request()`, fill bigMap=0, x, y, serverId, worldId=0, type=0, lod=0,
  index (Int32[] of AOI ids, ≤160), blockSize=10, firstTime=false, leftBottom, rightTop, battleFieldFirst=false;
  send with `CS.WorldGetBlockMessage.Instance:Send(r)` (pass the Request itself, not an object[]).
  AOI id = (y//10)*100 + x//10; tile index = x + y*1000 + 1. Add sent AOIs to `_curViewIndex` while scanning and
  remove the ones you added afterwards.
- One request in flight at a time: wait for `WorldPointManager.isRecvViewPoints == true` (reset it to false via
  reflection before each send; BindingFlags value 52), 1.5 s timeout. Pipelining loses point data; >160 areas fails.
- The client deletes points outside the camera shortly after a reply, so **read and keep records for each reply
  as it lands** (before the next send), not at the end.
- `ParseWorldGetBlock` drops replies whose serverId is not the current server: camera-free works only for the server
  the game is currently on. Remote/Auto server scans keep the existing server-switch flow, then use camera-free.
- Points: 63 linear chunks of 160 areas cover the world in ~16 s with counts matching the camera scan
  (Cities ~4176, Resources ~7990, Secret Task ~370, Ghost ~180, Monsters incl. Zombie Boss ~13.5k).
- Trucks: in the per-request march push (`push.world.march.world.get.new`, one per request), only near each
  request's rectangle. 625 requests of 40×40-tile rectangles with **one** area each = 256 Trucks (camera 180–266)
  but 148 s one-at-a-time. Overlapped at 20/s = 34 s, server answered all, but only 66 % read because each push
  replaces `WorldMarchDataManager` march set before the sampler sees it.

## Task A — production camera-free scan for every kind except Truck (do first)
1. Implement it inside the real probe `tools/current_live_resource_probe.lua` and the C# source
   (`src/LWBridge.Desktop/CurrentClientMapBlockSource*.cs`) as the default method for current-server scans, with
   automatic fallback to the existing camera scan if it fails (timeouts, errors, zero rows). Keep camera code intact.
   Probe limit: Lua chunk must stay ≤ 200 top-level locals (put new state on the module table).
2. Records must be identical in shape/fields to the camera route: names, levels, owners, Zombie Boss, Ghost plunder
   rewards, Secret Task rewards, Resource remaining amount, Monster protection, Black-tile flag. If a detail needs
   an extra per-record request, add it and include its time.
3. Stop must work mid-scan; after finish/Stop the camera, `_curViewIndex` and game state are exactly as before.
4. Target: all-kinds scan ≤ 20 s, single kind ≤ 20 s.

## Task B — fast Truck scan
1. Overlapped 40×40-tile Truck-only requests (one area each), capturing every march push **on arrival**
   (read-only; e.g. read the pushed SFS objects queued in `WorldMarchDataManager._messages` / its merge dict each
   frame, or another read-only spot you prove). Do not replace game handlers.
2. Find the fastest rate the server still answers fully (try 10/20/40 per s); keep Truck rewards/owner/quality
   fields like the camera route. Target ≤ 40 s with Truck count ≥ camera scan in the same session (allow natural
   drift: compare with two back-to-back camera rounds). If you cannot reach full count, ship the best accurate
   option and say so; never ship a fast but lossy default.
3. Camera Truck scan stays as fallback.

## Acceptance gates (lead will audit these)
- A/B accuracy in the same game session: camera scan vs camera-free for each kind (use
  `tools/map_014_lead_native/compare_ab_rounds.py`); loss must be no worse than camera-vs-camera drift.
- Timing table for each kind and mixed/all, real numbers.
- Real **packaged Windows app** (built ZIP from your final commit, extracted, real UI): a full scan for each kind,
  filters, Stop mid-scan, Clear, and the Home regression. Also live-confirm two earlier fixes: Ghost reward filter
  shows plunder share; Clear works while the game is on another server (home 2212).
- All existing checks pass (`tests/LWBridge.Desktop.Checks`, `tools/check_ghost_reward_lua.py`, other repo tests).
- Behaviour-difference ledger entry (OWN_DESIGN: camera-free method, why, user effect, evidence).

## Rules (unchanged)
Background only: no mouse/global keyboard, focus forcing, minimizing or control of owner windows. Keep the game in
Chinese. No claims, plunder, sharing, attacks, marches, spending, relocation or updater actions. Stop only your own
exact game session and restore scripts only after confirmed exit. Respect tool denials, never bypass them. No merge,
publication, force-push, reset or discarding work. Subagents allowed; one agent controls the live game at a time.
Diagnose → fix → retest yourself; do not stop for ordinary failures.

## Handback
Write `docs/MAP_015_WORKER_REPORT.md`: what was built, timing table, A/B accuracy table, packaged-app results with
artifact paths, ZIP path + SHA-256, anything not working with the exact reason. Commit, push, verify remote SHA.
Status line: READY_FOR_LEAD_AUDIT or PARTIAL.
