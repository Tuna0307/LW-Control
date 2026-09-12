# PM review 16 — Overview zero-open checkpoint

Date: 2026-09-12. Reviewed commit: `0758006fb0ece52abdf4aa0e5698da1163a526bb`, `research/offline-controller`, **Close Overview functional matrix for PM audit**. Worktree was clean. Local HEAD, origin tracking ref and GitHub branch all equalled this revision before PM documentation edits.

**Decision: RETURN FOR CORRECTION.** Accept the recorded successful live lifecycle paths, but do not approve O01–O06 as zero-open. PM found two source-confirmed functional/ownership defects. The whole Overview page is also not complete: shared header S02, S03 and S06 are still unfinished. Player City and all Map Data implementation remain on hold until the owner explicitly resumes them after the audit/repair return.

This is a source/evidence audit, not a new live experiment. PM did not build, execute application tests, inject code, start/close the game or operate its windows. Negative reproductions below are assigned to Web. Existing owner acceptance is preserved for the paths actually observed; it does not cover the newly identified edge cases.

## PM16-01 — P1 — selected installation is not propagated to the active lifecycle

**Status: OPEN; owner: Web. Affects O01/O02 and status consistency.**

At the reviewed revision, [LWBridgeWindow.cs](../../src/LWBridge.Desktop/LWBridgeWindow.cs:115) resolves the game root once and constructs `OverviewLifecycleService` with that value. The service stores it in a readonly field ([OverviewLifecycleService.cs](../../src/LWBridge.Desktop/OverviewLifecycleService.cs:43)). Folder selection calls `backend.SaveGameRoot` ([LWBridgeWindow.cs](../../src/LWBridge.Desktop/LWBridgeWindow.cs:1686)); [LWBridgeBackend.cs](../../src/LWBridge.Desktop/LWBridgeBackend.cs:67) only saves configuration through `GameInstallationService`. No lifecycle rebind occurs. `StartAsync` and helper arguments continue using the constructor's root.

Consequences supported by this source trace:

- Open the app without a valid installation, select a valid root, then Launch: the active lifecycle still has null and returns `GAME_ROOT_NOT_FOUND` until the app is recreated.
- Change from valid installation A to valid B while stopped: root/status queries now use B, while Launch still targets A. This defeats the selected-root contract even though `SaveSelectedRoot` unit tests pass.
- Related observation gap: [GameInstallationService.cs](../../src/LWBridge.Desktop/GameInstallationService.cs:148) accepts any same-named process when `expectedPath` is null. A missing/invalid selected root must not turn a foreign LastWar process into selected-install status.

**Required fix:** resolve/revalidate the configured installation at a safe lifecycle boundary or implement an atomic coordinated root change. Keep the exact active owned session bound to its original root until stop/restoration completes; do not redirect its cleanup to a newly selected directory. Reject or clearly defer changes during launch/running/repair/recovery according to a documented rebuild policy. Status and the next launch must agree without an undocumented app restart. Invalid/cancelled selection must retain the prior valid configuration. Unknown process identity must stay unknown/unmatched.

**Required isolated regressions:** missing-root -> select valid -> Launch invokes helper for the selected root; A -> B while stopped -> B is used by status and helper; active/repair-owned A cannot be retargeted by a B selection; cancelled/invalid selection preserves A; invalid root plus a foreign same-named process is not selected-game success. Exercise backend/lifecycle integration, not only the path validator. Use fake helper/process seams and temporary files; do not move the user's real game to reproduce this.

## PM16-02 — P1 — journal PID/path do not identify the same process incarnation

**Status: OPEN; owner: Web. Affects O06 and shared close/recovery ownership.**

[TryGetRepairSnapshot](../../src/LWBridge.Desktop/OverviewLifecycleService.cs:314) checks journal schema, profile, request/session agreement, permitted stage, PID, selected path, backup containment and the existence of an originals object. Its final `ProcessMatches` check verifies only PID/path. [run_overview_bridge.py](../../tools/run_overview_bridge.py:250) records PID/path in the recovery journal but does not retain the acquired process start time there. `run_stop` rechecks those same journal/PID/path fields; [close_owned_game_process_for_restore](../../tools/run_live_resource_probe.py:359) again checks PID/path before normal close, without process creation identity.

**Failure condition:** the old owned game exits while LWBridge is closed, its journal remains, and a later game at the same executable path receives the reused PID. The old journal still passes repair detection and the close helper can close this replacement game as if it were the old owned session. Matching request/session strings inside the old journal does not establish the identity of the current Windows process. This is a source-confirmed missing check, not a claim that PID reuse happened during Web's successful live test.

