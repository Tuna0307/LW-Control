# LW Control reference UI and Start Game recovery

Source artifact: `C:\Users\chimw\OneDrive\Desktop\Github\LW\LWControl.zip` (`LWControl.exe`).

This document separates recovered reference behavior, proven current-build behavior, and remaining unknowns. The implementation must continue to prefer recovered evidence over guessed behavior.

## Recovered reference UI

The reference application exposes these top-level destinations:

- Home
- Map & Data
- Squads & AFK
- Automation
- Hotkeys
- Settings

The recovered top bar includes `Start game`, `Refresh`, a region chip, `Evidence`, and a language selector. The embedded UI marks the launch control as `data-topbar-launch-game` and routes it through `launchGame()`.

The native `LaunchGameAsync()` implementation was also recovered from `LWControl.dll`. It verifies the supplied `LastWar.exe` path, skips launch when that exact game is already running, resolves `LastWarLauncher.exe` only when the game path has the expected `...\Game\LastWar.exe` layout, and starts the official launcher with `UseShellExecute=false`, `CreateNoWindow=true`, and a hidden window. This confirms the reference bot launches the official launcher rather than starting `LastWar.exe` directly.

### Hotkeys

The embedded reference Hotkeys page contains these six groups:

| Group | Keys | Recovered behavior | Recovered gate / warning |
| --- | --- | --- | --- |
| Attack Targets | Q / W / E / R | Send squads 1 through 4 to the target under the pointer. | Foreground game + world target |
| Recall Squads | A / S / D / F | Recall squads 1 through 4 to the base. | Foreground game + active march |
| Shield Countdown | Space | Hold Space to show remaining shield time over protected cities. | Foreground game + shielded city |
| Use Shield | F6 / F7 / F8 | Use 8-hour, 12-hour, or 24-hour shield items. | Foreground game + matching shield item. The reference warns this immediately consumes the matching item. |
| Equipment Schemes | Alt+1 / Alt+2 / Alt+3 / Alt+4 | Apply equipment schemes 1 through 4 to configured squads. | Foreground game + saved scheme |
| Random Teleport | F9 | Use a random teleport item and wait for position-change evidence. | Foreground game + home ready. The reference disables this hotkey by default because it may consume an item. |

The rebuilt Hotkeys page displays all six recovered groups. They are read-only/PENDING until their underlying features are implemented.

### Settings

The reference Settings view contains four recovered sections:

- **Display & Behavior**: Windows DPI scaling and hide-in-background behavior.
- **Appearance & Language**: Chinese/English plus Cyan, Gold, Indigo, Rose, and Emerald accent choices.
- **Hotkeys & Foreground Gate**: fixed right-mouse menu toggle, Q/W/E/R, A/S/D/F, Space, F6/F7/F8, Alt+1..4, F9, and a Save gameplay hotkeys action.
- **System & Diagnostics**: Export runtime logs and Refresh runtime state.

The rebuilt desktop mirrors these controls. Language and runtime refresh are currently functional; unrecovered behavior remains disabled. Existing Daily Task/planner controls are preserved in a separate `Recovered Tools` settings tab so recovered functionality is not discarded while the reference shell is reconstructed.

The recovered feature catalog contains exactly 42 unique features. The grouping and action labels below come from the embedded reference UI.

### Map & Data

| ID | Feature | Description | Actions |
| --- | --- | --- | --- |
| `map_scan` | World Scan | Scan players, resources, monsters, and alliance targets. | Scan |

The recovered World Scan UI also exposes filters for All Targets, Player Bases, Resource Points, Monsters/Bosses, and Alliance Targets; search by name/alliance/coordinate/ID; and `Locate in Game`.

### Squads & AFK

