# LwTool vs LW-Control: Home and game-connection method

Research date: 2026-10-23

Research branch: codex/lastwarplugin-connection-research

LW-Control baseline: main@45e151c8

LwTool production backend SHA-256:
f041b8b38cd71764c2a854a0b16d1038898395ca6903e8cd52a5d7cfdfb0168d

LwTool production UI SHA-256:
b2b08cf2fb607612350a6e6333bedbc2554cb469e465c74c2b000c015a0cf317

Authenticated lwtool.dll SHA-256:
aa5f5156e872d0301aece10d662153914946b935a00e5fcb54c472f194e03831

## Revised owner answer

The earlier broad conclusion that "LW-Control is better" is no longer
supported now that LwTool's authenticated production backend, UI and Route-C
native DLL have been recovered.

The two products use different but serious production architectures:

- LwTool Route-C launches the official launcher suspended, injects a small
  parent bridge into that launcher, observes the launcher's CreateProcess
  calls, authenticates and rebinds a per-launch BOOT package to the real game
  child, and injects/loads its bridge in that child. Commands and results then
  travel through per-instance shared-memory mappings.
- LW-Control prepares an augmented official Lua package before the official
  launch, runs its Lua bridge inside the game's own Lua/xLua runtime, connects
  an in-game managed adapter to an authenticated host named pipe, and uses
  exact launch/session identity through readiness, recovery and Stop.

Neither design is simply "better."

The clearest current advantages are:

- LwTool has the structurally leaner ordinary command path. Its controller and
  game communicate through shared mappings instead of LW-Control's current
  named-pipe-to-managed-adapter-to-disk-mailbox-to-Lua boundary.
- LW-Control has the stronger normal single-window Stop and cleanup contract.
  It retains exact PID, path, creation time, profile/session and challenge
  identity through termination and restoration. LwTool's normal Home Stop
  still executes an image-wide taskkill for LastWar.exe, even though Route-C
  knew the exact game child during startup.
- LwTool has strong late-connect and false-disconnect recovery. A slow game
  can become connected later without being relaunched, and process disappearance
  is cross-checked with IPC and memory evidence before being accepted as a real
  death.
- LW-Control has a more explicit single Start-completion contract: an exact
  process plus authenticated route plus same-session readiness heartbeat is
  required before the lifecycle reports the instance as connected.

Efficiency cannot be declared from architecture alone. Shared memory does not
automatically make LwTool faster, and a named pipe does not automatically make
LW-Control slower. LwTool polls its result mapping as quickly as every 3-5 ms
and deliberately paces ordinary calls to roughly an 80 ms minimum; it also
reserves about 15 MiB of shared mapping address space per GameClient. LW-Control
currently has a 10 ms managed-adapter loop, a 10 Hz Lua Overview pump and
periodic file publication. Equivalent connected-state CPU, memory, startup and
command round-trip measurements have not been collected.

## Evidence classes

This report distinguishes:

1. Recovered LwTool source facts: behavior directly present in the authenticated
   recovered-core.py / recovered-ui.html with exact hashes and line anchors.
2. Native LwTool facts: behavior tied to the authenticated lwtool.dll hash and
   to specific Route-C exports, RVAs, call VAs and targeted decompilation.
3. LW-Control released source facts: active composition at main@45e151c8.
4. Observed behavior: actions performed during this research pass, such as the
   authenticated DLL download and protected-file verification.
5. Unknowns: behavior that would require a live connected benchmark or further
   runtime evidence.

The decrypted backend, UI, DLL, runtime key, token and raw captures remain in
the ignored artifacts workspace. They are not included in Git.

## Stage-by-stage comparison

