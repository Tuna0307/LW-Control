# LWB317-MAP-OWNER-FIXES-016: owner test feedback on the merged fast scanner (2026-10-16)

Assigned by the project lead after the owner (CEO) tested main `be19b9a8` (MAP-015 merged, PR #11).
Branch: `codex/map-owner-fixes-016` (created from main). Open a **draft** PR to main. Never merge or publish.
Read first: `docs/MAP_015_CAMERA_FREE_SCAN_FINDINGS.md`, `docs/reviews/2026-10-16-MAP-015-LEAD-AUDIT.md`,
`docs/MAP_015_WORKER_REPORT.md`. The owner's screenshots showed: home 2212, current game server 2211 (cross-server),
Auto Scan enabled with target 2211 / 20 min / Fast / Secret Task, and the Scheduled Plunder tab.

Owner wording is quoted; every item must be reproduced in the real packaged app first, then fixed and retested.

## Bugs
1. **Launch opens the game, closes it, opens it again.** "everytime when i click launch game, it will open up the
   game and then close the game and then open up the game again". Find the cause (e.g. script install/restore,
   readiness retry, admission restart) and make Launch start the game once. If a restart is truly unavoidable,
   explain why in the report and hide it from the user where possible.
2. **A scan starts by itself after launch.** "after launching the game it will start to auto scan the map, when i
   did not even click scan map". Likely the saved Auto Scan schedule firing on connect, but it showed as a Manual
   Scan in progress (Secret Task, server 2212). Required: nothing scans unless the user pressed Start/Run now, or
   Auto Scan is enabled — and then its first run waits for the interval (no immediate run on launch) and the UI
   clearly says it is an automatic scan. Verify which one happened and fix.
3. **Clear Map Data leaves servers behind.** "sometime still see some server name remain in the list". After Clear
   for a server, that server must disappear from every Saved server list (Manual, Auto, other tabs) unless it still
   has data. Check all tables/caches that feed the list.
4. **Train scan fails on 2211.** The owner was on 2211 next to a live UR 同盟貿易列車 (Railway, alliance trade train,
   "下一站時間 01:19:01"). Train scan "fail, and go back to scanning screen by screen, end up it cant find also".
   Find why the fast path failed and why the camera fallback missed it (cross-server state, train-list refresh
   limited to own alliance/server, type filter, etc.). Train must be found reliably on the current server.
5. **Auto Scan shows "The action could not be completed."** when switching to the Auto Scan tab. Find the failing
   call and fix; any real error must say what went wrong in plain words.

## Removals (owner decision — record in the behaviour ledger as OWNER_DIRECTION)
6. Remove **auto plunder** and **Scheduled Plunder** completely (tab, Random delay, schedule jobs, settings, backend).
7. Remove **Add to schedule** and **Share to alliance** buttons/features.
8. Remove the **Normal / Fast** choice everywhere (Manual and Auto). Every scan always uses the best method: fastest
   accurate path with automatic fallback.
   Existing saved settings/data must still load without errors after the removals.

## Improvements
9. **Saved server "All servers"** option in the Saved server dropdown: show rows from every saved server, with a
   Server column, working search/filters/sort/Jump.
10. **Scan other servers without switching servers.** Today camera-free only works on the current server because
    the client's `WorldPointManager.ParseWorldGetBlock` drops replies whose serverId differs from the current one.
    Investigate reading the raw `world.get.block` reply (the managed sampler in `tools/map_009_offline/managed_sampler`
    already reads incoming SFS replies read-only) or another supported route, so a remote server can be scanned
    with the same fast sweep without moving the player. If it cannot be done reliably, make the existing
    switch-scan-return flow as fast and automatic as possible and report the measured times. Never move the
    player's city or perform account actions.
11. **Less clunky Map page.** After removals: one clear row of scan controls (categories, Start/Stop, Clear),
    progress with category and server, results tabs only for categories (no dead tabs), consistent spacing, no
    horizontal scroll at the app's default window size, readable at 1100 px wide. Keep the familiar look; show
    before/after screenshots in the report.

## Acceptance (lead will audit)
- Each numbered item: reproduction evidence → fix → real packaged-app retest (screenshots or logs).
- Fast scans keep working (single kinds, all nine, Truck) with timings no worse than MAP-015.
- All existing checks pass (`tests/LWBridge.Desktop.Checks`, map_014/map_015 Python tests, UI build) plus new
  regression checks for items 1–5 and 9.
- Report `docs/MAP_016_WORKER_REPORT.md`, one final ZIP from the final commit with SHA-256, status
  READY_FOR_LEAD_AUDIT or PARTIAL with exact reasons.

## Rules (unchanged)
Background only (no mouse/global keyboard/focus/minimize/control of owner windows); keep the game in Chinese; no
claims, plunder, sharing, attacks, marches, spending, relocation or updater actions; stop only your own exact game
session and restore scripts after confirmed exit; never bypass a tool denial; no merge, publish, force-push, reset or
discard. Subagents allowed, one agent controls the live game at a time. Fix/retest loops without stopping for
ordinary failures.
