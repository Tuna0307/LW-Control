# PM review 17 — review-16 correction return

Reviewed 2026-09-13 at `7e1bee7742c710e778cc4d820db962da9062a901`, branch `research/offline-controller`. The worktree was clean; local HEAD, origin tracking ref and GitHub branch all matched that revision before PM documentation changes.

**Decision: accept PM16-02 and PM16-03 within their offline/source-evidence scope; partially accept PM16-01 and return two specific failure cases below.** Working live Launch/message/Close, startup/reconnect and repair scenarios remain credited to their original builds. The corrected code has no new live-game run. Neither zero-open lifecycle nor whole-Overview completion is approved.

PM reviewed the actual code changes, test source, durable findings, local artifact hashes and GitHub CI logs. PM did not implement application code, run application tests, perform binary experiments or operate the game. New failure cases are source-confirmed; isolated execution/reproduction belongs to Web.

## Accepted correction slices

- **PM16-02 / LWB-PM16-001: accepted, IMPLEMENTED/OFFLINE-TESTED.** The helper persists the acquired Windows process creation timestamp in the journal. Repair, heartbeat readiness, stop request/result and recovery identity checks carry that timestamp alongside profile/session/PID/path. The normal-close helper compares journal/request/current creation identity, rechecks before close, and includes a final PowerShell timestamp check. Isolated tests cover reused PID/path, missing/unreadable creation identity, a replacement between initial and final helper observation, exact matching identity and already-exited restoration. This closes the original static PID/path-only finding; it is not a blanket audit of every possible OS race or new live acceptance.
- **PM16-01 / LWB-PM16-002: accepted for the demonstrated stopped-path cases only.** `SaveGameRoot` now coordinates validation, persistence and `RebindGameRoot`. Missing-root -> valid selection and stopped A -> B are tested through backend/lifecycle integration. Running ownership and the recognized active repair journal block retargeting; null expected process paths no longer match arbitrary same-named processes. The two failure-state gaps below prevent full closure.
- **PM16-03 / LWB-PM16-003: accepted as evidence reconciliation.** The saved disassembly excerpt is durable and its hash matches. The missing historical generator command is explicitly unavailable, not invented. The excerpt labels Python 3.12.10 / Capstone 5.0.7 / pefile 2024.8.26 as currently installed versions; this does not independently prove the historical generator environment. No new binary experiment is required to close this documentation task. Current handoffs must reflect review 17 rather than keep saying all corrections are finished.

## PM17-01 — P2 — failed launch permanently blocks selecting another root in that app session

**OPEN; Web; continuation of PM16-01.** At the reviewed revision, [StartAsync](../../src/LWBridge.Desktop/OverviewLifecycleService.cs:438) assigns `instanceId` and `challenge` before calling the helper. Both catch paths set error state when `gamePid` is null but retain that session ID. [RebindGameRoot](../../src/LWBridge.Desktop/OverviewLifecycleService.cs:134) rejects any non-null `instanceId`. Normal Stop cannot release this state because it requires an owned game PID/path.

Concrete source trace: select valid root A -> helper fails before creating a game or recovery journal (for example process-start/preflight failure) -> helper is no longer active -> select valid B. Even with no game, helper or recovery obligation, selection returns `GAME_OPERATION_IN_PROGRESS`. Restarting the app or completing a successful same-root cycle is an unnecessary workaround. Current failed-launch tests check desired-running intent, not subsequent folder selection.

**Required correction and regression:** distinguish an abandoned launch attempt from live/uncertain cleanup ownership. Release stale attempt identity only after verifying there is no remaining helper, owned process or pending journal; never simply clear it on all exceptions. Add an isolated backend/lifecycle regression for failed-before-launch A -> select B -> successful helper invocation for B without app restart. Also prove an active/timed-out helper, partial launch or pending restoration still prevents retargeting. Invalid selection/configuration persistence failure must preserve the old root and state. Retain useful failure feedback and same-root retry.

## PM17-02 — P2 — unsupported or unfinished journals can be treated as no pending recovery

**OPEN; Web; continuation of PM16-01.** [HasPendingRecoveryJournal](../../src/LWBridge.Desktop/OverviewLifecycleService.cs:173) returns false for non-object JSON, missing/unsupported schema, or missing/nonmatching profile. For a matching schema/profile it returns true only for three recognized active/close stages (or a missing/non-string stage). Other stage strings return false.