| Stage | LwTool active production | LW-Control active production | Supported assessment |
| --- | --- | --- | --- |
| Home Start | Production UI calls py_start_game, which reaches _do_start_game and immediately selects hard-enabled Route-C. | Home routes Start to the retained per-profile lifecycle owner. | Both have a real backend lifecycle, not a UI-only launch button. |
| Game integration | Native Route-C launches official LastWarLauncher.exe suspended, APC-loads a parent bridge, hooks launcher child creation and binds the real LastWar.exe child. | Helper augments the official LWScripts package before official launch; game loads bridge through its Lua/xLua runtime. | Different tradeoffs. LwTool avoids shared installed Lua-package mutation; LW-Control avoids a remote launcher injection step. |
| Initial child identity | Route-C Job Object, expected parent/game paths, BOOT name/nonce/instance, process/thread checks, same-job checks and child state. | Exact selected-install game PID/path/start time plus launch session/challenge and authenticated pipe-client image. | Both have strong startup identity. |
| Command transport | Per-instance shared mappings LwTool_CMD / RESULT / OUT / ERR with callbackId correlation. | Framed authenticated named pipe host↔managed game adapter; sequence-numbered files bridge adapter↔Lua; FEATURE018 also has exact-owner file exchange. | LwTool is structurally simpler for ordinary command transport. Actual speed/CPU remains unmeasured. |
| Native/proxy ready | Route-C BOOT packet starts bound to dummy PID 1; native child bridge authenticates/rebinds it to actual child, signals ready, then game proxy eventually signals .ack. | Helper and game bridge publish exact-session ready/heartbeat after in-game bridge initialization. | Both reject a simple process-name-only "ready." |
| Application connected | Numeric getServerId + valid auth token + game UID verification before connected=true. Slow Lua can be promoted by late-connect watcher for up to 600 s. | Exact PID/path/start + authenticated route + fresh matching heartbeat/ready. | LwTool tolerates late readiness well; LW-Control has a cleaner lifecycle-level exact-session readiness predicate. |
| Single process monitoring | Generic single helpers later use image-name process probes, then watcher cross-checks IPC and process memory before accepting death. | Lifecycle continues to carry exact owner/session process identity. | LW-Control preserves stronger single-session identity after launch. |
| Transport health | Shared-memory pings / getServerId probes; sustained-failure thresholds; callback/result correlation. | Authenticated pipe liveness + heartbeat; same-session pipe reconnect. | Both substantive. |
| Game recovery | 5 s recovery/watch loops, false-positive checks, late-connect, single restart backoff and UI/game disconnect signals. | 2 s recovery monitor; 30 s hang / 60 s disconnect / 180 s login-health thresholds and explicit retry tables. | No blanket winner. Policies differ. |
| Single Home Stop | taskkill /F /IM LastWar.exe /T, then cleanup. | Exact PID/path/start/session Stop; proves process exit before exact package restoration. | LW-Control is clearly safer/more isolated here. |
| Multi-instance isolation | Separate feature-gated LwToolNew path has per-instance generations, suffixed IPC mappings, environment identity and targeted per-instance cleanup. | Real simultaneous owners are intentionally blocked because the current Lua package is shared per Windows user. | LwTool has broader implemented multi-instance machinery, but this is not the normal single Home path and was not live-benchmarked here. |

## 1. LwTool active Home Start

### UI and backend path

The production UI's normal Home Dashboard contains the Start Game button:

- recovered-ui.html:393-413

The normal toggle calls the backend start/stop functions:

- recovered-ui.html:3764-3771

Backend start:

- recovered-core.py:30197-30259

The asynchronous launch reaches _do_start_game:

- recovered-core.py:30252-30320

Route-C is not an optional dormant experiment. The current source hard-enables
it:

- recovered-core.py:274-280

The normal launch reaches _route_c_start_single and returns there before the
retained older libzstd/folder-swap implementation:

- recovered-core.py:30564-30633

This is the key active-composition boundary. Older launch implementations below
that return remain useful historical code but are not credited as production
Home behavior.

### Per-launch BOOT identity

RouteCSession owns one single-window BOOT transaction:

- recovered-core.py:460-531

It creates:

- a random 16-byte nonce;
- a random 32-byte MAC key;
- a BOOT mapping with a unique random name;
- a .key mapping containing the MAC key;
- a .mk mapping containing the authenticated master key;
- a .ready event;
- a .ack event.

The initial packet is deliberately packed with PID 1:

- recovered-core.py:488-491

That dummy PID is an anti-stale staging mechanism. The current production path
does not call Python publish() to replace it. The native launcher bridge must
authenticate the packet and rebind it to the actual LastWar child:

- recovered-core.py:502-515

The Route-C environment contract includes:

