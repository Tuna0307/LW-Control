# Connection-method comparison

## Current answer

LW-Control Home manages the selected game's lifecycle and bridge readiness: validated installation, launch, session registration, connected/disconnected state, exact-owner Stop, repair/restart, Auto Launch and AutoReconnect. Home is the control surface; the actual method is implemented by native lifecycle services, launch helpers and the game-side bridge.

**No connection-speed, CPU, RAM, transport-throughput or recovery-efficiency winner has been established.** The competitor's unauthenticated launcher and Python bootstrap are recovered; its game attachment and connected runtime are not. A green label, a language choice, a process count at login or the use of mmap cannot establish a faster or more reliable game connection.

## Evidence matrix

| Connection stage | LW-Control at 45e151c8 | LastWarPlugin | Current conclusion |
|---|---|---|---|
| Select installation and launch owner | Home derives admissible lifecycle actions; native Start validates the root and existing/foreign process ownership before creating session state. | Recovered renderer submits a selected profile and menu mode to native IPC. | Both have launch controls; competitor native admission is unknown. |
| Launch preparation | Native Start settles official-client updating separately from the candidate's bridge-readiness budget. | Authentication precedes downloading encrypted Python core/UI; exact post-auth game-launch preparation is unavailable. | No comparable startup timing. |
| Runtime loading | Product composition enables the host control-pipe launch binding and invokes the current overview helper. Trace actual composition before making claims about individual RPC/transport classes. | Production bootstrap reads a 32-byte named-mapping key, AES-GCM-decrypts `core.py.enc`, optionally decompresses, then executes `core.py`. | The competitor's application-loader mechanism is recovered; it is not proof of how the Python core attaches to the game. |
| Decide game is connected | `IsSnapshotReady` checks PID, executable path, process start time, required authenticated route and matching heartbeat. Heartbeat identity includes profile, session, challenge and game PID. | Renderer comments describe native-fed mmap status using a live PID and `ACT`; actual publisher/validation logic is unavailable. | Retain LW-Control's proven checks. Do not infer competitor inferiority from missing backend evidence. |
| Stop and reconnect | UI captures the owner across asynchronous requests; native lifecycle/recovery code owns cancellation, cleanup and recovery state. | Core behavior remains encrypted/unavailable. | No comparative reliability or recovery result. |
| Status presentation | Selected-view updates have owner/revision fences; sidebar polls every three seconds with action/revision guards. | Native callback updates off/connecting/connected labels. | Different visible contracts. This is not a measured connection-performance comparison. |
| Multiple games | Released application intentionally enforces one game owner for shared-installation safety. | Launcher advertises multiple profiles/window slots. | Broader competitor product intent; simultaneous-game isolation and efficiency are unverified. |

Source anchors:

- `src/LWBridge.UI-0.3.17/src/HomePage.jsx:84` — Home lifecycle gating and controls.
- `src/LWBridge.UI-0.3.17/src/App.jsx:1163` — captured-owner Start/Stop handling.
- `src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx:156` — non-overlapping sidebar polling and revision checks.
- `src/LWBridge.Desktop/LWBridgeWindow.cs:380` — enabled production control-pipe launch binding.
- `src/LWBridge.Desktop/OverviewLifecycleService.cs:810` — Start admission/session identity.
- `src/LWBridge.Desktop/OverviewLifecycleService.cs:880` — official-client settling and bounded candidate preparation.
- `src/LWBridge.Desktop/OverviewLifecycleService.cs:1861` — readiness checks and process identity.
- `src/LWBridge.Desktop/OverviewLifecycleProfileInstanceStatus.cs:123` — matching heartbeat fields.

## Corrections that affect interpretation

- The competitor renderer's disabled Launch button does not gate every entry point. The retained mock-IPC test shows double-click submission while another profile is connecting when `_launching` is false. Whether native code rejects that request is unknown.
- The schema field `LastLaunchedAt` is not a proven populated history feed: no recording path was found. History display is outside the owner's immediate connection-method question.
- Capturing encrypted files alone does not recover their plaintext without the matching key. The owner has never logged in; no authenticated session or valid license has been supplied.
- The recorded Ghidra/decompiler memory pressure measures the analysis tools, not LastWarPlugin's product memory usage. It must not appear as a competitor connection-efficiency benchmark.

## Required measurements when comparable access exists

Use the same game build, machine, account/server conditions, update state and cold/warm-cache definitions. Record each trial's identity, timestamps, success/failure and exact cleanup. Keep authentication/download/update time distinguishable from connection time.

| Metric | Required observation |
|---|---|
| Startup latency | Click/command accepted → official launcher/game process → bridge attachment → independently confirmed game readiness. Report individual trials and median/tail results; avoid conclusions from one run. |
| Readiness correctness | Delayed login/bridge, stale heartbeat, wrong session, reused PID, wrong executable and process exit. Use controlled cases where possible; label simulations separately. |
| Reconnection | Disconnect/exit detected → retry admitted → matching new session ready; include retries, false connected states and failed-cleanup cases. |
| CPU/RAM | Exact owned process-tree accounting during equivalent idle connected windows and during startup/recovery; separate game cost from launcher/helper cost. |
| Transport efficiency | Identify actual active command/result and status channels, message rate, latency, serialization/polling overhead and backpressure. Do not assume dormant source classes are active. |
| Isolation and cleanup | Correct owner throughout Start/Stop/profile changes, installation restoration, leaked children, stale routes and shared-file conflicts. |

The current eight-second unauthenticated observation cannot provide these connected-state metrics. Actual competitor game-connection logic, transport, recovery and performance remain **UNKNOWN**.
