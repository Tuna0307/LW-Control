# PM-020 — integrate independent Map state review

Date: 2026-10-02. Reviewer checkpoint:
`6f52096d050c7d2b005d61e6c5b6e80cd1538eeb`.
Decision: **ACCEPT for the focused three-table source/local review scope**.
The broader LWB317-UI-LEAD-TABLES-001 remains AWAITING_REVIEW for remaining scope.

## Basis

Lead inspected the reviewer commit: review/work-item delivery and independent
evidence only, with no product edits. The checker extracts actual recovered
functions and actual current MapTable JSX; expectations are not a duplicate
guessed implementation. English and Japanese cover 56 task comparisons,
seven timestamp inputs, nine Treasure world cases, eleven player cases and nine
name cases per locale, plus six table renders. Metadata, status classes/labels,
selection disablement and Treasure actions are checked against actual source.

Reference MapDataPanel-B4GXEND2.js SHA-256:
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
Zero-based UTF-8 anchors: P 8666, F 8753, world resolver w 4705, player resolver
T 4957, name resolver Ke 7610, column factory nt 9594, pe 16822 and me 17072.
Reviewer validator verifies those expressions and the exact target executable
SHA-256 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

Lead replayed the independent checker with --verify-record and its validator;
both pass. Prior eight-table checker --verify-record and its protected-WIP/source
validator pass: 288 column cases, 15,264 values, 72 renders and 3,150 Treasure
cases. Canonical npm check and check:production-build pass. Package fingerprints
remain 1280d8a4df7aec2f81260a8081c0dda7626bad06e8771d25cea4e3ae0c49daf2 /
b2a903176188b57cd02f281ffaded42aa05a0d6503595e25ee3e5fcfa09127ae.
No product change requires a rebuild. Direct reviewer remote SHA was verified.

## Findings and limits

The independent review's no-blocking-mismatch result is supported for its
explicit formatting/selection scope. Recorded browser DOM checks cover the
three tabs, English/Japanese labels, disabled offline row actions and zero
captured console errors. Lead validated screenshot hashes and opened both exact
saved images. They show the table shell but clip most relevant row/column states;
they do not independently demonstrate all asserted statuses. Lead has not
replayed the browser. Acceptance rests on executable actual-code checks and the
reviewer's recorded DOM observations, not full screenshot visual proof.

The reported Treasure limitation is valid and remains an open UI gap:
the original owns refreshing state at byte 31675 and passes it at byte 56776;
current normalized-LF MapDataPage MapTable caller at byte 40877 omits the prop.
The table/helper accept it and produce Checking for absent states when true.
That proves formatting, not page-level reachability. The assigned review
explicitly separated those concerns, so its narrow acceptance stands while
LWB317-UI-MAP-TREASURE-REFRESH-001 is queued for a separate source/local UI task.
This gap must not disappear merely because native Treasure refresh is out of scope.

No full Map/UI acceptance, original post-auth pixels, native actions, gameplay,
live game-text acquisition or asset parity is established here. Other five normal
tables, Scheduled Plunder, filters/action details and wider interactions remain
separate. Source/local acceptance does not upgrade overall matrix/live statuses.

## Integration and next assignment

Updated UI master, parity matrix, feature ledger, lead sheet, work items and
handoff with the narrow acceptance and open producer gap. Existing protected
AFK/scratch/parent screenshot WIP is unchanged and unstaged; historical evidence
is not regenerated. Checks and judgments are recorded under
evidence/lwbridge-0.3.17/ui/LWB317-PM-020/lead-verification.json.

Next sole worker task: LWB317-REVIEW-HOME-BUSY-001, a medium independent review
of Home busy labels, precedence, disabling and App input producers. No product
edits, native picker or game launch. Its screenshot must visibly show the controls
under review. Trade 003E and Map follow-ups are not assigned in the same run.
