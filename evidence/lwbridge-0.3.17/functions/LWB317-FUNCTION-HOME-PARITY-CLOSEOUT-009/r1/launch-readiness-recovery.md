# 009-R1 C — launch/readiness static recovery (LEAD009-03)

Reference: `reference/lwbridge-0.3.17.exe`, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Method: hash-gated static disassembly (`tools/lwbridge317/home009_native.py`), rip-relative string tables, caller indexes.
The reference was never executed. Every fact below that has an address is asserted at instruction/byte level by
`tools/lwbridge317/home009_launch_contract.py` (run: exit 0, output in `launch-contract-run.json`).
States: EXACT_CONTRACT = recovered from the image bytes; ADAPTATION = current-client mechanism; UNKNOWN/BLOCKED as stated.

## What 0x1D5009–0x1DDBB2 is

`0x1D5009` is the Rust `async fn` body of the original **profile launch** (three callers: `profile_instance_start`
handler `0x20715F`@`0x207808`, `profile_instances_reconcile` `0x203021`@`0x2053FA`, `profile_instances_update_and_restart`
`0x2057DC`@`0x206963`). Its packed log/error literals (`0x83C468–0x83CE00`) give the stage order that the earlier
campaigns only partly identified (008/009 called it "launch readiness"). Stage order by first reference:

| order | stage (literal) | address | protected input? |
|---|---|---|---|
| 1 | `STATE_UNAVAILABLE` (authorization state) | `0x1D5411` | state read |
| 2 | log `profile launch requested primary=… clear_login=…` | `0x1D58AB` | — |
| 3 | `GAME_ROOT_NOT_FOUND` | `0x1D5BE1` | — |
| 4 | log `capacity check started` / `completed max_profiles=… wait_ms=…` | `0x1D640B`/`0x1D64DD` | `max_profiles` is entitlement-derived |
| 5 | unmanaged-game gate: `UNMANAGED_GAME_RUNNING{pids}` or close + 5 s wait → `GAME_CLOSE_TIMEOUT` | `0x1D6548`–`0x1D74E5` | no |
| 6 | log `closed unmanaged games count=`, `delegated bridge injection to launcher` | `0x1D7380`/`0x1D73E2` | — |
| 7 | `running >= max_profiles` → `PROFILE_LIMIT_REACHED running= capacity=` | `0x1D7586`–`0x1D7690` | entitlement |
| 8 | ticket/registry/lease stages (`independent_official`/`cached_reusable`/`primary_official`, `/api/multi/leases`, `MULTI_LEASE_DENIED`, `SERVICE_UNAVAILABLE`, `LeaseResponse`, `LeaseActivationResponse launchProof`) | `0x1D78DE`–`0x1DB556` | **protected service** |
| 9 | instance directory + descriptor (`LAUNCH_DESCRIPTOR_FAILED`, `descriptorJson`, `descriptorSha256`) | `0x1D8EC0`–`0x1DAAA6` | local, partly auth fields |
| 10 | launcher: `profile launcher starting…`, `LAUNCH_FAILED`, `OFFICIAL_LAUNCHER_RESTARTED` retry, `LAUNCH_TASK_FAILED` (single-instance mutex), `profile launcher reported instance= game_pid=`, `LAUNCH_REPORT_FAILED`, `LAUNCH_TICKET_INVALID` | `0x1DB9FA`–`0x1DCDA0` | launcher/ticket |
| 11 | pipe-registry refresh + 90 s/250 ms wait → `BRIDGE_START_TIMEOUT`, else `profile launch connected instance= game_pid=` | `0x1DD10C`–`0x1DDB0B` | no |

Stages 5, 6 and 11 are fully local and decoded below. Stage 8 is the only part that needs a protected service
response; its **inputs** are named, its error literals are catalogued, and the local code after it is decoded.
It is not a blocker for stages 5/6/11, and nothing was run against the service.

## 1. `closeUnmanaged` — `0x1D6548`…`0x1D74E5` (EXACT_CONTRACT)

