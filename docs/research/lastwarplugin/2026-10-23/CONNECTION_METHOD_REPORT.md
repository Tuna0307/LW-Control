# LastWarPlugin vs LW-Control: game-connection method report

Research date: 2026-10-23
Research branch: codex/lastwarplugin-connection-research
LW-Control released baseline: main@45e151c8
LastWarPlugin target SHA-256: 3d90f6c2d12f8aecd2035406ecaaaef7c109b6fff8a8a48e12c1c0be002ac311
Status: PARTIAL for the competitor, COMPLETE for the released LW-Control connection-chain trace. No product code was changed.

## Owner answer

The current evidence does not support saying that LastWarPlugin has a better, more reliable, or more efficient game-connection method than LW-Control.

LW-Control is the better-supported reliability choice today. Its released path can be traced from Home Start all the way through official game launch, in-game bridge loading, authenticated communication, readiness, monitoring, reconnect, Stop, exact cleanup and Lua-package restoration. It binds the connection to exact process and launch-session identity and fails closed when that evidence no longer matches.

Efficiency is not settled. LW-Control has visible optimization headroom: the current game-side managed adapter can wake every 10 ms, validates a lease file, checks mailbox files and peeks the named pipe; Lua also has a 10 Hz Overview pump, a 0.75 s heartbeat file and a five-second pipe heartbeat. LastWarPlugin's shipped Launch Center claims realtime mmap-derived status, which could make status publication cheap, but that is only a renderer/status contract. Its authenticated core, actual game attachment, steady-state communication and reconnect implementation are unavailable. A fair connected-idle CPU/RAM or start-to-connected comparison therefore cannot be made.

The practical recommendation is to keep LW-Control's connection architecture and improve its known polling/file-I/O costs incrementally. Do not replace it based on LastWarPlugin's mmap/ACT comments or multi-open UI until the competitor's authenticated core is recovered and measured under equivalent connected conditions.

## Evidence boundary

Three evidence classes are used throughout this report:

1. **LW-Control production source** — active composition and call sites in main@45e151c8. Source that merely exists but is not normally composed is not treated as production behavior.
2. **LastWarPlugin shipped-artifact evidence** — exact EXE bytes, recovered embedded HTML/JavaScript, native imports/xrefs and the recovered production bootstrap. These prove what is present in the supplied binary, not what an unavailable encrypted payload does.
3. **Controlled runtime evidence** — task-owned LastWarPlugin execution before authentication, with no login, credentials, gameplay or account-changing action. Analysis-tool memory is excluded from product-memory claims.

Sanitized machine-readable findings are retained in connection-method-evidence.json. Raw private captures, local user paths, credentials, account data and keys are not committed.

## Stage-by-stage comparison

