# MAP-006 R1 — independent acceptance and release

2026-10-11. **ACCEPTED / MERGED / PUBLISHED** for the currently available Map scope.
Whole Map remains **PARTIAL**. This supersedes the earlier CHANGES_REQUIRED
decision for LEADMAP006-01, preserving that review and its negative receipt.

## Accepted scope

Manual City, Resource, Monster, Truck, Railway and Secret Task/Dispatch acquisition;
applicable stored browsing, filtering, sorting, pagination and persistence;
previously accepted City marks/export/Clear; passive navigation; bounded multi-server
Auto acquisition, cancellation and return. Accepted single-game Home is preserved.
Ghost positive acquisition, full seasonal Treasure and live plunder/claims/sharing
are pending. Their incomplete providers are not accepted working operations.

The worker's genuine v24 six-kind scan completed 2,500/2,500 blocks with zero
failures, publishing City 7,060 / Resource 8,003 / Monster 8,850 / Truck 459 /
Railway 2 / Dispatch 1,710. Counts are observations, not fixtures or fixed totals.
Earlier lead read-only native/SQLite/XLSX audits and current source inspection
support applicable stored controls. No private player dataset is included in the ZIP.

## Independent R1 review

Reviewed the exact c90576b8..66c349a4 product/test/tool diff and the scheduler,
native transition ownership, acquisition metrics and raw private benchmark receipts.
The cycle now owns and awaits exact-run Stop before return; Cancel/Retire signal
the cycle rather than racing an independent Stop against its finally block.
Stop/return errors stay visible; successor ownership and retirement are fenced.

Fresh lead execution passed:

- All five canonical frontend check groups and nine 1,383-key catalogs.
- Desktop default native suite, including the nine new held Start/Stop, foreign
  target, successor, retirement and Stop/return-failure scheduler scenarios.
- The original source-linked lead Stop probe: returnBeforeStop=false, one return,
  reached origin, canceled outcome, lastError=null, zero external game actions.
- Actual isolated Windows named-pipe recovery/concurrent-route probe.
- Fresh canonical UI build/package validation and Release Desktop build,
  zero warnings/errors. Initial ignored local dist metadata was stale; rebuilding
  it resolved the check. The submitted ZIP's independent UI validation passed.
- Independent ZIP extraction, source/EXE version/hash and private-file exclusion.
  The extracted executable rendered an inert Map capture, exited 0 and recorded
  no JS issues. Its fixture displayed Simplified Chinese despite --language en;
  this is startup/render proof, not new English native-pixel evidence.
- Read-only R1 native DB consistency: 178 unique Truck records, zero staged rows
  or jobs, source uuid/march identity equals every stored key, typed coordinate/
  identity fields, and capture timestamps inside the actual completed run.
- Fresh installed-script hash comparison and process inventory: exact original
  script triplet restored, no LastWar/launcher/Desktop actors. No new lead live
  game launch, scan or account action was performed.

The worker's genuine cross-server R1 receipt separately proves 2212 -> 2213
active Stop -> 2212, canceled exact run, zero partial publication, no Auto error
and verified cleanup. It is inspected worker live evidence, not a new lead attempt.

## Speed and accuracy: measured limits

Truck-only Fast on v24 server 2212 completed in **97.887 seconds**, with **340
application-to-Lua probes**, zero retries, observed 10,000-AOI coverage and 178
unique Trucks. Lua probes are not attributable network-packet totals.

A direct AOI candidate timed out after 8.065 seconds without confirmed fresh
data. A fresh official Train list completed in 306-355 ms but contained no Trucks
on the target server, so it cannot replace full target-server acquisition.
Rejecting these candidates and retaining the working adaptive scanner is valid.
This finite screening does not prove globally fastest acquisition, perfect recall,
original encrypted traversal, or comparative full-world performance of a second
successful method. Field presence and DB consistency are not an independent
world-object ground-truth audit. Do not equate row counts with perfect accuracy.

## Release identity

- PR #8: https://github.com/Tuna0307/LW-Control/pull/8 — merged.
- Product source: `66c349a4b8e63119e2cc397ed346eb40d64abbab`.
- Main merge: `70b952ab44dde084ea95e3c613b9fe2d702ab426`.
  Product `src`, `tools`, `tests` are identical to the reviewed source.
- Release tag: `map-available-v0.6.0` (publication receipt records upload result).
- ZIP: `LW-Control-MAP006-AVAILABLE-RC-66c349a4b8e6.zip`, 9,564,974 bytes,
  83 archive entries / 81 application files. Reviewed RC filename retained.
- SHA-256: `346895824612B3A3DDFC774AFFCFCCC25DA873941F9306C958021A5D171B30AB`.
- EXE SHA-256: `55ACBE26AD43D2697906342562CDD950A41CAED92763B570CDFC8B08451B202A`.
- EXE ProductVersion: `1.0.0+66c349a4b8e63119e2cc397ed346eb40d64abbab`.
- Windows x64; .NET 10 Desktop Runtime, WebView2 and Python available to the app.
  Auto Launch Game defaults ON. Extract the whole ZIP and run Launch LWBridge.cmd.

Unknown resource/monster labels can remain where current metadata is unavailable;
they are not fabricated names. Original commercial licensing/login is excluded.
Owner-only action witnesses and simultaneous official two-game Home F-07 are not
included in this release. Preserve original negative records and ignored receipts,
DBs and packages. Retire the merged feature branch after ancestry/checkout checks.
