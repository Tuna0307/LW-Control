# Regular AI task — review 9

Start from the latest worktree/HEAD; PM reviewed `b83987b` on 2026-09-10. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [the current audit](lwbridge-project-status.md), [BACKLOG.md](../BACKLOG.md) and the relevant findings indexed in [README](README.md). All 47 acceptance cases remain required. Preserve the login-free recovered UI and completed legacy cleanup.

## Active priority — supersedes PM9-A-first instructions

Follow [display one real resource point](first-live-result.md). Connect to the real game, acquire one real resource point and display it in the rebuilt Map Data page. Research is limited to missing prerequisites for that demonstration. PM9-A/future automatic updates and unrelated export/migration research are deferred; retain minimal current-file checks.

The user grants standing permission to open/close/restart the game and launcher, including an existing session, and control the computer via Computer Use for testing. See AGENTS.md section 3; do not ask repeatedly. Use only capabilities permitted in the active environment, and preserve production session-ownership/readiness distinctions.

## Ownership and evidence rules

You own research, permitted binary analysis, implementation and validation by default. Tool discovery/installation and checkpoint commit/push are pre-authorized under AGENTS.md. Verify original/current source identities before new recovery; do not guess behavior, constants or live success. Save each finding immediately with reproducible evidence. Work on one named item per coherent checkpoint; avoid another task's files and concurrent builds in the same output directory.

The [current audit](lwbridge-project-status.md) accepts R6-047–051's bounded research and verifies the user's official-game recovery. Read [the update comparison](lastwar-update-readiness.md) for current script/table applicability and [Map Scan findings](lwbridge-map-scan.md) for exact original/current contracts.

## PM8-0 — closed; user performed recovery

The user deleted data and reinstalled. The AI verified resulting logs/files under PM8-004; it did not deliver an automatic repair. Active scripts are version 14, all 14 sampled core anchors are unchanged, and the current table is unchanged from the pre-reinstall failure snapshot. Do not repeat reinstall. The repaired official client is available subject to fresh checks; rebuilt connection/scan readiness remains independently gated.

## PM9-A — deferred until the first live result

The [update-readiness task](lastwar-update-readiness.md) remains useful future work, but it is no longer the next checkpoint. Use existing read-only diagnostics for the minimal current-client checks required by the demonstration. Implement only an indispensable named subset if it directly blocks that test; do not build an updater as a substitute for connecting to the game.

## PM7-A — completed; do not repeat

**DONE as R6-038 (`62aabda`).** `SelectOptionSource` plus `ReadOptionAggregatesAt` now form one reusable service for published/staged scopes with coherent counts/options and meaningful isolation/WAL tests. It has no production caller yet; `map_data_options` remains unavailable. Exact public progress JSON and authoritative state are still required. Do not turn its internal database-row shape into guessed public JSON.

## PM7-B — establish the bridge handler and authoritative state seam

**Owner: regular AI. Status: IN PROGRESS through R6-046.** Deliver the actual bridge-handler/current-runtime/readiness mapping needed for a production state provider. R6-039–041 establish runtime material/persistence and device-key boundaries; R6-043 corrects diagnostic labels and establishes secure-proxy helper contracts; R6-044 establishes server-source fallback/active-read mismatch; R6-045/046 establish package-consumer and envelope path/reader boundaries. These are static findings, not decoded handlers or a working connection.

Remaining questions: exact envelope field/agreement ownership; package-file and crypto-input ownership; host provisioning linkage; secure/plain parity; protected command handlers; dispatcher failure/timeout identities; and the separate map-state-unavailable emitter. Use the existing evidence index and [ESC-005](daybreak-escalations.md) to track each result/attempt. Do not inspect or reroute the SB-79 denied target, invent decrypted contents or assume adjacent values belong to the same field. A current-client getter chain is not automatically the original bridge handler.