* **Input** — `profile_instance_start` payload object, key `closeUnmanaged` (`0x207784`, length 14): JSON boolean
  value → that value; key missing or non-boolean (and a non-object payload) → **false** (`0x2077AF xor eax,eax`).
  Reconcile (`0x2053D9`) and update-and-restart (`0x206940`) store `false` in the same launch slot. The shipped UI
  (`App.jsx` `startGame`) sends `{closeUnmanaged:true}` for the Start button.
* **State** — `unmanaged` = LastWar processes of `<root>\Game\LastWar.exe` (`0x41DEA4`) whose PID is **not** in the
  managed-PID hash set (`0x39D7DE` skips when `0x30A354` = `contains`; an empty set keeps everything), PIDs sorted
  through `0x2A2887` (collect + `u32` sort: insertion sort `0x25D68D` for < 21 elements, merge `0x2EE38D` otherwise).
  Ascending order follows from the default `Ord`; no comparator argument is passed (MEDIUM confidence, not byte-proven).
* **Action / result**
  1. empty → continue to stage 6.
  2. non-empty and `closeUnmanaged == false` → log `profile launch rejected code=UNMANAGED_GAME_RUNNING count=N`, error
     **code = message = `UNMANAGED_GAME_RUNNING`**, details `{pids:[…]}` (`0x1D6781`–`0x1D6828`), after the rollback
     future `0x1D420D` (see §5).
  3. non-empty and `true` → terminate every listed PID **in order** with the path-verified `0x41E543`/`0x41D84B`
     (`QueryFullProcessImageNameW`, `OpenProcess(1)`, `TerminateProcess(h,1)`; no wait). Any `Err` ends the launch at
     once (rollback `0x1D420D`, error returned unchanged); later PIDs are not touched (`0x1D65B7 jo` continues only on `Ok`).
     Errors: `PROCESS_QUERY_FAILED` ("Unable to verify the target process path."), or `IO_ERROR` with message
     `open target process: <os error>` / `terminate target process: <os error>` (`0x41DA31`, `0x41DA6D`, builder `0x2A1944`).
  4. After the last PID: `deadline = Instant::now() + 5 s` (`0x1D693F` `0x5CFC70`, `0x1D6944 mov r8d,5`,
     `0x1D6950 0x5DC950`; stored secs `[r14+0x498]`, nanos `[r14+0x4A0]`). Loop `0x1D723B`:
     **list → empty? → deadline? → sleep 100 ms → repeat**. Empty → success (stage 6). `now >= deadline`
     (`(secs,nanos)` lexicographic, `0x1D72D0`–`0x1D72EB`) with processes still present → cleanup then
     **`GAME_CLOSE_TIMEOUT`** (code = message, 18 bytes, `0x1D7563`). The wait is `0x641035` with `0x5F5E100` ns (`0x1D7305`).
* **Clock** — monotonic `Instant` (secs+nanos) via `0x5CFC70`; not wall-clock. Cap: with 100 ms sleeps the last list is at
  about 5.0 s; a process that vanishes exactly when the 5 s iteration lists it still succeeds (list precedes the deadline test).
* **Current (before R1)** — `OverviewLifecycleService.StartAsync` ignored the payload and always threw
  `UNMANAGED_GAME_RUNNING` ("Close the game started outside this application first.", no pids). **Discrepancy:
  the shipped Start button (closeUnmanaged:true) was refused where the original closes and continues.**
