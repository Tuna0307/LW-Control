# MAP-013 — live native causal observer continuation, 2026-10-12

## 2026-10-13 LEADMAP013-04 and WorldReady repair (checkpoint PARTIAL)

**This continuation is offline/inert only.** The original eight owned
game receipts were retained; **no new game launch, packet, populated
first/second-region reply, AOI cache insertion or benchmark was observed**.
The production map scanner, Normal/Fast, Home, saved datasets, native
names, mixed discovery and Auto Stop-before-return are unchanged.

**LEADMAP013-04 corrected:** The actual
`tools/map_009_offline/run_map013_owned_observer.py` reader no longer
accepts a reply merely because `requestId` matches. It uses a separate,
source-schema-specific `ExpectedReply` for three producers:
`current_overview_bridge.lua.write_world_ready_result` emits
`schemaVersion=1`, `bridgeVersion`, `profileId`, **`sessionId`**,
`gamePid`, `challenge`, UTC `capturedAt`, `state`, and,
for a successful world, actual `serverId`, `worldId` and
positive tile dimensions. Player tile is **optional** in the real
producer and validated only when emitted.
`current_live_resource_probe.lua.write_bulk_aoi_result` emits
`schemaVersion=1`, `probeVersion`, `profileId`,
**`launchSessionId`**, PID, challenge, capturedAt and requestMode;
positive target tile/AOI indices must be present. Its diagnostic
output **does not echo an authoritative serverId/worldId**. It cannot
establish native remote response provenance merely because the
original outgoing request supplied a serverId. The private observer
now also emits its original owner challenge, probeVersion and
capturedAt (all confined to ignored raw receipts); its
`launchSessionId` is distinct from WorldReady's `sessionId`.
The reader rejects malformed/missing/mismatched fields, stale
file/capture times, future captures, wrong statuses, error-free
failed states and foreign owners. `pending` is not a terminal
success. The original lead inverse remains untouched at
`proof/MAP-013-LEAD-REPLY-INVERSE-2026-10-13.json`. Independent
inert tests execute that exact former foreign-owner example and
additional missing-version, wrong-world/server, wrong-session,
stale, pending, terminal-failure and positive controls. This
validates **diagnostic file ownership**, not game packet-to-cache
association.

**Actual WorldReady Lua source path, tested inertly:** Host uses
`publish_command` (atomic file replace), verifies the owner
lease via the existing `write_lease`; the v24 bridge's
`M.Pump` reads control/lease. A rejected/expired lease exits
*before* `pump_world_ready`. When admitted,
`read_world_ready` reads and **attempts to delete the command before**
validating schema/version/token. The original ignores the removal
status. If those fields are invalid it silently
returns nil **without any result**; a wrong owner is instead
returned as a request.error and produces a `failed` result.
Accepted commands are processed by `begin_world_ready`:
existing `WorldScene` succeeds immediately; otherwise
`SceneUtils.ChangeToWorld(callback)` is attempted and a
pending transition begins. `pump_world_ready` confirms
**actual WorldScene type**, not just a non-null
`SceneManager.World` (which may still be `CityScene`).
It writes `proven` on a WorldScene, `failed` on unavailable/
throwing transitions or after the source's **10s** pending
deadline. `write_world_ready_result` uses a direct file writer
that can return false; the original ignored the return.
This explains concrete *possible* silent/timeout routes,
but the archived 234333 receipt lacks the stage telemetry
required to choose one. **Its actual first failed branch is
still unknown.**

**Additional inert diagnostic audit after `a0eacc3e`:** The initial
instrumenter labelled a protected `os.remove` attempt as
`command_consumed`, even if removal failed. It also rate-limited
by stage alone, possibly suppressing a different command within
the same second. The opt-in copy now distinguishes
`command_consumed` from `command_remove_failed` using both the
`pcall` result and `os.remove` return, rate-limits by stage **and**
sanitized request ID, and records deferred busy control/lease reads.
The diagnostic writer also protects all stage bookkeeping, bounds
and sanitizes untrusted detail, and records successful stages only
after a successful file write and close. The originally empty
`ChangeToWorld` callback now adds a protected
`transition_callback_invoked` log and retains its return behavior;
an exception during invocation is separately logged as
`transition_invocation_failed`. Inert tests exercise stage filtering
for concurrent IDs, including a failed removal and another request.
Instrumented Lua syntax/default-path tests still pass. These added
stages have **not** been executed live; they cannot retroactively
identify the archived 234333 failure or qualify scanner output.

