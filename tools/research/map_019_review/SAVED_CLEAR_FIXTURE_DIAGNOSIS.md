# MAP016 Saved Clear: accepted source exists, but normal startup destroys its Map rows

2026-10-24. Read-only / inert evidence only. No production, owner database,
recovery journal, game, installed scripts or package changed. No live pass.

## Exact failure

The production proof in
src/LWBridge.Desktop/LWBridgeWindow.Map016AcceptanceProof.cs:13-114
requires an explicitly isolated exact game owner, live home **2212**, fresh
disabled Auto schedule and *saved* Resource count for 2212 greater than zero.
It also requires 2213 saved Resource count zero, and the saved-server list
must contain 2212 but not 2213. All are tested **before** starting the remote
scan. It subsequently runs one genuine owned Resource Auto scan against 2213,
returns to 2212, clears only 2213 using the real packaged WebView,
reloads that same WebView, and verifies both dropdowns plus all persisted
home/remote invariants. Synthetic records are not admissible.

## Source provenance

The hardcoded source in tools/live-harness/run_ui.py:339-357 is:

    artifacts/map014-lead-ui/native-20261013-104453/profiles/map014-native-ui/map-data/map-data.db

It has **22,414** published rows, all 2212, including **7,975 Resource**,
a completed 2,500/2,500 2212 scan, and no 2213. Main DB SHA-256:
6c2a68ba19b7e10f352cc70c2a049ee3fde3c0688c6d9a1a81cdeea7c804cc65.
The original WebView proof subsequently failed on native Truck PNG rendering;
this older session is **not** a fully accepted packaged UI receipt, although
its persisted Resource records came from a real scan.

A stronger independently accepted home source is:

    artifacts/map014-takeover/native-ui-20261016-144924

Its actual packaged proof and owned-exit/restoration receipts succeed.
The completed fullworld scan ID/selected kinds match SQLite, the scripts
were restored, and no recovery journal remained. Its WAL-clean source DB is
77,250,560 bytes, SHA-256
d9f07e2a07ca9f62bacd3359abe277c0f1e587d267c611765179b375ddf0b6b6.
It contains **28,462** published rows, **8,002 Resource** on 2212, zero
2213, and one completed 2,500/2,500 home run. No trimming or invented rows
are needed to obtain a genuine 2212-only historical database.

The harness already uses SQLite read-only backup into a **fresh isolated**
profile DB before app launch (run_ui.py:349-352) and writes a source SHA receipt.
The failed run artifacts/map014-takeover/native-ui-20261024-080957
references the earlier 7,975-row source. Its closed clone still occupies
58,408,960 bytes, yet has **zero** map records, **zero** scan runs,
and an empty saved-server list.

## Root cause: R18 session-only startup policy

Program.cs:110-129 calls StartupMapScanCleanup.Clear on **every normal startup**,
including this isolated packaged proof. StartupMapScanCleanup.cs:5-34 obtains
per-DB process leases and invokes MapStore.ClearAllScanData on the profile DB.
MapStore.cs:517-529 deliberately deletes scan_records, scan_blocks, scan_runs,
map_records and treasure_claim_states under OWNER_DIRECTION R18: scan rows are
valid only for the current app session. Settings, player marks and action
history remain. MapStore.Query.cs:16-26 derives the saved-server selector
from map_records, so it also loses 2212 after startup.

This creates a **product proof/lifecycle contradiction**. Any prior-process
saved source is cleared *before* MAP016 can read it. Changing the historical
fixture, stripping foreign-server rows, or selecting a newer accepted source
cannot satisfy the proof as written. Suppressing R18 cleanup, replacing a
running owner's SQLite file, importing stale cross-session rows after startup,
or fabricating scans would bypass production safety invariants. **Do not.**

## Reproducible safe audit (no game or DB write)

    python -m unittest tools/research/map_019_review/test_audit_saved_clear_source.py -v
    python tools/research/map_019_review/audit_saved_clear_source.py --source-run artifacts/map014-takeover/native-ui-20261016-144924 --failed-run artifacts/map014-takeover/native-ui-20261024-080957

The added read-only audit verifies accepted genuine source and exact fullworld
scan receipts, restored owner, no foreign rows, SQLite integrity and DB hash.
It refuses a nonempty WAL or rollback journal rather than silently reading
an incomplete main-file snapshot, and asserts that product startup still wipes
historical scan rows. Six inert tests check acceptance, contamination,
rejection of forged/stale provenance, WAL failure, input boundaries and the
actual production R18 deletion applied **only to a temporary test clone**.
No live-ready fixture generator is implemented because none can preserve
prior-session rows through the current supported startup boundary.

## Recommended production proof redesign (prime decision)

Keep R18 normal startup clearing. After exact packaged ownership is ready
on live 2212, run one *new genuine current-session 2212 Resource scan* through
the normal scanner, verify a completed 2,500/2,500 home scan and positive
published 2212 Resource rows; **then** perform the existing owned 2213 Auto
Resource scan, return, UI Clear/reopen and unchanged-home assertions in that
same app/game/WebView session. The receipts should distinctly correlate both
native scan runs, verify no cross-session source reload, and preserve exact
cleanup and normal startup semantics. The proof wording must reflect a
*current-session preserved home* instead of preloaded historical saved rows.

This needs reviewed production proof changes, a new source-pinned package
and actual live qualification by the sole owner. None were undertaken here.
Current packaged Saved Clear remains **NOT QUALIFIED**.
