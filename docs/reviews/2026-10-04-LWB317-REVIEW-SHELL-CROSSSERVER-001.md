# Project-lead review — Cross-server popover, 2026-10-04

Decision: **COMPLETE / ACCEPTED for assigned recovered-source/local UI scope**.
Reviewed worker delivery `52710d3d1950cceccee2439c4979d3b99c797782`, including
production correction `5318467c43598993b8c9cad499643e037fbc7dde`.
No acceptance-blocking mismatch was found. No production correction was needed.

## Independent assessment

The lead read the complete App diff, recovery matrix, actual original helper/popover
bytes at UTF-8 byte 350405 (3406 bytes, popover starts at 350656), and parent callback
at 365565 (229 bytes) in `index-BVfnK1wp.js`. The worker's current integrity validator
verified the exact target EXE and asset identities, immutable baseline, current App,
recorded checks, screenshots and protected WIP. Recovery source-locators describe
the pre-correction dispatch App; the focused results and current validator pin the
corrected App. The baseline recovery runner is intentionally historical and was
not executed against the corrected App or rewritten.

Outside pointer-down, profile-keyed import/reset, event-time error localization,
offline/scan Enter gating, enabled input/trigger during busy states, list predicates,
and jump → summary → changed-history → close ordering agree with recovered source.
The parent tolerates summary rejection. The removed action-path status refresh is
source-backed and does not remove the independent App polling owner.

`LWB317-REVIEW-SHELL-CROSSSERVER-001/run-independent.mjs` executes the actual original
component and current App with inert API/page bindings. **11/11 new cases pass**:
seven numeric boundaries including blank and fractional input, obsolete profile import,
delayed history success and rejection while input/locale change, and exact original
parent summary rejection. Original supplied current-server props are updated on
successful jump to match the current App summary path before comparing snapshots.
This corrects a test setup mismatch; it required no product change.

The attempted additional subagent review ended at a usage limit and supplied no
assessment. Acceptance relies on the lead's completed direct review and executable
comparison, not on an unavailable peer recommendation.

## Re-executed validation and images

The lead reran `milestone-c/run-current-checks.mjs`: focused actual-App **12/12**,
affected retention/Map ownership/Home acknowledgement **3/3**, canonical `check`,
`build`, `check:production-build`, protected WIP **7/7**, and repository diff check
all pass. `milestone-c/validate-current.mjs` passes before and after the rerun.
Vite emitted its existing non-failing large-chunk advisory.

The lead inspected all three worker JPEGs: EN/light, JA/dark and narrow JA/dark.
Artifact dimensions and measured CSS viewport are recorded separately; narrow
popover fits the recorded 800×543 CSS viewport. The worker's recorded offline
Enter/Escape/click-away/return browser interactions and zero warning/error capture
remain worker observations. The lead did not independently drive a fresh browser
session and does not relabel these as independent browser observations.

Direct remote lookup matched the submitted full delivery SHA before review edits.
Reruns introduced no normalized change to worker result files. Historical evidence,
seven protected WIP hashes and unrelated working-tree paths remain preserved.

## Limits and continuation

The original contracts are `EXACT_BYTES` / `EXACT_CONTRACT`; this accepts their
assigned local implementation, not `LIVE_PROVEN` native behavior. Positive actions
are controlled inert callback proof. Native jump/history persistence, Last War
operations and original post-auth pixel equivalence were not exercised. Original
summary informational logging is not recreated by this UI unit.

This closes only SHELL-CROSSSERVER-001. Broader shared-shell/profile surfaces,
Map-tab summary-before-transition scheduling, untranslated Map notices, final
integration/inventory and original visuals remain separate queue gates. A new
bounded assignment is required before a worker starts one of them.

Independent evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-SHELL-CROSSSERVER-001/`.
Worker delivery/evidence remain historical and are not relabeled as the lead review.
