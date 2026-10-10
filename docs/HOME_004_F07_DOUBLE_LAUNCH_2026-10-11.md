# HOME-004 F-07 — actual current-client double-launch attempt

Date: 2026-10-11 (Singapore). Existing Windows checkout, branch
`codex/home-complete-delivery-004`. The owner explicitly asked us to try two
game executables because their own attempt did not keep both running.

## Scope and preflight

- Official installed client: `C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game`.
- Tested normal `LastWarLauncher.exe` and its `Game\LastWar.exe`; no game source,
  network setting, account controls, gameplay, official updater repair or bridge
  helper was modified or exercised.
- Before launch, no game, launcher, sync or LWBridge desktop processes were
  running. The original version-24 Lua triplet was SHA-256 verified and copied
  into a separate task-owned, hash-verified backup.
- Exact task receipts: `artifacts/home-004/f07-direct-double-launch-20261011/`
  (`preflight.json`, `attempt-1.json`, `launcher-baseline.json`, process snapshots,
  `second-launch-observation.json`, `second-launcher-observation.json`,
  `close-result.json`).

## Genuine process observations

| Attempt | Windows process result |
| --- | --- |
| First bare `LastWar.exe` | Returned PID **61816**, but no related process remained nine seconds later. The game's recent Unity log included `Start Process ...LastWarLauncher.exe`, suggesting bare EXE is not a valid sustained launch baseline on its own. |
| Normal first official launcher | Launcher PID **46688** started a real, sustained game PID **45268** at the exact installed executable. |
| Second bare EXE with first game running | Second `LastWar.exe` PID **58608** was present alongside PID **45268** at **2 seconds**, but had exited by **6 seconds**; still absent at 12 seconds. |
| Second official launcher with first game running | Launcher returned PID **64984**. At **2, 7, 15 and 22 seconds**, only the original game PID **45268** remained. No second sustained game resulted. |

No game login, account action, gameplay or Map scan was performed. The same
installed current client and Windows user were used, so this did not test a
separate legitimately installed game, independent user profile or bridge adapter.

## Exact cleanup

Baseline game PID **45268** was compared against its saved executable path and
creation timestamp (only **0.355 ms** recording precision difference). Its
normal `CloseMainWindow` succeeded and it exited within 15 seconds. A fresh
read-only process check found **zero** related game, launcher, sync or desktop
processes. Original Lua SHA-256 hashes still matched the preflight:

- `LWScripts.data`: `d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495`
- `LWScripts.txt`: `b84cfa8762d9f8b93fcbe9e64b8715e84d1549248c2e98dc7dc272321476933b`
- `version.txt`: `c2356069e9d1e79ca924378153cfbbfb4d4416b1f99d41a2940bfdb66c5319db`

The three previously completed F-04/F-06 task journals were still absent.
No script restoration was needed in this direct-launch task: the script package
had not been swapped.

## Interpretation and acceptance

The observed installed current client did **not sustain two simultaneous game
processes**, whether retried through the EXE or official launcher. The brief
second PID is not a working second authenticated game. This corroborates the
owner's experience, but **does not prove that every Last War version or every
supported multi-client arrangement forbids concurrency**.

The existing shared-Lua admission guard remains necessary. The missing F-07
dependency is a genuinely supported second client and independently owned
mutable Lua package/bridge transport, or a proven equivalent. **Whole Home
remains PARTIAL**. Supporting only sequentially selected profiles would be a
separate explicit owner/lead product-scope decision, not a worker-certified
pass of the originally advertised simultaneous-game requirement.

## Follow-up: search for an alternate official login executable

At the owner's further request, the **same installation was inventoried
read-only** for a second executable that might offer a separate login. The
available EXE entries were:

| Installed entry | Finding |
| --- | --- |
| `LastWarLauncher.exe` | Official FUNFLY-signed normal launcher. One absolute `app_dir` in `LastWarLauncher.json`. |
| `Temp/LastWarLauncher.exe` | Same SHA-256 `f05f302959d3f7dad08cf477f44415591675c586c81a1c86814ba8a51c72ae62` as root launcher; not a separate login application. |
| `Game/LastWar.exe` | Only installed FUNFLY-signed game executable; SHA-256 `905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d`. |
| `LastWarSync.exe` and `Temp/LastWarSync.exe` | Byte-identical official sync/update utilities by hash comparison; **not gameplay/login executables**, not invoked. |
| `Game/UnityCrashHandler64.exe` | Unity crash handler; not a game login path. |
| `Game/AntiCheatExpert/ACE-Service64.exe`, `ACE-Setup64.exe` | Security service/setup executables; not game/login entrypoints, not invoked or modified. |

