# Project-manager checkpoint — review 6

Reviewed 2026-09-09 against `3138fe996ab87211ee328edf9cedee88c8c71026`, branch `research/offline-controller`; worktree clean at start. All 14 post-review-5 commits are already integrated. GitHub was independently observed at the same revision in this audit. [Review 5 and follow-ups](reviews/2026-09-09-review-5-and-followups.md) are historical. This PM checkpoint updates planning/evidence only.

## Decision

**Accept the new time-filter implementation and the useful offline option/publication/export research. Do not call the two pages close to complete.** The rough engineering estimate is **about 25–30% combined**, with Overview approximately 20–25% and Map Data 25–35%; see [the explicit planning rubric](lwbridge-completion-estimate.md). No full case in the 47-case acceptance matrix is newly signed off.

A rejected operation is not automatically a Daybreak assignment. Some associated questions were subsequently answered, some denials concerned documentation or delivery, and some contracts remain unresolved. Current outcomes are recorded explicitly in [the escalation register](daybreak-escalations.md). A model switch is not a way to bypass a denied operation, and this audit does not certify that every historical alternative was permitted merely because a prior task reported success.

## Accepted changes since review 5

| Commits / finding | Accepted result | Production boundary |
|---|---|---|
| `e09a875`, `b36e52d` | Hidden/quoted host probe with primary wait limit; persisted cross-kind quality/above-five/combination tests | Fresh normal host run and backend checks pass. The helper still has unbounded waits in its cleanup path; PM6-01 below. |
| `f54346d` / R6-014 | Recorded precise Unix-ms clock producer; completion/plunderability predicates and default truck/railway arrival filtering wired into `map_search` | Fresh deterministic boundary tests pass. ESC-001's clock contract is resolved by regular-AI findings; live ingestion/gameplay is not proven. |
| `b9f73c2`, `127ad58`, `897414b` / R6-015/016/021 | Persisted-only option SQL/aggregate/count helpers and frontend count propagation/fallback contracts | Helper call sites are test-only. `map_data_options` still rejects `MAP_INDEX_UNAVAILABLE`; native source/count assembly remains unknown. |
| `0187bb5`, `55788fb` / R6-017/018 | City-export frontend envelope and partial native workbook/package/column structure | No native writer enabled. Full pagination, row typing/A–C/J semantics, filename/cancel details and large-ID behavior remain open. |
| `3ee6d20`, `3720b3f` / R6-019/020 | Test-only kind/server staged replacement and deterministic rollback after delete | Good transaction proof; no production eligibility, generation ownership, native keys or scan completion service. |
| `3c3c2c1`, `3138fe9` / R6-022/023 | Prefix/xref negative results then exact positive-decimal treasure/supplies option-key finding | Key formatting added only to persisted-option test helper. This resolves the key question, not public option source selection. |
| `159db1a` / R6-024 and R6-025 continuation | Native option source candidates plus native summary envelope/server/count flow | R6-025 removes the synthetic production `map_summary` success path and keeps it fail-closed. Internal run-scope selection, complete options assembly, native ingestion and live proof remain open. |
| `297b63d`, `f41c06a` | Delivery/schema restriction documentation | These are records of limits, not newly working page features. |

Review 6 itself audited committed source/locators/findings without rerunning binary disassembly. The later R6-025 continuation used the existing hash-locked bounded inspector on the verified LWBridge reference for the `map_summary`, shared scan-state and count-helper functions; deeper helper dumps denied as SB-17 were not retried or rerouted. Tests verify the rebuilt code against its fixtures, not independent current-client parity.

**Post-review regular-AI continuation — `LWB-R6-028`, 2026-09-09:** the recovered `scan_blocks(run_id,block_index)` schema now has internal test-only checkpoint read/write infrastructure. Deterministic coverage proves same-key replacement, file-backed reopen durability and cascade deletion when the owning `scan_run` is cleared. This removes one storage prerequisite for future interrupted-scan resume/reconciliation, but it does not recover scheduling, acknowledgement, retry/status transitions, completion eligibility or native record keys and enables no public scan command. The Release build and deterministic backend suite pass. SB-25 through SB-27 record three new automatic-review rejections from combined research reads; none was retried/rerouted or used as technical evidence. A separate process-only check found no running LastWar/LWBridge target.