**Deterministic next-run diagnostics implemented but not live-run:**
An *opt-in, fail-closed exact-source* transformation of the
existing bridge in
`tools/map_009_offline/worldready_instrument_013.py`
records source-specific bounded stages:
control/lease accepted, rejected or deferred, command read/removal outcome,
schema/token/owner admitted or rejected, transition invocation,
pending, completed WorldScene or 10s timeout, and result write
success/failure. Unique source seams and full Lua parsing are
required; source drift refuses packaging. Python's read-only
`snapshot_worldready_stages` records publication and subsequent
command/control/lease/result existence with exact owner checks
without copying secrets. The **unmodified wrapper control and
resource-probe SHA-256s remain byte-identical** without opt-in;
actual v24 `run_check_only` of the initial opt-in candidate succeeded
without launching the game (probe encoded size **57,531**,
wrapper **34,691** bytes). The corrected stage instrumenter also passed
actual v24 `check-only` admission (unchanged installed files; probe
**57,531**, wrapper **34,959** encoded bytes). Neither a Lua syntax pass nor
this offline admission is a live end-to-end WorldReady or
AOI-positive witness.

**Exact previously denied operation recovered from existing
conversation/tool record, not repeated:**
`mcp__Chat_On_Steroids_Core__write_stdin` with
`session_id=55411`, `chars=""`, `yield_time_ms=1000`,
following a diagnostic runner started with
`python tools/map_009_offline/run_map013_owned_observer.py
--baseline-region`. Returned verbatim redacted error:

> Script error: This tool call was blocked by OpenAI because we
> couldn't determine the safety status of the request.

The denial explicitly concerns **that tool poll**. The tool
reported no more specific prohibited action or policy category.
Do not infer blanket future permission or that the game scanner
itself was rejected. Do **not** retry, reroute or bypass the denied
operation. The original 234333 runner's final.json and exact
owned cleanup showed it ended before region dispatch and restored
the original scripts; this document does not treat that result as
a native protocol negative. A future live native packet/callback/
cache trial still needs an independently permitted execution path
and exact task-owned cleanup. The enabled production route remains
the accepted serial camera-assisted scanner.

Current v24 offline additional source boundaries:
`NetworkManager.Send` method `0x06001677` forwards
`IRequest` to its internal network proxy.
`NetworkManager.OnExtensionResponse(BaseEvent)` method
`0x06001687` forwards the received event via an obfuscated
network handler call, while its `(string,SFSObject)` overload
`0x06001684` is an empty method body in this admitted build.
`WorldPointManager.SendAoiRequest` `0x0600351d`
constructs the request and invokes its primary call at
`IL_00d8`, with a guarded secondary send at `IL_0150`.
`HandleViewPointsReply` `0x06003551` checks an
`ISFSObject` envelope and invokes its local block callback
from `IL_0104` for indexed segments; a later fallback
passes the complete payload.
`ParseWorldGetBlock` `0x06003553` includes
an early world/server comparison at `IL_0047..006a`
before it parses point/march lists. These are actual static
control-flow locations; the obfuscated dispatch's **wire command,
payload values and in-session callback identity remain
unattributed**, not a fabricated success. No camera-free or
consecutive shortcut passed a new positive gate.

Lead review, 2026-10-13: new local-event and WorldReady receipts accepted as
bounded evidence. Fix LEADMAP013-04 reply-owner validation and diagnose command
orchestration inertly before more trials. Preserve exact reported safety denial;
no bypass/retry of denied action. Read
`reviews/2026-10-13-MAP-013-OBSERVER-LEAD-REVIEW.md`. Scanner remains PARTIAL.

**Status PARTIAL.** This is new task-owned live evidence, not a replay of
R15/R16 and not a complete AOI wire or cache trace. Retain the existing
accuracy-qualified camera-assisted shared scanner. Normal/Fast, Home, native
names, saved profile maps, mixed work-sharing, Auto and UI are unchanged.

## Source-supported boundaries

- Current admitted Assembly-CSharp.rdl SHA256:
  bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e.
  WorldPointManager.Init method 0x06003529 registers **local managed EventId**
  1433, 661, 2821011 and 2821014 through the **static** GameEntry.Event
  accessor. EventComponent.Subscribe/Unsubscribe accept EventId and
  Action<object>. These IDs are **not confirmed AOI wire message IDs**.
- WorldPointManager.SendAoiRequest (0x0600351d) creates a native request,
  while SendViewRequest (0x0600356a) creates a viewport request.
  HandleViewPointsReply (0x06003551) consumes ISFSObject and view-index
  entries; ParseWorldGetBlock (0x06003553) clears cached collections,
  checks current-world/server identity and parses point/march arrays.
  NetworkManager.Send (0x06001677) forwards an IRequest to its proxy;
  OnExtensionResponse exposes BaseEvent and (string,SFSObject) overloads.
  **No live outbound wire ID -> corresponding inbound segment ->
  point/march cache insertion attribution has been established.**
