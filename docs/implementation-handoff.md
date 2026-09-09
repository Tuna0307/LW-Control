# Regular AI task — recovery, implementation and integration

Updated 2026-09-09 by project-manager review 6 against `3138fe9`. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [the audit](lwbridge-project-status.md), [BACKLOG.md](../BACKLOG.md) and the relevant subject findings. This task and [the Daybreak task](deep-binary-handoff.md) share the full requirements in `task.md`.

## Ownership and objective

You own ordinary implementation **and reverse-engineering, including deeper binary analysis when permitted and within your capabilities**. Finish the highest-priority supported slice and integrate accepted specialist findings. Do not route work to Daybreak merely because it is native, difficult or time-consuming. Use the installed tools and install a useful missing dependency under the standing authorization.

Only request Daybreak after exhausting the relevant permitted approaches you can identify and documenting the attempts. Follow the [escalation register/template](daybreak-escalations.md); pending requests do not pause independent work. Environment restrictions remain in force and cannot be bypassed with a model change.

## Accepted baseline — preserve and build on it

| Finding | Current result |
|---|---|
| R6-003/004/005 | Persisted default search, eight frontend query families/consumers and alliance/no-alliance/name-key/item predicates. |
| R6-006 + correction R6-010 | `specialOnly` is dispatch/ghost; `reindeerOnly` is truck only. The original R6-006 JSON's railway entry is historical and superseded. Review 5 corrects the old inspector's emitted metadata. |
| R6-007 | Literal keyword substring search over name/alliance/UUID/JSON with backslash, percent and underscore escaping in recovered order. |
| R6-008 | Count and page share a deferred SQLite snapshot; a second WAL writer test passes. This is rebuild implementation policy, not recovered original transaction mode. |
| R6-009 | Paired ordinary-treasure/supplies selection and dispatch level predicates implemented offline; option SQL families recovered but complete options response remains unavailable. |
| R6-011 | Metadata/future-schema/legacy-import contracts partially recovered. Supported version, migration ordering/threshold and timestamp producer remain unknown. No migration implementation accepted yet. |
| R6-012 → R6-013 | Ordinary quality `n/r/sr/ssr` binds `1/2/3/4`; `ur` uses `>=5`. Only ordinary truck UR gets the special-UR exclusion. Earlier broad adjacency interpretation is superseded. |

Both completed task streams are already committed on the same branch. No cherry-pick or duplicate reimplementation is needed. None of these results establishes production capture, owned game launch or complete Map Data functionality. For example, the real treasure query still includes unresolved viewer/visibility/lucky flags; an isolated selector test does not make the whole tab usable.

Post-review continuation has also produced `LWB-R6-018` and later R6 storage/options checkpoints. The earlier SB-10 delivery denial is historical: subsequent pushes succeeded, and the branch was verified through `LWB-R6-022` commit `3c3c2c1e5fe54647289a289317aa4c3df4c574a6` as historical delivery evidence; review 6 independently verified GitHub at `3138fe9`. Preserve the restriction record, but do not treat GitHub delivery as currently blocked.

## Review 6 management direction

R6-014 closes ESC-001; R6-023 closes treasure key formatting. ESC-002/003/004 cover remaining schema/options/export questions. **A test-only helper is not an enabled UI function.** Next checkpoint should advance a concrete production-blocking contract (options/summary, owned launch or native ingestion) or complete its ESC packet with honest method/alternative evidence. Avoid indefinitely adding synthetic variations while leaving the same public command blocked.

For every restriction-related checkpoint, state the contract outcome: resolved with finding, still investigating with a named next method, or request pending with missing fields. "No escalation needed" is appropriate only for the named resolved question or a concrete continuing regular-AI method, not as a blanket statement about unresolved functionality. The [outcome dashboard](daybreak-escalations.md) is current.

## Next work in priority order