| Stage | LW-Control released method | LastWarPlugin supported finding | Assessment |
| --- | --- | --- | --- |
| Home Start | Home routes Start to the retained runtime owner for the requested profile, not merely the currently selected UI card. | Launch Center posts an IPC action with profile and menu mode. | Both expose profile-targeted launch contracts; only LW-Control's backend owner routing is fully recovered. |
| Pre-launch integration | Helper reads the official LWScripts package, preserves the original LuaEntry, installs a wrapper plus bridge modules, then launches the official LastWarLauncher.exe. | Native strings include launch game profile=, menu_mode=, LastWarLauncher.exe / LastWar.exe and local profile metadata. Actual post-auth launch implementation is unavailable. | LW-Control's integration mechanism is proven; competitor launch strings alone do not reveal attachment. |
| Game attachment | The game loads the augmented Lua package through its normal Lua/xLua runtime. The bridge then loads LWBridge.GamePipeAdapter.dll in-process. No external remote-memory attachment is required by this chain. | Actual mechanism is unknown. The outer EXE does not directly import the common remote-memory APIs checked, but the encrypted Python core can dynamically resolve APIs and remains unavailable. | No valid claim that one attachment method is inherently better. LW-Control is known and auditable. |
| Initial process ownership | Helper waits for exactly one LastWar process from the selected installation and records PID/path/start time. | Renderer comments say native/core status uses a live PID plus ACT from realtime mmap. Exact PID lifecycle rules are not recovered. | LW-Control has much stronger proven stale-process fencing. Competitor may have more checks in the missing core. |
| Session handshake | Per-launch profile/instance/token/build binding. Named-pipe hello is checked against the OS-reported pipe-client PID, actual client executable path and exact pending token registration. | Application bootstrap uses Local\\LwTool_MasterKey mapping, but that mapping decrypts core.py and is not the game-session handshake. No game-session handshake is recovered. | LW-Control wins on available reliability evidence; competitor unknown. |
| Readiness | Start succeeds only after exact process identity, an authenticated route and matching fresh same-session readiness/heartbeat evidence. Helper has a 90 s bridge-ready window and 250 ms poll. | Launch Center displays off / ing / on and waits for native/core lwSetStatus; UI has a 90 s fallback. Comments say ing/on are mmap-derived from live PID + ACT. Meaning and freshness of ACT are unknown. | LW-Control readiness semantics are proven; competitor UI state is not enough to compare reliability. |
| Steady communication | Authenticated named pipe carries framed JSON between host and managed adapter. Lua and adapter currently exchange frames through sequence-numbered JSON mailbox files. FEATURE018 also has a separate exact-owner file request/response path. | mmap is confirmed as a status concept in shipped JS. Named-pipe/socket/WebSocket primitives exist in the outer EXE/runtime, but none has been linked to the actual game-communication core. | LW-Control is more complex but known. Competitor transport overhead cannot be calculated. |
| Monitoring | Lua writes exact-session heartbeat state; host verifies process identity and authenticated-route state. Native recovery monitor runs every 2 s. | Native/core pushes renderer state through lwSetStatus. Actual health monitor, freshness rules and failure classification are hidden. | LW-Control has source-proven monitoring. |
| Transport reconnect | Same launch identity can retry the game-side pipe once per second, up to 90 attempts after a valid prior session/initial host-unavailable case. Authentication failures do not mint a replacement identity. | Unknown. | No supported competitor advantage. |
| Game recovery | AutoReconnect requires both AutoReconnect=true and GameDesiredRunning=true. It uses explicit hang/disconnect/login-health thresholds and retry tables. | Unknown. Launch Center's 90 s UI fallback is not a reconnect implementation. | LW-Control has a proven recovery design; competitor cannot yet be compared. |
| Stop | Stop invalidates recovery first, targets the captured owner and sends PID/path/start/session evidence to the helper. | Actual stop/cleanup implementation is unavailable. | LW-Control is better evidenced. |
| Restoration | Helper restores the preserved official Lua package only after the owned selected-install process is proven gone; failure retains evidence rather than blindly restoring. | Unknown. | Important known LW-Control reliability property. |
| Stale-state cleanup | Runtime files are removed only when they still match the expected session/challenge; pipe route removal is generation/registration scoped. | Unknown. | Strong LW-Control stale-successor protection. |
| Profile isolation | Native controller/session ownership is retained per profile. Real simultaneous game ownership is intentionally blocked because the mutable Lua package is shared per Windows user. | Launch Center advertises multi-open slots and sandbox/import profile concepts. Core implementation, isolation boundary and cleanup are unavailable. | Competitor contract is broader; implementation quality is unknown. |

## LW-Control: actual released connection chain

### 1. Home command reaches a retained per-profile runtime owner

The released app does not make connection ownership depend on whichever profile card happens to be selected at the moment an asynchronous command completes.

- src/LWBridge.UI-0.3.17/src/HomePage.jsx:95-100,131-136 dispatches lifecycle actions.
- src/LWBridge.UI-0.3.17/src/App.jsx:1163-1189 captures the Start owner.
- src/LWBridge.UI-0.3.17/src/App.jsx:1191-1228 captures the Stop owner before the asynchronous status read.
- src/LWBridge.Desktop/LWBridgeWindow.cs:749-786 routes status/start/stop by explicit profile ID to the retained owner.
- src/LWBridge.Desktop/ProfileRuntimeOwner.cs:74-156 defaults startRecoveryMonitor and startBridgeTransport to true and constructs the production lifecycle around the bridge host.

