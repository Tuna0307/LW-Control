# LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2

Lead assignment, 2026-10-06. Status: **ASSIGNED**.
R1: CHANGES_REQUIRED solely Equipment save-mode semantics.
Global recoverable UIUX: PARTIAL pending this correction and lead acceptance.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Reviewed worker delivery: `0a6e0c35065f44ce5c8151a3a0e37ee9989e4638`.
Later lead review commits are expected. Read actual HEAD/status, AGENTS.md,
docs/AI_WORK_PROTOCOL.md and
`docs/reviews/2026-10-06-LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R1-LEAD.md`.
Preserve unrelated work; never reset to the reviewed delivery.

## Entire remaining assigned correction

Fix the one demonstrated Equipment save-mode mismatch in canonical
`src/LWBridge.UI-0.3.17/src/SquadsPage.jsx`, then complete its affected proof and
final integrated checks. Do not restart already accepted page visual campaigns.

Recovered `SquadPanel-HC3-DJei.js` helper `P` byte 180274 calls
`u.state.edit(e,false)` then `u.state.flush(false)`. Current
`flushPreviewConfig` calls `equipmentConfig.store.flush()` (default true).
Original `T` config store leaves a later move unsaved when a pending first Save
finishes; current clone automatically confirms that move without another Save.

Exact executable reproductions and results:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1-lead-review/profile/`
and `r1-lead-review/lead/equipment-owner-adversarial.mjs`.
They mount actual current SquadsPage keyed A→B→A, retain its pending profile-owned
config store, invoke real rendered local callbacks and execute original T/P.
The controlled adapter is inert; no native or physical HTML5 drag is claimed.

Restore the source's false flush mode. Preserve profile-scoped config ownership,
component-local transient state, editing, explicit queued saves, error handling,
Retry/Discard, Activity cleanup and offline/native action fences. No registry
clearing, new autosave policy, redesign or native implementation.

## Acceptance and closeout

1. Keep the immutable lead failure record. Add a fresh R2 distinguishing comparison:
   pending rename → B → A → unsaved move → first ack; original/current must both
   confirm the first draft and leave the move dirty. A subsequent explicit Save
   must confirm the move. Also exercise an explicitly queued second Save while the
   first is pending, and rejection/Retry/Discard with profile isolation.
2. Recheck profile A/B/A/B and affected Equipment save/pending/error/retention cases.
   Include actual rendered consumers and exact original P/T semantics rather than
   only a string assertion that flush(false) exists. Audit all Equipment callers
   of this shared flush path for the intended mode.
3. Preserve AFK and accepted page visual authority by identity/current affected
   replay as appropriate. Use new R2 evidence/adapters for deliberate current
   source evolution; never repin frozen R1/A–E records merely to make them green.
4. Run fresh complete-App verification including the new distinguishing save case,
   settled relevant EN/light and JA/dark captures, source/evidence integrity and
   console checks. Run canonical frontend check, fresh build, production-package
   verification, archive/unrelated-WIP preservation and git diff checks.
5. Update current status/handoff, create a dated delivery review, commit/push to
   origin/research/offline-controller and verify the direct remote SHA. Return
   AWAITING_REVIEW only after this complete correction and its final checks.

This is one bounded assignment. Continue automatically through code, proof and
delivery; no fixed time block. Subagents may be used with exclusive file/browser
ownership, but one coordinator verifies/integrates and commits. Inspect listener
ownership; lead-owned port 4441 is stopped at review closeout. Never stop owner
listeners or sessions. No Last War launch/control, native/gameplay/provider work,
updater/OS action or original-service/auth access. Final UIUX acceptance stays
with the lead; loaded native assets/protected runtime pixels remain separate.
