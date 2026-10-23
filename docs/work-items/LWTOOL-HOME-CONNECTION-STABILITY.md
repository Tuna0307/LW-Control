# Home connection stability — owner priority, 2026-10-23

## Owner request and launch authority

Owner reports that LW-Control sometimes loses its connection to the game and
then reconnects by itself. The priority is to understand and improve this
behavior using LwTool's recovered production design. General UI/AFK efficiency
work did not resolve this symptom.

Latest owner direction: focus on Home first, improve our game connection using
what can be learned from the competitor, and continue. Owner explicitly said
the worker is free to open and close the game or program, with no restriction
on doing so. This authorizes opening, closing and restarting LW-Control,
LwTool, their relevant launchers and the game as needed for this investigation,
including clean serial comparison runs and packaged/native tests. An absent
existing task-owned game, or normal startup creating an app/game window, is
not a blocker and does not require another confirmation.

Identify exact target processes and use their normal Stop/Close controls and
restoration where possible. An existing target app/game session may be closed
when necessary after identifying it and preserving recovery/restoration state.
Do not stop unrelated applications or perform image-wide cleanup. Preserve
account/gameplay limits, credentials, Chinese game language and exact file
restoration. This permission does not authorize arbitrary gameplay, real-money
spending, account binding changes, licensing bypass or anti-cheat changes.
Background/programmatic inspection is preferred; do not manipulate unrelated
owner windows or treat older launch restrictions as a reason to skip live work.

## Branch and starting evidence

Repository: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control.
Continue codex/lwtool-full-improvement and draft PR #16. Existing implementation
checkpoint: 0cdf4de8daa9d45f8b1db808c0081801044541cf. Preserve its three
optimizations and unrelated/pre-existing .claude/ content. This is the next
priority on that branch, not a new whole-product comparison or a request to
merge/release.

Read this task, AGENTS.md, docs/research/lwtool/2026-10-23/LEAD_REVIEW.md,
CONNECTION_METHOD_REPORT.md, FULL_PRODUCT_METHOD_MATRIX.md, FINAL_QUALIFICATION.md
and artifacts/lwtool-connection-20261023/WORKER_HANDOFF.md. Reuse the recovered
authenticated core/UI/native DLL and previous native decompilation. Do not
repeat the missing-core investigation or unchanged expensive tool setup.

Maintain the released main@45e151c8 and pre-Home-change branch checkpoint as
comparison baselines. Distinguish an existing release symptom from a regression
introduced by this branch. Record exact sources, game build, configuration and
package identity for each observation.

## Required outcome

Deliver an implemented and live-qualified Home connection improvement that
addresses demonstrated loss/recovery weaknesses, with honest limits on what
was reproduced. The final handback must answer:

1. Which connection is failing first, and what evidence shows it?
2. Does the game remain healthy while LW-Control marks it disconnected?
3. Which LwTool mechanism addresses the observed failure or expensive boundary?
4. What changed in LW-Control, and how did stability/recovery/performance change?
5. What remains unproven for the owner's original intermittent symptom?

Do not stop after another source comparison or housekeeping-only optimization.
Do not claim a spontaneous root cause from an artificial fault alone. If the
natural event cannot be reproduced, use bounded fault tests to qualify concrete
recovery changes and state that the natural cause is still unresolved.

## 1. Trace Home end to end and find the first failure

Home includes game-root selection, Start/Close, connected/starting/recovery
status, auto-launch, auto-reconnect, and repair/update/restart. Trace the actual
active path, not only the React page.

Distinguish these layers explicitly:

- UI status/profile generation and native lifecycle state;
- actual game PID/path/start identity and process responsiveness;
- LW-Control host route/authenticated named pipe;
- managed adapter state and host lease admission;
- adapter-to-Lua inbound/outbound mailbox publication and consumption;
- Lua tick/game-thread availability and heartbeat/readiness freshness;
- the game's own server network/login health;
- transport reconnect versus full game recovery/relaunch.

Collect bounded, correlated timestamps/reason codes across those layers during
normal connected operation and each induced failure. Find the earliest broken
predicate. Distinguish a real game/server disconnect, local transport failure,
stale heartbeat, blocked game tick, file contention, scheduling stall, helper
loss, wrong UI state or delayed publication. Record actual host/adapter/Lua
states and elapsed gaps, not merely the final Disconnected indicator.

Add minimal opt-in bounded diagnostics where current evidence is inadequate.
Keep tokens/credentials and account-specific payloads out of logs/Git. Avoid
instrumentation whose disk/CPU cost changes the failure being studied. Existing
logs/retained evidence should be inspected before trying to recreate a crash.

## 2. Use LwTool as a concrete design reference

Trace and, where needed, observe its active Home Route-C path: launcher/child
identity, native ready versus actual Lua/account ready, shared-memory command
exchange, health detection, late-connect, false-disconnect handling and recovery.
Compare mechanisms specifically against our observed failure modes.

