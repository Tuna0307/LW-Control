# MAP-008 independent lead review — 2026-10-12

Reviewed source: `473d64836f75c226ad6e4f3db1fd6ec004485351`,
`codex/map-action-providers-007`, PR #9. Decision: **CHANGES_REQUIRED for
accuracy-proof acceptance; scanner performance qualification remains PARTIAL**.
No merge or publication. Accepted main/v0.6.0 remains preserved.

## What the lead independently checked

- Read the owned production changes from lead checkpoint `641c70d7` through
  delivery: preview retirement, placement policy, all-spatial deferred camera
  restoration, world matching and bounded engine/source diagnostics.
- Canonical frontend `check`: PASS; nine catalogs contain 1,389 keys each.
- `check:map008-lifetime`: PASS, 8 mounted inert cases. This supports closure of
  LEADMAP007-01/02 for the tested retirement and successor paths.
- `check:production-build`: PASS; source fingerprint
  `a6037a9e85316b647d49bcb64636e4be6bc3c230a508578b588f988481e15452`,
  UI package fingerprint
  `defcba52e13800e5e7bbf174e74641790059a9957161481c60cb9d95f3dfb9a2`.
- Actual production planner/engine controlled probe: PASS, 255 selections and
  15 adversity cases. Its intentionally failing diagnostic callback logs an
  expected error without changing durable successful publication.
- Desktop default Release checks: PASS including Home, MAP-005/006 and MAP-007;
  no fresh live game actions were performed by this review.
- Direct remote branch head equals the reviewed source. PR #9 attached to chat.
- Opened the candidate ZIP: SHA-256
  `57E57F6927312F4E15B781A502AB29357F523EBF7DE922916E2A8D6A9437E94E`,
  103 files (127 entries including directories), embedded source equals reviewed
  source; no `.db`, `.sqlite`, `.pdb` or installed original script triplet names.
  This is integrity/name inspection, not a newly executed packaged live scan.

The all-spatial deferred restoration change addresses a concrete observed
omission, rather than treating full AOI counters as proof of complete populations.
Archived same-target contrast documents 30 native AOIs with zero selected records
under immediate restoration versus positive spatial records under deferred
restoration. The lead inspected that record and source; did not repeat its game
experiment. Corrected scans are slower than the inaccurate historic trials.
That is an accuracy correction, not evidence of a new speed advantage.

## LEADMAP008-01 — native auditor accepts uniformly foreign owners

**P2, reproducible proof-tool defect, not a demonstrated live scanner escape.**
`audit_native_fields.py` establishes its expected owner from the first witness,
then only requires subsequent witnesses to agree. It does not bind that tuple to
an independently established expected profile/session/process for the requested
scan. Changing every witness to the same wrong owner therefore still passes.

Read-only archive used: the final City Auto server-2212 run
`621db75ef6524f16ad3f6c1c86f10379`. Its original sample passes 198 comparisons.
Replacing all profile/session/PID values with a controlled foreign tuple also
passes those same comparisons. This archive has one native response; rerunning
the worker's own inverse checker on it fails at `wrong_session` because changing
the last response changes the only response and the auditor accepts it.

Bind audit admission to an independently verified run-owner receipt, not identity
learned solely from the witness being audited. Reject uniformly wrong profile,
session and PID, a single-response foreign owner and mixed owners. Keep legitimate
archived baselines passing where independent ownership can be established. Missing
archive identity is an explicit unqualified proof, not permission to manufacture it.

## LEADMAP008-02 — required native contract fields inferred when absent

**P2, reproducible proof-tool defect.** Under `--require-deferred`, the auditor
defaults an absent `deferRestoreUntilResponse` to true and an absent
`requestMethod` to the expected method. Removing both fields from the same genuine
archive still passes. It therefore accepts less evidence than the flag claims.

Require explicit fields for the strict current contract. Preserve older incomplete
receipts as historical/limited evidence, not silently upgrade them. Add missing,
null and wrong-value tests and revalidate qualifying archived receipts.

Both negative observations are preserved in
`docs/proof/MAP-008-LEAD-AUDIT-2026-10-12.json`. Reproducer:

```powershell
python tools/map_008_lead_review/check_native_audit.py --db artifacts/map-006/trial-scan008stage_20261011/isolated/profiles/map-006-scan008stage_20261011/map-data/map-data.db --native artifacts/map-006/trial-scan008stage_20261011/auto-watch-city/native-621db75ef6524f16ad3f6c1c86f10379.jsonl --run 621db75ef6524f16ad3f6c1c86f10379 --server 2212 --out artifacts/lead-map008-20261012/new-lead-observation.json
```

Its exit 0 saves observed acceptances; it is not product acceptance. Original
DB/native inputs are read only and mutations use disposable witness copies.

## Remaining qualification, already honestly declared by the worker

Normal/Fast individual measurements are predominantly n=1, around 189–199 seconds,
both 340 Lua probes and actual camera concurrency one. There is no demonstrated
material Fast advantage. n=2 observed six-kind pairs also show no separation;
missing positive Railway field witnesses narrow strict accuracy qualification.
Do not make Fast slower artificially or promise a speedup the client cannot supply.

Continue a finite alternating comparison for representative individual and mixed
workloads after proof corrections. Independent live Railway values and active
Monster protection remain missing, not confirmed product failures. Use supported
controlled field checks for rare states and retain exact live limitations; do not
blindly launch repeated full-world scans hoping for an unavailable event.
Ghost/full seasonal Treasure positives remain owner-deferred. No complete
camera-free/full-target-server replacement has been established by the tested
alternatives, which does not prove every possible alternative impossible.

Correct the proof tools, revalidate available archived evidence and invalidate
unsupported qualification labels without overwriting historical records. Bind
qualifying run evidence to actual producer/source identity as well as run ownership;
the compact benchmark's `sourceCheckpoint=5e44531b` predates the final camera fix,
so it alone does not identify the corrected source used in later measurements.
Use available capture/build receipts; do not repin old evidence retrospectively.

## Exact continuation

Same branch, same PR, solo. Fix LEADMAP008-01/02 and finish ready MAP-008
qualification work under the existing work item. Preserve passing product
corrections, negative records and main. No new release archive unless product
source changes or package identity needs correction. Return READY only for gates
actually passed; otherwise precise PARTIAL scope. Lead acceptance remains pending.