1. **Completed post-review 6 — PM6-01.** `LWB-PM6-001` replaces parameterless cleanup waits with a bounded owned-process cleanup helper, preserves both the primary probe failure and any cleanup failure, and adds a deterministic termination-command-failure self-test that runs in CI before the normal isolated host matrix. The five-second cleanup wait is explicit test orchestration policy, not a recovered runtime constant. See `../evidence/lwbridge-implementation/2026-09-09-pm6-host-cleanup.json`. Preserve this regression; the next active priority is a production-blocking options/summary, owned-launch, or native-ingestion contract.
2. **Completed 2026-09-09 — cross-kind quality regressions.** Persisted railway/dispatch/ghost UR rows now prove they retain `isSpecialURQuality=true`; truck UR includes a non-special quality above five while excluding the special row. The regression also covers page/count consistency, server isolation, and a combined railway UR + retained-item query. Preserve the cross-connection snapshot regression.
3. **Advance options/summary and remaining query semantics through permitted evidence.** Start at R6-009/011/013/014/015/016/021/022/023/024/025/026/027/030/031, readable frontend/current-client artifacts and existing SQL. R6-014 closes the reward cutoff source/unit, frontend completion-status clock and frontend plunderability predicates with the original precise Unix-ms wall clock. R6-015 validates the recovered option SQL families against a deliberately persisted-only `map_records` helper; R6-021 extends only that offline helper with the eight recovered frontend count keys. R6-023 supersedes R6-022's suffix gap: the native option-assembly branches and decimal formatter prove exact `treasure:<decimal treasureType>` / `supplies:<decimal suppliesType>` keys. R6-024 narrows the native source abstraction to exactly published `map_records` scoped by `server_id=?1` or staging `scan_records` scoped by `server_id=?1 AND run_id=?2`. R6-025 recovers the successful `map_summary` envelope and proves that shared `scanState.serverId` is both the summary server and count-helper server while helper `0x140344F1D` controls the optional run scope. R6-030 closes that selector and run identity. R6-031 then closes native no-alliance production and corrects the persisted test kernel: NULL/empty grouped alliance names contribute directly to `noAllianceCount` and never enter `alliances[]`; it also recovers the exact top-level options insertion order and proves persisted scan progress is the latest non-discarded row by server while active scan progress uses the exact `scanRunId`. R6-026 pins the frontend ordered multi-sort state/per-kind columns plus eleven scalar native parser comparisons, four direct expression references and the adjacent sort-expression/null-order string inventory. R6-027 closes the public direction-literal branch: `asc -> ASC`, ordinary `desc -> DESC`, and `distance desc -> ASC`. It still enables no alternate production sort because the distance value transformation/rationale, complete native ordered multi-sort/null-order/tie-break assembly, remainingLootCount SIMD-key proof, shield/distance data flow and railway quality branch remain unresolved. The principal remaining options gap is exact `scanProgress` JSON serialization/empty/error behavior; complete summary unavailable/error behavior and foreign-radar/lucky behavior also remain. ESC-003 stays NEEDS_INFORMATION while regular AI traces the common scan-progress conversion path after `0x14025B07E`; no specialist task is assigned. Do not replay recorded denied operations, including SB-17/SB-18 and SB-29 through SB-31.
4. **Storage and export.** Recover schema-version/migration/timestamp prerequisites before claiming original-compatible migrations. `LWB-R6-019` provides test-only transactional staging-to-published replacement for the already recovered one-kind/server delete/copy slice, `LWB-R6-020` proves that slice rolls back to the previous published rows on a deterministic mid-transaction failure, and `LWB-R6-028` now provides restart-safe test-only persistence over the recovered `scan_blocks(run_id,block_index)` identity with reopen and clear-cascade verification. Keep original scheduling/ack/retry/status transitions, production eligibility, failure semantics and native record-key derivation gated while extending independent generation infrastructure. `LWB-R6-017` pins the city-only frontend export request; `LWB-R6-018` pins the native default sheet/yes/no values, filename prefix/extension/dialog label, result/error vocabulary, embedded A-L OOXML layout/package/styles and partial D-L field adjacency. Continue export with exact A-C row mapping, protect/shield fallback, per-column string/numeric/datetime typing, internal pagination/full-filter scope, filename timestamp/default directory, cancellation branch and lossless large-ID reopen behavior. Do not turn pageSize 200 into an invented export limit, and do not replay SB-08/SB-09.
5. **Lifecycle and capture.** DB-01/02 launch inputs/ownership, DB-03 native keys/normalization, DB-04 scheduler/capture and DB-06 status/actions remain in your research backlog. Their labels do not reserve them for Daybreak. `LWB-R6-029` records that the tracked build-1078 official-runtime snapshot/architecture does not expose record-key/scan-bridge or selected known map-row vocabulary; the current `Assembly-CSharp.rdl` and active `LWScripts.data` are the next concrete DB-03 current-contract sources if an existing permitted decoder/loader path is available. Select a concrete permitted source trace, document what was tried and request a specialist only if the escalation rule is met. Keep unsupported live operations unavailable.

## How to request the specialist

Create or complete an ESC record with the exact missing contract, source identity, attempted methods and concrete results. Include failed approaches and why each sensible alternative is exhausted, unavailable or inapplicable. State precisely **why Daybreak may help** beyond "it is a different model" and the smallest permitted question it should answer. Do not propose replaying a denied operation.

The PM reviews the request, approves a bounded scope or returns it for more evidence. Until approval, retain ownership and continue useful independent work. After a specialist return, verify the source, integrate only supported behavior, add meaningful tests and report what remains unknown. Do not convert static findings into live success.

## Checkpoint delivery

Inspect current branch/worktree first; avoid overlapping edits and simultaneous builds into the same `bin/obj`. Record work-item ownership and exact touched files in your checkpoint. Do not stage another task's unfinished changes.

Run appropriate checks sequentially where outputs overlap:

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release -- --verify-real-config-unchanged
node tools/check_lwbridge_preference_provider.cjs
node tools/check_lwbridge_live_transport.cjs
node tools/check_lwbridge_transport_boundary.cjs
```

Run host/visual checks when the changed behavior requires them. Never run a denied binary verifier as routine CI. Save findings immediately, update ledger/backlog/ESC status, commit/push the coherent checkpoint and verify the remote. Report completed slices, tests, remaining gates and ESC requests separately. All 47 full acceptance cases remain required.

Use [the completion estimate](lwbridge-completion-estimate.md) only as PM planning context. Do not increase completion percentages for raw finding/helper counts or claim a working scan from a transaction test.
