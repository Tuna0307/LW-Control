# PM-012 — accept focused Trade selection unit

Date: 2026-10-01. Reviewed correction:
`bfc652fd9586f64bbb9c4c221af065e44ad337e3`, following `a24bf6c`.
Disposition: **COMPLETE / ACCEPTED for focused source/local Trade selection**.
This closes CORRECT-003B and CORRECT-003B-R1. Parent CORRECT-003 remains PARTIAL;
full Automation, original pixels, native persistence and purchasing are unaccepted.

Direct remote lookup matches local HEAD. The only R1 product changes are the two
assigned composition expressions. Currency options now retain the first offer
for each positive currency ID, then sort by ID; goods labels retain the first
label per ID in encounter order. These match original AutomationPanel-BJ0gIqFh.js
at UTF-8 bytes 5605 and 1197, hash
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.
The lead's original harness and failing baseline were preserved unchanged.

Independent checks passed:

- Unchanged PM-011 actual-production-expression harness: all three cases pass.
- CORRECT-003B actual-handler/source-hash/anchor harness: immediate and queued
  saving, last-currency guard, goods selection/final-good disable and actual
  Retry/Discard pass. The previous saving-lock fix remains correct.
- Canonical npm.cmd run check, build and check:production-build; nine locale
  catalogs still have 1,383 messages, with 482 referenced keys. Source fingerprint
  ac160064f925ef2c17eb40e5f1ca91b76b50085dd0c88f66218e2b9e5ef6c37e; artifact
  fingerprint 43ac4d3d94fd709561e422e3192c2761330b1fddf87bee24824f9ef9ff4a93ec.
- All R1 evidence JSON parses, preserved baseline hash matches, all three reused
  screenshot hashes match, and git diff --check passes.

The worker's focused browser recheck records currency/goods labels, toggling,
Show exclusive and last-currency protection. It is worker evidence; no new lead
browser/game session was performed. The production package includes preserved
uncommitted AFK fixture work, which is outside this acceptance. AFK/scratch/parent
screenshot WIP remains unchanged and unstaged.

Next small task: CORRECT-003C, only Trade Station Purchased items presentation.
Existing history implementation and fixtures are inputs, not proof. Verify source
ordering, day grouping/totals, repeated items, row fields, missing optional fields,
locale formatting and the empty panel using local QA. Keep selection/weekly work
accepted. Cross-server controls, runtime summaries, Assist, AFK, native/service
integration and gameplay require separate assignments.

Matrix/ledger overall evidence states stay IMPLEMENTED_NOT_VALIDATED / BLOCKED
for original/native parity. The owner receives a copyable prompt; no worker chat
or subagent is automatically dispatched.