- LW_IPC_ROUTE=c;
- LWINSTANCE;
- LW_ROUTE_C_BOOT_NAME;
- LW_ROUTE_C_NONCE;
- LWTOOL_MK_NAME.

Source:

- recovered-core.py:496-500

### Production native DLL acquisition

The active single start verifies/downloads lwtool.dll:

- recovered-core.py:30466-30494

It checks /api/lwtool-hash and obtains the current protected DLL through the
normal authenticated /api/download/lwtool endpoint when required.

This research reproduced that normal download without launching the game.

Observed protected response:

- bytes: 4,784,720;
- SHA-256: 9d5a8d6dd6fab1cf636c446050a348b5da09c4ff76b83f35552fc221ccb16d5f;
- protected format: DLLENC1.

After local authenticated decode, the DLL's own protected header, expected
plaintext length/hash and MZ signature verified:

- plaintext bytes: 4,784,640;
- SHA-256: aa5f5156e872d0301aece10d662153914946b935a00e5fcb54c472f194e03831.

No token/key values are retained in this report.

## 2. LwTool native Route-C launch and attachment

### RcOrchestrateSingle

The Python active path calls:

- recovered-core.py:30497-30534
- lwtool.dll export RcOrchestrateSingle, RVA 0xA5E20

The input structure includes exact:

- official launcher path;
- game executable path;
- game root;
- DLL path;
- BOOT mapping;
- nonce;
- random Route-C state mapping;
- instance index;
- timeout.

The targeted native decompile proves that normal single Home:

1. builds the Route-C environment;
2. creates a Route-C state mapping;
3. creates a Windows Job Object;
4. creates the official launcher suspended with CreateProcessW;
5. assigns it to the Job Object;
6. queues the parent bridge into that suspended launcher;
7. resumes the launcher;
8. waits for the real game child to be identified and accepted.

The active single CreateProcessW call is at VA 0x1800A6510.

When timeoutMs is zero, the internal native child/state waiter defaults to
120,000 ms.

Failure paths close/terminate the owned launcher/job rather than simply
returning an unowned process.

### Parent launcher injection

RcQueueParentBridge is export RVA 0xA6A20.

It loads the same DLL locally and resolves RcParentInitialize. It validates the
x64 image and converts the export to an RVA. The active launcher injection
helper then:

- VirtualAllocEx at 0x1800A21F6 and 0x1800A2218;
- WriteProcessMemory at 0x1800A22E5 and 0x1800A231D;
- VirtualProtectEx at 0x1800A2351;
- FlushInstructionCache at 0x1800A2367;
- QueueUserAPC at 0x1800A237B.

This is specifically the normal single Route-C parent bridge. A separate
feature-gated multi orchestrator contains a CreateRemoteThread path; that
mechanism is not attributed to normal Home.

The practical meaning is important: LwTool does not merely "watch the game
through mmap." Shared memory is part of the authenticated BOOT and command
protocol, but the active normal launch also performs native in-process bridge
loading through the suspended official launcher.

### Launcher child hook

RcParentInitialize is export RVA 0xA99E0.

Targeted decompilation shows it:

- opens and validates LW_ROUTE_C_STATE_NAME;
- validates Route-C state magic/version;
- reads expected LW_ROUTE_C_GAME_EXE;
- reads expected LW_ROUTE_C_PARENT_EXE;
- uses LWINSTANCE;
- installs interception around kernel32 CreateProcessW / CreateProcessA.

This explains how the injected official launcher bridge observes the real
LastWar.exe creation rather than racing a global process-name scan.

### Native child selection

The relevant internal native state/child waiter begins at VA 0x1800A2500.

It is significantly stronger than a process-name test:

- CreateFileMappingW: 0x1800A266A;
- MapViewOfFile: 0x1800A26A1;
- Sleep(50 ms): 0x1800A2731;
- OpenProcess: 0x1800A27C5 and subsequent sites;
- OpenThread: 0x1800A27DD;
- GetProcessIdOfThread: 0x1800A2809;
- IsProcessInJob: 0x1800A2825 and subsequent sites;
- ResumeThread: 0x1800A29CC;
- EnumWindows: 0x1800A2C6A;
- ReadProcessMemory: 0x1800A301E.

