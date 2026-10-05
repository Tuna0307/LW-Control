# Independent final UIUX lead review

Date: 2026-10-06. Reviewed delivery: `5c0a5cb3c9e76fd573ee7facdebfd3bdd53a6758`.
Decision: **CHANGES_REQUIRED — one demonstrated Squads/AFK profile ownership defect.**
Global recoverable UIUX remains PARTIAL pending this focused correction and lead acceptance.

## Accepted proof

All six earlier LR-A1/A2/A3 and LR-S1/S2/S3 findings are corrected for their
recovered-source/local scope. The whole-AFK and conditional-Automation proof gaps
are closed. No other blocking presentation defect was found in this review.

The lead independently ran canonical frontend check, a fresh production build
and production-package verification. Fingerprints match the worker delivery:

- Build: `80bc0f785cfdbae6d204bd5b0c19009bbe087bfa0ec61b5f950f57fdcc4abad0`.
- Package: `b57f76bdbcd92c4c64f4b7667c781c487d9375aeb53667ad48fed02c4b205f22`.

A fresh isolated served-App replay on lead-owned port 4440 passed 267 assertions,
captured 16 decoded screenshots, verified 67 served source dependencies and all
nine 1,383-key catalogs, and recorded zero console/page issues. Its read-only
validator also passed. Eight fresh captures were manually inspected by the lead.
The runner was copied byte-for-byte into `lead-review/`; this preserves historical
E outputs and makes clear the new run's lineage rather than claiming a newly
independent original whole-App pixel oracle.

Independent bounded reviewers provided these reports under the same evidence root:

- `lead-review/automation/README.md`: ACCEPT; corrected error/Train cases,
  24 conditional cases, 28 original/current compositions and 30 additional Train
  inverse cases pass; Automation profile store ownership is corrected.
- `lead-review/shell-afk/review.md`: accepts shell fixes, 13 independent shell
  assertions, 30 whole-AFK compositions, 14 freshly replayed browser pairs,
  11 Join checks and 25 mounted AFK assertions; identifies the blocker below.
- `lead-review/global/report.md`: ACCEPT for D/E identity and inheritance;
  verifies 13 current pins, 16 inherited proof identities, four references and
  17 original E–H byte slices. Map/E–H/CSS/catalog/reference assets are unchanged.

The lead inspected those reports and independently reproduced their only blocker.
Passing integration counts do not rebut an additional distinguishing case omitted
from that suite. Semantic mutation proof includes executable and structural
contracts; it is not nine complete browser mutation runs. Historical A hashes
and evidence remain frozen rather than being repinned to conceal source evolution.

## LR-PROFILE-AFK-001: local draft/error crosses account profiles

**EXACT_CONTRACT:** recovered `SquadPanel-HC3-DJei.js` component `I`, UTF-8 byte
28070, obtains selected profile `g=c()` and supplies that owner to the
`task:monsterSweep` and `task:staminaPotion` draft hooks. Original main `me`
(export `Dt`, byte 195132) calls registry `re` (193714), which keys stores by
`JSON.stringify([profile, scope])`; store `T` starts at 191297.

Execution of those exact original functions proves A and B have distinct stores:
both start with Potion minimum 50; A's invalid local edit to 10000 stays with A;
B remains 50; returning A restores 10000.

Current `App.jsx` passes profile identity to Automation but omits it from the
`march` route props. `SquadsPage.jsx` lines 336–339 use these ownerless identities:
`afk:${previewState}`, `afk-potion:${previewState}`,
`afk-garrison:${previewState}`, `afk-zombie:${previewState}`.
`previewConfigHook.jsx` retains them in a module-level registry. A keyed React
remount therefore reuses the previous account's draft, confirmation and error.

The lead's independent executable `lead-review/afk-profile-browser-lead.mjs`
used the actual served App with one unchanged `shell-profiles` fixture:

| State | Current App | Exact original registry |
| --- | --- | --- |
| Local 1 initial Potion minimum | 50 | 50 |
| Local 1 after inert consumer edit | 10000, dirty | 10000, dirty |
| Actual switch to Local 2 | **10000, dirty; inherited error** | **50, separate clean owner** |

This invokes the real React input consumer through the native input setter and
input/change events, without enabling the offline-disabled field. It is an inert
ownership proof, not a claim that a physical offline edit is available. The same
proof method is used in the worker's accepted Automation owner test. Neither
native commands nor gameplay were invoked. Browser issues were zero. Original
execution, identities, values and limits are recorded in
`lead-review/afk-profile-browser-results.json`; two fresh captures are included.

Correct the selected-profile handoff and source-backed AFK store identities.
Do not clear every store on profile change: the original retains A's draft for
its return. Audit all four affected keys against the exact recovered owner
contract; Equipment ownership must not be changed without separate source proof.

## Continuation and boundary

Assigned correction: `docs/work-items/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R1.md`.
Complete the finite profile fix, inverse owner/error/deferred-save cases and
fresh integrated current-source verification in that one assignment, then return
for lead review. Earlier six fixes and accepted visual comparisons remain closed
for their bounded scope. This does not reopen every historical campaign.

Native/provider-positive functionality, updater/OS operations, loaded native game
assets and protected original-runtime complete-App pixels remain separate and
unproven. No native-function phase is started by this review. Lead changes are
documentation/evidence only; worker delivery source and historical proof are preserved.
