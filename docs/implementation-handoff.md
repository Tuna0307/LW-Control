# ChatGPT Web implementation task — PM review 17

## Current owner priority — finish two fixes, then Map Data, 2026-09-13

**Web is authorized to continue directly to Map Data after solving and verifying PM17-02 and PM17-01.** Deliver the correction checkpoint with applicable checks, evidence, commit/push and remote/CI verification, then begin **Player City first**. No additional PM approval or whole-Overview signoff is required to start. [Delivery sequence](map-data-delivery.md) and [copyable prompts](team-workflow.md).

**S03 complete Refresh Status and S06 cross-server travel are PENDING/DEFERRED by the owner.** Do not work on them or require their completion before Map Data. S02 remains unfinished and unassigned; other header gaps do not mean the whole Overview page is complete. Preserve all 47 acceptance requirements and historical proof limits.

Use one feature at a time: fresh real Player City acquisition -> normal Search/display -> newer result -> same-profile reopen, with automatic technical evidence. Recover only direct missing contracts and fix the first broken link. Keep unperformed Overview live regression explicitly tracked; old live results are not proof of the corrected build.

This instruction supersedes older stop-before-Map-Data and return-for-permission wording below. Existing operation-specific restrictions and cleanup/ownership requirements remain unchanged. No automatic Daybreak assignment.

### Retained earlier resource/PM15 checkpoint — not the active assignment


Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md), [current audit](lwbridge-project-status.md), [first-live-result contract](first-live-result.md) and the relevant evidence. Audit base: `7ca6d5c`; inspect actual HEAD/worktree before editing. Preserve concurrent work. All 47 acceptance cases remain required.

## Current assignment — PM15-01/02

Review-15 repairs are implemented at `1e9af6e` and documented by `LWB-PM15-001`. Current-session evidence is isolated by collector-generated session directory plus exact launched PID/start/end ownership; failed process/app/UI/runtime/store evidence is explicit and cannot pass as clean. Seven isolated PM15 regressions pass. **Current action is PM audit of the returned package; no owner repeat or live test is requested.** Do not replay the rejected live workflow or switch to monster research.

## Owner clarification — current, 2026-09-11

Web now owns implementation and all technical verification/capture. There is no separate Sol task. The owner can only follow plain UI steps, describe visible outcomes and provide screenshots. PM audits rather than implementing or testing.

**Delivered foundation:** automatic collection and the beginner guide exist. The two review-15 collector fixes are now delivered offline and await PM audit. Read [team-workflow.md](team-workflow.md), fill [live-test-handoff.md](live-test-handoff.md) and [user-test-checklist.md](user-test-checklist.md). Never ask the owner to obtain command output, inspect JSON/SQLite, calculate hashes or debug recovery. Web implements the scripts, validates actual collection and handles interpretation.

**Current delivery:** the owner completed the first permitted passive check against reviewed build `2d3915d` in attempt `20260911T085114Z-eb1cd35e-7f21ad24`. The selected profile had no published map server and the normal UI showed `MAP_SAVED_CONTEXT_UNAVAILABLE`; no scan/game action ran. `LWB-PM13-009` fixes the package defect exposed by that attempt: the frontend deliberately does not send `map_search` when no saved server exists, so the old collector incorrectly returned `INCOMPLETE`. The repaired collector records the normal `map_summary` error plus empty published-server state as `COMPLETE_NO_SAVED_CONTEXT`, and the guide now tells the owner to screenshot/close before Search on that branch. **Do not ask the owner to repeat the current check.** Fresh acquisition remains SB-97 BLOCKED/NOT_RUN and is not part of this repair.

The diagnosis at `537a5b9` is complete within saved-log limits. Navigation worked; an observation was rejected before local execution and no fresh Start was sent. Do not repeat closed work, guess a code defect or use an owner-run collector to reroute the restriction.

## One active function

**Resource scan -> storage -> normal Resource Search/display -> second fresh refresh -> reopen.** This function remains partial. Do not switch to monster research, export, auto-update, broad original-pipe parity or unrelated fixes while it remains active. The next function is monster acquisition plus search, after resource acceptance; monster search alone cannot create missing game records.

