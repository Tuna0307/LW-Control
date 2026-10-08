# HOME 009 C/D — original 0.3.17 `game_recovery.rs` monitor and recovery run

Source: `lwbridge-0.3.17.exe` SHA-256 `4E9C3113…D6783`, static x64 disassembly only (`tools/lwbridge317/home009_native.py`). Every statement below names an RVA. `EXACT_BYTES` = value read from the image; `EXACT_CONTRACT_RECONSTRUCTED` = control flow derived from the instructions (not execution of the original). Rust source file: `src\services\game_recovery.rs` (string `0x82c3a8/0xd668b8`). Module code lives in `0x419645–0x41f4f8`.

## 1. Module map

| RVA | Role (derived) |
|---|---|
| `0x1d1328` / spawn call `0x1d1353` | creates the monitor future `0xe561d` (callers: only this site) |
| `0xe561d–0xe5725` | monitor loop: `interval(2 s, 0 ns)` built at `0xe5650–0xe5663` (`edx=2, r8d=0` → `0x642dcc`); each tick `0x642b9f`; if `[ctx+0x126]==0` the loop ends, else calls `0x41a8a0` (`0xe56ce`) |
| `0x41a8a0–0x41ad16` | **monitor tick**: health classification, starts a recovery with reason `processExit` / `hang` / `disconnect` |
| `0x41b03a–0x41b3bc` | `start_recovery(ctx, reason, updateDetected)`: creates status `waiting`, increments notice id, spawns `0xe5884` |
| `0xe5884–0xe6bf7` | **recovery run** (async): 2-second state loop, update-activity watch, retry scheduling, launch |
| `0x4199ba` | `is_active_state(state)`: true for `waiting, repairing, launching, verifying, updating, maintenance` |
| `0x41dad8` | installation fingerprint (file metadata of LastWar*.exe, xlua.dll …) |
| `0x41dfb1` | update-process-present predicate (Toolhelp snapshot; names `LastWarLauncher.exe`,`LastWarUpdater.exe`,`LastWarSync.exe`; image path verified via `0x4197d8`) |
| `0x41e613` | kill update processes (same three names) |
| `0x41e543 → 0x41d84b` | terminate game by PID with image-path verification (`OpenProcess(PROCESS_TERMINATE)`, `TerminateProcess(h,1)`) |
| `0x41e3cf` | single PID + image path (`<root>\Game\LastWar.exe`, ASCII case-insensitive) presence predicate used by close polling |
| `0x41ba64` | `game_healthy(S,pid)`: shared game-state record present (`[rec+0x8c]==1`) and `[rec+0x88]==pid` |
| `0x2d47a6` | `bridge_online`: registry lookup (`0x2c9e9b`,`0x2c73d9`) |
| `0x41b7ef` | config boolean `auto_force_update_reload` (string `0xd67f2a`, len 0x18); missing / non-boolean ⇒ false |
| `0x419e2a` | launcher/updater/player log classifier (strings include `e999`, `e109`, `connect to server failed`, `loading error`, `download`, `update error`, `disk full`) |

## 2. Monitor tick `0x41a8a0` (cadence 2000 ms)

Preconditions (`0x41a8c0–0x41a8e5`): `auto_force_update_reload == true` (`0x41b7ef`), `ctx[+0x124] != 0` (set by game start `xchg [r14+0x124]` in `0x11157c`/cleared in `0x128a38`; behaves as desired-running), `ctx[+0x126] != 0` (monitor armed). Otherwise return **without touching the observation state `M` (`ctx+0x60`)**.

Then: a recovery already active (`0x4199ba` on the current status state) ⇒ return (`0x41a932–0x41a951`). Game root unresolved (`0x41d2ce`) ⇒ return. `ctx[+0x120]==0` ⇒ return.

`pid = find_game_pid(root)` (`0x41b451` → Toolhelp by name/path `LastWar.exe`).