Choose one concrete permissible evidence path that unlocks the provider or finish ESC-005's method/result/alternative packet with specific specialist value. If that path cannot progress, move to independent PM7-C/R5 work rather than indefinitely add adjacent marker-only checkpoints. Document precisely what each new finding enables and which prerequisite still prevents an actual user-visible result.

Success means a source-attributed trigger → handler → current-runtime response → state/error mapping, with exact types and limits sufficient to implement the supported provider. Implement only the proven slice. Summary must use authoritative scan state, R6-030 run selection and R6-033 error propagation; a synthetic unavailable state must not become successful summary data. If a necessary live target is absent, record that limitation, not denied user permission. If a specific permitted research approach is exhausted, record methods/results and specialist value instead of guessing.

## PM7-C — native identities and the smallest real scan path

**Owner: regular AI. Status: IN PROGRESS through R6-051; progress independently when B cannot proceed.** R5 launch/owned process/bridge handshake remains a critical dependency; read [injection findings](lwbridge-injection.md) before changing launch code. Production start remains gated until its inputs, ownership and authoritative readiness/outcomes are established.

R6-047 gives the original shared upsert layout; R6-049 gives the original identity decision; R6-051 gives scalar source/fallback mappings. R6-048/050 independently establish current PointInfo/resource identity fields and xLua ownership in hash-identical Assembly-CSharp. Do not re-recover these or keep the original identity formula marked entirely unknown.

Next, trace one current resource object through its actual native capture/serializer into those exact builder inputs. Establish concrete types, null/invalid-value behavior, resource naming/gather/removal and update ownership. Field-name similarity and available managed getters are not capture provenance. Implement only the supported normalizer/ingestion slice with a traceable source, retaining production gates until the real provider/session exists. Preserve lossless IDs and unknown optional values; do not invent a point/UUID conversion or a frontend fallback key.

Success remains a supported current-client connection, a bounded real scan and displayed authoritative results. If an earlier session/handshake prerequisite is missing, name it and continue PM7-B/R5 rather than relabel an offline insertion as a working scan. No denied SB-84 query is to be repeated via another model/tool.

Tie each checkpoint to the missing ingestion contract. Before enabling scan completion/resume, recover scheduler/capture/acknowledgement/cancellation/generation eligibility. R6-019/020/028 transaction and block storage tests alone prove none of those transitions. Acceptance needs correlated authoritative current-client evidence for the smallest supported scan, then broader coverage under `task.md`.

## PM7-D — schema, export and remaining query gaps

**Owner: regular AI. Status: BACKLOG, or independent work while a prerequisite is unavailable.** Preserve R6-011 schema metadata, R6-017/018 export structure and R6-026/027 sort findings. Resolve exact migration thresholds/timestamps; workbook row mapping/typing/full-filter pagination/filename/cancel semantics; and ordered multi-sort expressions/null/tie-break behavior before enabling dependent operations. Do not invent schema versions, export limits or distance semantics.

ESC-002 and ESC-004 need per-method tool/version/locator/output records, considered alternatives and a narrow permissible specialist question. A negative search should state exactly which sources it covered. Complete each packet only when its evidence supports the claim; no arbitrary attempt count or unrelated tool collection is required.

## Daybreak decisions and restriction reporting

No specialist is assigned. ESC-001 is CLOSED. ESC-003 is **NOT_ASSIGNED by PM review 7**: the recorded attempts support review, but no specific permitted specialist method is identified for the remaining progress serializer. Do not keep requesting the same generic packet, repeat resolved source/no-alliance work, or reroute the denied callback. Reopen with a concrete permissible approach or newly available evidence/target and exact benefit/return criteria. ESC-002/004/005 remain NEEDS_INFORMATION. ESC-005 tracks the remaining loader/handler gap; PM8-0 is closed by user-operated recovery; PM9-A is readiness integration, not a specialist assignment. All decisions and operation-specific restrictions are in [the register](daybreak-escalations.md).

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
