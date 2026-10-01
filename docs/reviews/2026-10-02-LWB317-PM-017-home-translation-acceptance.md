# PM-017 — accept independently reviewed Home error translation

Date: 2026-10-02. Independent review commit:
baf5473c09bcb371780b947a9d624e04a45517ad. Reviewed implementation:
LWB317-UI-HOME-ERROR-001, initially delivered at 537a2b3 and retained in current code.
Disposition: **COMPLETE / ACCEPTED for focused recovered-source/local Home
translation and recovery-detail composition**. The main lead integrates the
returning reviewer's independent recommendation, not a self-review of its own code.

The review commit contains no product changes. Lead read the review, actual
independent checker, cases, locale/source locators, browser JSON and screenshot.
The 13 cases execute extracted original Ir/Lr and actual current translatedError,
with explicit distinguishing expectations for reversed/deduplicated priority,
namespace order, object/message/Error/stringification, lowercase object codes,
unknown input and generic fallback. Actual original qr and current Home rendering
also agree on Japanese recovery detail. No mismatch or correction is required.

Exact source index-BVfnK1wp.js SHA-256:
44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
Zero-based UTF-8 bytes: Ir 328453, Lr 328684, qr 336694. Target executable hash
rechecked: 4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.
Source and English/Japanese locale byte locators/hashes are pinned in independent
review evidence. Original Lr has an optional custom fallback-key argument; current
recovered Home uses its default, so this API difference is outside this acceptance.

Lead independently reran check-review.mjs --verify-record and review evidence
validator, plus the unchanged HOME-ERROR-001 saved report/evidence validator:
13 independent cases, 60 existing edge cases, 4,230 nine-catalog checks and 27
Home render comparisons pass. Canonical check and production package verification
pass. The review changed no product files, so no additional rebuild was necessary;
the worker rebuilt with unchanged build/package fingerprints:
53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e /
96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782.
HEAD/tracking/direct remote matched the review SHA; git diff --check passes.

Worker browser evidence records English generic fallback and Japanese recovery
composition with native controls disabled and no captured errors. Lead viewed the
hash-validated Japanese image; no new lead browser, picker or game session opened.
Evidence: evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-001/; lead checkpoint
verification: evidence/lwbridge-0.3.17/ui/LWB317-PM-017/verification.json.

Limits: native error-object/producer contracts, App rejection-to-string reduction,
original runtime pixels, native persistence and gameplay/lifecycle are not accepted.
HOME-ERROR-002 and HOME-BUSY-001 remain AWAITING_REVIEW; Trade 003E also remains
pending. UI matrix remains IMPLEMENTED_NOT_VALIDATED / BLOCKED as applicable.
Existing AFK/scratch/parent screenshot WIP remains untouched and unstaged; package
verification includes existing AFK fixture bytes, outside this unit's acceptance.

Next small assignment: LWB317-REVIEW-HOME-ERROR-002, independent source/local
review of error channels and existing frontend callback/host response mapping.
No native picker or gameplay execution is assigned.
