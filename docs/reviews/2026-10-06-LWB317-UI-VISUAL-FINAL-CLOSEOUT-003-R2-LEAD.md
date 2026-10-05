# Final recoverable UIUX lead acceptance

Date: 2026-10-06. Reviewed worker delivery:
`75b7b215679a8ea7986af72bdbdd7039c5278b70`.

**ACCEPTED for the assigned recovered-source/local UIUX phase.**
R2 closes LR-EQUIPMENT-FLUSH-001. The six earlier corrections, AFK/Automation
composition gaps and AFK/Equipment profile-owner correction remain accepted.
No demonstrated source/local UIUX blocker remains from these campaigns/reviews.
This closes CLOSEOUT-003/R1/R2 and their REMAINING-002/FINAL-CAMPAIGN-001 parents
for this bounded scope; historical pending/changes-required entries are preserved
as history, not current continuation.

## Independent review and checks

The lead verified actual HEAD and direct origin revision match the delivered SHA,
with a clean startup tree. The production diff from `e159710f` changes exactly one
line: Equipment's shared explicit-save path calls `flush(false)` instead of
`flush()`. The global draft-engine default and all other production files remain
unchanged. This matches exact original Squad `fd.P`, UTF-8 byte 180274.

The lead independently executed against its own current-source Vite port 4442:

- Worker focused Equipment verifier, rewrite-free: **38 assertions**, 2 pinned
  captures, zero issues. It executes original T/P and mounted current consumers.
- Worker Equipment regression verifier, rewrite-free: **17 assertions**, zero issues.
- Fresh copies of both proofs in `r2-lead-review/`: same 38/17 passes, with fresh
  focused captures and results; no historical/worker record was overwritten.
- Fresh complete-App replay and readonly final validator in `r2-lead-review/`:
  **282 assertions, 67 served sources, 16 decoded complete-App screenshots, nine
  1,383-key catalogs, 12 semantic mutation detections, zero console/page issues**.
- Canonical frontend check, fresh production build and production-package check:
  PASS. Build `ff5b005c08e03051a7bc58aa2e88a44261592ffa96b5f2e9bfb85a62c09cc53d`;
  package `c0928be052ae6d376f939b8b8f7581df874fd8422bfa4a31f6c0c1b676eef737`.
- Submitted readonly R2 source/evidence/screenshot/tool acceptance-chain validator:
  PASS. Historical R1/R1-lead/A–E and accepted parent records remain preserved.

The distinguishing trace now matches the original: pending rename on A → B → A →
unrequested equipment move → first acknowledgement confirms only the first draft
and leaves the later move dirty. A later explicit Save confirms it. An explicitly
queued second Save still drains. Rejection/Retry/Discard retain their source-like
behavior and profile isolation. The captured dirty state is an intended unsaved
state, not failed persistence or native execution proof.

The fresh runner/support files are byte-identical copies of the audited worker
files, except the copied readonly validator's two R2 result-file lookup paths
were relocated to r2-lead-review. Its local self-hash descriptor was updated
for that path-only adaptation; no assertion/result/source pin was relaxed or
rewritten. The unmodified submitted validator also passes against its original
packet. Fresh outputs are in a separate directory. They establish actual current
served-App execution and source identity; inherited original page-local authority
comes from the accepted renderer/pixel/contract packets. This is not a newly
obtained protected original complete-App pixel oracle. Semantic mutation coverage
includes executable and structural checks; it is not twelve full browser runs.
The optional unfinished worker audit is not counted as acceptance evidence.

## What is complete

Assigned recovered post-auth frontend presentation and local interaction contracts
for Home, Map, Automation, Squads/Equipment, City Layout, Hotkeys, Mini Games,
Settings, and shared shell/sidebar/dialogs are accepted through their reviewed
source/local evidence and final integration. Original login/account/licensing UI
remains intentionally excluded. No redesign or fallback was introduced.

## Remaining separate dependencies

Acceptance does not establish native/provider-positive functionality, Last War
compatibility/live execution, updater/OS behavior, loaded native game assets or
protected post-auth original complete-App runtime pixels. Existing unavailable
provider actions stay fenced and documented. Visual states that depend on those
future producers/assets must be verified when they become available; they are
not relabeled LIVE_PROVEN by this UI closeout. This is not a claim that every
possible state was exhaustively tested or that the complete working clone exists.

Next assignment: `docs/work-items/LWB317-FUNCTION-READINESS-HOME-MAP-001.md`, a
read-only source/contract/provider readiness audit for the functionality phase.
It authorizes no native implementation or gameplay test. This review changes
documentation/evidence only. Lead-owned port 4442 is stopped at closeout; owner
listeners/sessions are preserved.
