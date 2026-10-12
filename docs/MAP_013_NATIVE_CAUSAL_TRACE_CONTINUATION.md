# MAP-013 — live native causal observer continuation, 2026-10-12

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
