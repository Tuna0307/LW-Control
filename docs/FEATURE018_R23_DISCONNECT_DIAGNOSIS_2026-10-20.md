# R23 owner reconnect diagnosis — 2026-10-20

Task 3 was investigated read-only against the owner's actual R22 records. No
game/app control, installed-script edit, or account action was used for this
diagnosis. Times below are Singapore time (UTC+08:00).

## What the records prove

- **PROVEN:** the owner's extracted R22 executable (`1f45410f6d4e` package) admitted
  game PID **364**, created **00:54:36.8525012**, and wrote its host-start record at
  **00:54:52.9396951**. Session: `11bf37d1eaad433ab6818013e42dbb23`.
- **PROVEN:** at **00:58:05.0924468**, the app's normal exact-owned Stop helper
  recorded `TerminateProcess`, `accepted=true`, `alreadyExited=false`, and a
  signalled process handle. Thus the captured process was still present when the
  helper closed it. All three installed script files then matched the original
  backup hashes exactly. This evidence does not support a claim that PID 364 had
  already crashed or disappeared.
- **PROVEN:** the same R22 executable admitted replacement PID **30688**, created
  **00:58:16.1342833**, and wrote host-start at **00:58:27.0527833**. Session:
  `84c55cc5ea1a458dbf4f85c309e8395c`.
- **PROVEN, separate later cleanup event:** `host-exit-restore-error.txt` records
  a failed close at **01:11:21.3416193**. The surviving journal was
  `closing_owned_game_for_restore`, with `OWNED_INSTALLATION_STILL_RUNNING` and
  survivor PID **46456**. It correctly withheld script restoration while another
  process of that installation remained. This happened after the reported
  reconnect/relaunch; it is not evidence of the initial disconnect trigger.

## What is unknown

The reason the original session became unavailable to the app is **UNKNOWN**.
The retained evidence proves a controlled app-side termination and a replacement
launch. It cannot distinguish an automatic recovery from another app-side Stop
and Start, nor attribute the initial condition to a server disconnect, stale
heartbeat, authenticated pipe loss, login/game-health timeout, or a native
recovery event.

`home004-recovery-events.jsonl` does not exist in the owner's evidence directory.
Before R23, `PublishRecoveryStatus` persisted recovery reason/state only when
`LWBRIDGE_HOME004_RECOVERY_TRACE=1`; normal owner usage therefore left no trigger
record. `recovery.json` is the install/restore transaction journal, not a history
of recovery decisions. Its later stage cannot recover an earlier trigger.

The current `Player-prev.log` is the later game session and contains ordinary
startup/asset warnings, with no retained crash or disconnect classification.
`Player.log` is a subsequent startup which calls the official launcher's
`ApplicationLaunch.RelaunchApplicationPC`; it cannot explain the earlier PID 364
transition. A bounded Windows Application query for event IDs 1000, 1001 and
1002, **00:50–01:15**, returned `NoMatchingEventsFound`. Absence of those events
does not prove the game or network was healthy.

Source inspection of `Feature018GameCommandService` and the attack Lua found no
direct game Stop, process termination, or recovery-start call in the Q error
route. The single-send-fence failure is an ordinary refused action result. No
retained command/heartbeat timeline establishes it caused the disconnect. R23
therefore does not change recovery timing or suppress a legitimate restart on
the assumption that Q caused it.

In plain words: the app deliberately closed and restarted its game process, but
the previous build did not save why it decided recovery was needed. The logs
available now cannot honestly settle the underlying cause.

## R23 observability correction

`OverviewLifecycleRecovery.cs` now records the exact trigger at admission and
subsequent recovery state transitions, independently of the opt-in Home harness.
`OverviewRecoveryAuditWriter.cs` writes under
`%LOCALAPPDATA%\LWBridgeRebuild\overview-evidence\<captured-session>\`:

- `recovery-events.jsonl`
- `recovery-events.previous.jsonl` (one bounded rollover)

Each file is limited to 128 KiB. Records contain controlled reason/source,
bridge/game/process health booleans, observed timeout durations, state, attempt
count, fresh-heartbeat and authenticated-route observations, and transport
counters. They contain no challenge, token, UID, profile,
command payload, image path or raw error text. Evidence survives runtime-file
cleanup. I/O failure cannot change recovery behavior.

Trigger sources distinguish `native_game_request`, `process_missing`,
`bridge_offline`, and `game_unhealthy`. They report the observed boundary; a
`bridge_offline` entry alone still does not identify a network/server cause.
The separate `HeartbeatFresh` and `AuthenticatedRouteConnected` fields distinguish
a stale/unreadable heartbeat from a fresh game heartbeat with a missing pipe.
Existing inert exact-threshold tests assert the persisted difference between a
present process with lost authenticated route (60 seconds) and a live route with
unhealthy game/login state (180 seconds), including credential exclusion,
bounded rotation and unwritable-destination handling.

## Evidence locations

Under `%LOCALAPPDATA%\LWBridgeRebuild\overview-evidence\`:

| Relative file | SHA-256 |
| --- | --- |
| `11bf37d1eaad433ab6818013e42dbb23/host-start.json` | `0bba02b73175630e14272644922c234aee3e46084165b2f32f646c0b624d60d1` |
| `11bf37d1eaad433ab6818013e42dbb23/host-stop.json` | `db1bacfd6622941d19377a41e16dd35fe08a0c4f4405d01d14da51722eaad26a` |
| `11bf37d1eaad433ab6818013e42dbb23/attempt-summary.json` | `abb11fc06ffd44f981d5f4a1bee58dc3df30610dd3dec7be16bcfe578de8ea08` |
| `84c55cc5ea1a458dbf4f85c309e8395c/host-start.json` | `57593c3451002a64c8b44c298604405fe2c5717fe4cbbd9a53f2ad26d4065251` |
| `host-exit-restore-error.txt` | `86f27856b85a9abab867b1dcd41243e1b95da7c7bff5c6ca83b7fd33637b2dd6` |

Game logs read: `%USERPROFILE%\AppData\LocalLow\FunFly\Last War-Survival Game\Player-prev.log`
and `Player.log`. These are rotating logs, not immutable historical receipts.

The later stale journal was independently inspected before the primary worker's
supported cleanup. Its backup
`overview-bridge-backups/20261019-165808-246adbef36944a87b2ef412173937eef`
matched its recorded original hashes: data `49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d`,
metadata `52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0`,
version `5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca`.
The installed data then equalled the journal's candidate package hash
`d6cdde3857b58f1c795039918fbcc36446cd5895a3e30c800de612813ddbb341`.
`operation.lock` was the normal one-byte Windows lock file; no operation-owner
metadata or lease file was present. File presence alone is not a held OS lock.
The supported `run_overview_bridge.py stop` route rechecks journal identity,
acquires the normal operation lease, independently proves the captured process's
exit, and refuses restoration if any process of the installation survives.

## Validation

The final diagnostic source built in Release with zero warnings/errors.
`dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-build`
passed through `HOME_FEATURE_DELIVERY_CHECKS_OK`, including the new persisted
trigger, privacy, rotation and I/O-failure checks. These are inert checks with
zero game launches; they do not establish the unknown historical trigger.
