# LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005 — worker review

Date: 2026-10-07

State: **AWAITING_REVIEW**

Branch: `research/offline-controller`

Assignment checkpoint:
`e18d8f3fdbad0a23804c783e5497ad450d74d49c`

Checkpoint commits:

- A expiry correction:
  `9da977db8ad83c716c207a92515cc1db058e190c`
- B managed correlation:
  `70348efb38ec107b57b698c2166787c5b8c4c82e`

During B, owner/lead documentation advanced the branch through
`fe0f5d2735d06a83f52c2033bec02de4300b26d0`. Those commits were preserved
without reset, discard or rewrite.

Final acceptance remains with the project lead.

## A — LR-GHOST-EXPIRY-005 corrected

The independently reproduced mismatch is corrected in
`CurrentClientMap317ActionProvider.PrepareGhostPlunderRows`.

The helper now preserves the established reader and all existing identity,
protection, counter and row-preservation guards while matching the exact host
expiry boundary:

- positive expiry after plunder: accepted;
- zero expiry: accepted;
- negative expiry: accepted;
- missing expiry: accepted;
- positive expiry equal to plunder: rejected;
- positive expiry before plunder: rejected.

The operative predicate is now equivalent to:

`taskExpireTime > 0 && plunderAt >= taskExpireTime`

for the expiry failure branch.

The lead's historical failing evidence remains untouched at:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004/lead-review-2026-10-07/expiry-results.json`

Its SHA-256 remains:

`77FB89FC7BF4325E5CE656DFC6451E5209A0666367322FF7ABE880424978A7D8`.

Fresh corrected packaged-method evidence is separate under this work item in
`a-expiry-results-corrected.json`.

Accepted helper rows preserve their raw JSON unchanged, so the frontend-owned
random delay and original field/value identity are not rewritten.

### Reader contract preserved

Numeric parsing was not broadened.

The pre-existing Desktop helper path still throws
`InvalidOperationException` for string-valued expiry before the later
string-parsing expression can be reached. The fresh replay explicitly records
both a numeric string and malformed string as rejected by the helper while the
host's broader reader accepts/defaults them.

This difference is outside LR-GHOST-EXPIRY-005 and is intentionally preserved
because the assignment forbids broadening numeric parsing.

### Public preparation remains fenced

`PrepareGhostPlunderTasksAsync` still returns
`GAME_PROVIDER_UNAVAILABLE`.

Its explanation is corrected so that it no longer says preparation is blocked
*because* terminal Ghost correlation is unavailable. It now distinguishes:

1. exact protected `prepareGhostPlunderTasks` row transformation/rejection
   semantics remain incomplete; and
2. downstream Ghost execution terminal-result correlation remains separately
   unresolved.

No preparation/persistence or execution capability was enabled.

## B — managed request/result correlation

Disposition:
**PARTIAL_SOURCE_PROVEN_MANAGED_CORRELATION**.

Current managed identity:

- `Assembly-CSharp.rdl` SHA-256
  `BFB740B4570C58BD2BCC7FB83F9B83D8121CE10FB1BF49040E9FB8B08E958B3E`;
- RGMD root `0x9a92a4`.

The new strict read-only inspector is:

`tools/lwbridge317/inspect_map_expiry_correlation.py`.

It deliberately does **not** claim a general inverse for the modified CIL
metadata-token encoding. Only independently validated exact-hash operands are
named. Unknown operands remain raw, and the evidence retains method bytes,
MethodDef rows, RVAs, file offsets, body hashes, raw operands and unresolved
operands.

The parser proof also mutates validated token values and confirms that mutations
and unrelated values remain unresolved, preventing convenient-name fallback.

### Send path

Current Lua still serializes through:

`SFSNetwork.SendMessage(cmd, ...) -> NetworkManager.SendLuaMessage(cmd, bytes)`.

Managed `NetworkManager.SendLuaMessage` is MethodDef 5752, RVA `0xad890`.

Validated dataflow:

1. load `NetworkManager._futureManager`;
2. call `FutureManager.getFutureId()`;
3. write that integer under the managed future field in the outgoing SFS object;
4. call `FutureManager.onSendRequest(fuid, msgId)`;
5. continue the normal send path.

`onSendRequest` constructs `msgSendInfo(fuid,msgId)` and adds it to
`Dictionary<int,msgSendInfo> _sendInfos` if that exact key is not already
present.

Thus fuid is a real specific pending-request key, not a FIFO slot.

### Receive path

`NetworkManager.OnExtensionResponse(BaseEvent)` first performs
`SyncPingPong(-1)`, then dispatches through `MessageFactory`.