This matters for stale-state protection: switching the UI to another profile does not transfer process/session ownership.

### 2. Start modifies the official Lua package before launching the game

LW-Control is not attaching to an arbitrary already-running LastWar process. Its production method is a pre-launch integration:

1. Read the selected installation and reject conflicting ownership.
2. Generate a random session/instance identity and challenge.
3. Register a per-launch pipe token.
4. Build a candidate LWScripts package.
5. Preserve the official LuaEntry as a separate entry.
6. Replace LuaEntry with a wrapper that calls the official entry and pumps the LW bridge.
7. Append the Overview/FEATURE018 bridge modules.
8. Verify LENC serialization and package identity.
9. Launch the official LastWarLauncher.exe.

Key source:

- src/LWBridge.Desktop/OverviewLifecycleService.cs:810-1052
- tools/run_overview_bridge.py:455-610
- tools/run_overview_bridge.py:948-977
- src/LWBridge.Desktop/LWBridge.Desktop.csproj:20-33

The project file copies both the Lua bridge and LWBridge.GamePipeAdapter.dll into the published OverviewBridge payload. This is active production composition, not an unused experiment.

The advantage of this method is that connection logic executes in the game's own Lua/xLua environment after normal official launch, so the bridge can observe game runtime state without an external ReadProcessMemory/WriteProcessMemory loop. The tradeoff is that the installed per-user LWScripts package is mutable shared state and must be restored correctly. The released code therefore admits one real package owner at a time.

### 3. Helper identifies the exact game it launched

The helper polls the selected installation for LastWar processes every 250 ms. It fails if multiple matching processes make ownership ambiguous and records the matching game's PID/path/start time.

- tools/run_overview_bridge.py:948-977 — selected game acquisition.
- tools/run_overview_bridge.py:1018-1058 — exact launcher inventory and helper-owned process selection.

The helper then publishes a game report containing the exact launch session/challenge/game PID. The host refreshes the matching pending registration before connect-capable control is exposed.

- tools/run_overview_bridge.py:1304-1335
- src/LWBridge.Desktop/OverviewLifecycleService.cs:1431-1525

### 4. Game-side bridge establishes an authenticated named-pipe route

The game-side Lua validates that its environment still matches the active profile, instance and build and that the pipe token has the expected form. It then loads LWBridge.GamePipeAdapter.dll and sends a hello containing the profile, instance, token, game PID and build.

- tools/current_overview_bridge.lua:349-455

The managed adapter waits up to three seconds for the pipe, opens it read/write, sends the hello frame and maintains the worker:

- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:79-123
- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:136-255

The host handshake does more than trust a JSON PID:

- verifies the claimed PID equals GetNamedPipeClientProcessId;
- verifies build ID;
- obtains the actual client image path for that PID;
- compares it with the expected LastWar executable path;
- verifies exact profile, instance and token against an unexpired pending registration;
- assigns a monotonically increasing route generation.

Source:

- src/LWBridge.Desktop/LWBridgeControlPipeIsolatedHandshake.cs:83-150
- src/LWBridge.Desktop/LWBridgeControlPipeRegistry.cs:39-75,117-191
- src/LWBridge.Desktop/LWBridgeControlPipeHostState.cs:385-436

Generation-scoped removal means an old disconnected route cannot accidentally delete a newer route for the same instance.

### 5. The active communication path is hybrid, not a pure pipe

The host-to-adapter path is a real authenticated Windows named pipe with framed JSON. Lua does not directly own the OS pipe. The managed in-game adapter converts frames into sequence-numbered mailbox files for Lua and consumes Lua's outbound mailbox files:

    Host
      |
      | authenticated framed named pipe
      v
    LWBridge.GamePipeAdapter.dll inside LastWar
      |
      | pipe-inbound-NNNNNNNN.json / pipe-outbound-NNNNNNNN.json
      v
    current_overview_bridge.lua

Evidence:

- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:184-233
- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:332-399
- tools/current_overview_bridge.lua:3608-3793
- src/LWBridge.Desktop/LWBridgeControlPipeHostState.cs:292-337

The ordinary Lua RPC handler currently accepts the recovered getStatus call shape and returns game state. Other function names in this narrow handler fail with pipe_command_unsupported:

