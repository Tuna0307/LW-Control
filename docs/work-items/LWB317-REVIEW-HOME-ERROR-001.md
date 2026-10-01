# LWB317-REVIEW-HOME-ERROR-001 — independent Home translation review

Owner: returning worker as independent reviewer. State: ASSIGNED, awaiting start.
Date: 2026-10-02. Current implementation baseline: 47c243af1a311e331b6ac0f51d84e16782f32c47.
The lead review/assignment documentation checkpoint follows it; use current HEAD.
Do not reset, discard WIP or restart an older campaign.

## One small review

Review only LWB317-UI-HOME-ERROR-001, which the project lead implemented at
537a2b35ec79b021310f141f32ed0d00997c3597. Evaluate the current retained helper
in Pages.jsx against original 0.3.17 Ir/Lr, and its Home recovery-error rendering.
The later HOME-ERROR-002 state adapters are already in current code/evidence;
do not revert them or broaden this review into picker callbacks/channel state.
HOME-BUSY-001 and accepted UI-SWITCH-LOCALE-001 remain separate units.

Repo: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Branch: research/offline-controller. Read AGENTS.md, AI_WORK_PROTOCOL, task.md,
lwbridge-ui.md, implementation-handoff and this assignment. Inspect status first.
Target exe: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe,
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

## Inputs and acceptance

Original index-BVfnK1wp.js under evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/,
SHA-256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 bytes: Ir 328453, Lr 328684, qr 336694. Verify directly; record
locators for each fact. Existing delivery:
docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-001.md and matching evidence folder.
PM-015 is historical baseline evidence, not a current passing-check requirement.

Read the actual helper, exact original functions and existing differential checker.
Assess string/object/Error inputs, code extraction, duplicate/reversed priority,
error/auth.error/update.error namespace order, generic localized fallback and
recovery.failedDetail composition. Separate helper fidelity from existing App
rejection-to-string reduction; this review does not accept native error contracts.

Independently choose a few distinguishing source-vs-production cases and execute
the actual extracted functions; preserve reproducible evidence. Rerun existing
HOME-ERROR-001/check-home-errors.mjs --verify-record (60 edge, 4,230 catalog, 27
Home render cases) and validate-evidence.mjs. Do not regenerate saved reports.
Inspect original/shared/locale evidence rather than assuming catalog correctness.
Bounded browser QA: existing home-error-unknown and home-recovery-error-unknown
fixtures in an appropriate English/Japanese locale; observe the displayed generic
message/recovery composition and unchanged disabled native controls. Save one useful
image. Source/preview proof does not establish original pixel or native parity.

Run canonical check, build and check:production-build. Confirm source/image/JSON
evidence and git diff --check. Preserve current helper/channel/busy/switch regressions;
record a concrete failure if encountered, without weakening checks or editing them.

## Outputs and stop

This is a review, not a product implementation task. Do not change product code,
existing evidence or master acceptance states. Record defects with an exact source
comparison and smallest suggested correction for a separately assigned fix.

Create docs/reviews/<actual-date>-LWB317-REVIEW-HOME-ERROR-001.md and
evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-001/ with independent cases,
verification and browser evidence. Update this assignment's delivery section only.
Return REVIEW_COMPLETE with recommendation ACCEPT for focused source/local scope
or CHANGES_REQUIRED, plus findings, checks, limits, paths, commit and remote SHA.
The main project lead makes the final acceptance decision.

Preserve byte-for-byte and leave unstaged:
- src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js
- .scratch-lwb317/
- evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/

Do not stop unowned servers or close user/other AI tabs. Existing preview server
port4319/PID62280 was pre-existing; inspect ownership first. Do not launch/control
Last War, access original protected services, add fallbacks, spawn subagents or
review other pending campaigns. No fixed time block. Review/stage owned files,
commit/push origin/research/offline-controller and verify exact remote revision.
Stop after this one review; no automatic next task.