`MessageFactory.DispatchResponse(string,SFSObject)`, MethodDef 6192,
RVA `0xb592c`, conditionally tests the same future field. When present it:

1. reads the integer fuid;
2. obtains `GameEntry.Network.getFutureManager()`;
3. calls `FutureManager.onServerMsgCome(fuid,serverTime)`.

`onServerMsgCome` checks the keyed `_sendInfos` entry, obtains its send
timestamp for timing statistics, then removes the same fuid entry.

Therefore fuid is source-proven managed pending-request identity **when the
response actually contains the future field**. It is more than a free-running
timing counter.

### Managed-to-Lua boundary

After future accounting, MessageFactory continues with the same SFS response
object.

The recovered current path is:

`GameEntry.Lua -> XLuaManager.Env -> SFSObjectExtention.ToLuaTable ->
XLuaManager.DispatchResponse(cmd, object)`.

`SFSObjectExtention.ToLuaTable`, MethodDef 17285 / RVA `0x1a8414`,
enumerates SFS object entries and writes their key/value pairs into the Lua
table. A real future field that exists in the response is therefore not stripped
by this conversion.

Current Lua subsequently dispatches that raw table to a fresh message instance.
The existing decoded Ghost proof remains valid: arbitrary injected fields survive
the current handler, but injection does **not** prove the real server emits them.

### Reuse, reset and ordering

`NetworkManager.Connect` calls `FutureManager.reset()`.

Reset zeroes `_futureId` and clears `_sendInfos`. The same numeric fuid can
therefore be reused across connections and is **not** a globally durable Ghost
job identifier.

Within one pending dictionary lifetime, matching is key-based rather than FIFO:
a response containing a pending fuid targets/removes that entry independently of
response order.

The separate `push.ghost.recon.steal` point-identity push remains separate and
is not conflated with direct request completion.

## Exact remaining correlation edge

The direct `ghost.recon.steal` response producer/schema still does not prove
that the managed future field is emitted or echoed for that command.

The unresolved edge is:

`direct Ghost server response producer -> future field present in SFS response -> Lua raw table -> durable Ghost task result`.

Static evidence sufficient to close it would be a direct response schema,
serializer/producer or other managed body proving the future field for
`ghost.recon.steal`. Otherwise a future owner-authorized raw response witness
would be required.

Until then:

- no production correlator was added;
- no request queue was added;
- no real Ghost transport was added;
- Ghost scheduled execution remains fenced before the Dispatch path;
- public Ghost preparation remains unavailable.

## C — verification

Fresh checks passed:

- corrected packaged host/helper expiry replay;
- Desktop default/module-initializer production-helper checks;
- `--map317-native-boundary-check`;
- `--map317-plunder-worker-boundary-check`;
- `--map-campaign-canonical-check`;
- `--profile-runtime-owner-check`;
- `--overview-bridge-normal-composition-check`;
- standalone `LWBridge.Map-0.3.17.Checks`: `LWB317_MAP_CHECKS_OK`;
- `tests/map_provider_decoded_body_oracles.py`: 4/4;
- `tests/map_blocker_boundaries_lua_checks.py`: 4/4;
- `tests/map_provider_semantics_lua_checks.py`: 3/3;
- `tests/map_expiry_correlation_parser_checks.py`: PASS;
- current-client runtime contract: PASS;
- current Map compatibility: PASS;
- final 005 evidence validator: PASS.

Release builds:

- `LWBridge.Desktop`: 0 warnings / 0 errors;
- `LWBridge.Desktop.Checks`: 0 warnings / 0 errors.

Canonical frontend:

- static/integration `npm run check`: PASS;
- production build: `LWB317_PRODUCTION_UI_BUILD_OK`;
- package verification: `LWB317_PRODUCTION_UI_PACKAGE_OK`.

Canonical plunder verification remained inert:
`providerMode=inert-local`, `externalProviderCalls=0`.

No Last War or LWBridge process was launched or attached.

## Tooling negatives

Preserved as non-product findings:

- the first new C# expiry test fixture used a literal newline in a normal
  interpolated string and failed compilation before test execution; corrected;
- exploratory attempts to fit a global modified-token inverse were rejected
  because held-out known operands did not validate a simple affine/bit model;
- a broad whole-assembly FutureManager xref scan was stopped as too expensive and
  replaced by exact type-filtered/wrapper-validated scans.

None changed game/runtime state.

## Acceptance boundary

Home/Map remains **PARTIAL**.

No `LIVE_PROVEN` state is upgraded.

Treasure/Ghost public provider methods remain unavailable.

Live/shared-desktop work remains **ON_HOLD_BY_OWNER**.

Historical 003/004 evidence and the lead's reproduced expiry negative remain
preserved. Final acceptance remains with the project lead.