| ID | Feature | Description | Actions |
| --- | --- | --- | --- |
| `region_jump` | Region Jump | Read the current server and jump to another region or return home. | Refresh; Jump; Return Home |
| `quick_attack` | Quick Attack / Recall | Attack with Q/W/E/R and recall with A/S/D/F. | Read-only Probe; Q Team 1; W Team 2; E Team 3; R Team 4; A Recall 1; S Recall 2; D Recall 3; F Recall 4 |
| `secret_mobile_squad` | Secret Mobile Squad | Open the official dispatch screen, dispatch eligible heroes, and claim completed rewards. | Open Dispatch Tasks; Dispatch; Claim |
| `zombie_gold` | Zombie / Auto Gold | Attack a zombie, gather gold once, or continuously dispatch idle squads to gold mines. | Start Auto Gold; Gold Once; Zombie; Status; Stop Auto Gold |
| `continuous_gathering` | Continuous Gathering | Cycle selected resources, reserve one idle squad, and strictly verify every gathering dispatch. | Status; Gather Once; Start Gathering; Stop Gathering |
| `auto_join_rally` | Auto Join Rally | Filter alliance rallies and join with an idle squad. | Start Auto Join; Status; Stop Auto Join |
| `team_swap_outfit` | Squad Equipment | Swap squad gear and save or apply equipment schemes. | Swap Gear; Save Scheme; Apply Scheme |
| `random_teleport` | Random / Alliance Teleport | F9 triggers random teleport; alliance teleport remains a menu action. | F9 Random; Alliance Teleport |
| `hospital_heal` | Hospital Heal | Treat wounded troops and verify queue changes. | Heal Now |
| `auto_reconnect` | Auto Reconnect | Monitor exits or disconnects and recover the game. | Enable; Stop |

The reference grouping array also contains `map_scan`; its route is Map & Data, so it is not duplicated in the Squads & AFK page in the rebuilt desktop UI.

### Automation / Daily

| ID | Feature | Description | Actions |
| --- | --- | --- | --- |
| `auto_mail_claim` | Read & Claim Mail | Read mail and claim valid attachments. | Read & Claim |
| `auto_radar` | Auto Radar | Scan, filter, claim, dispatch, and fight radar tasks. | Read-only Probe; Run Once; Pause |
| `auto_truck` | Auto Truck | Select a truck, cargo, and guards before departure. | Depart |
| `camp_armored_reward` | Camp / Armored Rewards | Claim camp and armored vehicle rewards. | Collect |
| `alliance_train` | Alliance Train | Queue, board, observe, and claim train rewards. | Queue; Board; Observe; Claim |
| `auto_train` | Auto Train | Claim completed queues and start the next training. | Train Once |
| `troop_promotion` | Troop Promotion | Promote eligible troops to the allowed tier. | Run Once; Pause |
| `apply_position` | Apply Position | Apply for an available position and verify appointment. | Apply Now |
| `alliance_gift_claim` | Alliance Gifts | Claim alliance gifts and reward chests. | Claim All |
| `use_stamina_item` | Use Stamina Item | Safely use stamina recovery items at a threshold. | Use One; Use at Threshold |
| `auto_reward_collect` | Reward Collector | Collect currently available task and building rewards. | Collect All |
| `daily_free_claims` | Daily Free Claims | Claim only rewards verified as free. | Run Once; Pause |
| `auto_attack` | Auto Attack | Search for a target and dispatch an attack squad. | Attack Once; Start; Stop |
| `auto_rally` | Auto Rally | Select a target and create an alliance rally. | Create Once; Start; Stop |
| `auto_chat` | Auto Chat | Send alliance notices or scheduled messages. | Send Once |

### Automation / Event

