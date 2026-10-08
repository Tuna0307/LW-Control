# 009-R2 A/B — launcher, post-connect, finalizer, restore/restartRequired recovery

Reference `reference/lwbridge-0.3.17.exe`, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`; static only.
Byte assertions: `tools/lwbridge317/home009_launch_contract_r2.py` (run record `launch-contract-r2-run.json`). Confidence markers:
**EXACT** = asserted at instruction/byte level; **INFERRED** = consistent with every call site, not byte-proven.

## A1. Launcher attempt loop (EXACT, `0x1D5009` stage 10)

* The loop variable is an inclusive `u8` range initialised to **1..=2** (`0x1DBE91 [0x87A]=2`, `0x1DBE99 word [0x878]=0x100`, i.e. exhausted=0,
  start=1); `0x1DC1A4–0x1DC1CE` yields the attempt number into `[r14+0x87B]`.
* Result mapping per attempt (`0x1DC2A1–0x1DC345`): a launch-task join failure becomes error **`LAUNCH_TASK_FAILED`** (code = message,
  18 bytes); an error whose code is exactly **`OFFICIAL_LAUNCHER_RESTARTED`** (length 27 compared at `0x1DC345`, bytes at `0x1DC354–0x1DC387`)
  **and** whose attempt number is 1 (`0x1DC390 cmp [0x87B],1`) logs `profile launcher retrying instance= … reason=official_launcher_restarted`
  (`0x1DC3D0`) and runs attempt 2; any other error, or the same error on attempt 2, is final. **Maximum two launcher attempts, one retry,
  retry trigger = the launcher restarting itself.** The terminal path logs `profile launcher failed instance= code=…`, lists the game
  processes (`0x41DEA4`), awaits the rollback future `0x1D420D` and returns the error.
* Launch report (JSON object, `0x1DC6D1–0x1DC719`): `pid` (3-byte key) must be a JSON number that fits 32 bits, stored at `[r14+0x860]`; missing or
  non-numeric/out-of-range ⇒ log `launcher failed … code=LAUNCH_REPORT_FAILED` and error **`LAUNCH_REPORT_FAILED`** (code = message) after rollback.
  When the request was the primary registration (`[r14+0x86D]`): `gameLaunchTicket` (16-byte key) and `gameLaunchTicketExpiresAt` are read for the
  cached-ticket log (`format=… reusable=…`); `LAUNCH_TICKET_INVALID` is the ticket-side failure.
* The reported PID's image is checked against `<root>\Game\LastWar.exe` (`0x1DCD6C 0x41E3A2`); mismatch ⇒ `LAUNCH_REPORT_FAILED`.
* Clock/units: none in the attempt loop (no sleep, no deadline). Cancellation: the loop is a future; dropping it runs no explicit branch.
* **Current comparison.** The clone has two single retries with different triggers (official Lua update failure; launcher did not issue the
  game start, R7-062) inside C# `StartAsync` — same attempt cap (2) but not the same trigger, and it has no "launcher restarted itself"
  observable. No equivalent supported state was demonstrated, so no change. Candidate: map "official launcher exited and a new launcher
  process appeared before the game" to the retry once the current launcher's restart behaviour is observed (live dependency).

## A2. Post-connect (EXACT, after the connected-PID image check)

Ok path `0x1DD6DB…`: (1) `0x1E71D6 → 0x2DBEBA` — **read `recovery.json` of the instance directory** (error label "read recovery record"),
validate that the record's instance id equals the directory's (`RECOVERY_RECORD_INVALID`, "recovery instance does not match its directory",
`0x2DC148–0x2DC15C`), set `has_game_pid=1` and `game_pid=<connected PID>` (`0x2DC1F1/0x2DC1FB`) and **commit** it through `0x2DB7CC` (temp file `recovery.<…>.tmp`, flush, rename);
(2) `0x23C1E4` inserts the record into the controller's instances map under a poisoned-lock check (`STATE_UNAVAILABLE`); (3) `0x23FAC5`
completes the instance: lookup by id with PID equality (else `INSTANCE_NOT_FOUND`), state `awaitingIdentity → running`, game PID tracked
for the monitor through `0x41BD6F` (monitor record pid set, `valid=false`, timestamp from `0x2C9034`), events emitted; finally log
`profile launch connected instance= game_pid=`.
Failure path (`0x1DD88B…`): any error from step (1) terminates the game with `0x41E543(root, connected pid)`, awaits rollback `0x1D420D`, and returns the error.
**Current comparison.** The clone's equivalent journal is the installation journal `recovery.json` (game pid/started-at are written after
`ready`) plus `OverviewLifecycleService` state; the original's per-instance record carries bridge build and lease data the clone has no analogue for.
Failure semantics match in effect (owned game closed, journal preserved/restored by the R1 gates). Not a discrepancy that can be corrected
without inventing the missing fields.

## A3. Finalizer `0x1DDDE3` (role EXACT, body partial)

Called only from the rollback future `0x1D420D` and from reconcile `0x2046EE`. Contains the lease release endpoint (`/api/multi/leases/{id}/release`,
literal at `0x1DE026` — **protected service**), the recovery-record removal (`0x1DE5C8 → 0x2DD578`, result dropped through `0x1E16A0`) and a
bounded step based on `Instant::now()` (`0x1DE463`). Reading: instance teardown = (optional) lease release + remove the instance's recovery/lease
records. Lease-release request/response bodies are protected inputs; the local record-removal branch ignores its error. INCOMPLETE: the async
frame states between `0x1DDF66` (`/api/logout` literal) and `0x1DE357` (`0x1DE6F0/0x1DFEE6/0x1E0BBD/0x1E0C27`) are not mapped to protocol fields.
Current counterpart: R1 Stop/cleanup (`CleanupExitedOwnedSessionAsync`, journal restore); there is no lease.

## B1. Restore of managed games and `restartRequired` (EXACT/INFERRED)

`0x2A0CB7` (called from `0x21E6E6` in `0x21C5B4`, app start) restores a managed game from its recovery record:
log `restoring managed game pid= instance= profile= phase=recovering lease=pending identity=pending`; when the recorded build differs from the
current build it logs `managed game requires bridge update pid= instance= profile= old_build= current_build=` (`0x84B703`) and sets the status
reason **`restartRequired`** (`0x2A0FB8`); otherwise it registers the instance with a **now + 90 000 ms** pipe-registry window (`0x2A1289–0x2A12CE`, same
`0x15F90`) and tracks the PID (`0x41BD6F`). So `restartRequired` = *restored game whose bridge build is outdated*.
Reconcile (`0x203021`) then handles each restartRequired id before launching (`0x203AB7–0x203D4D`): find its status item and loaded recovery
record; no record ⇒ `{profileId, error:"RECOVERY_RECORD_NOT_FOUND"}` (literals in the `0x83CFF8–0x83D048` recovery-error table; value constructor `0x31EF22` at `0x203C75`); process/record mismatch ⇒
`RECOVERY_PROCESS_MISMATCH` (condition INFERRED from the literal's position, not byte-proven); otherwise terminate the stale managed game (`0x203DCC`, path-verified) and wait with the 100×100 ms close wait
(`0x204593`), remove the instance (`0x23E158`), and the profile is launched in the main loop because kind-0 entries are part of the launch list.
`profile_instances_update_and_restart` (`0x2057DC`) is the manual button for the same state (UI `repairRequired`).
**Current comparison — NOT changed.** The clone cannot adopt a running game across host restarts (new session/challenge/pipe registration), so
*every* previous-session game is `RepairRequired`; making reconcile auto-terminate it would close games the original keeps (same-build restores
silently re-adopt). The clone's early return plus the manual update-and-restart command is the retained current-client adaptation. Equivalent
supported state requires session adoption, an unavailable capability; recorded as a dependency, not as parity.

## B2. `{profileId, error}` entries

`profileId` (key referenced at `0x203BD7`, 9 bytes) + `error` (`0x203C5F`, 5 bytes) with the value built by `0x31EF22` from a literal: for recovery failures the
value is the **code literal** (e.g. `RECOVERY_RECORD_NOT_FOUND`). Launch failures are collected at `0x2054CB…` into a Vec of 24-byte entries;
whether the `error` value of a launch failure is the code or the message is **not byte-proven** (INCOMPLETE). The UI consumes `error || message`.

## Remaining (individually)

* Async frame states of the finalizer (`0x1DDF66–0x1DE357`) and exact record schema (`0xA8`-byte structure fields beyond pid) — INCOMPLETE static.
* Launch-failure `error` value shape (`0x2054CB`) — INCOMPLETE static.
* Protected: lease release/activation bodies, ticket decisions, entitlement capacity.
* Live/capability: session adoption for restored games; launcher self-restart trigger.
