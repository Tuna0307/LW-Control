# Project-manager checkpoint — review 8

Reviewed 2026-09-10 against `4ec7ef8d1bb3c70f816265b1d2328143b6e3c7de` on `research/offline-controller`, clean at start. Nine commits follow PM review 7 (`fe97542`). This replaces the [review 7/follow-up narrative](reviews/2026-09-10-review-7-and-followups.md) for current planning.

## Result for the owner

**One real data-service integration checkpoint is complete; eight further commits provide research. No new end-to-end game feature is enabled.** The two pages remain roughly **25–30% complete** as a broad engineering estimate. Local UI/settings and offline data work exist; game launch/connection, real scanning, complete results/export and automation remain unfinished. No full acceptance case is newly signed off.

There is now a separate official-game update problem. The launcher downloaded a patch that matches its advertised checksum, but the output it produced fails the expected checksum. The old active script and all 14 sampled executable/container anchors are unchanged; the latest data table changed. The game was not repaired in this audit. See [the incident and first recovery task](lastwar-update-incident.md). Do not treat passing rebuild tests or the old version number as a healthy updated game.

## Accepted work since the last audit

| Work | Decision and remaining limit |
|---|---|
| `62aabda` / R6-038 / PM7-A | Accept the shared published/staged option/count service and isolation/concurrent-snapshot tests. It replaces the prior split helpers, but its actual callers are still tests; the public options command still rejects. PM7-A is complete as bounded service integration only. |
| `c091445`, `183cdfe`, `7c0e237` / R6-039–041 | Accept recorded runtime-material persistence and device-key boundary findings as static reference evidence. No real state provider enabled. |
| `01663b4`, `c545663` / R6-042/043 | Retain marker locations; R6-043 supersedes the earlier interpretation of diagnostic labels as configuration names. Secure-proxy helper findings do not establish plain-proxy parity or complete loader/handler behavior. |
| `03443cf` / R6-044 | Accept recorded server-source fallback and active-read mismatch rules; not live connection/readiness proof. |
| `ef1f3d1`, `4ec7ef8` / R6-045/046 | Accept recorded package consumer and bounded envelope-reader boundaries. Field ownership/remaining handlers stay unresolved; do not reroute SB-79's denied operation. |

Evidence locators, hashes and reproducible commands remain in [Map Scan findings](lwbridge-map-scan.md) and [the index](README.md). This audit reviews code, saved findings and source metadata; it does not independently repeat reference disassembly/decryption or certify every historical alternative around an operation restriction. No new guessed runtime behavior is enabled.

Post-review continuation `LWB-R6-047` advances PM7-C without changing public behavior: the verified reference now pins the shared native `map_records`/`scan_records` upsert helper, exact staging selector, shared record bind layout and `record_key` input slot at `+0x78`. It confirms the persistence boundary consumes an already-derived identity. Per-kind construction of that slot, production ingestion and live scan proof remain blocked; a later focused binary-dump follow-up was rejected by automatic safety review and was not rerouted.

## Audit corrections and next tasks

- **PM8-0 / first:** recover a healthy official launcher update and establish a fresh script/table/runtime baseline. `LWB-PM8-002` reproduced the same verified patch/output CRC mismatch on a fresh launcher run and recovered the official Super Cleanup/integrity-repair surface. The current UI-automation safety restriction blocks selecting that launcher control, so repair and successful normal launch remain open. The [incident document](lastwar-update-incident.md) carries exact evidence and limits.
- **PM7-A / closed:** do not repeat source/count integration. Tests now pass for one shared service; public progress serialization and authoritative state remain prerequisites.
- **PM7-B / continue:** actual handler/current-runtime linkage and a usable state provider. Keep the research focused on what unlocks this result, with named remaining contracts. The loader gap is now explicitly tracked as **ESC-005 NEEDS_INFORMATION**, not silently buried in SB entries.
- **PM7-C / next critical path:** supported launch/handshake plus native identity/normalization and the smallest real scan. R6-047 narrows native identity work to the upstream producer that fills the shared record's `+0x78` `record_key` slot. Offline recovery may proceed while launcher repair is pending; live acceptance waits for a valid client.
- **PM7-D / independent backlog:** full export, migrations, remaining sort/query rules and targeted specialist packets.

The regular handoff has been shortened, the stale instruction to start completed PM7-A corrected, and prior audit follow-ups archived. All 47 original acceptance rows are unchanged. The current-client architecture document now distinguishes its historical baseline from the new failed-update observation; existing recovery evidence is preserved by hash/build rather than indiscriminately invalidated.

## Daybreak decision

No new assignment approved. ESC-001 CLOSED; ESC-003 NOT_ASSIGNED under its earlier reviewed serializer decision; ESC-002/004 still NEEDS_INFORMATION. **ESC-005** tracks the now-concrete unresolved loader/handler question, missing permissible-method/specialist-value evidence and operation restrictions. The regular AI must complete that packet or advance an independent permitted question. An update CRC error, missing observation target or safety denial alone is not proof Daybreak can solve it or may repeat a denied operation.

## Verification and limits

Fresh Release build passed with 0 warnings/errors. Backend checks returned `ok=true` in all five groups with real-config preservation and no game/launcher running. Recovered frontend integrity, five preference scenarios and both transport checks passed. The update-health collector directly reproduced the patch/base matches and temporary-output mismatch without modifying installed files. Python syntax, evidence JSON, Markdown links and the 47 acceptance rows are checked at delivery.

The host probe and screenshot matrix were not repeated because their code/UI did not change; no live game acceptance, reference-binary verifier or installed-client decoder was run. [Review 8 evidence](../evidence/lwbridge-implementation/2026-09-10-pm-review-8.json) records the reviewed commit, scope and limitations. Commit/push and remote verification are required for this PM checkpoint; delivery commit is reported separately from the reviewed implementation above.