- tools/current_overview_bridge.lua:3750-3785

FEATURE018 is also actively composed and uses a separate exact-owner file exchange while commands are outstanding:

- src/LWBridge.Desktop/ProfileRuntimeOwner.cs:194-217
- src/LWBridge.Desktop/Feature018OwnedGameExchange.cs:15-41,73-115,140-247

This distinction matters for efficiency. Calling the architecture "a named-pipe solution" hides the Lua/adapter mailbox work that also happens in production.

### 6. Readiness requires process, route and game-side evidence

Connected is not accepted from a process name or heartbeat alone. Production readiness requires:

- exact PID;
- exact executable path;
- exact process start time;
- authenticated route for the same instance;
- fresh heartbeat carrying the same profile/session/challenge/game PID.

Source:

- src/LWBridge.Desktop/OverviewLifecycleService.cs:184-209
- src/LWBridge.Desktop/OverviewLifecycleService.cs:1861-1899

The helper-side ready file is also identity checked before Start completes:

- tools/run_overview_bridge.py:1250-1301

The current ready window is 90 seconds after game report. The helper checks readiness every 250 ms. On timeout, the native lifecycle does not leave a silently accepted process as connected; Start goes through owned cleanup.

### 7. Monitoring and connection overhead

Source-backed normal cadences:

| Mechanism | Current cadence | What it does |
| --- | ---: | --- |
| Lua Overview pump | max 10 Hz | Reads control/lease, pumps Overview work, readiness, transport state and heartbeat logic. |
| Lua heartbeat.json | minimum 0.75 s spacing | Publishes exact-session ready/login/connect/account/world state. |
| Pipe heartbeat | 5 s | Keeps the authenticated route active; host idle timeout is 30 s. |
| Host lease write | 1 s | Proves host ownership/liveness to game-side code. |
| Managed adapter idle loop | 10 ms sleep | Validates lease, checks outbound mailbox, peeks pipe, then sleeps when idle. |
| Recovery monitor | 2 s | Evaluates process/bridge/health failure evidence. |
| FEATURE018 result poll | 50 ms while outstanding | Waits for one correlated exact-owner response. |
| Pipe RPC call deadline | 200 ms | Bounds recovered host-to-Lua status call. |

Source:

- tools/current_overview_bridge.lua:3839-3872,3936-4030,4102-4121
- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:184-233,258-323
- src/LWBridge.Desktop/OverviewLifecycleService.cs:1938-1975
- src/LWBridge.Desktop/OverviewLifecycleRecovery.cs:18-20
- src/LWBridge.Desktop/LWBridgeControlPipeCallRegistry.cs:22-34
- src/LWBridge.Desktop/LWBridgeControlPipeTransportLimits.cs:24-26
- src/LWBridge.Desktop/Feature018OwnedGameExchange.cs:15-18,190-247

The 10 ms adapter loop is the clearest efficiency target. On each idle iteration it can read and validate lease.txt, check the next outbound mailbox and PeekNamedPipe. This does not prove high CPU usage: operating-system caching, scheduler behavior and game load matter. It does prove more wakeups and file-system work than an event-driven direct Lua/managed queue would require.

Transport queue limits are safeguards, not measured memory consumption. The configured 64 MiB outbound / 32 MiB inbound byte budgets must not be reported as normal RAM usage.

### 8. Transport reconnect preserves the same identity

If a previously valid pipe worker is lost because the host disappears or the pipe closes, Lua retries only the transport using the same environment/session identity. It does not invent a new profile/session/token after an authentication error.

- tools/current_overview_bridge.lua:349-455

The current retry spacing is one second, capped at 90 attempts. A completely unauthenticated failure other than the initial host-unavailable case is rejected.

### 9. AutoReconnect is separate from transport reconnect

LW-Control has two different recovery layers:

- **transport reconnect** — tries to reconnect the existing game/session route;
- **game recovery** — can retire/relaunch the game after sustained failure.

The native recovery monitor runs every two seconds:

- src/LWBridge.Desktop/OverviewLifecycleRecovery.cs:18-20,99-117

