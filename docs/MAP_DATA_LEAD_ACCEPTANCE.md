# Stored Map data — independent lead acceptance, 2026-10-09

**Accepted for stored City/Resource browsing and City XLSX export**, after correcting one further original pagination boundary. PR #5 delivers this slice on top of released Home v0.2.0. Scanning and complete original Home/Map parity remain incomplete.

The lead independently reviewed worker `ba14ac6a5de26873e5ee0d79959c34371b2fbd49`, reran native Home/Map checks and canonical frontend/package verification, and revalidated 62 exact original native facts without rewriting historical evidence. The worker's finite f64 level-bound and i64 page-envelope corrections are supported. The strict legacy DTO remains separate from the native original-compatible boundary.

## Additional original offset correction

The worker's saturation of `(page-1)*pageSize` was not equivalent. The original stores the lower-bounded page at `0x3E1157`, stores the clamped page size at `0x3E1218`, and at `0x3E583B–0x3E58B8` appends the size, loads the page, subtracts one, performs a signed 64-bit `imul` and appends that exact integer as OFFSET. There is no overflow clamp. The SQL uses LIMIT/OFFSET (literal at `0xD6057E`).

For page `9223372036854775807`, size 200, the original product wraps to -400, which SQLite treats as a zero offset. The worker returned empty rows by saturation. The lead first changed the actual native-boundary assertion to the recovered outcome: it failed against worker production. Production now uses explicit unchecked i64 multiplication. The inverse passes, as do four non-overflowing/negative/positive-wrap boundary cases and five additional f64 inversion/string/nonfinite/extreme cases. These are static original machine arithmetic plus actual production command/SQLite proofs, not execution of the protected original runtime.

One lead boundary test initially used an off-by-one page constant; it was independently recalculated and corrected. The failing log is retained alongside the distinguishing product failure. No product policy was changed to satisfy that mistaken expectation.

## Genuine stored data and export revalidation

Both archived source database hashes match the worker preflight. A separate read-only SQLite backup of the isolated published database contains City 7,000 and Resource 8,008, with zero staging. The lead invoked actual production `Map317CommandService` against that independent copy: counts, distinct 50-row pages, descending level order, empty keyword, City marked-only, City `NANO 1` count 1, Resource `100281` count 2,675, filtered City export independent of page, and newly reopened native Resource count pass. No game or scan was launched for this review. An initial probe incorrectly applied City marked-only semantics to Resource; the expectation was corrected to the source/UI-scoped behavior and its failed log retained.

Both worker native UI XLSX files independently reopen and match all 7,000 records and 12 EN/JA headers, including one duplicate UUID represented by distinct source identities. All cell mismatch and missing-row counts are zero. The database file SHA differs from the earlier preparation snapshot after ordinary native writes; original source hashes and row/value checks remain correct. Do not confuse a physical database hash with published-row equality.

The actual packaged UI captures were inspected for filtered City, Resource page 2/161 retained across tabs, and Japanese/dark narrow layout. The screenshots show native stored results and game-stopped browsing after a server context had been established. The documented restart dependency remains: after app restart a fresh real connection may be needed to establish a browse server; offline startup is not claimed to select one. No new live repeat is needed for this query arithmetic correction. Worker live Stop receipts, process exit and the installed original script triplet are independently checked. Full worker UI issue/attempt history is retained locally, including the initial original-default Auto Launch admission.

## Delivery and limits

Worker ZIP hash matches `350190B2822D5B937DD069E4BEC64089F00ABF3FFFDCE96259F56BAA70873456`; it predates the lead offset correction. The accepted release is rebuilt from merged main, identifies that exact commit and undergoes extracted-package smoke and production UI integrity checks. No private datasets or workbooks are bundled.

Small sanitized results are in `proof/map-data-003-lead/`. The raw datasets/workbooks/captures stay in ignored local artifacts. Full original runtime pixels, encrypted scanner traversal/retry, outside-world running-server status and cancelled-scan publication proof remain separate. Resource export remains unsupported; no fallback or new scanner policy was added.
