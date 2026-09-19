# Project-manager status — Player City owner-visible gate complete, 2026-09-13

## Latest implementation checkpoint — LWB-R7-050, 2026-09-19

Railway alternate sorting is IMPLEMENTED/OFFLINE-TESTED from hash-locked original recovery and deterministic multi-row acceptance. Five public Railway keys are production-enabled with ordered multi-sort, nulls-last ASC/DESC, plain quality, itemCount/itemKey coupling, protectTime zero-to-NULL handling and stable record-key ties. The final read-only current-v19 Fast attempt completed 2,500/2,500 but the live map contained zero Railway rows; the positive-population gate failed by design and no live ordering claim is made. Exact v19 restoration and clean process shutdown passed. City, Resource and Dispatch/Ghost alternate sorts remain open/fail-closed. See [R7-050 evidence](../evidence/lwbridge-implementation/2026-09-19-r7-railway-sort-fidelity.json).

## Latest implementation checkpoint — LWB-R7-049, 2026-09-19

Current implementation has advanced beyond the historical review snapshot below. Truck alternate sorting is LIVE-PROVEN read-only on current v19: six public Truck keys are recovered and production-enabled with ordered multi-sort, nulls-last ASC/DESC, special-UR quality=100, itemCount/itemKey coupling and stable record-key ties. The final exact-code Fast run completed 2,500/2,500 in 130.175 s, published/reopened 469 Trucks, and passed 15 independently computed sort scenarios before reopen and the same 15 after reopen. Exact v19 restoration and clean process shutdown passed. No consuming game action was used. Other kind-specific alternate sorts remain open/fail-closed. See [R7-049 evidence](../evidence/lwbridge-implementation/2026-09-19-r7-truck-sort-fidelity.json).

## Latest implementation checkpoint — LWB-R7-048, 2026-09-19

Current implementation has advanced well beyond the historical Review 18 snapshot below. **Truck result filters/options are LIVE-PROVEN read-only on current-v19.** A final exact-code Fast Truck run completed 2,500/2,500 in 131.059 s and published/reopened 435 Trucks. All 435 carried `currentGoods`, `maxLootCount` and frontend-compatible `remainingLootCount`; ordinary-UR/reindeer/plunderable queries matched independently derived expected UUID sets; 18 retained-item options were produced; and selective item `reward:7:600002` matched exactly 283/435 rows across database reopen. Exact v19 restoration and clean process shutdown passed. No robbery/attack/collection/consuming action was used. Truck alternate sorting was still open at R7-048 and is subsequently closed by `LWB-R7-049`. See [R7-048 evidence](../evidence/lwbridge-implementation/2026-09-19-r7-truck-filter-options.json) and [Map Scan recovery](lwbridge-map-scan.md).

**Review 18 decision: APPROVED for the prepared read-only owner-visible saved-row check.** The audited implementation is `7da866595089698a00d89b14cd650b849a5bac90` on `research/offline-controller`; local/remote matched and GitHub Actions run `34726315539` completed SUCCESS, including Windows job `103640930629`.

`LWB-PC-001` is accepted as LIVE-PROVEN for the bounded technical Player City core: two distinct fresh city-only current-client acquisitions, exact `WorldPointManager._pointInfos` source, real `BuildPointInfo` / pointType 6, strictly newer second capture, active-profile/server persistence, normal `map_search`/summary, exact package restoration and clean owned process/recovery state. `LWB-PC-002` is accepted as the separate fresh-process same-profile reopen/search proof. `LWB-PC-003` now closes the selected owner-visible gate: the reviewed normal Map Data page rendered the persisted row, the owner's later Search correlated to that same row, the app exited 0, same-profile/same-saved-city checks passed and cleanup remained clean.

PM independently parsed the committed evidence and rechecked the implementation boundaries. Shared City evidence is identity/path redacted; owner-evidence startup suppresses game auto-launch; state-changing commands are blocked before backend dispatch; City Search payload/results are sanitized; and the City DOM correlator reads only coordinates, level and updated time rather than Player/Alliance cells. The collector requires the exact launched PID/session, existing saved City context, Search→render correlation, clean postflight and no identity leak.

The successful live runs used only `StartViewRequest+UpdateViewRequest(true)`. The recovered targeted `SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)` fallback remains IMPLEMENTED/OFFLINE-TESTED, not LIVE-PROVEN. Resource SB-97, S02/S03/S06, the corrected-build Overview live regression and non-Player-City Map Data work are unchanged and not accepted by this review.

**No Player City owner action remains.** Attempt `20260913T042954Z-c20cd9c6-2b81ad37` completed the Review 18 read-only check. The first City query/render happened automatically during initial page load and the second followed the owner's manual Search, explaining why the persisted row was already visible before Search; neither query launched the game or performed a fresh scan. The next ordered Map Data category is Resource Point, but fresh Resource Start remains blocked by SB-97. [Detailed review 18](reviews/2026-09-13-review-18-player-city.md).

