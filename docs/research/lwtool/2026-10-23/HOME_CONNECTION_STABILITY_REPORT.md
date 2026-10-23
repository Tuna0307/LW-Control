# Home connection stability report — 2026-10-23

## Executive result

The owner's reported *visible* intermittent disconnect was **not reproduced
naturally** in the initial 15-minute baseline or the final 30-minute candidate
soak.  The candidate soak did naturally catch one sub-second `heartbeat.json`
read/publication transient at 29m53s while the exact game, authenticated route,
direct transport and game/server state all remained healthy; it recovered on the
next one-second sample and was too short to establish the reported user-visible
disconnect.  The investigation nevertheless separated two real local failure
classes that can make Home look offline while Last War itself remains healthy:

1. a short Lua/game-thread heartbeat-publication stall, where the authenticated
   route stays live but the strict five-second Home heartbeat becomes stale; and
2. a true local named-pipe loss, where the managed adapter observes the broken
   pipe before the host monitor loses its authenticated route, while the exact
   game process and game/server heartbeat remain healthy.

LW-Control already had a generation-safe same-session authentication path for
ordinary transport reconnect.  This assignment keeps that stronger ownership
model and adds two LwTool-inspired improvements of our own:

- **corroborated presentation state**: short heartbeat-only stalls are shown as
  checking/reconnecting when exact process identity and the authenticated route
  still corroborate the owner, without weakening the strict command gate; and
- **bounded direct adapter↔Lua queues**: authenticated pipe frames no longer need
  numbered disk mailbox files when the current xLua/managed API supports the
  direct seam.  Lua is still called only on the game thread and the Lua-authored
  heartbeat remains authoritative.

The direct path was live throughout the final candidate soak and the candidate
also completed same-game route loss, scheduling stall, process-exit recovery,
Stop-during-reconnect, delayed readiness, three warm starts and three cold host
starts.  Packaging is qualified separately from the implementation commit.

## 1. What failed first?

### Natural observation

No connection layer failed during the 900-second baseline: 901 samples retained
one exact game owner, one authenticated route, healthy game/server state and a
fresh heartbeat.  There were no sampled login/network error markers.

The 1800-second candidate soak produced 1801 samples with the direct path active
in all 1801.  One sample at 1793.234 s could not read the Lua heartbeat.  On the
next sample the heartbeat was healthy again; the exact game PID did not change,
the authenticated route never sampled false, the monitor never sampled an
unhealthy game/server state, and there were zero matching Player.log
login/network markers.  This is a naturally observed publication/read transient,
but not evidence that the owner's longer visible disconnect starts at the same
layer.

### Controlled local route failure

For the failure mode the owner described most closely — LW-Control transport
goes away while the game keeps running — the retained-registration route fault
was repeated five times.  The first sampled break in every cycle was the
**managed adapter's pipe state** (`PeekNamedPipe` class), followed by loss of the
authenticated host route on the next lifecycle observation.  Throughout this
interval the same game process remained alive and its game/account heartbeat
continued to report logged-in/server-connected health.

All five cycles reauthenticated the same exact session without relaunching the
game.  Adapter-fault→reconnected durations were 1.231–1.273 s, median 1.249 s.

### Controlled heartbeat/game-thread delay

A separate publication stall reached 13.316 s heartbeat age while the adapter,
pipe route and game remained healthy.  An exact-handle 12-second whole-process
suspension independently reached 12.741 s heartbeat age without dropping the
pipe route.  In both cases the first failed predicate was `lua_heartbeat`, not
the game process or game-server state.

This shows why a single `Disconnected` label was too coarse: route loss and
heartbeat-only degradation are different failures with different recovery needs.

## 2. Did Last War remain healthy?

**Yes for both controlled false-disconnect classes.**

During retained-registration pipe faults the exact game PID stayed unchanged,
the heartbeat stayed fresh, `gameReady/loggedIn/connected` remained true and no
matching `connect to server failed`, `E005`, `Loading error : E109` or
`OnLoginError` marker appeared.  During the 12-second scheduling stall the route
itself also stayed authenticated; after resume the same process immediately
returned to full health.

This is deliberately narrower than claiming the same for the owner's unobserved
natural event.

## 3. LwTool mechanisms and decisions

