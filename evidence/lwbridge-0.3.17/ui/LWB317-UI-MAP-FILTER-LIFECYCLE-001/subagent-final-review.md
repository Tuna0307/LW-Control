# LWB317-UI-MAP-FILTER-LIFECYCLE-001 — independent final review

Review target: `d04f470..9c38591` on `research/offline-controller`.
Reference authority: hash-pinned LWBridge 0.3.17 Map asset through the existing exact original-component runner.

## RESULT

**CHANGES_REQUIRED.** One **MEDIUM** lifecycle mismatch remains. The focused task and accepted-regression suites otherwise pass.

### MEDIUM — delayed Clear acknowledgement duplicates the post-ack options request

Counterexample: start Clear on server 321, change to server 322 while Clear is pending, then resolve the old Clear with an acknowledged server-321 state.

- Exact original component: option requests after the transition are `[322, 321]` — one request for the transient server 322, then one request for server 321 after the acknowledged Clear restores that response server.
- Current `MapDataPage.jsx`: option requests are `[322, 321, 321]`.
- The final current state still matches the original (`browseServerId`/data server returns to 321), and generation fencing prevents an obsolete options result from winning. The extra request is nevertheless a source-visible request-lifecycle difference in a race explicitly covered by this task.

Cause: `clearData()` explicitly calls `loadOptions(next.serverId)` after setting `browseServerId` (`MapDataPage.jsx` around lines 595-623). The following data-server effect (`MapDataPage.jsx` around lines 436-448) observes the 322 -> 321 transition, invalidates that first post-ack request through `optionsGeneration`, and launches a second server-321 options request. In the original, `tr` updates the data server and options revision in the same acknowledged-clear update; the options effect runs once for the resulting state (`MapDataPanel.pretty.js` lines 398-416 and 466-470).

The task checker currently misses this distinction: `delayedClearAckAfterServerChange` asserts only the final scan server, browse server and alliance state. Its same-server stale-options case correctly expects one pre-clear plus one post-clear request, but it does not count post-ack requests across a server transition.

Correction should preserve the original lack of a Clear-request generation fence while ensuring one post-ack options load. Same-server Clear still requires a fresh options load; a returned server that changes the current data server can let the existing data-server effect own that reload.

## Other reviewed semantics

No additional mismatch was found in the assigned scope. Exact original/source checks and current production agree on refreshed option validation, raw `all`/`none` sentinels versus `name:${encodeURIComponent(name)}`, malformed/non-prefixed alliance retention with empty query projection, failed/deferred Clear state preservation, selective acknowledged-Clear resets, stale search/options fencing, Treasure lazy defaults and mount persistence, false-valued Treasure query fields, and omission of those fields on unrelated tabs.

Accepted behavior also remains intact in the replay coverage: manual Search still has no typing debounce, Resource/Monster name behavior matches, Dispatch/Ghost and Truck selection ownership matches, request disposal and navigation/page-cache behavior pass, Scheduled Plunder interaction boundaries pass, and the isolated Treasure `Checking` presentation replay passes.

The production diff is bounded to `MapDataPage.jsx` plus the deterministic `mapPreviewApi.js` lifecycle fixture changes. I found no unrelated Scheduled Plunder, Checking, table, AFK, native or gameplay scope expansion.

## VALIDATION

- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/subagent-source-lifecycle.mjs` -> `LWB317_SUBAGENT_SOURCE_LIFECYCLE_OK`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/check-filter-lifecycle.mjs` -> `LWB317_UI_MAP_FILTER_LIFECYCLE_CHECKS_OK`.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs` -> current failures `0` across 38 runs.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-navigation-replay.mjs` -> current failures `0`, 17 searches; six historical baseline defects reproduced.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs` -> 38/38 current scenarios match the exact original component.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/replay-historical.mjs` -> filters, table regression, Treasure Checking, row actions and Map-state reviews all pass.
- `git diff --check d04f470..9c38591` -> pass.
- Independent inline replay of the delayed-Clear/server-change race -> exact original option calls `[322,321]`; current option calls `[322,321,321]`.

Protected pre-existing WIP remained untouched and unstaged: `previewAfkFixtures.js`, `.scratch-lwb317/`, and `LWB317-UI-CORRECT-003/screenshots/`.

## BLOCKERS

The MEDIUM duplicate options-request lifecycle mismatch above blocks source-exact acceptance of `9c38591` for this task. No environment or tooling blocker was encountered.