## Historical review 15 — deferred resource recorder work

### Project-manager checkpoint — review 15

Reviewed 2026-09-11 at `154ce35`, branch `research/offline-controller`, clean at entry. This review covers the three owner-evidence commits after `3a9278a`. [Detailed source findings and acceptance criteria](reviews/2026-09-11-review-15-owner-evidence.md).

## Result for the owner

**Automatic collection has been implemented, and the owner already completed the permitted empty-profile check. No repeat is needed.** There was no saved map server/resource in that profile; Search cannot display a resource that has never been acquired there. This is not owner error. The no-context recorder repair is supported by the reviewed source and Web's offline report.

**The recorder is not yet accepted for a future saved-row reopen test.** Two source-level defects need Web fixes: session-two validation can reuse session-one proof, and a failed process observation can be mistaken for no running processes. These flaws concern how the recorder decides a test passed. They do not prove the app's saved-data feature itself is broken.

The live resource feature remains incomplete: no fresh scan was performed in these owner checks. SB-97 remains unresolved; monster acquisition and the remaining two-page scope remain unfinished.

## Review decisions

| Item | Decision |
|---|---|
| Owner attempt `20260911T085114Z-eb1cd35e-7f21ad24` | Accept the documented no-saved-context observation; four original evidence hashes verified. No new live success. |
| LWB-PM13-009 no-context diagnosis/repair | Accept interpretation and source-backed offline repair scope; five current source/guide hashes verified. Repaired build was not retested by the owner. |
| PM15-01 / P1 | OPEN: bind reopen proof to its own app session; prior-session Search/render must not pass the new session. |
| PM15-02 / P2 | OPEN: failed/unknown process and evidence observations must not count as clean/success. |
| PM13-04 / PM12-D | ACTIVE/BLOCKED: recorder fixes do not clear fresh acquisition or full acceptance. |
| Next owner | Web, two bounded recorder fixes with isolated regressions. No owner, Sol or Daybreak task. |

## Evidence and validation scope

PM reviewed the collector, host recorder/command gate, normal map_summary error capture, worker check reports and original stored attempt. The original postflight has exit code 0, matching runtime fingerprints, cleanupClean=true, zero rows and zero Search responses. The old INCOMPLETE result remains preserved rather than rewritten as live success.

Web reports Release build, collector/preflight, deterministic/native/browser checks passing. This PM audit did **not** run those suites, launch the app/game, interact with the desktop or implement repairs. PM15-01/02 are source-confirmed findings with offline reproduction assigned to Web; they are not newly executed live reproductions. All 47 acceptance cases remain required; none is newly signed off.

## Next instruction

Read [the detailed review](reviews/2026-09-11-review-15-owner-evidence.md), [Web prompt](team-workflow.md), [technical packet](live-test-handoff.md) and [owner guide](user-test-checklist.md). Preserve the owner's evidence and do not ask for another empty-profile check. Fix PM15-01/02, verify the collector offline, document/build/commit/push/verify and return to PM. No repeated rejection diagnosis, speculative application fix, new acquisition attempt or unrelated research is assigned.

## Historical review 14 and contributor follow-ups

The following is retained historical context. Review 15 above supersedes its readiness and next-task statements.

### Project-manager checkpoint — review 14

Audited 2026-09-11 at `7ca6d5cc8d47f14fde6b73af21cf19d2bedbb23d`, branch `research/offline-controller`, clean and synchronized at entry. Eight commits since `d4571bd` were reviewed. [Review 13 and contributor follow-ups](reviews/2026-09-11-review-13-and-followups.md) are historical.

## Current coordination and diagnosis — 2026-09-11

Web preparation `9c20896`, attempt `ccb74b0` and diagnosis `537a5b9` are recorded. Native navigation worked in the former adapter session; a get_window_state was rejected upstream before local dispatch, and no fresh Start was sent. The diagnosed route was codex-chatgpt-web 5.0.6 / chatgpt-web/high. Ordinary quota/spend exhaustion is not indicated; reviewer-specific cause remains UNKNOWN. [Saved diagnosis](../evidence/lwbridge-implementation/pm13-sol-resource/20260911T044419Z-5af44117/rejection-diagnostics.md).

The owner removed the separate Sol role. Web is the single primary implementation/technical verification worker. Its current session has files/shell access but no native UI observation/control. The owner provides guided permitted UI actions, descriptions and screenshots only. `LWB-PM13-008` delivered and passed PM review for the passive saved Resource check. The owner then ran attempt `20260911T085114Z-eb1cd35e-7f21ad24`: the direct profile had no saved map server, the UI displayed the expected `MAP_SAVED_CONTEXT_UNAVAILABLE` guidance, no scan/game action ran, runtime/cleanup stayed clean, and the old collector returned a false `INCOMPLETE` because the recovered frontend intentionally returns before `map_search` on that branch. `LWB-PM13-009` records that result and repairs the collector/guide; no owner repeat is required.

