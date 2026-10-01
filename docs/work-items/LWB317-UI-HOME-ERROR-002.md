# LWB317-UI-HOME-ERROR-002 — separate Home error channels

Owner: project lead. State: CHANGES_REQUIRED after independent review / PM-018; initially assigned 2026-10-02 after owner said
continue until the other AI is ready. Baseline: 537a2b35ec79b021310f141f32ed0d00997c3597.

Goal: separate folder-selection/root errors and action errors through App/Home.
Recover cancellation/invalid-selection and successful-root-update error handling
from original Xt/Jt, and preserve independent placement from qr. Inspect the
existing current host selection result contract; add no new host schema/provider.

Scope: App.jsx existing preference/picker callbacks and Home state props;
Pages.jsx error branches/browser-only fixtures; focused actual callback/render QA
and necessary updates to Home integration/previous translation check adapters.
Preserve acknowledged preference writes, reconnect profile scoping/missing-profile
rejection, busy ordering and native lifecycle fences. Keep existing polling unchanged.

Sources: index-BVfnK1wp.js SHA-256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6;
qr 336694, Jt 367489, Xt 367703 (verified AST byte locators in evidence).
Current GameInstallationService.cs NativeGameRootSelectionResult and SaveNativeSelection;
LWBridgeWindow.cs SelectGameRootAsync. Target exe hash
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

Non-goals: Home busy text/shared switch localization, new native lifecycle,
Last War launch/control, original auth/service access, other panels, fallback or
subagents. Preserve unrelated AFK/scratch/parent screenshot WIP unchanged/unstaged.

Acceptance/output: exact locators/hash manifest, actual production callback tests
for independent clears/failures, deferred ack, missing profile, picker cancel/
invalid/valid/status failure; original-vs-clone render placement with both errors,
root missing/resolved and recovery detail; representative local browser QA/images.
Translation regression, Home integration, canonical check/build/package, JSON/
source/image validation and git diff --check must pass. Dated review + evidence
under LWB317-UI-HOME-ERROR-002, conservative current masters/handoff. Review,
commit/push explicit paths, verify remote; stop at AWAITING_REVIEW checkpoint.

Delivery: docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-002.md. Nine actual
callback scenarios, four original picker cases, 72 render comparisons and five
browser observations pass. Independent review pending. Next: HOME-BUSY-001.

Lead integration, 2026-10-02: PM-018 integrates independent review db3aae3:
HOME-ERROR-002 is CHANGES_REQUIRED for root-status acknowledgement/polling. Root/action
placement remains useful; correction LWB317-UI-HOME-ERROR-002-R1 is ASSIGNED.
HOME-BUSY-001 remains AWAITING_REVIEW. See
docs/reviews/2026-10-02-LWB317-PM-018-home-channel-review-integration.md and
docs/work-items/LWB317-UI-HOME-ERROR-002-R1.md.
The original keep-polling-unchanged constraint is superseded only for root-status
retrieval by the explicit R1 assignment. Historical delivery evidence is preserved.