**Required fix:** persist an authoritative process incarnation identity and validate it on repair detection and immediately before destructive/close actions, including relevant shared recovery paths. The acquisition helper already observes `startedAtUtc`; choose and document a stable creation-time/process-handle policy rather than inventing original LWBridge semantics. Missing/unreadable legacy identity must not authorize closing a live process. Preserve a separate safe already-exited restoration path and a clear recoverable error. Keep profile, session, exact path, journal, backup and operation-lock checks; a timestamp is an additional check, not a replacement.

**Required isolated regressions:** same PID/path but different creation identity is rejected before close/termination/restoration against the live replacement; missing/unreadable identity fails closed; identity changes between initial detection and final close validation are rejected; exact matching original remains repairable; already-exited original can follow the validated restoration path without touching a replacement. Cover host and helper boundaries, not a mocked helper that always accepts. Use synthetic process identities, not forced real PID reuse.

## Findings accepted within their actual limits

| Finding | PM assessment |
|---|---|
| LWB-OVR-012 | Retain the indexed original `resourceMissing/targetMissing/needsRepair/installed` and argument-free `profile_instances_update_and_restart` response shape. The frontend consumes `restarted.length`, `errors.length`, `errors[0].error`. The rebuild's journal-based repair analogue is IMPLEMENTATION POLICY, not original proxy-bundle installation parity. PM inspected the finding and source/frontend mapping; did not rerun binary disassembly. |
| LWB-OVR-013 | Credit validator and isolated path/process tests plus current-install diagnostic. Reopen O01 integration closure under PM16-01; those tests do not demonstrate that a new selection reaches the existing lifecycle. |
| LWB-OVR-014 | Credit the recorded successful old-session close/restore -> fresh-session ready -> final close/restore technical live path. Reopen O06 ownership closure under PM16-02. Owner-visible repair UI is reported by Web; PM did not independently observe that window. |
| O02/O03 and LWB-OVL-003 | Preserve prior owner-accepted Launch/message/Close on the tested installation and session. Do not extend that acceptance to changed-root or reused-PID cases. |
| O04/O05 and LWB-OVR-011 | Preserve owner acceptance of startup, process-exit reconnect and intentional Close non-resurrection. Confirmed-event, hang, maintenance and updater families retain their stated offline/live coverage; no blanket acceptance of every recovery trigger. |

`UpdateAndRestartAsync` returns the recovered array-shaped envelope, does not report a restarted entry after a stop-helper failure, validates restoration before starting afresh, and is globally callable consistently with the recovered argument-free wrapper. The local single-profile result contents are explicitly policy. Existing tests cover foreign profile/PID/path, outside backup root, helper failure and successful relaunch, but not PM16-01/02. Startup reconcile suppresses the false unmanaged-start error only when `TryGetRepairSnapshot` succeeds; that guard inherits PM16-02 and must be strengthened with it, not broadened to arbitrary running games.

## Whole Overview page versus lifecycle completion

The [full contract](../../task.md) section 5.2 explicitly includes the shared header controls. Completing O01–O06 cannot by itself close them.

| Shared item | Current source-backed assessment | Whole-Overview consequence |
|---|---|---|
| S01 Game status | Current owned heartbeat is implemented/live-demonstrated; selected-root/identity consistency needs PM16 fixes. | Preserve the successful path; do not claim every status edge is closed. |
| S02 Pending-task counter | `LWBridgeBackend.CreateStatus` returns `pending = (int?)null`; original semantics remain unknown. | **Blocks a fully working Overview header.** Recover and implement actual semantics before claiming completion. |
| S03 Refresh Status | Recovered Refresh invokes host/proxy calls and `call_lua("getStatus")`. Neither production async service nor backend implements that call; it falls to `COMMAND_NOT_IMPLEMENTED`. Local refresh works only partially. | **Blocks full Overview completion.** Existing Overview heartbeat does not implement this runtime operation. Replace the stale generic "bootstrap missing" explanation with this precise gap. |
| S04 Theme | Implemented/offline-tested; CI frontend checks pass. | No new defect identified here; no new owner acceptance claimed. |
| S05 Language | Nine bundled UI languages exist; `lastwar_localize` returns an empty dictionary. | Overview language selection is distinct from game-derived labels elsewhere. Do not claim full S05 parity. Runtime-name work is an Overview blocker only for a concrete visible Overview/header/dialog consumer; Map Data-only names remain deferred. |
| S06 Cross-server | The header action exists, history is saved, but production `server_jump` is unimplemented; authoritative travel/context confirmation remains open. | **Blocks full Overview/header completion.** A working history dropdown is not working travel. This audit does not assign cross-server or Map Data implementation yet. |
| S07 Navigation | UI foundation/offline checks exist; broad live scheduler/navigation coverage remains open. | Retain relevant no-duplicate lifecycle/navigation regression coverage. Do not expand this audit into implementing Map Data or certify all-page live behavior. |

