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

### Owner-reported missing names — LEADMAP008-03

**P2, reproduced product wiring defect.** Owner sees Unknown Monster/Resource.
The scanner already records `monsterNameKey` and `resourceNameKey`; the Lua source
reads resource names from GatherResource configuration and monster names from
templates. The renderer resolves these through `gameTexts`, defaulting to an empty
dictionary. Current App page props do not supply that dictionary, MapRoutePage only
injects it for explicit preview fixtures, and no production UI caller of
`lastwar_localize` was found. The backend locale command/cache reader exists.

Read-only installed locale-cache inspection found manifest-verified EN/JA/zh-CN
values for archived scanned keys. Actual production column callbacks render
Unknown without texts and Gold Mine/Farmland/Glutton/Doom Walker with those existing
EN values. Evidence: `docs/proof/MAP-008-LEAD-NAMES-2026-10-12.json`.
The cache advertises sourceVersion 23443; its existence/hash is not a current-v24
native name-equivalence proof. Do not hardcode these example names.

Connect current-game name-key resolution to native Map tables and name filters.
Verify config/key/language identity, bounded requests (backend MaxKeys=200),
language and profile/game-root replacement, old-response retirement and retained
stored-data display. Handle truly absent keys honestly and make localization-load
failure observable rather than silently presenting every known row as Unknown.
Prove a clean extracted candidate resolves names using supported installed-game
or admitted cache acquisition, not an accidental dependency on this owner's old
cache or preview dictionary. Use real representative native names in EN and a
non-English locale; rare/event names may retain precisely disclosed limits.

### Additional efficiency hypothesis, not an established capability

Current corrected probes restore the home view and wait for its response after
each target response. Include that cost in the phase breakdown. Investigate whether
supported consecutive target-view requests can maintain fresh request-scoped
manager state with restoration at scan completion/Stop instead of after each
region. Test as a bounded region candidate first; do not remove the proven safe
restore behavior until successor ownership, freshness, completeness and exact
Stop/error restoration pass. This is an alternative to evaluate, not a claim that
it works, is camera-free or will halve scan time.

Same branch, same PR, solo. Fix LEADMAP008-01/02 and finish ready MAP-008
qualification work plus LEADMAP008-03 under the existing work item. Preserve passing product
corrections, negative records and main. No new release archive unless product
source changes or package identity needs correction. Return READY only for gates
actually passed; otherwise precise PARTIAL scope. Lead acceptance remains pending.

## Subsequent worker R5 evidence (2026-10-12) — for independent lead re-review

**This is the implementer's new evidence, not an independent lead decision.**
Original review findings and read-only lead negatives above remain intact.
Source checkpoint `9222e917a6fef5c75b32e0682d1600e1fbdb0103` first ran four
genuine v24 all-eight scans on one task-owned server-2212 session in the order
Normal/Fast/Normal/Fast. All four completed 10,000 fresh AOIs, 2,500 durable
blocks, 340 serial native probes and zero retries/failures/actions. Observed
whole-scan seconds: **210.651 / 207.424 / 207.905 / 206.860**. Normal n=2
median 209.278 (range 207.905–210.651), Fast n=2 median 207.142 (range
206.860–207.424). Both last-run native/SQLite auditors include genuine
Railway `TrainType=2` positive fields as well as ordinary Treasure and five
other positive kinds. The second N/F pair passed 18,114/10,785 distinct
field comparisons with zero discrepancy. First pair lacks a native
Railway-positive sample, so strict complete-positive qualification remains
**one paired comparison**. A repeatable material Fast advantage is still
unproved. Compact private-data-free source receipt:
`docs/proof/MAP-008-R5-GENUINE-2026-10-12.json`; detailed limitations and
earlier R3 comparison: `docs/MAP_008_SCAN_METHOD_CONTINUATION.md`.

Genuine native phase sums quantify the previously unmeasured home-view
overhead: **89.981–90.460 seconds home restoration per full scan**, over
340 probes each (about **49.5%** of native target+home response time).
Consecutive-region operation did **not** meet the successor correlation,
independent freshness/completeness and Stop/error cleanup gates, so the
proven safe per-probe restoration remains in production. No speculative
speed or complete world-recall claim was accepted.

Auditor strict owner/deferred inverses retained their **1 real + 15 rejected
mutations** acceptance behavior. A newly found proof-tool gap was corrected:
the sampler/auditor did not recognize actual Lua `treasure_point` and
`supplies_point`; representative real ordinary Treasure responses now match
saved identities/fields without discrepancies. These are still samples, not
full seasonal Treasure proof (seasonal and Ghost positives remain deferred).
The current stored Monster snapshot showed **8,831 ordinary Monster rows but
zero positive active shield** and no confirmed current protected enrichment;
active-protection-positive remains pending without manufactured evidence.

**LEADMAP008-03 was reopened on new end-to-end evidence:** the pre-R5 live
production Resource table displayed Unknown Resource despite installed name
keys. Actual native `game_root_status` returns `root`, whereas App passed
`gameRootStatus.path` to the loader, preventing requests. The source mapping
was repaired and the invalid-root UI state made observable. Updated mounted
MapRoutePage/MapDataPage integration covers the exact native root contract,
language switches and held obsolete replies. Independently rebuilt native
Windows Desktop reopened the genuine saved Resource/Monster tables and
displayed real installed-game names in Simplified Chinese, EN and Japanese.
The source-identified extracted clean package must include this R5 fix;
the original 9222 archive does not. This is a worker closure candidate,
**pending independent lead acceptance**.

The passive game was closed through Home and both task `finish` and `reentry`
re-verified native file hashes, no owned processes/journals, Auto off and
zero action jobs. Live claims, plunder, sharing, marches and spending were
not performed. PR #9 remains unmerged until an independent lead reviews the
new source, test and package identities. R5 disposition: **PARTIAL**.
