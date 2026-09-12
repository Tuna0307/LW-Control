# Next delivery — Map Data, Player City first

Owner priority change, 2026-09-13. This is the active sequencing instruction and supersedes the review-17 stop-before-Map-Data instruction. It does not mark any unfinished feature complete or override operation-specific restrictions.

## Transition already authorized by the owner

Web first fixes PM17-02 (unfinished/unknown recovery journals) and PM17-01 (abandoned launch identity), meeting the source/isolated-regression criteria in [review 17](reviews/2026-09-13-review-17-pm16-return.md). Deliver the coherent correction with applicable checks, documentation, commit/push and remote/CI verification. Keep the corrected-build normal Overview regression packet and any unperformed live verification explicitly tracked; do not relabel old live evidence as new-build proof.

**Then continue directly to Map Data. No further PM audit, permission prompt or whole-Overview signoff is required merely to start this already-authorized work.** If a real unresolved lifecycle problem blocks a Map Data operation, fix that direct prerequisite and record the reason. Do not start Map Data before the two assigned corrections are solved, and do not spend further checkpoints on unrelated Overview polish.

**Deferred by the owner:** S03 complete runtime Refresh Status and S06 actual cross-server travel. Leave their existing partial behavior and unknowns accurately labelled; neither is a prerequisite for starting Map Data research/implementation. S02 pending-task semantics remains unfinished and is not newly assigned. S05 runtime names require only the mapping actually needed by the selected Map Data function. If a minimal current-session status/server fact is essential to city acquisition, implement that specific evidence-backed dependency without expanding it into the deferred header features or real cross-server travel.

## First user-visible result — Player City

Retain the existing queued category order, beginning with **Player City (`city`)**. Work on one complete normal-page path: acquire fresh real city data from the current permitted game session -> persist it for the correct profile/server -> Search -> display the returned cities in Map Data. The [full task contract](../task.md) and [feature ledger](lwbridge-feature-ledger.md) remain authoritative for existing commands, filters and all 47 acceptance cases; this document sets delivery order rather than duplicating or weakening them.

1. Review existing city acquisition/query/normalization findings and current code before new recovery. Trace the actual Player City selection, normal Start/Stop/Search controls, source request/result, profile/server context, persistence/query and rendered rows. Record the first missing or broken link and work until it is resolved. No fresh dependency may be guessed from historical resource behavior.
2. Enable only recovered/current-client-backed city behavior. Reuse compatible Overview connection and persistence work. Use the current session's server; do not fabricate server IDs, cities, names, coordinates, counts or completion state, and do not invoke cross-server travel as a substitute for missing local data.
3. Deliver the basic acquisition/Search/display path before broad city filters/export/actions or another category. Surface actionable errors and distinguish no cities found, missing context, unavailable transport and failed acquisition. A stored fixture or previously captured city may support an isolated regression but cannot count as a new live acquisition.
4. Validate normal controls with automatic request/source/session/store/query/render evidence: a fresh acquisition, a distinct later acquisition whose new result is used, and same-profile reopen/search persistence. Test normal Stop, interrupted acquisition and cleanup appropriate to the changed code. Keep state tied to the correct profile/server and prevent late/stale results from being shown as the new run.
5. Prepare an exact tested build and simple owner guide when visible live confirmation is needed. Web captures logs, commands, hashes and other technical evidence automatically. The owner only follows clear UI actions, describes what is visible and supplies screenshots. Do not ask the owner to force adverse OS states or work around a denied operation.

Completion means the selected real-game city workflow works through the normal Map Data page with correlated evidence, not merely successful compilation, research, seeded rows or screenshots alone. Report implementation/offline/live/owner-observed states separately. Stay on this function until it works; then record the result and use the owner's existing category sequence for subsequent planning. Do not silently begin several categories in parallel or claim all Map Data complete from this first result.

## Worker continuity

Use [the main and continuation prompts](team-workflow.md). Continue through direct recovery, implementation, targeted verification and coherent commit/push checkpoints without routine PM stops. PM audits periodically; a checkpoint ending is not a reason to stop assigned work. Save resumable state and preserve concurrent changes.

Existing restrictions (including recorded resource-operation restrictions) remain operation-specific and unchanged. Determine the concrete permitted evidence path; a new category, helper, model or owner action must not re-create a denied operation. If no permitted direct work remains, report the exact blocker and next external condition. Daybreak is only for a complete reviewed specialist request, not an automatic assignment.
