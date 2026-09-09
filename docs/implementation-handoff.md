# Regular AI task — review 7

Start from the latest worktree/HEAD; PM reviewed `0273569` on 2026-09-09. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [the current audit](lwbridge-project-status.md), [BACKLOG.md](../BACKLOG.md) and the relevant findings indexed in [README](README.md). All 47 acceptance cases remain required. Preserve the login-free recovered UI and completed legacy cleanup.

## Ownership and evidence rules

You own research, permitted binary analysis, implementation and validation by default. Tool discovery/installation and checkpoint commit/push are pre-authorized under AGENTS.md. Verify original/current source identities before new recovery; do not guess behavior, constants or live success. Save each finding immediately with reproducible evidence. Work on one named item per coherent checkpoint; avoid another task's files and concurrent builds in the same output directory.

The [current audit](lwbridge-project-status.md) accepts PM6-001 and R6-024–037. Do not re-recover the clock, treasure option keys, source/run selector, alliance/no-alliance assembly, summary success envelope or command-level error propagation. Read [Map Scan's current finding summary](lwbridge-map-scan.md) for corrections and exact limits. Static current-client getters and LWBridge host requests are separate facts until their linkage is established.

## PM7-A — integrate the known option/count source contract first

**Owner: regular AI. Status: DONE as `LWB-R6-038`. Scope: R6/M07; bounded integration, not a full public-options completion claim.**

`LWB-R6-038` replaces the former test-only selector/published-only aggregate split with `MapDataStore.SelectOptionSource` + `ReadOptionAggregatesAt`. The service uses the recovered `map_records`/`scan_records` source scopes, exact raw run identity, recovered clock, option SQL families and all eight counts under one read snapshot. Published/staged, same-server other-run, other-server and concurrent-WAL cases pass. `LWBridgeBackend.map_data_options` intentionally remains `MAP_INDEX_UNAVAILABLE`; `MapScanProgressAggregateRow` is not promoted to public JSON.

Completed acceptance results:

1. One validated source context carries requested server and the optional exact run ID to every count/option query. Use staging only for reading state + same server + raw nonempty run ID; preserve the native no-trimming rule. Bind values; select SQL structure only from the two known internal sources.
2. Support both source scopes for known aggregate fields. Preserve NULL/empty alliance exclusion and summed `noAllianceCount`, option keys, recovered ordering and reward cutoff. Keep a coherent read snapshot as explicitly documented rebuild policy. Do not silently read published counts alongside staged options.
3. Prove the integration using conflicting published/staged records and two servers/runs. Check active, idle, server-mismatch, empty/null/whitespace run IDs, isolation and same-snapshot count/option consistency. Keep test inputs explicitly synthetic. Reuse existing tests where sufficient; do not add endless variations that mirror code.
4. Keep the exact public `scanProgress` serializer and real state-provider prerequisites visible. Do not use `MapPersistedScanProgress`'s convenient test shape as recovered JSON, substitute live-event fields, fabricate an empty response, or enable public `map_data_options` merely because aggregates pass. Missing prerequisites must still return the explicit existing unavailable boundary.
5. Delivery is recorded as `LWB-R6-038` with durable evidence. The next task is PM7-B: recover authoritative state/bridge-handler linkage and integrate only the supported provider. Exact public `scanProgress` serialization remains an independent ESC-003 NOT_ASSIGNED gap.

## PM7-B — establish the bridge handler and authoritative state seam

**Owner: regular AI. Status: IN PROGRESS through `LWB-R6-042`. Scope: R5/R6 shared readiness and summary.** R6-039 establishes the bounded package-key/runtime-material seam; R6-040 closes its direct host persistence gap. R6-041 narrows the host auth/device-key plus proxy CNG/BCrypt/file-loader boundary, and R6-042 pins exact proxy-side configuration names `DEVICE_KEY_PROVIDER`, `DEVICE_KEY_EXPORT` and `DEVICE_KEY_FORMAT` plus shared `DEVICE_KEY_MISSING`. These are static boundary facts, not recovered runtime values or an inferred decrypt algorithm. The next boundary is the source-attributed value/provisioning path for those exact names, then the device-key storage/read plus proxy open/read/decrypt call graph and protected bridge handlers/readiness semantics. Continue from R6-034/035 current-client ownership, R6-036 producer order/normalization and R6-037's exact invalid-server branch. Review saved package/loader evidence and existing tooling before selecting a new permitted method. The current-RDL decoder is available locally; missing MCP integration is not a blocker.

Recover the actual LWBridge bridge-command handler linkage for `getWorldMapState`/`getCurrentServerId`, readiness/null/source selection, propagated transport errors and the exact `map scan state is unavailable` condition. On the loader path, first establish the source-attributed device-key provider/name, runtime envelope open/read path and exact decrypt/verification dataflow; marker/import presence alone does not assign algorithms or parameters. Do not assume the managed getter chain is the bridge script. Encrypted package plaintext remains unknown until actually recovered. Respect each recorded operation restriction; a model/tool change does not permit replaying it.

Success means a source-attributed trigger → handler → current-runtime response → state/error mapping, with exact types and limits sufficient to implement the supported provider. Implement only the proven slice. Summary must use authoritative scan state, R6-030 run selection and R6-033 error propagation; a synthetic unavailable state must not become successful summary data. If a necessary live target is absent, record that limitation, not denied user permission. If a specific permitted research approach is exhausted, record methods/results and specialist value instead of guessing.

## PM7-C — native identities and the smallest real scan path

**Owner: regular AI. Status: TODO; progress independently when B cannot proceed.** R5 launch/owned process/bridge handshake remains a critical dependency; read [injection findings](lwbridge-injection.md) before changing launch code. Production start remains gated until its inputs, ownership and authoritative readiness/outcomes are established.

For DB-03/R7, use the maintained official-runtime architecture and current fingerprinted artifacts. R6-029 searched tracked text only; it did not prove native identities absent from installed code. R6-034/035's RDL decoder supplies an available managed-code route, subject to existing operation restrictions. Trace one selected kind from native source record to the original identity/normalization and index update; document unknown fields rather than defaulting them. Never substitute frontend fallback identity for native `record_key`.

Tie each checkpoint to the missing ingestion contract. Before enabling scan completion/resume, recover scheduler/capture/acknowledgement/cancellation/generation eligibility. R6-019/020/028 transaction and block storage tests alone prove none of those transitions. Acceptance needs correlated authoritative current-client evidence for the smallest supported scan, then broader coverage under `task.md`.

## PM7-D — schema, export and remaining query gaps

**Owner: regular AI. Status: BACKLOG, or independent work while a prerequisite is unavailable.** Preserve R6-011 schema metadata, R6-017/018 export structure and R6-026/027 sort findings. Resolve exact migration thresholds/timestamps; workbook row mapping/typing/full-filter pagination/filename/cancel semantics; and ordered multi-sort expressions/null/tie-break behavior before enabling dependent operations. Do not invent schema versions, export limits or distance semantics.

ESC-002 and ESC-004 need per-method tool/version/locator/output records, considered alternatives and a narrow permissible specialist question. A negative search should state exactly which sources it covered. Complete each packet only when its evidence supports the claim; no arbitrary attempt count or unrelated tool collection is required.

## Daybreak decisions and restriction reporting

No specialist is assigned. ESC-001 is CLOSED. ESC-003 is **NOT_ASSIGNED by PM review 7**: the recorded attempts support review, but no specific permitted specialist method is identified for the remaining progress serializer. Do not keep requesting the same generic packet, repeat resolved source/no-alliance work, or reroute the denied callback. Reopen with a concrete permissible approach or newly available evidence/target and exact benefit/return criteria. ESC-002/004 remain NEEDS_INFORMATION. All decisions and operation-specific restrictions are in [the register](daybreak-escalations.md).

For every affected unresolved contract, say: resolved by identified evidence; investigating with a specific next permitted method; unavailable target; or ESC status with the exact outstanding requirement. A restriction record is not a claim that the whole executable or all saved evidence is prohibited. Equally, later success is not proof a prior restriction was waived. Do not self-approve specialist scope.

## Checkpoint delivery

Run checks appropriate to changed behavior; sequence builds that share outputs:

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release -- --verify-real-config-unchanged
node tools/check_lwbridge_preference_provider.cjs
node tools/check_lwbridge_live_transport.cjs
node tools/check_lwbridge_transport_boundary.cjs
```

If host/probe behavior changes, also run `tools/check_lwbridge_host_probe.ps1 -RunTerminationFailureSelfTest` and its normal probe sequentially. Keep binaries, personal data and bulk scratch output out of commits. Document findings, update ledger/backlog/ESC outcomes, inspect/stage only your coherent changes, commit/push the existing branch and verify GitHub. Report public behavior enabled, offline-only work, remaining gates, checks and commit separately. Do not raise [completion estimates](lwbridge-completion-estimate.md) solely for finding/helper counts.