* **No process** (`0x41aa30–0x41ab35`): `M.missing = saturating(M.missing+1)` (`[M+0xac]`), `[M+0xb0]=0`; if `M.missing <= 1` return (`0x41aafe–0x41ab01 cmp esi,1; jbe`), else recovery reason **`processExit`** (`0xd67e5c`, len 11) with update flag 0. ⇒ **two consecutive 2-second observations**.
* **Process present** (`0x41ab48`): `M.missing=0`; `online = bridge_online()`, `healthy = game_healthy(pid)`, `updating = update_process_present(root)`.
  * If `!(updating || online)`: `hung = EnumWindows(0x41a472, {pid})` (`0x41aa60–0x41aa7c`); otherwise `hung=false`.
  * `offlineSince` `[M+0x98]` keyed by pid `[M+0xb0]`: if `updating||online` ⇒ both cleared; else if `[M+0xb0]!=pid` ⇒ set pid and `[M+0x98]=now`.
  * `unhealthySince` `[M+0xa0]` keyed by pid `[M+0xb4]`: if `healthy` ⇒ both cleared; else if `[M+0xb4]!=pid` ⇒ set pid and `[M+0xa0]=now`.
  * `hungSince` `[M+0x90]` keyed by pid `[M+0xa8]`: if `hung` ⇒ keep or set to `now`; else clear both.
  * **hang** (`0x41abf8–0x41ac20`): `now-hungSince >= 0x7530 (30000)` **and** `now-offlineSince >= 0x7530 (30000)` (`cmp rcx,0x752f; jle` ⇒ `>= 30000`) ⇒ reason **`hang`** (`0xd67e67`, len 4).
  * **disconnect/offline** (`0x41ac22–0x41ac3b`): if `!(updating||online)` and `now-offlineSince > 0xea5f` (`>= 60000`) ⇒ reason **`disconnect`**.
  * **login unavailable** (`0x41ac3d–0x41ac63`): `unhealthySince > 0 && !healthy && now-unhealthySince >= 0x2bf20 (180000)` **and** `online` ⇒ reason **`disconnect`**.
* No `processExit` update-detected flag; `hang/disconnect` start with `updateDetected=false` (`xor r9d,r9d`, `0x41ab32/0x41acb8`).

## 3. `start_recovery` `0x41b03a`

Same three preconditions. If the current status state is active (`0x4199ba`) it only sets `updateDetected` when requested (`0x41b121–0x41b131`) and returns. Otherwise: state=`waiting`, reason=<given>, `startedAt=now`, `nextRetryAt=None`, `attempts=0`, `updateDetected=<flag>`, `restarted=false`, `noticeVisible=true`, `noticeId = ctx[+0x118]++ +1` (`lock xadd`, `0x41b25b`), logs `game recovery started reason=`, emits `bridge://game-recovery`, spawns `0xe5884`.

## 4. Recovery run `0xe5884` (EXACT_BYTES constants, reconstructed flow)

Frame: `[+0x50]` reason, `[+0x68]` stable-verify-since, `[+0x70]` last-activity/state-change timestamp, `[+0x78]` login-unavailable-since, `[+0x80]` next retry deadline, `[+0x108]` run-start pid, `[+0x10c]` last pid, `[+0x110]` normal attempts, `[+0x114]` maintenance attempts, `[+0x118]` maintenance flag, `[+0x128]` current clock (`0x2c9034`).

Constants (all `EXACT_BYTES`):

| Value | Locator | Use |
|---|---|---|
| tick 2 s | `0xe69e3` (`edx=2,r8d=0`) | recovery loop cadence |
| `15000, 30000, 60000, 120000, 300000` ms | table `0xd67b08` | normal retry delay, index `min(counter,4)`, counter post-incremented |
| `120000, 300000, 600000` ms | table `0xd681b0` | maintenance retry delay, index `min(counter,2)` |
| `0xdbb9f` (`> 899999` ⇒ `>= 900000`) | `0xe6096` | update activity stall (15 minutes) |
| `0x3a98` (`>= 15000`) | `0xe6285` | stable verification |
| `0xea60` (`>= 60000`) | `0xe654f` | disconnected game: wait before terminate |
| `0x2bf1f` (`> 179999` ⇒ `>= 180000`) | `0xe6309` | login unavailable: terminate |

These are now **0.3.17 facts**; they coincide with the 0.3.1-derived constants in `OverviewRecoveryPolicy.cs`. Remaining differences are structural (see comparison file).