Accepted: bounded current-client acquisition, PM12-A restoration/ownership, PM12-C immutable result/session correlation, source-backed idle for the observed point, and PM12-B bounded-route lifecycle closure after `LWB-PM13-003`. Do not repeat those recoveries. PM12-D normal-window completion remains open. Read the audit before treating contributor checkbox claims as full acceptance.

Post-review checkpoints `LWB-PM13-001` and `LWB-PM13-001B` close PM13-01 saved browsing. The exact review-13 config/database was rediscovered in the Codex package-local cache; byte-identical persisted bytes were exercised through two ordinary app sessions, the Resource tab and explicit Search after restart, then the temporary direct-desktop copy was removed to restore prestate. `LWB-PM13-002` closes PM13-01b feedback, `LWB-PM13-003` closes PM13-03, and `LWB-PM13-004` closes PM13-02 at IMPLEMENTED/OFFLINE-TESTED scope. `LWB-PM13-006` corrects the real coordinate-button render shape and adds a repeatable ordinary-window saved-reopen verifier. The proof now binds each acquisition to an explicit normal Resource Search request/result and exact rendered row. PM13-04 is the active final resource gate.

## Repair status — begin with PM13-04, not the closed items

1. **PM13-01 — COMPLETED:** the exact source-backed saved row is visible on ordinary Resource Search after restart. Saved context is labelled separately from live readiness; zero/multiple-server cases remain explicit, and no fixture/manual row/new scan substituted for the persisted database. Evidence: `LWB-PM13-001` / `LWB-PM13-001B`.
2. **PM13-01b — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-002` preserves the structured `LIVE_RESOURCE_TYPES_UNSUPPORTED` error, provides localized Resource-only guidance in all nine recovered languages, retains the recovered no-records state, and separates missing-context from query-failure feedback. No category fallback or synthetic row was added. Connected normal-window revalidation remains PM13-04.
3. **PM13-03 — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-003` validates/correlates into a prepared row before mutation, makes `reading -> committing` the atomic completion-versus-cancel decision, and reserves helper cleanup ownership before `Process.Start`. Deterministic Stop/Close-before-commit, Stop-after-commit-decision and Close-during-helper-registration cases pass with prior PM12 lifecycle regressions.
4. **PM13-02 — COMPLETED (IMPLEMENTED/OFFLINE-TESTED):** `LWB-PM13-004` explicitly clicks Resource Search after each fresh acquisition, records its native request ID/payload/result, revalidates the request-owned immutable result tuple, requires exact server/record/point/coordinate/level/updatedAt agreement, and then requires the rendered five-cell row to match the correlated acquisition timestamp. `LWB-PM13-006` fixes the real first-cell shape by extracting the coordinate span from the recovered coordinate button instead of comparing the full `481,32\nJump` td text, and provides the separate saved-reopen verifier. Empty/loading/unrelated-stale/same-point-stale cases remain rejected.
5. **PM13-04 — ACTIVE/BLOCKED FINAL GATE:** `LWB-PM13-005` safely revalidated the ordinary rebuilt window for unsupported all-category Start and missing saved context; both show the new specific guidance, keep empty state separate, start no game/helper process, and restore the direct desktop root to prestate. `LWB-PM13-006` proves the corrected saved-reopen verifier against the exact historical server-2212 row and leaves the direct root clean. The remaining required operation is now exactly a permitted real resource-only Start -> correlated row -> second newer Start/Search/render, followed by that verifier after app reopen. SB-97 still blocks replay of the prepared live persistent-window proof. `LWB-PM13-007` confirms the bundled Computer Use skill is installed locally, but the contributor's web GPT session did not expose the required `node_repl` bridge; do not substitute Remote Desktop Commander, ordinary Node, WebView automation or a helper protocol.

If a required operation is actually restricted, document its exact tool/reason/target and the permitted alternative or required external change. Keep the function ACTIVE/BLOCKED and continue only its direct permitted dependencies. Do not endlessly generate unrelated research checkpoints, self-approve an escalation, or reroute a denied action through another executor/model. Existing user permissions do not need renewal.

## Current Overview continuation — 2026-09-12

