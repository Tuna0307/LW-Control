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
3. **Advance options/summary and remaining query semantics through permitted evidence.** Start at R6-009/011/013/014/015/016/021/022/023, readable frontend/current-client artifacts and existing SQL. R6-014 closes the reward cutoff source/unit, frontend completion-status clock and frontend plunderability predicates with the original precise Unix-ms wall clock. R6-015 validates the recovered option SQL families against a deliberately persisted-only `map_records` helper; R6-021 extends only that offline helper with the eight recovered frontend count keys and a persisted no-alliance value in the same snapshot. R6-023 supersedes R6-022's suffix gap: the native option-assembly branches and decimal formatter prove exact `treasure:<decimal treasureType>` / `supplies:<decimal suppliesType>` keys, now carried only by that offline helper. Do not expose the helper as the public command. R6-016 pins same-server propagation/reset and `noAllianceCount` filter fallback; do not infer the native producer/source branch from those consumers. Remaining gaps include original persisted-vs-run source selection at the public command boundary, exact native count/no-alliance/full response assembly, profile-to-summary-server/scan-state production, foreign-radar/lucky behavior and alternate sorts. A failed glob or truncated search is not an exhaustive investigation.
4. **Storage and export.** Recover schema-version/migration/timestamp prerequisites before claiming original-compatible migrations. `LWB-R6-019` provides test-only transactional staging-to-published replacement for the already recovered one-kind/server delete/copy slice, and `LWB-R6-020` now proves that slice rolls back to the previous published rows on a deterministic mid-transaction failure. Keep production eligibility, failure/retry semantics and native record-key derivation gated while extending independent generation infrastructure. `LWB-R6-017` pins the city-only frontend export request; `LWB-R6-018` pins the native default sheet/yes/no values, filename prefix/extension/dialog label, result/error vocabulary, embedded A-L OOXML layout/package/styles and partial D-L field adjacency. Continue export with exact A-C row mapping, protect/shield fallback, per-column string/numeric/datetime typing, internal pagination/full-filter scope, filename timestamp/default directory, cancellation branch and lossless large-ID reopen behavior. Do not turn pageSize 200 into an invented export limit, and do not replay SB-08/SB-09.
5. **Lifecycle and capture.** DB-01/02 launch inputs/ownership, DB-03 native keys/normalization, DB-04 scheduler/capture and DB-06 status/actions remain in your research backlog. Their labels do not reserve them for Daybreak. Select a concrete permitted source trace, document what was tried and request a specialist only if the escalation rule is met. Keep unsupported live operations unavailable.

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
