# Original 0.3.17 finalizer family: static recovery (HOME-MAP-COMPLETION-010 / c-finalizer)

Reference `lwbridge-0.3.17.exe`, SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
(verified by the script). Static disassembly only (python capstone/pefile, same as `tools/lwbridge317/home009_native.py`;
no pip install). Everything below is asserted by `decode_finalizer.py`; machine form in `finalizer-recovery.json`.
Nothing was executed; no protected service bytes exist, so server-side behaviour stays UNKNOWN.

## 1. What 0x1DDDE3 really is

`0x1DDDE3` is the async **release-lease** fn of the original multi-license service
(`src\services\multi_license.rs`): `release(state, instanceId) -> Result<(), Err80>`.
It is NOT a generic "finalizer of the launch"; the launch/stop/reconcile code calls it as a sub-step.
Poll output word: `0x8000000000000001` Pending, `0x8000000000000000` Ok(()), else Err (80 bytes).

| state (byte +0xE8, table 0x83DF8C) | entry | meaning |
|---|---|---|
| 0 | 0x1DDE2F | start: look up record |
| 1 | 0x1DE6E2 | re-poll after completion: panic helper 0x7A5150 |
| 2 | 0x1DE6EE | ud2 (poisoned) |
| 3 | 0x1DE07B | awaiting tokio-style Mutex Acquire (0x1F5F9A) on `[[state+0x150]+0x10]` |
| 4 | 0x1DDF43 | awaiting inner HTTP future 0x1DE6F0 (resume 0x1DE342) |

State 0: hash lookup (0x381DF, SwissTable, memcmp 0x7A3E30) of `instanceId` in the lease map `state+0x158`
(entries are String key + 0x98-byte record). **No record => Ok(()) at once** (no HTTP, no file removal, no event).
Otherwise: clone record; set `[rec+0x90]=1` (busy); path = `format!("/api/multi/leases/{}/release", record.leaseId)`
(template bytes at 0x83D27E); body = `{"instanceId": rec+0x48, "leaseToken": rec+0x18}` (keys built at 0x1DE10A/0x1DE1AD).
Then await the Mutex (the permit is held for the whole round trip, released at 0x1DE3DB before local mutation).

## 2. Inner future 0x1DE6F0 (authenticated JSON POST; also used by lease acquire/activate)

States 0..4 via table 0x83DFA0 (0x1DE72B, 0x1DF79B, 0x1DF7C9, 0x1DE8BF, 0x1DE85D).
Sequence: auth base-URL/config 0x3A530F (AUTH_URL_INVALID / AUTH_HTTPS_REQUIRED / AUTH_NOT_CONFIGURED, non-retryable)
-> device fingerprint 0x3A9637 (`reg.exe query HKLM\SOFTWARE\Microsoft\Cryptography /v MachineGuid`)
-> client build/integrity 0x3A8C1F (CLIENT_INTEGRITY_FAILED) -> if auth required and `[state+0x3A8]==0`: SESSION_INVALID
without sending -> send with headers `Accept: application/json`, `X-Device-Fingerprint`, `X-Client-Build-Id`,
`X-Client-Auth-Policy: 2`. Errors: transport timeout (0x50BD14 true) => `REQUEST_TIMEOUT`, other transport or body read
failure => `NETWORK_ERROR` (both retryable). Success = status 200..299 AND JSON object AND `ok==true` -> `data`.
Else `ApiError{code=/error/code if non-empty and only [A-Z0-9_], else "SERVICE_UNAVAILABLE"; detail=/error/detail}`.
No retry loop inside. **The finalizer never reads the response `data`** (dropped by 0x1E0C27): a 2xx/ok=true reply of any
shape is success. UNKNOWN: real timeout values, real server bodies (protected).

## 3. Outcomes (0x1DE3E2 branch on first result word)

| result | effects (in order) | returns |
|---|---|---|
| Ok | 0x31020F remove record from `state+0x158`; 0x2DD578 delete lease proof (opt. path rec+0x60), lease record file, instance directory `<dir>/instances/<id>` (every I/O error ignored); 0x3A0510 remove id from map `state+0x38`; 0x3A14EC emit event `bridge://multi-entitlement` | Ok(()) |
| Err (any code) | if record still present: `[rec+0x80/0x88] = Instant::now()+60 s`; 0x3A14EC emit; record, files, map entry **kept**, busy flag stays 1 | Err{code=api.code, message=clone(code), details=api.detail}; `retryable` discarded |
| Cancel/drop (0x1E1010) | state 3: drop Acquire; state 4: drop request, release permit; free path String; drop record clone | nothing else: no deadline, no removal, no event, no retry |

## 4. Who calls it, and error precedence (all call sites discard the Err)