Recovered authenticated LwTool uses a Route-C design that retains an existing
game, later promotes a slow-but-valid connection, and self-heals local IPC before
escalating to a game restart.  Its transport also avoids our numbered disk
mailbox boundary.  These are useful mechanisms; its broader image-wide Stop and
any weaker reply-correlation behavior are not.

LW-Control decisions:

| Mechanism | Decision | Reason |
| --- | --- | --- |
| Retain same healthy game on local transport loss | **Retain / qualify** | Existing host registry already preserves the claimed exact-session registration on ordinary EOF; live same-game reconnect succeeded five times. |
| Corroborate a disconnect | **Adopt** | Live heartbeat-only stalls proved strict heartbeat freshness can fail while exact process + authenticated route remain healthy. |
| Direct/event-signaled adapter↔Lua exchange | **Adopt with fallback** | Current xLua can invoke managed `Func<string,string>` delegates on the game thread.  Bounded generation-scoped queues remove numbered mailbox publication from the active path while preserving authority and correlation. |
| Adapter-generated liveness heartbeat | **Reject** | It would allow a managed background thread to make the route look healthy while Lua/game-thread work is stalled.  The heartbeat stays Lua-authored. |
| ~600 s late-ready policy | **Do not adopt as a blind timeout extension** | A live 20 s pre-ready suspension retained the exact reported PID/path/start/session and became healthy after resume. Current ordinary starts still complete well inside the existing 90 s bridge-ready window, so no 600 s global timeout is justified. |
| Image-wide Stop | **Reject** | Violates exact ownership and is unnecessary. |

### Competitor live-idle prerequisite

The required serial LwTool idle comparison cannot be made equivalent without
violating the task's prerequisite.  Recovered production UI states AFK/chat are
auto-started on connect and the old UI toggles were removed.  Chat has a free
supported stop call, but AFK Stop is Premium-gated and AFK is started again after
late-connect/self-heal.  On a session where that paid gate cannot legitimately
be used, there is no supported way to prove AFK stays disabled.  No license gate
was bypassed; competitor comparison therefore remains source/recovered-runtime
evidence plus LW-Control live qualification, as the work item explicitly allows.

## 4. Production changes

### Presentation-only disconnect corroboration

`OverviewLifecycleService` keeps its strict five-second heartbeat freshness for
`IsReady` and command authority.  `IsPresentationReady` adds only a 15-second UI
grace and rechecks the exact PID/path/start identity plus authenticated route on
every observation.  A readable heartbeat with wrong identity or `ready=false`
fails immediately.  `LWBridgeBackend` publishes this independently as
`presentationOnline`; Home uses it only to render the existing checking state.

Important anchors at this revision:

- `src/LWBridge.Desktop/OverviewLifecycleService.cs:66-67,191-220`
- `src/LWBridge.Desktop/LWBridgeBackend.cs:785-795`
- `src/LWBridge.Desktop/OverviewLifecycleProfileInstanceStatus.cs:69`
- `src/LWBridge.UI-0.3.17/src/App.jsx:1037-1041`
- `src/LWBridge.UI-0.3.17/src/HomePage.jsx:112`

Strict calls still fail closed when `xluaOnline`/`IsReady` is false.  Tests also
exercise a six-second heartbeat publication outage where presentation remains
online/checking but a command is still rejected as `GAME_DISCONNECTED`.

### Direct bounded in-game exchange

`PipeClientAdapter` exposes xLua-callable delegates for enable/state/dequeue/
publish/retire.  The managed named-pipe worker owns pipe I/O and bounded queues;
Lua calls the delegates only inside its already proven game-thread pump.  Limits
remain 4096 frames, 8 MiB max frame, 64 MiB host→Lua and 32 MiB Lua→host.  Queue
overflow fails the current generation rather than dropping data.  Retirement and
publication are generation-scoped so an old worker cannot corrupt a successor.

Important anchors:

- `src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:19-30,76-172,216-252`
- `tools/current_overview_bridge.lua:320-328,342-356,490-496`
- `tools/current_overview_bridge.lua:3719-3732,3783-3796,3828-3847`

If all direct delegates are not available, the established numbered mailbox path
remains the fallback.  In the first real candidate game the diagnostic reports
`adapterReadMode=direct`, `directCapable=true`, `directActive=true`; no numbered
pipe inbound/outbound files are present.