* **Correction (R1)** — payload key parsed exactly as above; refusal now carries code = message and `details.pids`;
  when true the unmanaged PIDs are closed through the new handle-bound `OwnedProcessTermination` (path-only identity,
  no exit wait, `QueryFailed → PROCESS_QUERY_FAILED`, `OpenDenied/TerminateFailed → IO_ERROR`), then the 5 s/100 ms
  loop; `GAME_CLOSE_TIMEOUT` on expiry. Reconcile/restart/recovery relaunch keep `false` (reconcile/restart proven;
  the monitor's relaunch flag is **UNKNOWN** — kept `false`).
  Verification: `--unmanaged-close-check` (9 groups; deadline boundary 4900/5000 ok, 5001/5100 timeout, elapsed exactly 5000 ms;
  ascending order; abort-on-error leaves later PIDs untouched; payload default table; refusal message/details).
* **Limits** — exact `<os error>` text of the IO_ERROR message is not reproduced (only the code and operation label are
  source-backed); "managed PIDs" are always empty in the clone at that point (single owned game, checked earlier as `GAME_RUNNING`);
  the real process enumeration was exercised only through inert seams here. Live proof: NOT performed (static/inert).

## 2. Bridge-connect wait — `0x1DD10C`…`0x1DD6B5` (EXACT_CONTRACT)

* **Input/state** — instance key (`[r14+0x390/0x398]`), launcher-reported game PID (`[r14+0x860]`), registry
  (`[[r14+0x250]+0x28]+0xE0`), root string for image checks (`[r14+0x330/0x338]`).
* **Order** — (1) after `0x1E11A5` returns, `deadline = wall_clock_ms() + 90 000` (`0x1DD114 call 0x2C9034`, `0x1DD119
  add rax,0x15F90`, `[r14+0x808]`); (2) `registry.refresh_pending(key, deadline)` (`0x2C75CA`, same value as expiry);
  `Err` → terminate the launched game with `0x41E543(root, game_pid)` (result discarded), rollback `0x1D420D`, return the
  registry error; `Ok` → (3) loop with **no initial sleep**: `now >= deadline` ? (`0x1DD3F7 cmp / 0x1DD3FE jge`, signed ms) →
  leave loop; else `pid_of(key)` (`0x2C6A65`): found → leave loop; not found → sleep **250 ms** (`0x1DD3B6`,
  `0xEE6B280` ns, `0x641035`) and repeat; (4) after the loop `pid_of(key)` once more: **not found → timeout failure**:
  log `profile launch failed instance= game_pid= code=BRIDGE_START_TIMEOUT` (`0x83CD6A`), `0x41E543` terminate the game,
  rollback, error code = message = **`BRIDGE_START_TIMEOUT`** (20 bytes, `0x1DD6A0`); found → (5) connected PID =
  registry PID, falling back to the launcher PID (`0x1DD49B cmovne`), `0x41E3A2(root, pid)` image check: mismatch →
  **`LAUNCH_REPORT_FAILED`** (code = message, `0x1DD6B5`) after terminate/rollback; match → continues to the final report
  `0x1E71D6`/`0x23C1E4`/`0x23FAC5` and log `profile launch connected instance= game_pid=` (`0x1DDB0B`).
* **Precedence consequence** — a registration that appears in the same iteration in which the deadline passes still
  succeeds (lookup after the loop). Clock moves backwards: `now < deadline` keeps waiting (no monotonic protection, wall clock).
* **Registry functions (EXACT)** — `0x2C75CA` = `refresh_pending(key, expiry)`: poisoned lock → `STATE_UNAVAILABLE`
  ("named pipe registry unavailable"); unknown key → `PIPE_INSTANCE_MISSING` ("named pipe instance is not pending");
  `expiry <= 0` or entry already connected (`[entry+0x40] != 0`) → `PIPE_REGISTRATION_INVALID` ("named pipe registration
  cannot be refreshed"); else `entry.expires (+0x38) = expiry`. `0x2C6A65` = `pid_of(key) -> Option<u32>` (`entry+0x28`).
* **Current comparison** — `LWBridgeControlPipeRegistry` already has `StartupRegistrationLifetimeMilliseconds = 90_000`
  and `RefreshPending` with the same three error codes (earlier campaigns). The **readiness wait** differs structurally:
  the clone's helper waits for a fresh bridge heartbeat/`ready.json` inside one `timeout_seconds` window (default 120 s)
  that starts at launcher start and also covers launcher→game spawn; the original starts a separate 90 s window at the
  launcher-reported game PID and tests registry connectedness (the game's pipe handshake). Different mechanism **and**
  different start point, so the same input state ("game starts, bridge never connects") yields a failure at a different time.
  **Status: observable timing difference identified, NOT corrected** — the equivalent start point and predicate in the
  current client cannot be derived without the launcher/ticket stage timings (protected) or a live witness, and shortening
  the clone's window could break legitimate slow current-client starts. Candidate action recorded for the lead.
* **Missing edges** — `0x1E71D6`/`0x23C1E4`/`0x23FAC5` post-connect report bodies; `0x1E11A5` (not in the unwind table).

## 3. `0x2A2887`, `0x2C75CA`, `0x2C6A65` — see §1 and §2

`0x2A2887` was described in 008 as the producer of a "non-empty async returned record"; it is a synchronous
`filter_map → Vec<u32>` + sort helper (callers `0x1D6533`, `0x1D72A8`, `0x205FDA`, `0x2A472C`, `0x2A500D`). The earlier
description is superseded by the byte assertions in `home009_launch_contract.py`. The 5 s/100 ms window is the
unmanaged-close wait, not a launch-response poll.

## 4. Error / cancellation precedence recovered for stages 5–11

STATE_UNAVAILABLE → GAME_ROOT_NOT_FOUND → (unmanaged: terminate errors | UNMANAGED_GAME_RUNNING | GAME_CLOSE_TIMEOUT)
→ PROFILE_LIMIT_REACHED → ticket/lease errors → LAUNCH_DESCRIPTOR_FAILED → launcher errors (LAUNCH_FAILED, LAUNCH_TASK_FAILED,
OFFICIAL_LAUNCHER_RESTARTED retry) → LAUNCH_REPORT_FAILED / LAUNCH_TICKET_INVALID → registry errors → BRIDGE_START_TIMEOUT.
The clone (`StartAsync`) checks closed → profile replacement → GAME_ROOT_NOT_FOUND → GAME_OPERATION_IN_PROGRESS → GAME_RUNNING →
UNMANAGED_GAME_RUNNING and keeps the relative root-before-unmanaged order. Cancellation by future drop has no explicit
branch in these states; user Stop is consulted through `instanceId`/`user_stop`/`INSTANCE_MISMATCH` (`0x1D9527`, source
not decoded).

## 5. Rollback future `0x1D420D` (PARTIAL)

Awaited immediately before every error return in stages 5–11 (e.g. `0x1D6624`, `0x1D6761`, `0x1D753E`, `0x1DD254`,
`0x1DD64E`, `0x1DD97B`). It is itself an async state machine (jump table `0x83DE94`): takes the instance lock
(`[ctx+0x210]`), runs the instance finalizer `0x1DDDE3` (461 instructions, uses `Instant::now`) and the registry removal
`0x23E158` (the same call that the user-Stop path `0x129622`/reconcile `0x204786` uses; error `STATE_UNAVAILABLE`).
Reading it as "release the reserved instance (rollback)" is consistent with every call site but the **finalizer body
`0x1DDDE3` is not decoded** (INCOMPLETE; next static action: map the callees listed by
`home009_native.py calls 0x1ddde3`, starting with `0x46D046`, `0x31020F`, `0x3A14EC`). Retries: the launcher stage has an
`OFFICIAL_LAUNCHER_RESTARTED` retry (`0x1DC35C`/`0x1DC3D0`; attempt count not decoded); stages 5 and 11 contain no retry.

## 6. Dependencies that remain (individual, not blanket)

| edge | kind | note |
|---|---|---|
| `max_profiles`/`PROFILE_LIMIT_REACHED` capacity input | protected entitlement | clone is single-instance; comparison not meaningful |
| ticket/lease/launchProof stage (`0x1D78DE`–`0x1DB556`) | protected service | local control flow around it is catalogued; not executed |
| launcher stage `0x1DB9FA`–`0x1DCDA0` retry/ticket-invalid mapping | static, not yet read line-by-line | next action recorded |
| finalizer `0x1DDDE3` | static, undecoded | see §5 |
| post-connect report `0x1E71D6`… | static, undecoded | |
| equivalent current-client start point of the 90 s window | live witness / protected launcher timing | see §2 |