It checks that the process/thread pair agrees, that the relevant processes
belong to the same Job Object, that executable/process identity matches, and
that the selected child exposes the expected Route-C state. It also reads a
small 0x48-byte state block from the selected game before accepting the native
stage.

### Game-child authenticated bind

RcBindAndQueueFromEnvironment is export RVA 0xA3EC0.

Targeted decompilation shows that the target child must present the expected
Route-C environment. The export reads:

- LW_IPC_ROUTE;
- LW_ROUTE_C_BOOT_NAME;
- LW_ROUTE_C_NONCE;
- LWINSTANCE.

It opens the BOOT and key mappings and validates the LWBIPC1 header, version,
header length, instance/nonce-bound data and key material. It verifies the
staged LuaS v2 payload, rewrites/rebinds the BOOT state to the real child,
signals the ready event, and then queues lwtool.dll into the selected child.

This closes the previously unknown native attachment chain.

## 3. Commands and results

### LwTool

GameClient:

- recovered-core.py:2134-2230

Mapping size:

- recovered-core.py:1022

Each client uses:

- LwTool_CMD: 5 MiB;
- LwTool_RESULT: 5 MiB;
- LwTool_OUT: 5 MiB;
- LwTool_ERR: 16 KiB.

Normal single production uses unsuffixed mappings. Per-instance clients use an
instance suffix:

- recovered-core.py:2141-2150

send() serializes commands with a per-client lock and assigns a monotonically
increasing callbackId:

- recovered-core.py:2169-2185

The response is accepted only when callbackId matches the pending command:

- recovered-core.py:2190-2204

Polling cadence:

- fast mode: 3 ms;
- normal mode first 100 ms: 5 ms;
- normal mode after that: 20 ms.

Source:

- recovered-core.py:2204

After a successful ordinary call, the source deliberately sleeps:

- recovered-core.py:2213-2215

The formula makes the ordinary call cadence roughly at least 80 ms total, with
at least a 30 ms post-result delay.

The mmap write/read helpers use a length-prefixed UTF-8 JSON frame:

- recovered-core.py:2047-2095

The result reader specifically avoids clearing a possibly half-published JSON
result immediately; a stable malformed length must remain malformed for 0.3 s
before it is discarded. This is a meaningful reliability measure for a
polling shared-memory transport.

Scoped automation also performs a final generation/UID/job authority check
immediately before writing IPC bytes:

- recovered-core.py:17655-17667

### LW-Control

LW-Control's active transport is hybrid:

1. controller ↔ in-game managed adapter: authenticated framed Windows named
   pipe;
2. managed adapter ↔ game Lua: sequence-numbered inbound/outbound JSON mailbox
   files.

Source:

- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:136-233
- tools/current_overview_bridge.lua:3608-3793

The host named-pipe handshake validates the OS-reported client PID, expected
build, actual client executable path, profile/instance and launch token:

- src/LWBridge.Desktop/LWBridgeControlPipeIsolatedHandshake.cs:83-150

LW-Control therefore has a stronger authenticated host transport boundary, but
its Lua/adapter boundary is more mechanically expensive than LwTool's direct
shared mapping.

### Transport assessment

LwTool has the architectural advantage for ordinary command exchange:

- no disk mailbox hop between controller and game Lua/proxy;
- one correlated request/result channel;
- per-instance mappings already exist.

But this is not a benchmark result. LwTool polls those mappings aggressively,
uses one lock/one outstanding command per client, and deliberately adds normal
call pacing.

LW-Control has more file I/O and wakeups, but its pipe reader/writer can block
on kernel I/O and it has explicit transport queue/backpressure limits.

The supported conclusion is "LwTool's command path is structurally leaner,"
not "LwTool is measured faster."

## 4. Connection readiness

### LwTool has two readiness levels

The active Route-C start call:

- recovered-core.py:30422-30556

first waits for native orchestration and then waits up to 60 s for the BOOT
.ack:

- recovered-core.py:30538-30556

That ACK proves that the game proxy has received/accepted the BOOT payload.
It is not equivalent to the Home application being fully connected.

The application connected state later requires working game IPC and account
identity.

The late-connect watcher documents the final test:

- recovered-core.py:31349-31398

It requires:

1. game is still running;
2. getServerId succeeds;
3. returned server ID is numeric rather than a pre-Lua stub object;
4. an authenticated tool token exists;
5. _verify_game_uid() succeeds.

Only then:

- connected=True;
- InitConfig is pushed;
- AFK/chat loops can start.

If the game takes longer than the normal boot flow, this watcher tries every
five seconds for as long as 600 seconds. This is a useful recovery property:
a slow Lua startup need not force a game restart.

### LW-Control

LW-Control's lifecycle Start is not considered ready from a process alone.

Helper readiness:

- tools/run_overview_bridge.py:1250-1301

requires the exact:

- bridge version;
- profile;
- session;
- challenge;
- game PID;
- ready flag/message;
- fresh ready timestamp.

Production lifecycle readiness additionally requires exact process identity,
authenticated route and fresh matching heartbeat:

- src/LWBridge.Desktop/OverviewLifecycleService.cs:184-209
- src/LWBridge.Desktop/OverviewLifecycleService.cs:1861-1899

### Readiness assessment

LW-Control's lifecycle has the clearer one-point "this exact owned session is
ready" predicate.

LwTool has a two-level design:

- native proxy/BOOT ACK;
- final game/account connected state.

Its late-connect watcher gives it better tolerance for a proxy that is loaded
but whose Lua/game API becomes ready late.

A useful improvement for LwTool is to expose those two states explicitly in
the Home status model rather than letting "starting" cover several distinct
native/game readiness phases.

## 5. Process identity and isolation

### LwTool startup identity is strong

At launch, LwTool has:

- exact launcher path;
- exact game executable path;
- Job Object membership;
- suspended launcher process/thread handles;
- Route-C state mapping;
- random BOOT name;
- random nonce;
- MAC key;
- master-key mapping;
- LWINSTANCE;
- launcher CreateProcess hook;
- native child process/thread/job validation.

The native startup path therefore does not deserve the earlier implication
that LwTool merely looks for any LastWar.exe.

### LwTool normal single monitoring loses some of that precision

After startup, generic single helpers still include global process-name probes:

- recovered-core.py:10642-10699

game_running() asks whether any LastWar.exe is alive. _get_game_pid() parses
the first LastWar.exe returned by tasklist.

The game watcher compensates for false negatives by requiring multiple misses
and testing IPC/memory before declaring the game dead:

- recovered-core.py:24258-24387

but this is still weaker identity continuity than retaining the exact
Route-C child PID/creation identity through the whole single-session lifecycle.

### LW-Control

LW-Control creates a random session and random challenge at Start:

- src/LWBridge.Desktop/OverviewLifecycleService.cs:810-864

The helper only accepts exactly one game from the selected installation:

- tools/run_overview_bridge.py:948-977

The authenticated pipe verifies the actual OS pipe-client PID and actual
client image:

- src/LWBridge.Desktop/LWBridgeControlPipeIsolatedHandshake.cs:83-150

That exact session/process identity is retained through Stop.

### Multi-instance note

LwTool's feature-gated multi code is substantially more isolated than its
normal single helpers:

- recovered-core.py:1215-1264
- recovered-core.py:2829-2874
- recovered-core.py:4497-4537

It uses per-instance generations, suffixed IPC and process environment
identity, and it has a targeted instance cleanup path.

This is real implementation evidence, but it is not the normal single Home
button and it was not live-tested in this pass.

LW-Control currently blocks simultaneous real owners because its installed Lua
package is shared per Windows user:

- src/LWBridge.Desktop/OverviewLifecycleService.cs:840-849

That is a meaningful product limitation if real multi-game use is required.

## 6. Disconnect, reconnection and recovery

### LwTool

The recovered backend has several independent recovery layers.

Automatic reconnect monitor:

- recovered-core.py:24074-24257

runs on a five-second cadence and probes IPC only when the game/controller is
in a suitable state. It distinguishes a connection that had previously worked
from a proxy that never became alive and uses sustained failure rather than a
single missing reply.

The game watcher:

- recovered-core.py:24258-24387

does not trust process enumeration alone. After three process-missing
observations it probes getServerId and samples game memory. If IPC is still
alive or game memory still proves the game exists, it records a false positive
and keeps the session.

