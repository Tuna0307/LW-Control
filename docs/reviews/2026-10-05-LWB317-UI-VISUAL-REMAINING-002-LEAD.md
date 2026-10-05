# Project-lead review — LWB317-UI-VISUAL-REMAINING-002

Date: 2026-10-05. Reviewed worker delivery:
`967a92c3553e4bc81272741f333803ee54ae5e44`.

Decision: **CHANGES_REQUIRED. Global UIUX remains PARTIAL.** The worker's
implementation and evidence are substantial, but passing submitted validators
does not close the exact local branches independently reproduced below.
This review changes documentation and adds evidence; it does not modify product
code or start native/gameplay recovery.

## Independent verification

The lead inspected current source, the worker diff and original assets, and
verified that HEAD and the direct remote branch matched the delivered SHA before
review. Three bounded independent reviewers covered AFK/Automation, shell/Home,
and Map/integration. Their findings were evaluated against actual source and
executable counterexamples rather than accepted from summaries.

- Canonical frontend check, fresh production build, package integrity and
  repository diff check passed. Build fingerprint:
  `fd149170725c947f44205a367e90bff72154366e1bfbc9ffe60c33d0ae219e46`;
  package fingerprint:
  `60b9e678d710cbcc8fdfac636a36f03ddfea23227b286b22ffada4bc1a4480a1`.
- Submitted M5 validator passed before this review's deliberate status-document
  updates: 67 served-App dependencies, 213 assertions, 11 decoded screenshots,
  zero recorded console/page issues and 64 route transitions.
- The lead independently re-executed the actual current-App browser runner in a
  separate output directory and task-owned server on port 4410. All 213
  assertions passed, with 11 fresh screenshots and zero console/page issues.
  Representative Automation and Japanese/dark Map screenshots were inspected.
  This is a fresh current-App integration execution, not an original/current
  whole-App pixel oracle.
- Independent Map source/CSS identity, E–H inheritance, historical pixel decoding
  and actual current-App Map-entry replay passed. Map's 13 exact historical pairs
  and two bounded Start Scan fences remain supported; no Map/E–H production
  defect was found in this review.
- The lead separately reran the shell review's exact-byte extracted callbacks
  and reproduced all three shell counterexamples. An actual mounted Automation
  DOM-drag-handler test independently reproduced missing Train target feedback.
  DOM DragEvents are handler proof, not physical HTML5 drag proof.
- The lead extracted and reran the Automation review's actual original/current
  renderer and callback script. Missing/empty/populated fixed selections, drag
  target and invalid-card geometry counterexamples all reproduced. The same
  generic error moves from y=219 with zero margins to y=231 with 12px margins.