This contradicts the claimed journal-free boundary. The existing helper writes additional unfinished stages, including `installed_<index>_<key>`, `restoring_after_failure`, `closing_failed_owned_game`, and `restoring_after_failed_owned_game_close` ([run_overview_bridge.py](../../tools/run_overview_bridge.py:238)). Initial journals originate at `backup_ready` ([run_live_resource_probe.py](../../tools/run_live_resource_probe.py:600)), and the Overview profile/session fields are populated later in successful startup. After an interruption, a pending journal may therefore lack the fields/stage that this predicate recognizes. On app restart, with no in-memory owned session, choosing another root can proceed while such a journal remains. Syntactically valid but malformed/unknown journal data must not prove cleanup is complete.

**Required correction and regressions:** classify absent, verified completed, pending and unknown journal states explicitly. Pending/unknown same-runtime ownership must block retargeting without deleting evidence. A foreign-profile record must not be silently treated as harmless in a shared runtime; establish a documented isolation rule or leave it blocked. Check known partial-install/failure/restore stages, unknown stage, missing profile/schema, unsupported schema, JSON null/array, unreadable data and absent journal through `SaveGameRoot`. Prove rejection preserves configuration/root/journal bytes. Prove a genuinely absent or verified completed cleanup state permits selection. Use temporary journals and fake helpers; do not corrupt or relocate the user's installation to reproduce these cases.

## Verification of the delivered package

| Revision | GitHub Actions | Result verified by PM |
|---|---|---|
| `9f181941c73b0b5b99e1640137c51d1063c3302e` | [34690077820](https://github.com/Tuna0307/LW-Control/actions/runs/34690077820) | SUCCESS at exact SHA |
| `c3d77e272bbd47a6bf59188a975bf0813caec8fd` | [34696597174](https://github.com/Tuna0307/LW-Control/actions/runs/34696597174) | SUCCESS at exact SHA |
| `7e1bee7742c710e778cc4d820db962da9062a901` | [34705461170](https://github.com/Tuna0307/LW-Control/actions/runs/34705461170) | SUCCESS at exact SHA; final submitted code/evidence |

Final CI logs show Release build 0 warnings/errors, deterministic backend `ok=true`, `failures=[]`, passing collector/preference/native WebView/transport checks, 35 browser checks and 20 identical screenshot pairs. These are offline checks; the helper lifecycle test also has a separate worker-reported local pass. None covers the new folder-selection failure cases yet.

PM verified the current local artifacts against LWB-PM16-003:

| Artifact | Matching SHA-256 |
|---|---|
| Release LWBridge.Desktop.exe | `fc8b3f140b3441e814d9d3d51dd285d77814ec35422b7181ac992be33d1acdb0` |
| Release LWBridge.Desktop.dll | `846ab46fe49b1cc24591d6f5f6aa4288a0a2efe871c1271d955e815c7e94aa2d` |
| tools/run_overview_bridge.py | `103224b89fb972ff82b75c6be925ce40054a5d1a197d045d51c7d7bfba79410e` |
| saved repair disassembly excerpt | `322e7a634ec7ecd664061cf7229731a5bb0718bda6a89b7e4802ad43035ca4f3` |

No source revision is inferred from a screenshot. No historical live evidence is promoted to the corrected build. The historical original-generator command remains unavailable. The PM documentation successor will move HEAD beyond the audited baseline; do not reset to it.

## Only next assignment and completion gate

Web fixes PM17-02, then PM17-01, reproducing both with isolated tests and preserving accepted PM16 work. Continue across coherent commits without routine PM approval; avoid unrelated research or expanding a new feature. Update the current status/ledger/handoff with exact code, evidence and test outcomes; commit/push/verify the actual corrected revision and applicable CI. Return to PM with both dispositions.

After these fixes pass, prepare one bounded corrected-build normal Overview Launch -> exact in-game message -> Close/restoration regression packet with automatic technical evidence and simple owner actions. Do not request real PID reuse, a second installation or damaged files from the owner. No live run is executed by this PM audit, and no prior restriction is cleared; identify actual permitted conditions before dispatch. If validation is unavailable, retain that explicit gate rather than silently reuse the old build's live success.

Shared S02 pending semantics, S03 runtime Refresh Status, S06 real cross-server travel and consumer-specific S05 runtime names remain unchanged and unfinished. The 47-case release matrix, protected bootstrap parity and wider event/update coverage stay separate. **No Player City, Map Data, cross-server implementation or other new feature is assigned. No Daybreak task. Only the owner resumes subsequent feature scope after the audit return.**
