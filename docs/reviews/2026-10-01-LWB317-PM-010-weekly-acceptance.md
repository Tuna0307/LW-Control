# PM-010 — accept focused weekly-settings correction

Date: 2026-10-01. Reviewed implementation:
`d065d08895088df9f3f053ebe0c78a6e048b6e91`.
Disposition: **COMPLETE / ACCEPTED for the focused local weekly-settings unit**.
This closes CORRECT-003A and its R1 correction; it does not accept full Automation,
original pixels, native config persistence or the parent CORRECT-003 campaign.

Direct remote lookup matches local HEAD. The worker changed only the two weekly
onChange paths in product code, plus focused tests/evidence/documentation. The
uncommitted AFK fixture, scratch files and parent screenshot remain preserved.

Both callbacks now use edit(...,false) followed immediately by handled flush(),
matching original AutomationPanel-BJ0gIqFh.js W at UTF-8 byte 27880 and its weekly
caller at 33073. The saving selector lock remains removed; unrelated field/store
debounce behavior is unchanged. Original default arrays, weekdays and choices
remain as established in the earlier weekly source evidence.

The lead read the revised actual-callback harness and independently reran it.
It extracts both production JSX onChange expressions, starts exactly one write
immediately per first edit, invokes the same callback again with the first reply
held, and verifies the second write follows the first acknowledgement. The older
reply updates confirmed state without replacing the newer draft; both stores end
clean with the latest value. It also exercises actual Retry/Discard button
callbacks after failures from actual weekly callbacks. No test-authored flush or
blur substitutes for production weekly behavior.

Independent checks passed:

- Actual weekly-callback deferred-write/old-ack/failure/Retry/Discard harness.
- Focused weekly source/default/weekday/restriction/store check.
- Worker evidence verifier: all source hashes, both screenshot hashes and byte
  anchors match; anchorIssues is empty.
- Canonical npm check: static/CSS, Home, Map, locale and draft checks; 482 locale
  keys referenced and nine 1,383-message catalogs.
- Existing production-package validation: source fingerprint
  a9814f4f2a11e713625ec351fcf0cee59c497b76be506efadd2905410ba50fba and artifact
  fingerprint 6759aa864d5421d420f64002933a78fff4f2457f63dba69cfbd275feb09b2788.

Package validation concerns the current working tree, which includes preserved
uncommitted AFK fixtures; it is not a clean-commit-only AFK acceptance. That WIP
is outside the graded scope. Worker browser rechecks are recorded in the updated
browser-results.json and remain worker evidence; the lead performed no new
browser/game session during this review. Original failing onchange-baseline.json
is preserved alongside the passing onchange-r1.json.

Parent CORRECT-003 remains PARTIAL. UI matrix/ledger rows retain
IMPLEMENTED_NOT_VALIDATED for original/native parity. Next small assignment:
CORRECT-003B, only Trade Station currency/goods selection and local save-state
verification. Trade history, runtime summaries, Dispatch Assist and AFK remain
separate later units. No native/gameplay campaign opens.