* **Launch failure wrapper 0x1D420D** (27 call sites inside 0x1D5009, list in JSON). Fixed order:
  (1) write-lock `A+0x210`, clear `Option` at `A+0x220`; (2) 0x2D1EB0 unregister the instance (instance maps under
  std Mutex at +0x130 and active-instance slot at +0xE8); (3) await Mutex `A+0x108`; (4) release (Err dropped);
  (5) unlock; (6) 0x23E158 profile unbind: checks INSTANCE_MISMATCH (0x84185F), removes `recovery.json`
  (0x2DBDE4, context "remove recovery record") and profile binding (Err dropped). Wrapper returns `()`: the launch error
  always wins; cleanup errors can never surface. If the wrapper is dropped in (3)/(4) steps (4)-(6) are skipped.
* **Lease-grant apply failure inside launch** (0x1D88CF jno 0x1D8A74): release called directly at 0x1D8AE2 (Err dropped),
  then the same error continues to 0x1D8F22 and the wrapper at 0x1D8F8A: release runs a **second** time (no-record no-op
  if the first removed it, otherwise a second HTTP attempt). Medium-high (not executed).
* **Stop 0x1D4468**: wait-for-close 0x1DDC84 (up to 100 x 100 ms, then `GAME_CLOSE_TIMEOUT` "The game did not close in
  time.") -> abort on error before anything is released; else unregister, lock, release (Err dropped), unlock,
  0x23E158 whose Err IS propagated (0x1D4B90 -> 0x1D4ECF), success text "stopped".
* **Reconcile 0x203021**: per element 0x1DDC84 (Ok) -> 0x2D1EB0 -> release (Err dropped) -> 0x23E158 (Err handled via
  0x204B33; projection not followed, UNKNOWN).

## 5. Indirect call `call [r13+0x20]` (0x1DEE65) resolved

Not a protocol callback. `r13` is the vtable pointer of a 32-byte element popped from a ring buffer (0x2E4902 `shl rax,5`);
args (data field, ptr, len) match `bytes::Bytes` drop. `.rdata` holds the five-slot Bytes vtables
`[clone,to_vec,to_mut,is_unique,drop]`: static x7 (drop 0x2A664 = `ret`), 0x7E9A98 (drop 0x35188->0x35287), 0x7E9CD0
(drop 0x35C1C: atomic dec + HeapFree). It runs only on the transport-error branch to discard buffered body chunks and has
no lease/ticket semantics. Confidence medium-high (signature and table shape; not RTTI).

## 6. Corrections to earlier labels

* 0x2DD578 is the **lease** file removal (proof / record / directory), not the recovery-record removal; `recovery.json`
  removal is 0x2DBDE4 reached through 0x23E158 (profile unbind).
* The "60" at 0x1DE468 IS an observable-adjacent value: a retry-after deadline stored on the record after a failed release
  (same pattern in four sibling lease ops: 0xE6F71, 0xE9115, 0x2D8131, this fn).
* The finalizer's Err payload equals the ApiError code in both `code` and `message` (0x2A2C0 = String::clone of code).

## 7. Clone comparison (src/LWBridge.Desktop/OverviewLifecycle*.cs; no edits made)

* `StartAsync` (OverviewLifecycleService.cs ~877-915): on non-cancel failure sets `phase/connectionState=error`,
  `lastError=ex.Code` only if `gamePid is null`, rethrows; `finally` cancels the control-pipe launch binding. The
  lease/runtime-file/adoption cleanup (`StopLeaseTimer(deleteLease:true)`, `ClearRuntimeSessionFiles`, `RemoveAdoptionRecord`)
  sits in `ResetCancelledStartState` (~969-1002), used by cancel paths. Original runs unregister + lease release +
  recovery-record removal at EVERY post-lease failure edge (27 sites) with all cleanup errors swallowed. Difference to review.
* No remote `/api/multi/leases/{id}/release` equivalent (protected service; clone uses local lease timer): by design.
* Clone maps restore failures to `GAME_CLOSE_FAILED` / `RECOVERY_RECORD_COMMIT_FAILED` in some branches; the original never
  surfaces cleanup errors from launch.
* Stop/reconcile line-by-line comparison of the original 100x100 ms close wait and error propagation: not done (UNKNOWN).

## 8. Remaining UNKNOWN (reasons in JSON)

U-1 real timeout/server bodies (protected); U-2 consumer of record +0x80/+0x88/+0x90 (renew loop, who clears busy);
U-3 identity of `A+0x220` Option and map `state+0x38`; U-4 exact lease file names (path builder 0x2DCD09 not decoded);
U-5 per-edge launch error codes for the 27 failure sites; U-6 reconcile projection of the 0x23E158 error; U-7 JSON key order.