[Workflow/prompts](team-workflow.md), [technical packet](live-test-handoff.md) and [owner guide](user-test-checklist.md) define the assignment. The passive owner result is not fresh live acceptance. SB-97 is not cleared and owner assistance is not a reroute. No Daybreak assignment. Prior PM/native checks below are historical, not an instruction for PM to resume testing.

## Result for the owner

**Resource scanning is an implemented test candidate, not a completed live feature. The two pages are not fully working.** The latest work includes real implementation repairs, not just research. Saved resource browsing/reopening has recorded normal-window proof; fresh acquisition through the latest ordinary window still lacks its final two-read/Search/render/reopen proof.

Computer Use setup and a real-game feature test are different tasks. A working screen/click tool proves only that an AI can operate the app. It does not prove a scan works or clear a restriction on a specific operation. The standard AI owns final resource verification; no new Daybreak assignment is warranted for a validation/capability gap.

## Accepted repairs and remaining deliverable

| Item | Audit decision |
|---|---|
| PM13-01 saved browsing | Closed for the documented historical real resource: server 2212, point 32482, coordinates 481,32, level 3. Existing normal-window evidence shows explicit Resource Search across restart. Saved context is distinct from live readiness; no fresh acquisition is claimed. |
| PM13-01b feedback | Closed at offline scope, with recorded ordinary-window unsupported-selection/missing-context checks. Unsupported categories are not silently discarded. |
| PM13-03 lifecycle | Closed at bounded offline scope. Commit/cancel ownership and helper-start ownership have deterministic race coverage. This is not all live fault acceptance. |
| PM13-02 proof correlation | Closed offline; subsequent coordinate-button correction and historical saved-reopen proof accepted. Empty/loading/stale/wrong-source results cannot satisfy the exact proof contract. |
| PM13-04 / PM12-D | **ACTIVE/BLOCKED final resource gate.** Passive owner check of the current empty profile is complete and correctly shows no saved server context. Need a permitted real Resource-only Start to create legitimate context, then correlated normal Search/render, second newer read/Search/render, saved reopen and cleanup evidence. SB-97 still blocks that fresh operation. |
| Monster and other categories | Acquisition unavailable; monster acquisition/search is queued after resource acceptance. An empty Monster table is not evidence it scanned successfully. |
| Remaining two-page scope | Overview launch/close/reconnect integration, automatic/full-world scanning, Normal/Fast parity, remaining filters/options/export and conditional game actions are incomplete. A visible control or persisted switch is not functional acceptance. |

The historical bounded helper route already acquired real resources twice. Those results make the work useful, but do not sign off the latest user-facing workflow. Resource naming and live Gathering also remain limited. All 47 full acceptance cases remain required; no full case is newly signed off in this review. Do not convert four closed repair items into an overall percentage.

## Verification in this PM audit

Fresh Release build passed with 0 warnings/errors. Desktop checks with `--verify-real-config-unchanged` passed: six groups true, no failures. Frontend regeneration/hash check, resource feedback checks, five resource proof browser cases, and the three isolated PM12 recovery/session/scoped-close regressions passed. These are build/offline checks, not a new live-game run.

Source review covered saved-server selection, validate-before-store and commit/cancel boundaries, helper ownership, exact query/result/render matching, and the saved-reopen verifier. Contributor evidence reviewed includes `2026-09-10-pm13-saved-target-rediscovered.json`, `2026-09-11-pm13-normal-window-gate.json`, and `2026-09-11-pm13-reopen-proof-correction.json` under `evidence/lwbridge-implementation/`.

The later owner-assisted passive attempt supplied the missing direct-profile observation: no saved map server exists, so saved Search/reopen cannot proceed on that profile until legitimate fresh data exists. `LWB-PM13-009` fixes only the collector classification for that state; it does not create data or clear SB-97.

[Audit evidence](../evidence/lwbridge-implementation/pm-review-14/audit.json) records seven matching historical proof/source hashes and unchanged full acceptance sections.

## Exact next task and restriction status

Follow [the test checklist](user-test-checklist.md) and [regular-AI handoff](implementation-handoff.md). Verify the current installed client fingerprint before live testing; historical client proof does not automatically cover a game update. Stay on the resource function and repair its first failed step before continuing.

SB-97 records automatic-review rejection of execution of the prepared live persistent-window proof because its safety status could not be determined. It remains unresolved. Do not replay or repackage that operation through a different model, native clicking, WebView or another executor. Tool availability is a separate question. A PM cannot waive an environment/platform restriction: any new live test must be independently permitted, otherwise record the exact required external change and stop that action. Do not ask the user to run the rejected harness as a workaround.

No Daybreak assignment. ESC-005 remains NOT_ASSIGNED/deferred original-pipe parity. No unrelated research or filler checkpoint replaces this final resource gate.