- An opt-in LuaEntry **wrapper** observer leaves the existing resource
  module unchanged. It additively subscribes four source-identified local
  events, captures bounded native object/pump states, calls the old
  bridge/probe Pump behavior and unregisters. It does not replace a game
  callback, bypass services, change AOI geometry or enable a scan method.
  Normal builds have no opt-in; tests verify exact default package hashes.

## Exact task-owned live observations

Compact redacted receipts with SHA256 values and raw private locations:
docs/proof/MAP-013-NATIVE-OBSERVER-2026-10-12.json.
Original raw results under ignored artifacts/map-013-live are preserved.
Each live attempt used a newly isolated runtime, matching profile/session/
challenge/PID, v24 admission, existing background launcher, exact-owned
Stop and restored game script backups. Game language stayed Chinese.

1. **230831:** a first, now-rejected, inline probe-module extension
   passed package admission but actual Player.log reported that LuaEntry
   could not require the modified LWBridgeLiveResourceProbe module.
   Lua startup failed. Helper failure cleanup restored the scripts;
   source preserved in ignored artifacts, not shipped.
2. **231337:** unchanged original probe **did** reach ready state;
   this differentiated the modified-module startup failure. A standalone
   WorldReady command was unanswered. That runner omitted host-lease
   renewal and initially had a Stop argument-order defect. The exact
   journaled owner was subsequently stopped manually using the official
   stop command; process-handle exit and original hashes are recorded.
3. **232008 / 232455 / 232829:** switched to an opt-in LuaEntry
   **wrapper**, leaving original probe bytes unchanged. All owned starts
   and exact stops/restores succeeded. The Lua global GameEntry.Event
   was unavailable. Source-correct **CS.GameEntry.Event** was present
   and all four managed subscriptions succeeded. Received one **661**
   event with numeric payload; this is not proof of a network AOI reply.
4. **233621:** 1,939 bridge-Pump calls, zero observed Pump exceptions,
   lease age 1 second, correct control session. WorldReady was not
   answered. SceneManager.World appeared after about 3 seconds but
   PointManager was absent for the observed 18-second window. This
   is a functioning observer and control loop, not a valid AOI cache.
5. **233908:** offsetting WorldReady publication from observer startup
   resulted in a **real official world transition**, method
   SceneUtils.ChangeToWorld(callback)+WorldScene, server **2212**,
   world **0**, 1000x1000. The private observer itself then failed
   trying to add managed Reflection.BindingFlags. Its sampling
   conversion was fixed; original Stop and restoration passed.
6. **234333:** next bounded first-region attempt did **not** reach
   region dispatch: WorldReady failed its wait, then the runner
   accessed an uninitialized world result. Existing owned cleanup
   exact-stopped the game and restored scripts. The runner now
   initializes world=None and rejects region requests without
   independently verified WorldReady/server/world admission.
   The tool blocked a live-session poll; no bypass/repeat followed.

No successful live first-region native request/reply segment trace
was captured, no second-region trial was attempted with these hooks,
and no new scanner identities/fields were qualified. This checkpoint
must not be called native scanner completion, faster acquisition,
universal accuracy, or proof that remote AOI is impossible.

## Exact remaining causal edge

First establish native SmartFox sender request object/message ID,
corresponding NetworkManager.OnExtensionResponse handler/segment keys,
connection world/server/LOD/subscription, and matching
HandleViewPointsReply + ParseWorldGetBlock cache transition.
The local EventComponent listener proves a supported additive hook,
**not** that native C# IRequest and SFSObject packets are attributed.
A read-only source-supported SmartFox callback/proxy observer or
verified native managed-method trace is required, keeping original
callbacks and manager lifetime intact. Before further trial,
diagnose inconsistent WorldReady publication without changing anchors
or assuming a mere timeout is a native protocol rejection.

Then prove fresh populated first, second and third separated regions,
independent identities/fields, complete segments, stale/wrong-server
rejection, cancellation and precise original camera/manager restoration.
Only accuracy-qualified candidates merit n>=3 alternating individual,
mixed or multi-server performance tests.

**Checkpoint safety:** original installed script hashes verified after
the final attempt; zero task-owned game actors, last exact Stop passed,
no Auto/reconnect or account-changing action enabled, no new UI/product
package, no merging. Independent lead review remains required.