Automatic game recovery is gated by both AutoReconnect and GameDesiredRunning:

- src/LWBridge.Desktop/OverviewLifecycleRecovery.cs:155-167

Current policy:

- hang: 30 s;
- disconnect: 60 s;
- login/health unavailable: 180 s;
- post-recovery stable verification: 15 s;
- normal retries: 15 s, 30 s, 60 s, 2 min, 5 min;
- maintenance retries: 2 min, 5 min, 10 min.

Source: src/LWBridge.Desktop/OverviewRecoveryPolicy.cs:8-29.

Recovery also requires an authenticated route for bridge-online classification; a fresh file heartbeat by itself is not enough.

### 10. Stop and restoration are exact-owner operations

User Stop first sets desired-running false and invalidates automatic recovery. If a recovery launch is in flight before a replacement PID exists, Stop cancels that path and waits for its cleanup. For an active owner, Stop captures the instance/challenge/PID/path/start time and passes those to the helper.

- src/LWBridge.Desktop/OverviewLifecycleService.cs:1280-1400

After a successful helper stop, the lifecycle:

- cancels the matching pipe launch binding;
- removes the matching adoption record;
- stops and deletes the exact lease;
- clears only runtime files whose embedded session/challenge still match;
- clears the native owner state.

- src/LWBridge.Desktop/OverviewLifecycleService.cs:1354-1380,2151-2175

The helper restores the official package only after proving that the owned selected-install game has exited. This prevents a stale Stop from restoring scripts underneath an unrelated successor.

- tools/run_overview_bridge.py:1580-1693

## LastWarPlugin: what is actually recovered

### 1. The application decryptor is known

The supplied EXE contains an encrypted-payload bootstrap. This is an application bootstrap, not the game-attachment method.

Confirmed chain:

1. authenticated wrapper obtains a 32-byte master key;
2. native wrapper creates the Local\\LwTool_MasterKey mapping;
3. wrapper starts the Python runtime with LWAPPENC pointing at the encrypted core;
4. 456 bytes at EXE file offset 0x21AFD3 are XOR-decoded with 0x5A and written to the Python child;
5. bootstrap maps Local\\LwTool_MasterKey and reads exactly 32 bytes;
6. reads and deletes the encrypted LWAPPENC file;
7. AES-GCM decrypts it using the first 12 encrypted bytes as nonce;
8. optionally zlib-decompresses;
9. compiles and executes the result as core.py.

Retained bootstrap: bootstrap.txt
Bootstrap SHA-256: 6372938bcbc80b500816d10ac164b2f6ebf56aca9521065a32441dd27d0b00ee

Native anchors:

- core.py.enc literal: file offset 0x1B52FA;
- Local\\LwTool_MasterKey: 0x1B52A8;
- LWAPPENC: 0x1B531A;
- CreateFileMappingW: VA 0x140020A7F;
- MapViewOfFile: VA 0x140020AA8;
- OpenFileMappingW: VA 0x1400290A9.

This tells us how LastWarPlugin protects and transfers its own program code. It does not tell us how core.py connects to Last War.

### 2. Launch Center proves a status contract, not an attachment mechanism

Recovered shipped JavaScript states:

- status values are off, ing and on;
- the renderer deliberately does not set ing itself;
- comments say native/core derives status from realtime mmap using a live PID and ACT;
- native/core calls window.lwSetStatus(id, state, slot);
- Launch posts profile and menu_mode through window.ipc;
- a 90 s renderer timer releases the Launch button if no newly opened window reaches on.

Source:

- scripts/embedded-02-inline-01.js:36-67
- scripts/embedded-02-inline-01.js:81-100

The exact EXE also contains adjacent launch/status strings:

| Literal | File offset |
| --- | ---: |
| launch game profile= | 0x21AE72 |
| menu_mode= | 0x21AE89 |
| LastWarLauncher.exe | 0x21AE9F |
| LastWar.exe | 0x21AEB5 |
| _accounts.json | 0x21AF25 |
| PROFILE_DELETE_FIRED uid= | 0x21AF34 |
| window.lwSetStatus | 0x218D88, 0x21AF67, 0x21AF7D |
| mmap | 0x2139EB, 0x2189B4, 0x219686 |
| ACT | 0x2189D0, 0x2196AC |