### Same-session fault hook and diagnostics

The isolated test hook can now close a real authenticated route **without**
unregistering its claimed registration, matching ordinary transport EOF more
closely.  It remains guarded by an isolated application root plus opt-in
environment switch and exact profile/session/PID ownership.  The recovery trace
and external sampler persist only sanitized state and error classes.

## 5. Measured baseline and candidate

### Baseline

- host start→final game/account healthy: 70.271 s (one recorded baseline start);
- 15-minute natural soak: 901/901 healthy; no natural disconnect;
- heartbeat age p95 0.798 s, max 0.854 s;
- five same-game injected route recoveries: median 1.249 s, max 1.273 s;
- exact-owned process-exit recovery: 26.031 s to fully healthy successor;
- Stop during a reconnect window: no successor, route count zero, desired-running
  false, exact script restoration.

### Candidate

- cold host start→final game/account healthy:
  `62.295 / 32.302 / 33.594 s`;
- three same-host warm Start→healthy runs:
  `24.937 / 24.516 / 25.656 s` (median `24.937 s`);
- 30-minute connected soak: 1801 samples, one exact game PID, direct transport
  active/capable for all samples, zero numbered-mailbox backlog, no sampled
  route/game/server failure and zero matching login/network log markers;
- valid-heartbeat age p95 `0.776 s`, p99 `0.820 s`, max `1.127 s`;
- one natural single-sample heartbeat read/publication transient at 1793.234 s,
  recovered by the next sample with route/process/game state still healthy;
- five candidate same-game route drops: managed adapter failed first each time,
  then host route; adapter-fault→reconnect `1.230–1.280 s`, median `1.258 s`;
- exact 12 s candidate pause: `lua_heartbeat` failed first, max observed
  heartbeat age `12.841 s`, authenticated route remained present, no matching
  login/network marker;
- exact game exit recovery: new PID/session/creation identity reached full
  health in `41.717 s`;
- atomic retained-route-drop + production Stop: no successor and byte-exact
  restoration of all three current official script files;
- delayed readiness: exact newly reported game suspended before ready for 20 s,
  resumed, and the same PID/path/start/session reached full health;
- authenticated host-only read-only RTT probe, 300 measured calls after 20
  warmups: p50 `0.192 ms` / p95 `0.313 ms` / p99 `0.659 ms` / max
  `1.105 ms`.

The RTT figure is for the authenticated Windows named-pipe/correlation layer and
is **not** presented as live game-thread RTT.

## 6. Whole-game recovery, Stop and no-replay safety

Baseline exact-handle exit recovery reached a healthy successor in 26.031 s; the
final candidate repeated the same boundary in 41.717 s with both session and
process-creation identity changed.  Candidate atomic Stop during a retained-route
fault exited only the exact owned process, cleared desired-running state, left no
successor and restored the current official scripts exactly.  No image-wide
`taskkill /IM` was introduced.

The direct queue tests cover FIFO, strict IDs, 8 MiB and queue-byte boundaries,
event wake, overflow failure, generation retirement and a real inert named-pipe
direct raw FIFO/no-mailbox exchange.  Live fault tests send no account-changing
gameplay command, so they cannot manufacture a gameplay replay success.

## 7. Remaining uncertainty

The original visible spontaneous disconnect is still unproven.  The 15-minute
baseline did not reproduce it.  The final 30-minute soak naturally observed only
a one-sample heartbeat read/publication transient; because it recovered in under
the sampling interval and never lost the route/game, it is not claimed as the
owner's visible symptom.  An injected local route drop is likewise not evidence
that the natural event starts at the pipe.  A future visible occurrence still
needs its correlated first-failure timeline preserved before its root cause can
be stated.

Final inert qualification after live cleanup passed the full Desktop suite
through `HOME_FEATURE_DELIVERY_CHECKS_OK`, all 49 Lua suites, direct queue plus
real inert named-pipe checks, isolated pipe recovery, full UI checks, release UI
(3 viewport/language variants, 16 conditional states, 41 screenshots, 0 errors),
Home fault-harness checks and package-boundary checks.

No merge to `main` and no release are authorized by this report.  Final handoff
stops at independent lead review.
