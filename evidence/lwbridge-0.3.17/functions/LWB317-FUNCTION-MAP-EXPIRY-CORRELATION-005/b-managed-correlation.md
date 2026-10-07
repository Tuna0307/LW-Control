# Checkpoint B — managed request/result correlation

State: **COMPLETE — PARTIAL_SOURCE_PROVEN_MANAGED_CORRELATION**.

B was resumed on latest descendant `fe0f5d2735d06a83f52c2033bec02de4300b26d0`.
The intervening commits after checkpoint A were lead/owner documentation only;
they were preserved without reset or rewrite.

## Source identity and tooling

Current managed source:
`Assembly-CSharp.rdl`, SHA-256
`BFB740B4570C58BD2BCC7FB83F9B83D8121CE10FB1BF49040E9FB8B08E958B3E`,
RGMD root `0x9a92a4`.

`tools/lwbridge317/inspect_map_expiry_correlation.py` is a strict, read-only,
hash-gated inspector. It deliberately does **not** claim a general inverse for
the modified CIL metadata-token encoding. It names only operands independently
validated by metadata signatures, trivial field/method dataflow, or generated
xLua wrappers. All other operands remain raw in the JSON evidence. Exact
on-disk method bytes, body SHA-256, MethodDef row, RVA, file offset, raw operand
and unresolved operands are retained in `b-managed-correlation.json`.

The new parser proof mutates validated token values and confirms the mutated and
unrelated values remain unresolved. This prevents convenient-name fallback.

## Recovered send correlation

Current Lua `SFSNetwork.SendMessage` remains the source-backed serialization
front end from SEMANTICS-003/BLOCKER-BOUNDARIES-004: it creates/serializes the
message and calls managed `NetworkManager.SendLuaMessage(cmd, bytes)`.

Managed `NetworkManager.SendLuaMessage` is MethodDef 5752, RVA `0xad890`.
Its validated dataflow is:

1. load `NetworkManager._futureManager`;
2. call `FutureManager.getFutureId()`;
3. write the returned integer under the same modified response/request key token
   `0xE47CA5B0`;
4. call `FutureManager.onSendRequest(fuid, msgId)`;
5. continue the normal SFS request send.

`FutureManager.onSendRequest` constructs `msgSendInfo(fuid,msgId)`, checks
`_sendInfos : Dictionary<int,msgSendInfo>`, and adds only when that key is not
already present. The dictionary key is therefore the specific `fuid`, not a
FIFO slot.

## Recovered receive consumption

`NetworkManager.OnExtensionResponse(BaseEvent)` first calls
`SyncPingPong(-1)`, then
`MessageFactory.Instance.DispatchResponse(event)`. Those modified operands are
independently identified by `NetworkManagerWrap._m_SyncPingPong`,
`MessageFactoryWrap._g_get_Instance` and
`MessageFactoryWrap._m_DispatchResponse`.

`MessageFactory.DispatchResponse(string,SFSObject)` is MethodDef 6192,
RVA `0xb592c`. It conditionally checks the same raw key token
`0xE47CA5B0`; when that key is present it reads its integer, obtains
`GameEntry.Network.getFutureManager()`, and calls
`FutureManager.onServerMsgCome(fuid, serverTime)`.
The modified call token for `onServerMsgCome` occurs exactly once in this exact
RDL, at this receive path.

`FutureManager.onServerMsgCome` checks `_sendInfos` by that `fuid`, obtains
the corresponding send timestamp, updates timing statistics, then removes that
same key. Therefore **fuid is a real managed pending-request identity when the
response contains it**. It is not merely an acknowledgement/timing counter.

## Managed -> Lua result boundary

After the conditional future accounting, `MessageFactory` continues with the
same SFS response object. The validated fallback path is:

`GameEntry.Lua -> XLuaManager.Env ->
SFSObjectExtention.ToLuaTable(SFSObject,LuaEnv) ->
XLuaManager.DispatchResponse(cmd, object)`.

`SFSObjectExtention.ToLuaTable`, MethodDef 17285 / RVA `0x1a8414`,
enumerates the SFSObject data-holder entries and writes their keys/typed values
into the Lua table. Thus an actual response field using the future key is not
stripped by this managed conversion.

Current Lua then dispatches the raw table to a fresh message instance. Existing
Ghost body proof remains unchanged: `GhostReconStealMessage.HandleMessage`
forwards that same raw table downstream; injected identity fields survive, but
synthetic injection does not prove a real producer emits them.

## Reuse, reset and ordering

`NetworkManager.Connect` calls `FutureManager.reset()`.
`reset` sets `_futureId` to zero and clears `_sendInfos` while resetting
metrics. A fuid can therefore be reused across connections and is **not** a
globally durable Ghost-job identity.

Within one pending dictionary lifetime, response matching is key-based rather
than FIFO: a response carrying a pending fuid targets that entry and removes it.
No source-backed FIFO/single-flight rule is required for the managed timing
association itself. Exact counter wrap mechanics beyond the recovered body are
not promoted.

The separate `push.ghost.recon.steal` point-identity push remains separate and
must not be conflated with direct-request completion.

## Exact remaining edge

The direct `ghost.recon.steal` response producer/schema is still **UNKNOWN** for
future-key emission. `MessageFactory` proves only a conditional
`ContainsKey/read` path for responses that carry the key; it does not prove the
server emits/echoes that key for Ghost.

Therefore no real Ghost transport, request queue, correlator or provider
capability was added. Public Ghost preparation/execution fences remain.

Minimum evidence to close this edge is one of:

- a source/protobuf/serializer schema proving direct `ghost.recon.steal`
  responses contain the managed future key;
- another static producer body proving that field is emitted for that command;
- a future owner-authorized raw direct-response witness. Live work remains
  `ON_HOLD_BY_OWNER`.

## Checks

`b-checks.txt` records:
- exact RDL SHA revalidation;
- Python compile of inspector and proof test;
- strict inspector regeneration;
- `LWB317_EXPIRY_CORRELATION_PARSER_CHECKS_OK`;
- task-owned temporary inspector output cleanup.

Exploratory whole-assembly/token-model scans that did not establish a validated
global inverse were not promoted. Their task-owned scratch was removed. Historical
004/003 evidence was not modified.
