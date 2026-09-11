# Project-manager priority reset — Overview-only delivery

2026-09-11, Overview implementation commit `dc82bc9` with passing CI run `34603661402`. **The technical lifecycle is live-proven; owner-visible normal Overview acceptance remains pending.** [Active Overview plan](overview-live-delivery.md).

## Owner's acceptance target

Normal Overview Launch -> actual game with verified injected/loaded bridge -> exact **LWbridge is running** message top-centre inside the game -> normal Overview Close ends the owned game/bridge with cleanup. The owner verifies it and explicitly chooses the next feature.

Current live acceptance: **NOT DELIVERED**. Launch/Close remain unaccepted; bridge readiness and the requested game-side message are not proven by the old resource helper. Auto-launch/reconnect and all Map Data expansion are deferred.

## Assignment and pending work

Web owns OVL-00–06 and may continue through their direct dependencies without a PM stop after every checkpoint. PM coordinates/audits only; the owner supplies descriptions/screenshots, while Web captures technical evidence automatically. No separate Sol or Daybreak task.

`LWB-OVL-002` now supersedes the earlier offline-only Overview checkpoint: two distinct current-client sessions reached correlated game-side readiness for **LWbridge is running**, exact owned normal Close succeeded, and the official script package was restored byte-for-byte after each run. `Start Overview Verification.cmd` is self-tested and the normal app/helper now record per-session evidence automatically. The remaining gate is the owner-visible normal Overview Launch/message/Close confirmation; do not mark owner acceptance before that screenshot/observation.

The user reports PM15-01/02 implemented and pushed at `1e9af6e`. At inspection six tracked handoff/evidence-index documents plus new PM15 evidence remain uncommitted. Preserve and finalize that coherent handoff under OVL-00; this priority reset does not re-audit or certify its regression claims. Do not request another resource check or expand the old recorder project. PM commits only its priority changes, leaving Web's unfinished evidence changes for Web.

## Current order

1. Finish the existing PM15 evidence delivery housekeeping.
2. Recover/implement only direct normal Overview launch and bridge-session dependencies.
3. Display the requested in-game text only for a genuinely ready same-session bridge.
4. Close the correct game and verify session/cleanup state.
5. Deliver automatic technical capture and a clear beginner guide; fix the first failed live step when permitted.
6. Wait for explicit owner verification and next-feature instruction.

Existing restrictions remain operation-specific and are not cleared. The old resource-first/monster-next queue below is historical and must not override this instruction. All 47 full-scope acceptance cases remain; this is the first selected feature, not full product completion.

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