| ID | Feature | Description | Actions |
| --- | --- | --- | --- |
| `fireworks` | Fireworks | Use one firework and verify the item, event queue, and alliance points. | Use One |
| `red_packet` | Red Packet | Claim one valid red packet and verify the chat record and server grant. | Claim One |
| `golden_egg` | Golden Egg | Open one claimable golden egg and verify its queue and reward. | Open One |
| `treasure_hunt` | Auto Excavator | Scan official dig sites and excavate within configured spend limits. | Run One Cycle; Start Auto Dig; Refresh Status; Claim Dig Reward; Fragment Dig; Stop Auto Dig |
| `plane_mission` | Trade Plane Takeoff | Open the Business Center and observe official state before taking off once or on an interval. Independent claim, dispatch, and reward priority are unsupported. | Open Business Center; Read-only Probe; Take Off Once; Start Auto Takeoff; Refresh Status; Stop Auto Takeoff |
| `double_reward_tracker` | Double Reward | Read official multiplier windows; currently only verified matching excavation activities are scheduled automatically. | Check Multiplier; Read-only Probe; Start Tracker; Stop Tracker |
| `arms_race_alliance_duel` | Arms Race + Alliance Duel | Read official tasks and scores, run only exactly mapped training or promotion tasks, and verify both progress and score growth. | Check Events; Read-only Contract Probe; Run Smart Once; Start Smart Mode; Stop Smart Mode |
| `mining_dispatch` | Mining Dispatch | Dispatch a gathering squad or recall one. | Dispatch; Recall |
| `ghost_scout` | Ghost Scout | Start a personal ghost task or claim one completed task reward. | Start Task; Claim Reward |

### Automation / Alliance

| ID | Feature | Description | Actions |
| --- | --- | --- | --- |
| `alliance_help` | Alliance Help | Help alliance building, technology, and healing requests. | Help Once; Start; Stop |
| `alliance_tech_donate` | Alliance Tech Donate | Donate to alliance technology and verify contribution. | Donate Once; Start; Stop |
| `resource_grab` | Resource Grab | Select a transport or resource target and attack. | Grab Once |
| `shield_display` | Shield Display | Display player shield status and remaining time. | Show / Refresh |
| `performance_overlay` | FPS / PING Overlay | Display live FPS and network latency in game. | Toggle FPS; Toggle PING |
| `alliance_ghost_scout` | Alliance Ghost Scout | Select a recommended task and assist an ally once. | Assist Once |
| `secret_task` | Secret Task | Refresh for free, dispatch heroes, and claim secret-task rewards. | Free Refresh; Dispatch One; Claim One |

## Current rebuilt desktop implementation

- `World Scan` is **AVAILABLE**. `Scan`, filtering/search, details, and `Locate in Game` remain wired to the current persistent World Scan runtime.
- `Daily Free Claims` is **PARTIAL**. The rebuilt project has a recovered Daily Task Claim runtime path, but the full 42-feature reference `daily_free_claims` behavior is not yet recovered. Its reference action buttons therefore remain disabled.
- Every other recovered reference feature is **PENDING** and its action buttons are disabled. No missing feature is presented as functional.
- The legacy Daily Task planning/import controls remain under Settings so current recovered functionality is not lost while the reference shell is reconstructed.

## Start Game behavior

### PROVEN current-build / installed-system facts

- The installed FunFly shortcut targets `C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\LastWarLauncher.exe` with no arguments.
- The game executable is under `...\Game\LastWar.exe`.
- The current persistent package contains the Daily Task runtime entry, World Scan runtime entry, and preserved original Lua entry.
- Current source payload verification and the recorded install-manifest hashes both pass.
- The persistent loader registers the Daily Task and World Scan runtimes when the game loads. World Scan readiness is proven by a fresh `lwcontrol-world-full-scan-probe-9` heartbeat, not by the launcher process alone.

### Rebuilt Start Game flow

1. If Last War is closed, inspect the current persistent runtime with `install_daily_task_runtime.py --status --json`.
2. If the exact current runtime is already installed, leave the protected game files unchanged.
3. If a previous exact LW-Control install is recorded and hash-stable but its payload source is stale, restore that recorded original, prepare the current candidate, and install the verified current runtime while the game is closed.
4. If installation state is partial or hash-ambiguous, fail closed instead of restoring or overwriting unknown files.
5. Launch the installed official FunFly `LastWarLauncher.exe`.
6. Wait for `LastWar.exe` and then require a fresh persistent World Scan heartbeat before reporting the game/runtime ready.