Only after those checks fail does it mark connected false and perform
game-close cleanup.

The connection also has a five-second core heartbeat:

- recovered-core.py:24389 onward

Single restart clears IPC buffers:

- recovered-core.py:11666-11677

and re-enters the normal start path with bounded restart delays.

Disconnect signals are deduplicated before recovery logic:

- recovered-core.py:9936-10042

### LW-Control

LW-Control separates:

- same-game transport reconnect;
- full game recovery.

The game-side pipe reconnect keeps the same session identity and retries once
per second up to 90 attempts after a previously authenticated session or
initial host-unavailable state:

- tools/current_overview_bridge.lua:349-455

The native recovery monitor runs every two seconds:

- src/LWBridge.Desktop/OverviewLifecycleRecovery.cs:1-175

Policy:

- hang: 30 s;
- disconnect: 60 s;
- login/health unavailable: 180 s;
- stable verification after recovery: 15 s;
- normal retry: 15, 30, 60, 120, 300 s;
- maintenance retry: 120, 300, 600 s.

Source:

- src/LWBridge.Desktop/OverviewRecoveryPolicy.cs:8-36

### Recovery assessment

Both are mature enough that the old "LW-Control has recovery; competitor
unknown" conclusion is obsolete.

LwTool's strongest behavior is false-positive avoidance and late self-healing.
LW-Control's strongest behavior is explicit separation between transport loss
and whole-game recovery while preserving exact session identity.

There is no defensible reliability winner from source alone.

## 7. Stop and cleanup

### LwTool normal single Home

This is the largest concrete weakness found in the active single path.

Normal UI Stop calls py_stop_game:

- recovered-ui.html:3764-3771
- recovered-core.py:31617-31625

The command is:

    taskkill /F /IM LastWar.exe /T

It is image-wide.

That means the normal Home Stop does not use the exact child PID that
RcOrchestrateSingle already recovered. If another LastWar.exe exists, this
command is capable of targeting it as well.

The wrapper _run_game_kill records bookkeeping but executes the supplied
arguments unchanged, so this is not merely a misleading string in dormant
code.

The multi-window code has a much better per-instance clean-close mechanism,
but normal single Home does not use that path.

### LW-Control

LW-Control Stop is exact-owner by construction:

- tools/run_overview_bridge.py:1580-1693

It validates:

- profile;
- session;
- PID;
- game path;
- process creation time;
- current selected-install process identity.

It then terminates the exact owned process, independently establishes exit,
checks that no process from the selected installation survives, and only then
restores the exact preserved official Lua package.

Runtime cleanup is also session/challenge scoped.

### Stop assessment

LW-Control is materially better here.

This is not a theoretical style preference. It is a concrete isolation
difference in active production code.

The highest-value LwTool reliability improvement is to retain the Route-C
child PID plus a creation/process identity token and use that exact identity
for:

- Home Stop;
- health monitoring;
- restart admission;
- cleanup.

The image-wide taskkill should be removed from normal Home.

## 8. Startup speed

No equivalent live timing was performed.

Source suggests different cost centers.

### LwTool startup work

LwTool:

- may download/update the authenticated LWL/DLL artifacts;
- creates BOOT/key/master-key mappings;
- creates the official launcher suspended;
- injects the parent bridge;
- installs child creation hooks;
- waits for native child identity/state;
- binds the BOOT packet to the real child;
- waits for proxy ACK;
- later waits for actual game/Lua/account readiness.

It avoids rebuilding/replacing an installed Lua package for each normal launch.

### LW-Control startup work

LW-Control:

- creates session/challenge/pipe binding;
- waits for official-client update stability;
- builds/verifies the candidate Lua package;
- installs it;
- launches the official launcher;
- discovers the selected installation's game process;
- registers the exact process;
- waits for game-side bridge readiness and authenticated pipe route.

### Startup assessment

It is plausible that LwTool can avoid some disk/package preparation that
LW-Control performs. It is equally plausible that native injection/hooking,
download checks or different game initialization points dominate startup.

No speed claim is supported without measuring from the same user Start action
to the same meaningful connected condition.

## 9. Command latency and CPU

### LwTool source facts

Result polling:

- 3 ms fast;
- 5 ms normal for the first 100 ms;
- 20 ms normal afterward.

Source:

- recovered-core.py:2169-2215

Normal successful commands are intentionally paced to about an 80 ms minimum.

This means the shared-memory design is not automatically "near-zero latency."

The feature-gated multi process discovery path is also an example of how
shared-memory/native designs can still burn CPU if surrounding discovery loops
are aggressive. The source comments report that a pre-cache implementation
measured roughly 23% of one core with three windows; a 3 s cache was added:

- recovered-core.py:2829-2839

That is a historical measurement recorded by the source author, not a fresh
measurement from this investigation.

### LW-Control source facts

Current cadences:

- managed adapter idle loop: 10 ms sleep when idle;
- Lua Overview pump: max 10 Hz;
- game heartbeat file: minimum 0.75 s spacing;
- pipe heartbeat: 5 s;
- helper game/readiness polling: 250 ms;
- recovery monitor: 2 s;
- FEATURE018 result polling: 50 ms while a request is outstanding.

Source:

- src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:184-233
- tools/current_overview_bridge.lua:3839-3872
- tools/current_overview_bridge.lua:4102-4121
- tools/run_overview_bridge.py:948-977
- tools/run_overview_bridge.py:1250-1301

### CPU/latency assessment

The strongest source-backed optimization opportunity is LW-Control's
Lua↔managed disk-mailbox boundary and 10 ms adapter polling.

LwTool's strongest optimization opportunity is to replace high-frequency
RESULT polling with signaling/sequence notification and to revisit the
intentional ordinary-call latency floor where it is no longer needed.

The source does not establish which whole product uses less CPU.

## 10. Memory

### LwTool

Each GameClient reserves:

- CMD 5 MiB;
- RESULT 5 MiB;
- OUT 5 MiB;
- ERR 16 KiB.

That is about 15.016 MiB of shared mapping capacity per client.

This is not equivalent to 15 MiB resident RAM or 15 MiB private committed RAM.
Shared mappings, touched pages and Windows accounting must be measured.

LwTool also includes a low-free-memory working-set trim:

- constants: recovered-core.py:41173-41178;
- implementation: recovered-core.py:41306-41368.

The source comment records a development observation of:

- Working Set 1031 MiB -> 117 MiB immediately;
- 225 MiB after 25 s;
- Commit stayed at 1892 MiB.

That evidence demonstrates page residency reduction, not lower committed
memory. It can trade working-set size for future page-ins.

### LW-Control

No equivalent connected-idle process-tree memory measurement was collected in
this pass. Queue capacity constants and analysis-tool memory are not product
memory and must not be compared with the LwTool numbers above.

### Memory assessment

No winner.

## Supported improvements

### LwTool

1. Preserve the exact Route-C child identity after startup.

   RcOrchestrateSingle already returns the game child PID and native state
   already proves job/path/thread identity. Store that identity in the normal
   single lifecycle instead of later falling back to global image-name probes.

2. Replace normal Home image-wide Stop.

   Stop should terminate only the exact Route-C-owned child, preferably with
   creation identity / handle / generation validation. If the original Job
   Object is appropriate to retain, a session-owned job can also provide
   deterministic tree cleanup.

3. Signal shared-memory results instead of polling them aggressively.

   Keep the existing mmap payload format if it is useful, but pair it with an
   Event/semaphore/sequence wake mechanism so the controller sleeps until a new
   result is available.

4. Re-evaluate the normal 80 ms command pacing.

   Measure why the post-success delay exists, retain it only for operations
   that need it, and avoid applying a global floor to read-only/status calls if
   no game-side safety requirement depends on it.

5. Measure whether 5 MiB x 3 mappings are necessary.

   If production payloads are far smaller, a bounded ring/slot protocol could
   reduce address-space/page pressure while preserving overflow protection.

6. Expose native-ready and game-ready separately.

   Route-C BOOT ACK, Lua/API-ready, account-verified and connected are distinct
   facts. Reporting them separately would make support/recovery decisions more
   precise.

### LW-Control

1. Remove the adapter↔Lua disk mailbox hop if current xLua allows a safe direct
   managed queue/callback/shared-memory bridge.

   Preserve the current host pipe authentication, session identity and command
   correlation.

