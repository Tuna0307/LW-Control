# Regular AI task — review 8

Start from the latest worktree/HEAD; PM reviewed `4ec7ef8` on 2026-09-10. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [the current audit](lwbridge-project-status.md), [BACKLOG.md](../BACKLOG.md) and the relevant findings indexed in [README](README.md). All 47 acceptance cases remain required. Preserve the login-free recovered UI and completed legacy cleanup.

## Ownership and evidence rules

You own research, permitted binary analysis, implementation and validation by default. Tool discovery/installation and checkpoint commit/push are pre-authorized under AGENTS.md. Verify original/current source identities before new recovery; do not guess behavior, constants or live success. Save each finding immediately with reproducible evidence. Work on one named item per coherent checkpoint; avoid another task's files and concurrent builds in the same output directory.

The [current audit](lwbridge-project-status.md) accepts R6-038's bounded integration and R6-039–046's recorded research with explicit limits. Preserve earlier recovered source/count/query/clock contracts and R6-043's correction of R6-042's label interpretation. Use [Map Scan findings](lwbridge-map-scan.md) for locators, not an ever-growing copied list of offsets in this handoff.

## PM8-0 — official launcher update health first

**Owner: regular AI. Status: OPEN; diagnosis complete, recovery unproven.** Follow [the incident/recovery task](lastwar-update-incident.md). The September 10 patch download and old active base pass their recorded checks, but the produced temporary output fails CRC. The main build remains 1078 and 14 sampled anchors are unchanged; the latest table changed. Preserve failed evidence, use supported official recovery, and prove update completion plus normal game startup before any live acceptance. Do not edit CRC/version metadata or promote the failing file. Refresh script/table and changed-binary applicability after recovery. Continue independent offline work if official recovery cannot progress.

## PM7-A — completed; do not repeat

**DONE as R6-038 (`62aabda`).** `SelectOptionSource` plus `ReadOptionAggregatesAt` now form one reusable service for published/staged scopes with coherent counts/options and meaningful isolation/WAL tests. It has no production caller yet; `map_data_options` remains unavailable. Exact public progress JSON and authoritative state are still required. Do not turn its internal database-row shape into guessed public JSON.

## PM7-B — establish the bridge handler and authoritative state seam

**Owner: regular AI. Status: IN PROGRESS through R6-046.** Deliver the actual bridge-handler/current-runtime/readiness mapping needed for a production state provider. R6-039–041 establish runtime material/persistence and device-key boundaries; R6-043 corrects diagnostic labels and establishes secure-proxy helper contracts; R6-044 establishes server-source fallback/active-read mismatch; R6-045/046 establish package-consumer and envelope path/reader boundaries. These are static findings, not decoded handlers or a working connection.

Remaining questions: exact envelope field/agreement ownership; package-file and crypto-input ownership; host provisioning linkage; secure/plain parity; protected command handlers; dispatcher failure/timeout identities; and the separate map-state-unavailable emitter. Use the existing evidence index and [ESC-005](daybreak-escalations.md) to track each result/attempt. Do not inspect or reroute the SB-79 denied target, invent decrypted contents or assume adjacent values belong to the same field. A current-client getter chain is not automatically the original bridge handler.

Choose one concrete permissible evidence path that unlocks the provider or finish ESC-005's method/result/alternative packet with specific specialist value. If that path cannot progress, move to independent PM7-C/R5 work rather than indefinitely add adjacent marker-only checkpoints. Document precisely what each new finding enables and which prerequisite still prevents an actual user-visible result.

Success means a source-attributed trigger → handler → current-runtime response → state/error mapping, with exact types and limits sufficient to implement the supported provider. Implement only the proven slice. Summary must use authoritative scan state, R6-030 run selection and R6-033 error propagation; a synthetic unavailable state must not become successful summary data. If a necessary live target is absent, record that limitation, not denied user permission. If a specific permitted research approach is exhausted, record methods/results and specialist value instead of guessing.

## PM7-C — native identities and the smallest real scan path

**Owner: regular AI. Status: IN PROGRESS through R6-049; progress independently when B cannot proceed.** R5 launch/owned process/bridge handshake remains a critical dependency; read [injection findings](lwbridge-injection.md) before changing launch code. Production start remains gated until its inputs, ownership and authoritative readiness/outcomes are established.

For DB-03/R7, use the maintained official-runtime architecture and current fingerprinted artifacts. R6-029 searched tracked text only; it did not prove native identities absent from installed code. R6-047 pins the native shared map/staging upsert helper, exact table selector, shared record layout and `record_key` slot at `+0x78`. R6-049 closes the original normalized identity formula upstream of that slot: first-present `pointIndex/mainIndex/pointId/index` becomes signed-decimal `record_key` for ordinary indexed rows, while exact truck/railway, any row with `marchUuid`, or a row without an index uses nonempty `uuid` else `marchUuid`. R6-048 separately pins current build-1078 resource/world-point point-index/UUID lookup dimensions. Correlate those current-client fields with the recovered builder inputs only through direct capture-side dataflow; remaining typed normalization/removal and producer mapping stay open. Never substitute frontend fallback identity for native `record_key`.

Tie each checkpoint to the missing ingestion contract. Before enabling scan completion/resume, recover scheduler/capture/acknowledgement/cancellation/generation eligibility. R6-019/020/028 transaction and block storage tests alone prove none of those transitions. Acceptance needs correlated authoritative current-client evidence for the smallest supported scan, then broader coverage under `task.md`.

## PM7-D — schema, export and remaining query gaps

**Owner: regular AI. Status: BACKLOG, or independent work while a prerequisite is unavailable.** Preserve R6-011 schema metadata, R6-017/018 export structure and R6-026/027 sort findings. Resolve exact migration thresholds/timestamps; workbook row mapping/typing/full-filter pagination/filename/cancel semantics; and ordered multi-sort expressions/null/tie-break behavior before enabling dependent operations. Do not invent schema versions, export limits or distance semantics.

ESC-002 and ESC-004 need per-method tool/version/locator/output records, considered alternatives and a narrow permissible specialist question. A negative search should state exactly which sources it covered. Complete each packet only when its evidence supports the claim; no arbitrary attempt count or unrelated tool collection is required.

## Daybreak decisions and restriction reporting

No specialist is assigned. ESC-001 is CLOSED. ESC-003 is **NOT_ASSIGNED by PM review 7**: the recorded attempts support review, but no specific permitted specialist method is identified for the remaining progress serializer. Do not keep requesting the same generic packet, repeat resolved source/no-alliance work, or reroute the denied callback. Reopen with a concrete permissible approach or newly available evidence/target and exact benefit/return criteria. ESC-002/004/005 remain NEEDS_INFORMATION. ESC-005 tracks the remaining loader/handler gap; PM8-0 is an official-update recovery task, not a specialist assignment. All decisions and operation-specific restrictions are in [the register](daybreak-escalations.md).

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