Investigate substantial candidates rather than automatically deferring them:

- retained exact-session reattachment while the same game is healthy;
- corroborated disconnect detection and bounded recovery hysteresis;
- late Lua readiness with exact-owner cancellation/Stop/restoration semantics;
- event/signaled or direct in-memory adapter-to-Lua exchange, if it removes a
  demonstrated failure point or cost center;
- lossless bounded queues, sequence/callback correlation, stale-generation
  retirement, timeout handling and reconnect without command replay.

For a direct bridge candidate, inspect current xLua/managed APIs and prove
game-thread ownership with an isolated experiment. Establish producer/consumer,
queue, publication, overflow and shutdown contracts; do not invoke game Lua
from an arbitrary background thread. If this route is genuinely unsuitable,
retain the decisive evidence and implement supported alternative fixes for
the actual connection fault. A broad statement that it needs more proof is
not a completed feasibility investigation.

Use our own maintainable implementation and current supported game interfaces.
Keep exact ownership, authenticated command authority and restoration. Do not
copy LwTool's image-wide Stop, accept unidentified replies as correlated, add
blind sleeps or simply extend heartbeat timeouts to conceal a broken path.
Normal send() return pacing in LwTool is per invocation outside its lock;
mapping capacity is not measured RAM or a command-rate guarantee.

## 3. Run real connected comparisons and focused fault tests

The owner has authorized target application/game opening and closing. Establish
the test session rather than stopping at the previous 'no connected pair'
statement. Use the owner's existing legitimate competitor login. Before an
idle run, disable unintended AFK/chat/other gameplay through supported settings
and verify it stays disabled on connect and recovery. Do not change a license
binding or bypass a paid feature to manufacture an equivalent run. If this
specific prerequisite fails, record exactly why and continue our own live
baseline/candidate qualification plus offline competitor evidence.

Run products serially with exact original-script restoration between methods.
Use comparable game/account idle state, game build and workload. At minimum:

- repeat cold/warm application Start-to-final-connected (at least three valid
  runs of each relevant case, preserving failures as well as successes);
- capture native/bridge ready separately from final game/account connected;
- measure connected-idle CPU/private bytes/working set and relevant I/O for at
  least 60 seconds, separating game/controller/helpers;
- compare an equivalent read-only command RTT distribution when supported;
- exercise at least five local transport-loss/reconnect cycles while the
  exact same game stays alive; verify stable owner and no command replay;
- exercise exact owned game exit/recovery, Stop during reconnect/late readiness,
  repeated Start/Stop and relevant stale-worker/successor failure cases;
- test brief game-thread/host scheduling or publication delays in an isolated,
  reversible way that does not alter anti-cheat or global networking;
- observe at least 15 minutes of ordinary baseline connection and 30 minutes
  on the final candidate, with periodic jobs, resource growth and every state
  transition recorded. Extend only when observed evidence warrants it.

An induced route drop is not evidence that the natural symptom has that cause.
Avoid disruptive whole-machine network changes. No account-changing gameplay
is needed for the connection workload; main remains read-only. Use exact UID
admission before any separately authorized account action and preserve current
owner directions if such a feature becomes relevant.

## 4. Implement, qualify and finish autonomously

Continue through diagnosis, prototype, implementation, fix/retest and final
packaging without routine handoffs for another prompt. Prioritize stable
communication with a healthy running game, accurate state, fast same-session
reattachment and graceful escalation when the game truly fails.

Preserve all unrelated accepted tabs, the existing improvements, source-server
ownership, automation no-replay/Stop semantics and supported localization.
Update Home status text only where it clarifies an established distinction
(for example reconnecting locally versus game login/network recovery).

Run the repository checks appropriate to changed paths, meaningful new failure
tests, the existing lifecycle/pipe recovery suites and affected Lua/UI checks.
Then verify the final source-pinned Windows package against the same live Home
scenarios, including exact cleanup and script restoration. Keep private
competitor payloads/credentials and research assets out of the package.

One primary agent owns integration and one controller owns live sessions and
installed scripts. Independent analysis/review subtasks may run in parallel.
Respect actual tool denials and explicit stops. A specific blocked dependency
does not stop other useful work; record it precisely, continue alternatives,
and ask only when genuinely required new information/authority is missing.

Keep a durable connection investigation ledger and sanitized evidence in
docs/research/lwtool/2026-10-23/. Push coherent commits to the existing branch
and update draft PR #16 to describe the final implementation and validations.
No main merge or public release.

Final handback: first-failure/root-cause evidence (or explicit non-reproduction),
LwTool mechanisms adopted/rejected, actual Home code changes, before/after live
measurements, fault/recovery/soak results, source/package hashes and paths,
restoration receipts, remaining limitations and rollback. Independent lead
acceptance remains separate. Do not declare the owner's intermittent
disconnect fixed merely because offline tests or a short pipe echo pass.
