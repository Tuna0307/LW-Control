# LWB317-UI-MAP-FILTER-LIFECYCLE-001 — independent final review

Review target: `d04f470..3e20fa1` on `research/offline-controller`.
Reference authority: hash-pinned LWBridge 0.3.17 Map asset through the existing exact original-component runner.

## RESULT

**PASS / ACCEPT for the assigned focused source/local UI scope.** The prior MEDIUM delayed-Clear request-lifecycle mismatch is corrected in `3e20fa1`, and no new blocker or scope regression was found.

The exact prior counterexample now matches the original: start Clear on server 321, change to server 322 while Clear is pending, then resolve the old Clear acknowledgement with server 321. The exact original component issues option requests `[322, 321]`; corrected current `MapDataPage.jsx` also issues `[322, 321]` and settles with server 321. The correction keeps same-server Clear ownership intact by explicitly reloading only when `next.serverId === dataServerIdRef.current`; when the acknowledgement changes the data server, the existing server-transition effect owns the single reload.

The complete production change remains bounded to `MapDataPage.jsx` and the deterministic `mapPreviewApi.js` lifecycle fixture work. `3e20fa1` itself changes only the Clear reload ownership condition plus the task checker/result evidence. I found no unrelated Scheduled Plunder, Treasure Checking, table, AFK, native or gameplay scope expansion.

The reviewed semantics remain aligned with the recovered original: option validation and alliance sentinels/encoding, malformed alliance retention with empty query projection, deferred/rejected Clear preservation, selective acknowledged-Clear resets, stale options/search/finally fencing, Treasure storage defaults and persistence, explicit false Treasure query values, normal-tab navigation/cache behavior, manual Search ownership, selection behavior, Checking presentation, and Scheduled Plunder boundaries.

This acceptance is source/local UI evidence only. It does not add native/gameplay or original post-auth pixel claims beyond the existing campaign limits.

## VALIDATION

- Independent replay of the prior delayed-Clear/server-change counterexample: exact original `[322,321]`; corrected current `[322,321]`; both return to server 321.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/subagent-source-lifecycle.mjs` -> `LWB317_SUBAGENT_SOURCE_LIFECYCLE_OK`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/check-filter-lifecycle.mjs` -> `LWB317_UI_MAP_FILTER_LIFECYCLE_CHECKS_OK`, including the new `[322,321]` delayed-Clear assertion.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs` -> current failures `0` across 38 scenarios.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-navigation-replay.mjs` -> current failures `0`, 17 searches; six historical baseline defects reproduced.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs` -> 38/38 current scenarios match the exact original component.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/replay-historical.mjs` -> filters, table regression, Treasure Checking, row actions and Map-state reviews all pass.
- `git diff --check d04f470..3e20fa1` -> pass.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-protected-wip.mjs` -> `LWB317_PROTECTED_WIP_OK files=5 staged=0`.

Protected pre-existing WIP remained untouched and unstaged: `previewAfkFixtures.js`, `.scratch-lwb317/`, and `LWB317-UI-CORRECT-003/screenshots/`.

## BLOCKERS

None for the assigned focused source/local UI scope at `3e20fa1`.