These are useful because they show that launch/profile/status concepts are present in the shipped wrapper/embedded Launch Center. They still do not reveal whether core.py uses an injected DLL, Lua replacement, shared memory control block, local IPC proxy, runtime hooks, process-memory operations or another mechanism.

### 3. Bounded native xrefs do not recover the game attachment

To avoid repeating the previous expensive whole-program Ghidra analysis, this pass used PE exception-function boundaries plus Capstone to resolve only selected literals/import calls.

The direct code reference to the LastWarLauncher.exe literal is:

- literal file offset 0x21AE9F;
- xref VA 0x14000A89B;
- function range 0x140009CCD..0x14000BDBB.

The relevant imported calls in that function include CoInitializeEx, CoCreateInstance, SHCreateItemFromParsingName, CoTaskMemFree and CoUninitialize. It is a COM path-selection flow, not evidence of the actual game launch/attachment operation.

The outer EXE contains CreateProcessW at VA 0x14014AEF8 and NtCreateNamedPipeFile at VA 0x14014FC73, but the current evidence does not connect those generic runtime functions to the Last War session. They are therefore not credited as the competitor game transport.

The outer executable also does not directly import the common remote-memory injection APIs checked:

- OpenProcess;
- ReadProcessMemory;
- WriteProcessMemory;
- VirtualAllocEx;
- CreateRemoteThread;
- NtWriteVirtualMemory.

That negative result applies only to the readable outer EXE. Python core.py can load ctypes/cffi/native extensions, resolve APIs dynamically or use a completely different mechanism. It would be incorrect to conclude "LastWarPlugin does not inject" from this import table.

### 4. Exact missing layer

The owner's machine has never supplied a valid LastWarPlugin authentication/license flow. No matching encrypted core.py.enc/ui.html.enc remains in the installation, and captured ciphertext without its corresponding key cannot be decrypted.

The following remain unknown:

- whether LastWarPlugin launches only the official launcher or also injects/loads another component;
- the actual game-attachment mechanism;
- whether the mmap described by the Launch Center is only a status record or the primary game communication channel;
- mmap naming/layout, ACT semantics and freshness rules;
- command transport after attachment;
- polling/message cadence;
- exact game PID/session/start-time fencing;
- reconnect thresholds and retry policy;
- stop/cleanup behavior;
- sandbox/profile isolation implementation;
- restoration behavior if it modifies game files;
- connected-idle CPU/RAM;
- start-to-connected latency.

The next decisive artifact is a normal owner-authenticated core.py payload or a task-owned decrypted in-memory equivalent from that legitimate launch. Post-auth ui.html is also useful if it contains connection controls. No fabricated credentials are needed or appropriate.

## Reliability comparison

### Process and session identity

LW-Control uses multiple independent identity checks:

- profile ID;
- random instance/session ID;
- random challenge;
- random per-launch pipe token;
- game PID;
- OS process executable path;
- OS process start time;
- OS-reported named-pipe client PID;
- named-pipe client executable path;
- bridge build ID;
- route generation/registration serial;
- fresh matching heartbeat.

No single stale PID or stale runtime file can satisfy all of those conditions.

The LastWarPlugin renderer contract only exposes "live PID + ACT" as the visible status basis. This is insufficient evidence to judge its full stale-state protection. The core may bind additional identity fields internally. Therefore:

- supported conclusion: LW-Control has stronger **proven** stale-state protection;
- unsupported conclusion: LastWarPlugin is definitely weaker.

### Failure detection

LW-Control distinguishes:

- process missing;
- process hung;
- authenticated route offline;
- game login/health unavailable;
- confirmed recovery/update signals;
- server-maintenance/update conditions;
- user-requested Stop.

Each has explicit timing and lifecycle behavior. The competitor's off/ing/on UI does not expose comparable failure classification.

### Reconnection

LW-Control also separates same-session transport recovery from whole-game recovery. That is a reliability advantage in the current evidence because a brief host pipe loss does not immediately require a full game restart, and a full recovery remains tied to desired-running state.

