# LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2

Project-lead acceptance, 2026-10-06: **RECOVERED-SOURCE/LOCAL UIUX ACCEPTED.** Delivery `75b7b215679a8ea7986af72bdbdd7039c5278b70` closes CLOSEOUT-003/R1/R2 and the REMAINING-002/FINAL-CAMPAIGN-001 parents for the assigned UIUX scope. All earlier six defects, composition gaps, profile ownership and Equipment save-mode findings are closed. Independent lead execution passes 38 focused Equipment assertions, 17 Equipment regressions, fresh 282 complete-App assertions, 67 served sources, 16 decoded captures, nine 1,383-key catalogs, 12 semantic mutations, zero browser issues, fresh check/build/package and archive integrity. Home, Map, Automation, Squads/Equipment, City Layout, Hotkeys, Mini Games, Settings and shared shell/dialogs are accepted through their bounded recovered/local proof. Native/provider-positive functionality, live current-game compatibility, loaded native assets, updater/OS behavior and protected-original complete-App pixels remain unverified separate dependencies. See `docs/reviews/2026-10-06-LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2-LEAD.md`. Historical PARTIAL/CHANGES_REQUIRED/AWAITING_REVIEW entries below are superseded only for this accepted scope. Next assigned task: `docs/work-items/LWB317-FUNCTION-READINESS-HOME-MAP-001.md`, read-only function readiness audit; no native implementation or live test is authorized by that task.


Lead assignment, 2026-10-06. Worker delivery status: **AWAITING_REVIEW**.
R1: CHANGES_REQUIRED solely Equipment save-mode semantics; R2 worker correction complete.
Global recoverable UIUX: PARTIAL pending independent R2 lead acceptance.

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

## Worker delivery — 2026-10-06

The bounded production correction is complete. `EquipmentContent` keeps the R1
profile-owned store and changes only its shared explicit save helper from the
global-default `equipmentConfig.store.flush()` to
`equipmentConfig.store.flush(false)`. `previewConfig.js` and the global default are
unchanged.

Fresh `r2/equipment-save-contract.mjs` executes exact recovered config-store `T`
and exact Equipment helper `P` at UTF-8 byte 180274, then mounts the current served
Squads page with rendered Rename, Save, Retry, Discard and drop callbacks. It passes
38 assertions with zero browser issues and records settled EN/light and JA/dark
Equipment captures. The distinguishing case proves that pending Save → B → A →
later unrequested move → first acknowledgement confirms only the first requested
draft and leaves the move dirty; a later explicit Save confirms that move. A
separate explicit second Save while the first is pending is queued and drained.
Rejection, Retry/Discard and A/B isolation also pass.

Fresh affected replay in `r2/equipment-regression.mjs` passes 17 assertions. The
fresh complete-App R2 packet requires the served 67-file source closure to differ
from the frozen R1 host closure only in `SquadsPage.jsx`, executes the focused R2
contract again in the full browser pass, and validates 282 assertions, 16 decoded
settled captures, all nine 1,383-key locale catalogs, 12 semantic mutations and
zero console/page issues. The added R2 mutation explicitly restores default
`flush(true)` and is detected by the dirty-after-first-acknowledgement contract.

Canonical closeout gates pass:

- frontend `check`: PASS;
- production build: PASS,
  `ff5b005c08e03051a7bc58aa2e88a44261592ffa96b5f2e9bfb85a62c09cc53d`;
- production-package verification: PASS,
  `c0928be052ae6d376f939b8b8f7581df874fd8422bfa4a31f6c0c1b676eef737`;
- legacy archive guard: `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10`;
- reference executable SHA-256 remains
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`;
- `git diff --check`: PASS before final staging.

Historical R1/R1-lead-review/A-E records remain unchanged. Native/provider-positive
execution, updater/OS actions, loaded native assets and protected original-runtime
pixels remain separate dependencies. Independent project-lead R2 acceptance is the
next gate.
