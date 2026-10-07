# Checkpoint C — Ghost response correlation

State: **COMPLETE — BT-03 PARTIAL/UNKNOWN; impossibility claim rejected, safe application correlation not recovered.**

## Current direct response

`GhostReconStealMessage.HandleMessage` checks `errorCode`, marks the raw table with `fromGhostreconStealMessage`, then forwards the **same table object** to reward handling and `ActGhostreconManager.GhostReconStealHandler`. The downstream manager reads only `stealTimes` and `reward`; neither body statically reads `uuid`, `ownerServer` or `pointId`.

That proves only **field non-consumption**. It does not define the complete server response schema.

`tests/map_blocker_boundaries_lua_checks.py` executes the actual decoded v22 body with two distinguishing synthetic inputs:

- when raw success contains `uuid`, `ownerServer`, and an opaque identity field, all survive unchanged into the downstream manager;
- when those fields are absent, the handler does not synthesize them.

Therefore the inference `handler does not read UUID -> response schema has no UUID` is invalid.

## Push response is separate

`PushGhostReconStealMessage` is a distinct command (`push.ghost.recon.steal`, versus direct `ghost.recon.steal`). It forwards its raw table to `ActDispatchTaskDataManager.PushHeroDispatchMissionStealHandler`, whose current body consumes `serverId`, `pointId`, and `playerInfo`. It does **not** consume task UUID.

This proves a current push-side location identity exists. It does not prove that the push is the direct request's terminal acknowledgement, nor that `serverId+pointId` uniquely identifies the durable Ghost job.

## Lua transport

`SFSNetwork.SendMessage(cmd, ...)` resolves `msgType = GetMsgType(cmd)`, constructs/serializes the message and calls managed `SendLuaMessage(cmd, bytes)`. `SFSNetwork.HandleMessage(cmd, raw)` resolves the message type again, creates a fresh empty message object, and calls its handler with the raw table.

No selected Ghost/SFS Lua body references `getFutureManager`; there is no Lua-side per-request pending dictionary, future ID, strict single-flight gate or ordering contract recovered on this path.

## Managed transport

Current `Assembly-CSharp.rdl` SHA-256 is `BFB740B4570C58BD2BCC7FB83F9B83D8121CE10FB1BF49040E9FB8B08E958B3E`.

`NetworkManager` owns `_futureManager`. `Main.Scripts.Network.FutureManager` owns:

- `_futureId`;
- `_sendInfos : Dictionary<int,msgSendInfo>`;
- `getFutureId()`;
- `onSendRequest(int fuid, string msgId)`;
- `onServerMsgCome(int fuid, int serverTime)`.

`msgSendInfo` owns `_futureId`, `_msgId`, and `_sendTime`. The RDL contains a unique `fuid` ASCII/user-string literal. `NetworkManager.SendLuaMessage(string msgId, byte[] sfsObjBinary)` and `OnExtensionResponse(string cmd, SFSObject so)` are present, and the preserved SendLuaMessage CIL has the expected local-integer / SFS-object write / repeated instance-field call shape.

However, this RG/RGMD build preserves **modified CIL metadata tokens** in these branches. The current parser cannot name the `SendLuaMessage` call/field tokens end-to-end. The audit therefore classifies FutureManager as a source-proven managed future/pending mechanism, but **does not promote** it to a source-proven Ghost application terminal-result key.

## Exact missing edge

`server/managed direct Ghost response -> Lua raw response table -> stable identity usable by durable Ghost job correlation`.

Minimum sufficient offline/live evidence would be one of:

- a raw/protobuf schema or managed receive body proving the direct Ghost response carries `uuid`, `fuid`, `pointId` or another stable request/task key into the Lua table;
- a static transport body proving FutureManager `fuid` is copied into the Lua response for this command and uniquely pairs request/response;
- a source-backed single-flight/ordering guarantee strong enough to make identity-by-order safe (none recovered);
- future owner-authorized live observation that captures the direct raw response identity and ordering without changing gameplay semantics.

Until then, correlation is **UNKNOWN**, not impossible and not safe.

## Ready offline implementation candidate

A follow-up can build a fail-closed correlation adapter/state machine that accepts only an **explicit source-backed terminal identity** and rejects absent/ambiguous/mismatched identity. It can have deterministic tests for UUID/future-ID/point-key variants without wiring any real action. The existing Ghost provider fence and scheduled Ghost safety guard must remain until one identity variant is source-proven.