### World Scan v9 live state

The rebuilt World Scan runtime is now live-validated at probe v9. Full persistent
scans leave Last War running, cover 10,000/10,000 logical blocks in 65/65
batches, traverse and restore all 500 monster views, and enrich player power plus
resource remaining/capacity through the current `WorldPointDetailManager` route.
The uncapped acceptance resolved 6,391/6,394 player powers and 7,952/7,987
resource amount rows; the remaining values stay unknown after one bounded retry.
Exact resource gather-end time is still unknown and is not fabricated.

The current workstation has one active monitor. World Scan does not require a
second display; any later GUI/mouse automation must target the single active
monitor.

## Recovered reference behavior vs unknowns

RECOVERED: the reference UI has a `Start game` control routed through `launchGame()`, and its native `LaunchGameAsync()` resolves and starts the official `LastWarLauncher.exe` from the verified `...\Game\LastWar.exe` path. It starts the launcher hidden with no shell execution and refreshes health afterward.

PROVEN on this PC: the official Windows Start Menu shortcut also targets the same `LastWarLauncher.exe` with no arguments.

RECOVERED startup-stage strings additionally show the broader reference flow: preparing the game bridge, installing the script container, starting Last War, and waiting for the game to connect. The rebuilt desktop follows that same order with the current verified persistent runtime and requires a fresh World Scan heartbeat before reporting ready.

## 2026-09-07 authorization/session recovery

The preserved Build 189 executable was unpacked again from its .NET single-file
bundle and the `LWControl.dll` / `LastWarControl.Auth.dll` managed code was
decompiled independently of the rebuilt desktop. The bundle is version 6.0 with
269 entries and includes the original `LastWarControl.App.Ui.overlay.html`
WebView2 resource. That resource is the exact React/CSS source of the reference
application shell and is therefore a stronger visual reference than the current
WinForms reconstruction.

### RECOVERED original client behavior

- `LicenseForm.AuthenticateAsync()` performs a normal `ActivateAsync()` request.
  A successful response stores the supplied key using `ProtectedLicenseStore`,
  records the authorized key in memory, and enters the main application.
- `AuthorizationSessionMonitor` waits two minutes between normal heartbeats and
  calls `HeartbeatAsync()` with a 20-second request timeout. Transient failures
  retry after 15 seconds and terminate the application after three failures.
- `SESSION_EXPIRED` and `SESSION_MISSING` heartbeat responses first trigger one
  `ActivateAsync()` reactivation attempt. Other non-transient heartbeat failures
  request application shutdown.
- A successful heartbeat may return `ExpiresAt`; the monitor forwards that value
  to `MainForm.UpdateAuthorizationExpiresAt()`, which posts an
  `authorization_update` message to the WebView UI.
- The WebView reducer only stores/formats `authorizationExpiresAt` for the
  top-bar `License expiry` display. The recovered desktop client contains no
  local `UtcNow >= ExpiresAt` termination check.

This explains the observed "remains open after the displayed expiry" behavior
at the client boundary: the process stays authorized as long as its periodic
heartbeat continues returning success. The reason a server may continue to
accept an already-open session past the license's displayed expiry is still
**UNKNOWN** until an authenticated live session is observed across that boundary;
the client code alone does not prove the server-side rule.

### Dynamic-test boundary

The preserved original executable was launched and the normal **Secure Access**
screen was observed. No license bypass, local expiry patch, or authentication
state modification was performed. Automated native-window key entry was blocked
by the current test environment, so post-authentication runtime observation with
the temporary valid key remains pending manual entry into the original login
form.