O04/O05 remain technically live-proven/owner-visible accepted under `LWB-OVR-010/011`. `LWB-OVR-012/014` retain the recovered repair contract and successful live repair path. Review 16 reopened O01/O06 edge closure; PM16-02 and PM16-01 are now corrected/offline-tested as `LWB-PM16-001` and `LWB-PM16-002`, and `LWB-PM16-003` reconciles their evidence/status. **This is a return-to-PM/owner checkpoint, not a claim that the whole Overview page is complete: S02/S03/S06 remain open. Do not begin Player City or Map Data until the owner explicitly resumes scope.**

## Readiness decision and test plan

Read [the resource test checklist](user-test-checklist.md). The implementation is ready as a controlled-test candidate, not a proven live release. The standard AI may test Computer Use availability, but must deliver the resource result or an exact unresolved execution blocker. Check the current installed client before acquisition. SB-97 remains a distinct restriction; neither tool discovery nor PM approval clears it. Never ask the owner to execute the rejected harness as a workaround. An independently permitted live method or an applicable environment change is required.

## Computer Use capability correction

On review 13 the PM successfully used the installed `computer-use` skill via `mcp__node_repl__js` and `@oai/sky` to inspect and click the real Windows app. Read the installed SKILL.md and guidance/API first; discover whether that tool is callable in **your** environment. Browser-only `mcp__cua_repl` native limitations do not establish that all installed native capabilities are absent. If sky is unavailable, record the exact capability gap and use permitted offline checks/prepare the UI handoff. Do not invoke the native helper executable or custom helper protocol as a workaround. SB-97's rejected live proof remains a separate restriction and is not cleared by native capability discovery.

## Resource function exit criteria

- Existing source-backed saved row survives app restart and displays with correct profile/server/coordinates/level/time and truthful saved status.
- Unsupported selection and unavailable context give clear, accurate UI feedback; no synthetic data appears in production.
- Stop/timeout/Close/duplicate Start tests cover late commit and cleanup ownership, not only request cancellation.
- A permitted fresh Start and a second fresh read reach the normal table with durable source/request/result/query/render correlation. The second read may return the same point; its acquisition time and query/render evidence must be newer.
- Required cleanup/integrity checks pass; missing names and unobserved Gathering remain explicit limitations. No full-map or all-category claim follows from this bounded result.
- Documentation, focused checks, coherent commit, push and remote verification complete. Only then submit the evidence to PM for acceptance and selection of the next user function. An external blocker is recorded, not counted as passing this gate.

## Next function, queued only: monster scan and search

Recover current-client monster acquisition/type/identity/field mappings and original LWBridge normalization/query semantics. Trace Start -> real response -> monster records -> persisted index -> normal Monster Search. Distinguish an empty scanned area from absent acquisition and from filter/context errors. Prove a real monster appears, matching filters find it, nonmatching filters exclude it, refresh updates it, and scope is maintained; never manufacture a mob or claim an empty result proves acquisition. Stay on this function until its stated scope works or an exact external blocker is recorded. Regular AI owns it; Daybreak only receives a complete reviewed ESC packet for a specific unresolved question.

## Checkpoints and reporting

Run checks appropriate to changes. Sequence builds sharing output. Baseline: Release desktop build; desktop checks with `--verify-real-config-unchanged`; three PM12 isolated recovery/session/scoped-close regressions; frontend `--check`; preference and both transport checks; browser verifier using the existing Playwright installation and Edge channel. The historical audit reproducer is retained as a known-failure demonstration. The replacement proof checks now reject empty/loading/stale results; do not reopen the fixed predicate without a new failure.

At each checkpoint report: current function; what the user can actually do; what changed; live versus offline evidence; precise remaining failure; next action within this same function; commit and verified remote. Save confirmed findings immediately. Commit/push coherent checkpoints without a fresh PM permission gate; a research checkpoint does not close the function.

## Role-specific prompts and continuation

Use [team-workflow.md](team-workflow.md) as the single current prompt source: Web prepares verified automatic collection and the owner guide, the owner supplies permitted UI observations/screenshots, Web diagnoses/fixes and PM audits. [live-test-handoff.md](live-test-handoff.md) records the current owner, build identity, pending step and results so disconnects do not erase progress. No Daybreak task is currently assigned.