The root contains a zero-byte `lastwar.lock` and `Launcher.log` contains
`already running` matches; these are **clues**, not proof of the particular
single-instance implementation. Deleting a lock or disabling anti-cheat is
not a supported multi-instance adaptation and was not attempted. Both normal
and Temp `LastWarLauncher.json` have the same hash and the one official app
root. No distinct bundled launcher/game executable advertised a second
account-login flow. All inspected EXEs with tested signatures reported valid
FUNFLY publisher signatures; do not infer an additional supported install.

Official FUNFLY Terms effective 2026-08-31 specifically disallow bypassing
features that enforce usage restrictions and interfering with security
measures: https://lastwar-h5.lastwargame.com/legal/20260827/terms_en.html .
This investigation intentionally did not bypass a mutex/lock, patch binaries,
alter protected services or attempt fake login tokens.

Community observations (not publisher certification) describe one normal
Windows game alongside a *separate Android game running in an emulator*:
https://www.reddit.com/r/LastWarMobileGame/comments/1txs709/dual_account_ls/ .
That could permit the owner to log a sub-account into a separate client,
subject to the game rules and emulator compatibility, but the current
`run_overview_bridge.py` adapter depends on the native Windows `LastWar.exe`,
per-user `LocalLow` Lua package and Windows named pipes. An Android emulator
**does not satisfy F-07 Home's independent authenticated second Windows bridge
by itself**. No emulator was installed, launched or claimed as verified here.

Follow-up outcome: **no supported alternate login EXE found within the
installed current client**. Existing genuine single-game and F-04/F-06
acceptance are unchanged. F-07 remains OPEN pending vendor-supported distinct
client/package integration or an explicit lead/owner scope decision.

## Explicit owner clarification: two official Windows PC games only

The owner **rejected the Android emulator alternative** and explicitly
requires **two simultaneous official native Windows PC Last War games**, with
separate account login, not account switching or two saved metadata profiles.
Do not offer an emulator as completion of this requirement or change product
scope to one active game without a new explicit owner decision.

Read-only host inspection on this same machine:

- OS: **Microsoft Windows 11 Home**, build `26200`, x64; approximately
  **15.71 GiB physical RAM**, Intel Core i7-13650HX, RTX 4060 Laptop GPU.
- Windows Sandbox is **not supported** on Windows Home, according to Microsoft:
  https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/
- No `WindowsSandbox.exe`, `VBoxManage.exe`, `vmrun.exe` or `qemu-system-x86_64.exe`
  was on the inspected `PATH`, and no common third-party VM platform was found
  in the checked Windows uninstall inventory. This is not an exhaustive search
  for all possible virtual-machine software.
- Community reports describe the official PC client rejecting both Hyper-V
  guests and Windows Sandbox with a virtual-machine error. The report is
  anecdotal, not official policy or a fresh test of this installed build:
  https://www.reddit.com/r/LastWarMobileGame/comments/1lubjq2/pc_version_trying_to_run_two_clients_blocked_by/
- Another September 2026 player report describes separate Windows users and
  Sandboxie not producing two lasting PC game windows:
  https://www.reddit.com/r/LastWarMobileGame/comments/1wlqw8w/how_to_open_2_separate_windows_for_this_game_on_pc/

No guest Windows license or separate official installation was available for
legitimate dual-client testing. We did not install a VM, make a second Windows
user, copy and start an unsupported fake installation, defeat VM detection,
disable security services or modify the user's game or network settings.

**Observed boundary:** the installed client runs one lasting official PC game;
the verified current bridge also shares one mutable per-user Lua triplet, and
there is no verified second independent authenticated PC process to attach.
A second **physical** Windows PC with its own legitimately installed official
game is a potential separate environment but was not supplied, connected or
tested. A second physical PC would also require a separately validated
cross-machine LW-Control transport; it cannot be counted as current F-07 PASS.
The official publisher's support channel at https://www.lastwar.com/ lists
`support@lastwar.com` for clarification of supported native multi-client use.

**F-07 stays OPEN / whole Home PARTIAL.** Do not claim the limitation is
mathematically universal, claim a second account login happened, or re-run the
already-negative official single-root duplicate-start experiment without a new
supported technical hypothesis.