**Post-review DB-03 continuation — `LWB-R6-029`, 2026-09-09:** tracked current-runtime readable evidence was searched for the missing record identity and selected known map-row vocabulary. It contains no direct hit beyond the LWBridge recovery document itself. The current build-1078 snapshot identifies `Assembly-CSharp.rdl` and active `LWScripts.data` as concrete next sources, but their contents were not decoded in this checkpoint and no `record_key` rule is claimed. SB-28 records a rejected combined repository tooling/reference search; it was not retried/rerouted. Native ingestion remains fail-closed.

**Post-review options/summary continuation — `LWB-R6-030`, 2026-09-09:** saved successful native-trace evidence for helper `0x140344F1D` was promoted into a durable finding after re-verifying the LWBridge reference hash. The exact optional run-scope rule is now recovered: use the active scan run only for `isReading=true`, a matching requested/scan-state server and a raw nonempty `scanRunId`; otherwise omit run scope and use published rows. An internal deterministic helper mirrors this decision, including the native non-trimmed string-length behavior. Public `map_data_options` and `map_summary` still reject with `MAP_INDEX_UNAVAILABLE` because native `noAllianceCount`, full options response/order/deduplication, exact public `scanProgress` serialization and complete summary unavailable/error integration remain open. SB-29 through SB-31 record three rejected combined/session-read commands; narrower reads succeeded and the denied commands supplied no technical evidence.

**Post-review options assembly continuation — `LWB-R6-031`, 2026-09-09:** bounded native xrefs and row-decoder tracing now close the original no-alliance producer and most of the public option assembly boundary. The grouped `alliance_name,COUNT(*)` loop decodes NULL to the same zero-length branch as empty text, excludes zero-length groups from `alliances[]`, and adds their grouped counts to the `noAllianceCount` accumulator. The persisted test kernel was corrected to match and no longer uses a separate implementation-policy no-alliance query. The top-level native insertion order is now pinned as `serverId`, `counts`, `alliances`, `names`, `dispatchLevels`, `noAllianceCount`, `rewardItems`, `treasureTypes`, `scanProgress`. Scan-progress record selection is also recovered: no active scope reads the newest non-discarded run by server; active scope reads `scan_runs WHERE id=?1` using the exact R6-030 `scanRunId`. Exact scan-progress JSON serialization/empty/error representation remains open, so no public options command is enabled.

## Which reported restrictions are resolved?

| Subject | Current state | PM action |
|---|---|---|
| Completion/reward clock, earlier SB-05 / ESC-001 | R6-014 records source/unit recovery and offline implementation | Close ESC-001 as resolved by regular AI; do not assign it to Daybreak. Historical operation denial remains recorded. |
| Treasure key suffix, SB-13/15 | R6-023 records exact key formatting; tests pass | Resolved question; no transfer. This is only one option field. |
| GitHub push, SB-10 | Later delivery succeeded; remote independently matches `3138fe9` | Historical delivery failure, not a binary-research task. |
| Frontend count/context reading, SB-07/14 | Existing evidence/recorded subsequent reads support limited consumer/documentation work | No open specialist request just to repeat those commands. Native count assembly is a separate unresolved contract. |
| Public option source/run/full response, SB-06/11 | R6-030 closes the R6-024 selector/run identity; R6-031 closes native no-alliance production, top-level insertion order and persisted/active scan-progress record selection. Exact scan-progress JSON serialization/empty/error representation remains open. | ESC-003 remains NEEDS_INFORMATION for that serializer boundary; no specialist assigned. |
| Summary server/count/scan-state flow, SB-17/18 | R6-025 recovers the successful envelope/server/count path and R6-030 closes helper `0x140344F1D`'s optional run-scope condition/run identity; unavailable/error integration remains open | Continue regular permitted investigation; no new specialist request from the historical restrictions alone. |
| Alternate `map_search` sorts, SB-20–23 | R6-026 recovers the public ordered multi-sort state, per-kind columns, eleven scalar parser comparisons, four direct expression references and the adjacent sort/null-order string inventory; production remains fail-closed | Continue regular permitted investigation of complete ordered multi-sort/null/tie-break composition plus remainingLootCount/shield/distance/railway-quality data flow; no specialist request from these restrictions alone. |
| Sort direction special case, SB-24 | R6-027 recovers `asc -> ASC`, ordinary `desc -> DESC`, and exact `distance desc -> ASC`; the distance value mechanism and full composition remain open | Resolved direction-literal question; continue regular investigation of the remaining distance/multi-sort mechanics. SB-24 itself contributed no evidence. |
| Full export mapping/pagination/typing, SB-08/09 | Still open despite partial workbook structure | ESC-004 created NEEDS_INFORMATION. Do not repeat the denied operations or pretend a complete writer exists. |
| Schema/migrations, SB-04/12 | Version/transition/timestamp prerequisites still unknown | ESC-002 still NEEDS_INFORMATION. No evidence that a different model may perform the denied database/range read. |