No percentage is assigned: the successful lifecycle scenarios, two open defects, unfinished shared controls and 47-case full release contract are different scopes. Protected-original proof/ticket/hello.ack parity remains separately tracked and unclaimed.

## Evidence and delivery verification

[GitHub Actions 34685129087](https://github.com/Tuna0307/LW-Control/actions/runs/34685129087) is SUCCESS with `headSha=0758006fb0ece52abdf4aa0e5698da1163a526bb`. PM read the run metadata/logs: Release build 0 warnings/errors, deterministic backend `ok=true` and `failures=[]`, collector/preference/native WebView/transport/frontend checks passed; browser output records 35 checks. These checks do not cover the new negative cases. The workflow does not execute `tools/test_overview_bridge_lifecycle.py` as a separate step; Web's separate local lifecycle-test report is not silently described as that CI step.

PM read the original local evidence under `%LOCALAPPDATA%\LWBridgeRebuild\overview-evidence\` for sessions `c49e4c763c4f48dc931a1525fff28d63` and `fb3384bfdaef4c948de86d67430b36fa`. The old host-stop identifies PID 26224 and exact restored data/metadata/version hashes at 07:50:49Z. The fresh host-start identifies PID 7256 at 07:51:04Z. Its helper-start records the same profile/session/PID and `ready=true`, `messageVisible=true`, exact text, `UpdateManager.AddUpdate` and the in-game render path. Final summary records close/exit and exact original restoration at 07:53:25Z.

The currently present build matches the recorded live identities: EXE `6971b08550840774bbaf5d472be3d5ac506a0e74939fef0539106a99d21a3e6d`; DLL `c56785a9429b6857e50a9b85eea148dde1c23ee47c1dc6018a9c5e2bdfffd4c0`. The tracked helper matches the host evidence: `959915b725e2d450cb45b82d84bfe9010366773b63ba487084502a3cdc37cbaf`. The live finding names the parent plus then-uncommitted implementation; these identities connect that recorded run to the available candidate, without claiming CI re-executed the live game.

Read-only audit fingerprints of selected original evidence (files left unchanged):

| Session / file | SHA-256 |
|---|---|
| old / host-stop.json | `5183a6a044c5ee083fb42116ac07f54f17feee164d791c657383a5dc42ef70ca` |
| fresh / host-start.json | `ef0dde265c6d54f9106ac3bd3483f87a97b47666b8f2ddbf4bc1442eebb2a54f` |
| fresh / helper-start.json | `0bf3c3733fdb5475c925eeaf116075bed8f62afa12930dc2fd7f02510de12dec` |
| fresh / host-stop.json | `0c08478bfe8528329e9028c888a9534819c3d49b2b5974a5e25712f7a7804e8f` |
| fresh / attempt-summary.json | `e9c502e629251e1d73bafe346e8d3dbd98bb720cba88ed587ada6bf8a6b265c0` |

## Web assignment and return gate

1. Fix PM16-02 ownership first, then PM16-01 selected-root integration. Proceed across coherent commits without routine PM approval. Preserve unrelated work and all existing restrictions; these are normal implementation defects, not a Daybreak assignment.
2. Reproduce each negative case offline before fixing it, add targeted integration/helper regressions and rerun applicable suites. Do not execute game actions merely to reproduce adverse identity/path cases.
3. **PM16-03 — evidence/document reconciliation:** update the latest O01/O06 conclusions without rewriting historical evidence, correct stale queue/status instructions, and strengthen LWB-OVR-012's reproduction entry with the exact existing script/tool version/command or saved disassembly excerpt (it currently says only Capstone/pefile at the locators). Reuse permitted saved evidence; no new binary experiment is requested by PM. Label any unavailable reproduction detail honestly.
4. Record the corrected build/helper identity, focused results and applicable CI on the delivered revision. If a changed lifecycle needs normal-path live regression, first prepare automatic capture and an exact simple owner guide; collect only permitted evidence. Preserve previous live results and distinguish new technical proof from owner confirmation.
5. Commit/push/verify each coherent checkpoint. Return the two defect dispositions, remaining shared S-items and exact delivery revision for PM audit. **Do not begin Player City, any Map Data category, cross-server implementation, broad parity research or a new feature.** The owner receives the audit and explicitly decides subsequent scope.

The PM documentation commit is a successor to the audited code revision. After that commit, equality is expected at the successor; `0758006` remains the fixed reviewed code baseline, not a reason to reset the branch.