No comparable LastWarPlugin reconnection implementation is accessible.

### Cleanup and restoration

LW-Control's strongest reliability property is cleanup specificity. Route removal is generation scoped; runtime files are removed only if they still contain the expected session/challenge; Stop uses captured process identity; official scripts are restored only after owned-process exit is proven.

This directly reduces the risk that a late Stop/recovery completion destroys a newer session's state.

Competitor cleanup is unknown.

## Efficiency comparison

### What can be said about LW-Control

The architecture avoids continuous external process-memory scanning for the connection path. Most game state is obtained in-process through the game's Lua/runtime objects.

However, its current bridge boundary is more expensive than it needs to be:

- adapter loop can wake at roughly 100 Hz when idle;
- lease.txt can be opened/read/parsed on those iterations;
- next outbound mailbox existence is checked repeatedly;
- pipe readiness is polled with PeekNamedPipe;
- Lua polls its mailbox at the 10 Hz Overview cadence;
- exact-session heartbeat.json is rewritten approximately every 0.75 s;
- additional FEATURE018 request/response files poll at 50 ms while a request is outstanding.

These are concrete optimization opportunities. They do not by themselves establish that the product is "slow" or that another product is faster.

### What can be said about LastWarPlugin

The Launch Center's status path is described as realtime mmap plus native-to-renderer lwSetStatus. If that mmap is a small shared status structure and native code waits/pushes efficiently, status updates could be low-overhead.

That is not enough to infer total connection efficiency. The missing core could also be polling the game, maintaining additional hooks/processes, using sockets/pipes, or performing expensive work. Its command/data path is unknown.

### Controlled product measurement

A fresh task-owned LastWarPlugin run was measured only in its unauthenticated wrapper state. No input/login/game action occurred.

Eight-second sample after startup:

| Metric | Observed |
| --- | ---: |
| Process tree | 1 LastWarPlugin + 6 WebView2 children |
| CPU time delta | 0.2969 s |
| Aggregate CPU as % of one logical core | 3.71% |
| Aggregate working set | 332.54 MiB |
| Aggregate private bytes | 158.89 MiB |
| Task-owned processes after cleanup | 0 |

This is a wrapper/WebView measurement. It must not be compared with LW-Control while connected to a game.

No task-owned LW-Control game session was already active during this research pass. Creating one solely for a benchmark would modify the live shared Lua package and start a real game; this was unnecessary for answering the supported comparison. Therefore fresh equivalent connected-idle CPU/RAM is marked **not measured** for LW-Control.

LastWarPlugin connected-idle CPU/RAM cannot be measured without the missing authenticated core. Startup latency to actual connected state is likewise unavailable.

The prior multi-gigabyte Ghidra analysis process is not product memory and is excluded.

## Fair benchmark required before an efficiency claim

When authenticated competitor access becomes available, use the same test definition for both products:

1. Same PC, game build and launcher installation.
2. Same account/game state where account changes are not required.
3. Measure cold and warm launch separately.
4. Start timestamp: user-visible/native Start admission.
5. Connected timestamp: each product's strongest proven ready condition, not just process creation.
6. Record process tree and exact child ownership.
7. Measure at least 60 seconds of connected idle with no automated/gameplay actions.
8. Report aggregate CPU time delta, logical-core-normalized CPU, private bytes and working set for controller/helper/bridge processes separately from the game.
9. Measure message/file/mapping activity with equivalent instrumentation.
10. Exercise one controlled transport-loss case and one game-process-exit case, recording recovery latency and stale-state outcome.
11. Verify Stop leaves no task-owned process and restores any modified files exactly.

Only after that test can "more efficient" or "faster to connect" be answered quantitatively.

## Recommendations

### Keep the LW-Control connection model for now

There is no recovered competitor mechanism strong enough to justify replacing an auditable working chain. LW-Control's reliability behavior is explicit and its risks are known.

### Optimize LW-Control where evidence already identifies overhead

Highest-value connection-path work:

1. Reduce the adapter's 10 ms idle polling. Prefer a blocking/event-driven wait for pipe or mailbox work while retaining bounded cancellation and same-session fencing.
2. Avoid reparsing lease.txt on every idle adapter iteration. A design can preserve the five-second freshness guarantee while waking on lease change or using a bounded cached timestamp plus periodic revalidation.
3. Remove disk mailbox hops between Lua and the managed adapter if the current xLua bridge can expose a safe direct managed queue/callback. Preserve the existing framed protocol, owner identity and correlation semantics.
4. Keep the current 0.75 s game-state heartbeat only if recovery/readiness actually needs that rate; measure whether a slower steady-state heartbeat plus immediate event publication gives the same recovery behavior.
5. Keep FEATURE018's 50 ms response polling confined to outstanding commands; do not turn it into a permanent idle loop.

Any optimization should retain the existing token/build/client-image handshake, PID/path/start checks, session/challenge matching, generation-scoped route removal and exact-owner cleanup.

### Do not copy competitor mmap until its purpose is understood

LW-Control could use shared memory for a small status record if measurement shows file-status publication is expensive, but LastWarPlugin's ACT contract does not establish that its mmap is the reason the whole connection system is better. A mmap can also carry stale data unless its owner/freshness/generation model is sound.

### Recover the competitor core only from legitimate owner authentication

The next research pass should stop speculating at the wrapper boundary and capture one of:

- authenticated core.py before/after its normal decryption in a task-owned process;
- a task-owned memory copy of the decrypted code;
- the matching encrypted core plus the legitimate session key from that same launch.

Then trace:

    Launch Center IPC
        -> core launch handler
        -> game process creation/selection
        -> attachment/loading method
        -> mmap/pipe/socket/module creation
        -> connected-state predicate
        -> monitor/reconnect loop
        -> Stop/cleanup/sandbox restoration

Until that layer is available, the competitor connection method remains unknown.

## Reproduction notes

LW-Control findings are reproducible by checking the cited released source at main@45e151c8.

LastWarPlugin binary identity:

    Get-FileHash -Algorithm SHA256 LastWarPlugin.exe

Expected:

    3d90f6c2d12f8aecd2035406ecaaaef7c109b6fff8a8a48e12c1c0be002ac311

Portable retained-evidence verification:

    python docs/research/lastwarplugin/2026-10-23/verify_evidence.py --exe C:\path\to\LastWarPlugin.exe

The bounded native checks used the PE exception-function table plus Capstone RIP-relative reference resolution and import-table resolution. Whole-program Ghidra analysis was deliberately not repeated.

## Unresolved gaps

The following are blockers to a definitive owner-to-owner comparison:

- no authenticated LastWarPlugin core.py;
- no authenticated LastWarPlugin post-launch UI/core runtime;
- no concrete competitor game attachment/loading primitive;
- no competitor game-session identity/freshness model;
- no competitor command transport/cadence;
- no competitor reconnect/cleanup/restoration implementation;
- no equivalent connected-idle CPU/RAM measurements;
- no equivalent start-to-connected timing;
- no proof that competitor multi-open/sandbox isolation survives crashes/reconnect/Stop.

These gaps are evidence limitations, not evidence that the competitor lacks those capabilities.

## Final verdict

**Reliability:** retain LW-Control. It is substantially better proven: exact process/session identity, authenticated route, fail-closed readiness, layered reconnect, recovery gating and successor-safe cleanup/restoration are all present in the released production chain. LastWarPlugin may be equally robust internally, but the missing authenticated core prevents proving it.

**Efficiency:** no winner. LW-Control has measurable architectural polling/file-I/O opportunities to optimize, especially the 10 ms managed adapter loop and Lua/adapter mailbox boundary. LastWarPlugin's mmap status contract is promising only for status publication; its total connected-state cost is unknown.

**Attachment/integration:** LW-Control's method is known and coherent: augment the official Lua package before official launch, run the bridge inside the game runtime, and use an authenticated host route. The main cost is shared mutable package state and the resulting one-real-game ownership restriction. LastWarPlugin advertises broader multi-open/sandbox behavior, but its actual attachment and isolation implementation is still hidden behind core.py.

No connection architecture change should be made from the current competitor evidence. The next decision point is recovery of the legitimate authenticated core followed by an equivalent connected benchmark.