**No new technical Daybreak assignment is approved.** The reason is not that every gap is solved; it is that the remaining records lack a completed permitted-method exhaustion/capability packet. The regular AI must stop using an unqualified "no escalation needed" for unfinished contracts: state solved, still investigating, or request pending, with the next concrete action.

## PM6-01 — host timeout cleanup still contains unbounded waits

**Source-confirmed limitation, not a reproduced runtime hang.** `tools/check_lwbridge_host_probe.ps1` uses `WaitForExit(55000)` for the main process wait, but its timeout and `finally` paths suppress `Stop-Process` errors and call parameterless `WaitForExit()`. If termination fails, cleanup can wait indefinitely; the overall helper is not strictly bounded.

The normal isolated probe passes all required gates in this audit. Retain that credit. The regular AI should use a bounded cleanup wait, preserve/report a failed termination, and test the timeout/termination-failure path with an isolated controllable child or test seam. Do not use an arbitrary new timeout as a recovered game constant; it is test orchestration policy. Do not terminate unrelated processes. This test-tool issue does not explain the missing game launch/capture implementation.

**Post-review resolution — `LWB-PM6-001`, 2026-09-09:** the helper now uses `Stop-OwnedProcessBounded` for its owned child, with a separately documented five-second cleanup orchestration bound and no parameterless `WaitForExit()` path. Primary probe failures and cleanup failures are both retained in the surfaced error. A deterministic self-test launches an isolated PowerShell child, injects a termination-command failure, verifies the failure is reported within a bounded observation window, and then cleans up only that child. The self-test completed in 184 ms and the normal native-host probe remained `ok=true`, `userConfigTouched=false`, `liveGameCommandsPerformed=false`. This closes PM6-01 only; no production Overview/Map Data capability is implied. Evidence: [PM6 host cleanup](../evidence/lwbridge-implementation/2026-09-09-pm6-host-cleanup.json).

## What to prioritize next

1. **Public capability blockers first:** continue the common `scanProgress` conversion/serialization path after `0x14025B07E`, summary unavailable/error integration, owned-launch or native-capture/identity contract. At each checkpoint, state which real UI operation became usable; if none, say "research/test-only checkpoint" and name the remaining production gate.
2. **Complete reviewable requests for stalled work:** regular AI fills ESC-002/003/004 with exact attempted methods/results, alternatives and why a specialist could help. If it resolves one itself, link the new finding and close the request. A denied command alone does not satisfy the escalation rule. Continue independent work whose contracts are known.
3. **Do not endlessly substitute more fixture helpers for the same unresolved public contract.** Retain useful R6-015/019/021/028 infrastructure; R6-028 specifically removes the restart-persistence prerequisite for future block reconciliation. The next checkpoint should return to a public options/summary, owned-launch or native-capture/identity contract unless another infrastructure slice removes a comparably concrete integration dependency. Keep the R5 launch and R7 capture critical path visible alongside R6.
4. **Preserve the completed PM6-01 regression.** `LWB-PM6-001` closes the unbounded cleanup defect. Do not spend the next checkpoint adding more host-test variations unless a new failure appears; production blockers remain higher priority.

## Fresh verification

[Review 6 evidence](../evidence/lwbridge-implementation/2026-09-09-pm-review-6.json) records the scope, source hashes and check results.

- Release build: zero warnings/errors; backend/profile/persistence/request/map suites all pass with the real-config preservation flag and no reported failures.
- Optional installed-client diagnostic valid; no game/launcher process in that snapshot.
- Frontend integrity, five preference-provider scenarios and both transport harnesses pass.
- The changed hidden host-probe helper was run: `ok=true`, preference/session/picker/late-close gates pass, isolated configuration and no live-game commands reported. This exercises the normal path, not failed termination.
- Syntax-only parsing of LWBridge inspector sources; production backend/source call-site inspection confirms options/publication helpers remain test-only. No binary verifier, new extraction, live game action or prior denied operation executed.
- No screenshot matrix rerun because recovered frontend assets/layout were unchanged; prior visual evidence remains historical. All 47 full acceptance rows are preserved.

## Cleanup

Replaced stale review-5/ESC-001 instructions, reconciled restriction outcomes against current code/evidence and remote state, added missing open-gap request records, kept historical findings intact, and updated both handoffs/backlog/ledger. The completion estimate is explicitly a planning judgment, not a claim of live feature success or a delivery date.