2. Replace the managed adapter's 10 ms idle polling with event/blocking waits
   where possible.

3. Reduce repeated lease-file parsing in the adapter.

   Keep the fail-closed lease freshness rule, but avoid opening/parsing the
   file on every idle loop if an event or bounded periodic revalidation can
   provide the same safety.

4. Consider a LwTool-style late-connect grace.

   If the exact owned game and session remain valid but Lua readiness is late,
   wait/reprobe the same exact session before escalating to a whole-game
   recovery. Do not weaken exact-session ownership to obtain this behavior.

5. Keep the existing exact Stop/restore semantics.

   LwTool's normal image-wide Stop is specifically not a behavior to copy.

## Fair benchmark required

The next performance comparison should use one test definition for both
products.

For each product:

1. Same machine, Last War build and installation.
2. Same account/game idle state.
3. Cold and warm launches measured separately.
4. Start timestamp at accepted Home Start action.
5. Connected timestamp only when the product's final game/account-ready
   predicate is true.
6. Record:
   - launcher creation;
   - game child creation;
   - proxy/bridge ready;
   - final connected.
7. After final connected, measure at least 60 s idle:
   - controller CPU time;
   - helper/adapter CPU time;
   - game CPU time separately;
   - private bytes;
   - working set;
   - mapping/file I/O rates.
8. Measure a representative read-only command round-trip P50/P95/P99 without
   gameplay mutation.
9. Exercise a controlled controller/transport loss without changing the game
   account and measure recovery.
10. Exercise owned game exit and verify no stale successor is stopped or
    adopted.
11. Verify Stop leaves zero task-owned helpers and restores any modified
    installation files exactly.

Only those measurements can answer "faster," "lower latency," "less CPU" and
"less RAM."

## Remaining evidence gaps

- No live Last War launch was performed during this pass.
- No start-to-connected timing exists under equivalent conditions.
- No connected-idle CPU/private-bytes/working-set comparison exists.
- No command RTT distribution exists.
- No live transport-loss/recovery timing comparison exists.
- No live update/launcher compatibility test was performed for the recovered
  Route-C DLL.
- The exact instruction-level implementation of every CreateProcess hook was
  not decoded. The parent hook registration, game-child bind export, active
  injection helper and child validation path are recovered.
- Actual resident/committed cost of LwTool's shared mappings is unknown.
- Feature-gated LwTool multi-window Route-C is source-backed but was not
  benchmarked and is not treated as normal Home.
- Normal single LwTool image-wide Stop semantics are proven by source, but a
  two-game destructive runtime demonstration was intentionally not performed.

## Final revised verdict

### Launch/attachment

No overall winner.

LwTool Route-C is now proven to be a deliberate launcher-mediated injection
architecture with strong native child identity. LW-Control is a deliberate
pre-launch in-game Lua integration architecture with exact restoration.

### Commands/results

Architectural edge: LwTool.

Its ordinary shared-memory path avoids LW-Control's current disk mailbox hop.
This is an implementation-structure advantage, not measured end-to-end speed.

### Readiness

Different strengths.

LW-Control has the clearer exact-session lifecycle-ready predicate. LwTool has
strong account-verified final readiness plus a useful late-connect recovery
window.

### Correct process/session isolation

Startup: both strong.

Normal single steady lifecycle/Stop: LW-Control stronger.

LwTool's native Route-C identifies the right child well, but the Python single
lifecycle later gives up some of that precision, most visibly in image-wide
Stop.

### Disconnect/reconnect

No supported blanket winner.

Both have substantive false-positive protection and recovery logic. Their
thresholds and escalation strategies differ.

### Stop/cleanup

LW-Control wins clearly for normal single Home.

### Startup/latency/CPU/memory

Unknown until equivalent live measurements are performed.

The correct current answer to the owner's original question is therefore:

**LwTool is not simply better or worse. Its active Route-C is more capable and
more carefully engineered than the earlier unauthenticated evidence showed.
Its direct shared-memory command channel is a useful design reference for
LW-Control. LW-Control should retain its stronger exact-owner Stop and cleanup
model. Performance claims require a real matched benchmark before either
method can be called faster or more efficient.**
