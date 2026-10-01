# LWB317-REVIEW-HOME-ERROR-002 — independent Home error-channel review

Owner: returning worker as independent reviewer. State: ASSIGNED, awaiting start.
Date: 2026-10-02. Current review baseline: baf5473c09bcb371780b947a9d624e04a45517ad;
lead acceptance/assignment documentation follows it. Use current HEAD without reset.

## One bounded review

Review only LWB317-UI-HOME-ERROR-002, implemented by the project lead at
09a929ee94da90228403a5885c11d45ea80bd6a2. Verify current App.jsx independent
gameRootError/gameActionError state and existing preference/picker callbacks,
plus Home error placement in Pages.jsx. Home translation is now accepted by
PM-017, and shared switch descriptions by PM-016; preserve them. Busy presentation
and Trade 003E remain outside this review.

Repo: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Read AGENTS.md, AI_WORK_PROTOCOL, task.md,
lwbridge-ui.md, implementation-handoff and this assignment; inspect git status.
Target exe: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe,
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

## Inputs and review criteria

Original index-BVfnK1wp.js under evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/,
SHA-256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 bytes: qr 336694, Jt 367489, Xt 367703. Inspect nearby producer/
consumer state as needed to evaluate this unit; attach exact locators to findings.
Read docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-002.md and its task evidence:
check-home-channels.mjs, channel-results.json and native-contract.json.

Inspect existing host result fields read-only: GameInstallationService.cs
NativeGameRootSelectionResult/selection outcomes, LWBridgeWindow.cs selector/JSON
serialization and LocalConfigStore.cs JsonOptions. Validate the mapping to canceled,
valid and current status replies without launching a native host/picker.

Check root/action independence at start, while awaiting a response, on failure,
cancel, invalid selection, successful selection/status acknowledgement and status
failure. Confirm action errors survive picker outcomes, root errors survive
preference outcomes, config/checked state changes only after acknowledged writes,
and reconnect retains profile injection/missing-profile pre-dispatch rejection.
Review clearing against original Xt/Jt carefully. Existing polling and rejection
string reduction were retained; identify any required parity gap rather than
assuming every difference is acceptable. Separate existing out-of-scope provider
limitations from defects caused by this unit, with evidence for either judgment.

Independently choose a few distinguishing scenarios, executing actual extracted
callbacks through the existing frontend bridge with synthetic local responses.
Include prior errors in both channels and at least one deferred reply. Do not use
handwritten copies of the handlers or invent a success response schema. Save a
small reproducible checker/result plus exact source references.

Rerun check-home-channels.mjs --verify-record (9 callback scenarios, 4 original
picker cases, 72 render comparisons) and validate-evidence.mjs. Preserve saved
reports. Check simultaneous errors and missing/unresolved/valid-root placement
against original qr. Browser QA is bounded to existing home-errors-both and
home-error-action-missing-root fixtures; verify independent messages and disabled
native controls, with one useful screenshot. No native picker/game action.

Run current accepted translation/switch and pending busy regression checkers plus
canonical check/build/package checks and new source/image/JSON/diff validation.
Do not weaken checks or regenerate earlier evidence to obtain a pass.

## Delivery and stop

Review only: no product code, existing evidence, helper/predicate refactors or
master acceptance changes. If a defect exists, record the exact source mismatch,
failure reproduction and smallest suggested correction for a separate fix task.

Create docs/reviews/<actual-date>-LWB317-REVIEW-HOME-ERROR-002.md and
evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/. Update this assignment's
delivery section only. Return REVIEW_COMPLETE with recommendation ACCEPT for
focused source/local scope or CHANGES_REQUIRED, findings, checks, limits, paths,
commit and verified remote SHA. Main project lead makes final acceptance.

Preserve byte-for-byte and leave unstaged:
- src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js
- .scratch-lwb317/
- evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/

Inspect server ownership; do not stop unowned servers or close user/other AI tabs.
Existing port4319/PID62280 preview was pre-existing. No original protected-service
access, Last War launch/control, backend campaign, fallback or subagents. No fixed
time block. Stage only owned review files, commit/push origin/research/offline-controller,
verify HEAD/tracking/direct remote equality, and stop after this one review.

Delivery: independent review completed 2026-10-02 with recommendation
CHANGES_REQUIRED. The exact root-status polling/Jt clearing mismatch, reproduction,
browser evidence and smallest source-shaped correction are documented in
`docs/reviews/2026-10-02-LWB317-REVIEW-HOME-ERROR-002.md` and
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/`. Product code and prior
task evidence remain unchanged; project-lead acceptance is pending.