Evidence root:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/lead-review/`.

## Finite correction list

| ID | Area | Independently demonstrated difference | Required correction |
| --- | --- | --- | --- |
| LR-A1 | Automation Train | Current reward drag-over handler never sets the recovered target `drag-over` state/class. Dragging source feedback alone remains. | Restore exact target feedback and its drop/end cleanup; preserve reorder/save semantics. |
| LR-A2 | Automation Train | With identical fixed modes and absent fixed-carriage arrays, recovered renderer selects zero/zero carriages; current field fallbacks select one/two. Explicit empty and populated arrays are valid in both. | Use recovered missing-field defaults. Preserve explicit source-valid fixture/config arrays; do not classify populated fixtures themselves as defects. |
| LR-A3 | Automation errors | Recovered generic card error uses a `div`; current uses a `p`, introducing paragraph margins and shifting the same validation error. | Restore exact generic error element/composition while preserving text, retry/discard and source-specific field errors. |
| LR-S1 | Home recovery | Selected profile A accepts profile B's recovery envelope; original `Pe` rejects it. | Apply existing profile-envelope ownership contract, preserving same-profile/raw events and effect cleanup. |
| LR-S2 | Home status | Two deferred five-second polls overlap; newer stopped proxy state can be replaced by older running state. Original recurring poll suppresses overlap and guards profile/closed ownership. | Recover periodic in-flight and acknowledgement ownership; preserve separate explicit Refresh behavior. |
| LR-S3 | Profile loading | Newly added preview selection B then C allows B's completion to clear C's still-pending loading state. | Fence loading completion to its selection owner/generation; preserve cache/return behavior. |

LR-S1 and LR-S2 predate the reviewed production diff. They are still relevant to
the assignment's complete source/local closure claim. LR-S3 is newly added.
All six concern local rendering/state consumers and can be corrected without a
native provider, service access or gameplay action.

Exact original/current locators and executable reproductions are in
`lead-review/afk-automation.md` and `lead-review/shell-home.md`.
`lead-review/map-integration.md` records bounded acceptance and its proof limits.
The coordinator rejected the preliminary interpretation that populated Train
fixture selections alone were a defect; the accepted LR-A2 counterexample uses
the same missing fields in both renderers.
The Automation review's additional save-error `error-text` versus `muted` class
difference is valid as a DOM observation, but no recovered CSS rule or visible
UI difference was demonstrated. It is a non-blocking fidelity note, not a seventh
proven UIUX defect or a reason to expand the correction campaign.

## Required composition-proof closeout

Two explicit assignment requirements also remain unclosed. These are evidence
gaps, not additional asserted implementation bugs:

1. M2's whole-`I` harness still executes the immutable pre-fix Squads baseline
   with replaced siblings. Its fresh pairs execute `pe/he` only in two initial
   EN/light states. Supply current full original `I` versus canonical Squads
   composition with matched profile/editor/toolbar and affected runtime/error
   states, using the assigned locale/theme/viewport modes. Preserve the accepted
   editor/Equipment slices rather than reopening their implementation.
2. M3's paired whole-page cases all use offline input and vary category/expansion.
   They omit the assigned positive/conditional/error/running compositions, which
   is why the three local differences escaped. Extend the matched exact renderer
   proof to the existing unclosed branch inventory: fixed Train/drag feedback,
   invalid generic cards, Chat reply/dispatch conditionals, populated Trade/history
   and task-busy settings. Reuse accepted lower-level contracts with matching
   current source pins; controlled positive UI inputs do not authorize actions.

These finite source-local comparisons follow the original work-item requirements;
they do not require protected runtime access, new feature implementation or an
open-ended additional visual campaign.

## Accepted evidence and limits

Retain the reviewed Map/E–H inheritance and current-App navigation/retention
proof. No unsupported broader redesign, page rewrite or historical record
regeneration is required. AFK/full-shell packets provide useful bounded evidence;
their saved passing markers do not exercise the six counterexamples.

The shell oracle substitutes inert page sentinels, and M5 full-App browser proof
executes the current app only. These can support compositional source/local
findings when stable child sources are independently pinned; they must not be
called new complete-App original/current pixel equality. Aggregate shell raw
pixel differences also do not by themselves prove an additional product defect:
excluded account controls and unavailable providers account for disclosed changes.

Loaded native game assets, provider-positive execution, updater/OS integration,
and protected post-auth original-runtime pixels remain separate dependencies.
The six local corrections must be resolved before accepting this UI assignment;
those separate dependencies must not be mislabeled as completed by its acceptance.

## Exact continuation

Correct LR-A1–A3 as one bounded Automation milestone and LR-S1–S3 as one bounded
shell/Home ownership milestone. Keep immutable counterexamples, run the inverse
cases on corrected production and rerun the affected accepted checks. Close the
two assigned composition-proof gaps above in a separate evidence milestone. Then run
fresh whole-App integration and return the correction packet to the project lead.
Do not rebuild previously accepted pages merely to increase test counts, overwrite
historical evidence or begin native/gameplay work. Global UI acceptance remains
with the lead after independent correction review.
