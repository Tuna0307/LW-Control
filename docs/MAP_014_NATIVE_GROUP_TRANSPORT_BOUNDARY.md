# MAP-014: actual bulk-AOI file transport versus authenticated native group

**Result: source boundary proven; native A/B/C group acquisition remains unproved.** The
checked-in v24 experimental producer and the current managed FastCity client
implement a supported **one-command/one-result probe**. The additional
authenticated Overview named pipe is real, but its actual current game-side
handler only supports `getStatus({})`. None of these paths currently
emits an authenticated, run-bound A/B/C group of 10,000 individual native AOI
response proofs, 2,500 type-enriched logical blocks and a separately
correlated **single final home reply**. The archived genuine A/B/C observation
and 10,000-cell union remain diagnostic evidence; they cannot be reclassified
as an accepted production group or a successful native shared ingress.

This is a **read-only source audit**, not a claim to have observed a running
game, an installed injector script executing, an authenticated transport
packet or a new benchmark. The script fingerprints **semantic anchors** from
the checked-in producer/client files at execution time. It fails if the
producer's whitelist, source keys, mode or version diverges, rather than
continuing to assert that a newly revised producer has the old boundary.
The original installed game scripts and archived receipts were untouched.

## Exact implemented path

1. Managed
   `src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:844-947`
   constructs `fastcity` + new GUID and atomically writes
   `live-resource/bulk-aoi-diagnostic.txt` with
   `schema=1`, `probeVersion=lwbridge-live-resource-probe-2`,
   `requestId`, `profileId`, `launchSessionId`, `challenge`,
   `gamePid`, `serverId`, `scanRunId`, `requestMode=coverage`,
   **one** `targetTileX/Y`, view/FOV settings, selected-kind flags and
   `deferRestoreUntilResponse=true`. It polls **one** fixed
   `bulk-aoi-diagnostic-result.json` until `requestId` equals this GUID.
   `ValidateFastCityBatchResult` (same file, ~1574-1740) checks the
   file's probe version, owner, selected kinds, camera restoration, native
   AOI geometry, requested native footprint and category-specific row
   accounting. `CurrentClientMapBlockSource.cs:460-499` bounds
   `capturedAt` to request start minus two seconds through now plus five.
   Existing source validation is meaningful **for each individual probe**.
2. Lua `tools/current_live_resource_probe.lua:2469-2577` reads the
   4,096-byte-limited command, removes that command file, parses ownership,
   and validates the active Overview lease's profile/session/challenge/PID.
   The explicit request-mode whitelist is `native`, `expanded`,
   `direct`, `coverage`, `anchor`, `edge`, `zoom`: **no group**.
   In `pump_bulk_aoi_diagnostic` (~5532-5653), the `coverage` branch
   selects one target cell as initial `indices = { target_aoi_index }`.
   There is one `bulk_aoi_request` in-flight state, progressed through
   ~6097-6505, and its terminal result follows the one-shot source path.
3. `write_bulk_aoi_result` (Lua ~5342-5503) writes one object with the
   per-request version/identity, sampled
   `requestedIndices`/`nativeCurrentSetCount`, `point_records`,
   selected-kind flags, native map diagnostics, camera/projection restore
   booleans and UTC `capturedAt`. It **does not serialize** `scanRunId`,
   `serverId` or `worldId` (although the command reads some of them), nor
   `groupGeneration`, `regionOrdinal`,
   `nativeReplyRequestId`, `nativeReplyAtUnixMs`,
   `requestStartedAtUnixMs`, a per-AOI reply ownership list, 2,500
   kind-specific logical block attestations, or
   `homeRestoreRequestId`/`homeNativeReplyRequestId`/
   `homeNativeReplyAtUnixMs`. A selected-stage echo inserted in the
   command or result would not be a native stage acknowledgment. The
   deferred-home branch (~6114-6148) checks manager/world flags and camera
   geometry, **not** an independent home request/reply event pair.
4. The received-only
   `src/LWBridge.Desktop/Map014NativeSharedReceiptSource.cs` interface
   (~8-27) explicitly has no production implementation.
   `ValidateReceivedEnvelope` (~229-389) requires the future owner-matched
   group, 10,000 **individual received** and timestamped native AOI proofs,
   2,500 per-kind block enrichments (four correlated AOIs per 20×20 block)
   and a final original home reply acknowledgment; only City is admitted
   for this speculative ingress. The diagnostic source-schema preflight
   (`CurrentClientMapBlockSource.SharedAdmission.cs:39-235`) requires
   three separately correlated A/B/C responses and a distinct matched home
   response, with physical AOI continuity. Its result is diagnostic only.

## Other existing producers, authenticated pipe and cancellation

The previous audit considered the `bulk-aoi-diagnostic` file pair. The
expanded executable source inventory now also inspects the actual separate
bridge transport and other native-source dispatch lanes:

| Existing route | Actual source and consumer | Native A/B/C group outcome |
| --- | --- | --- |
| **Authenticated named pipe** | `src/LWBridge.Desktop/LWBridgeControlPipeRpcSessionTransport.cs:83-143,274-315` serializes authenticated `call` commands and correlates `result` frames to pending IDs. `src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs:136-218,318-352` relays frames over `pipe-inbound/outbound-NNNNNNNN.json` mailboxes. In `tools/current_overview_bridge.lua:3470-3545`, the **actual Lua receiver** accepts `hello.ack` or `command`, then only `kind=call`, `fn=getStatus`, `args={}`. It returns connection, account/ready, server and world-position status fields. | **Transport exists; map group method, native AOI/result stream, group cancel and native home reply are absent from the current Lua RPC dispatcher.** A working pipe cannot by itself certify native group data. |
| **Other desktop pipe calls** | `LWBridgeBackend.cs:507-661` sends `getTradeStationCatalog`, `getMonsterCatalogOptions`, `getSquads` from specific backend actions; its generic `call_lua` exposure `~663-725` permits only `getStatus`. | The present Lua `process_pipe_inbound` rejects those three internal function names as `pipe_command_unsupported`. They do **not** constitute alternate native group ingress. Their catalog functionality should not be inferred to work from the generic transport alone. |
| **Game resource-source lanes** | `tools/current_live_resource_probe.lua:7047-7085` pumps image, Treasure-state, Monster protection, Resource detail/scan detail, Train-list, single bulk AOI, runtime/AOI diagnostics and single `read_command` under current Overview identity. These commands use their own request/result files and per-operation typed scopes. | No group pump, native group packet correlator, 2,500-block publisher, or native final-home acknowledgment across A/B/C. Point AOI state and March manager state remain separate proofs. |
| **Other Overview command files** | `tools/current_overview_bridge.lua:3688-3771` processes world state/readiness, navigation, server jump, March Follow, sharing/plunder and Truck commands, then the status-only pipe consumer. | These are existing UI/game operations with separately scoped results. They are **not** a received native full-world group producer. |
| **Product Map317 source selection** | `CurrentClientMap317ScanProvider.cs:26-49,174-215` normally uses the ordinary `CurrentClientMapBlockSource`; its optional `createSourceForAcceptedRun` factory defaults to `null`. The received-group `Map014NativeSharedReceiptSource` has **no production** implementation of `IMap014ReceivedNativeSharedGroupIngress`. | Controlled injected tests can exercise source/engine/SQLite, but there is **no selected native group source** in normal product composition. |

Cancellation is implemented for **managed** Map317 runs, not yet for
native group control: `CurrentClientMap317ScanProvider.StopMapScanAsync`
(~272-292) cancels the active scan token and joins its task;
`MapScanEngine.cs:85-113,335-355` attempts bounded native cleanup when a
source implements that interface and prevents canceled staging from
publishing. The received-only native group source's `CleanupAsync`
(`Map014NativeSharedReceiptSource.cs:195-225`) cannot acknowledge Stop
before it has a prevalidated final-home packet: it throws when
`verifiedNativeHome` is absent. No existing game-owned group cancellation
command is provided.

The **actual single bulk AOI** producer has its own fail/restore behavior:
`fail_bulk_aoi` (~5524-5529) tries a per-probe state restore and emits
one failed result. A lost Overview identity (`Pump` ~7210-7255)
terminalizes an in-flight bulk probe through that path. Its deferred
restore completion (~6114-6148) checks current native manager/world flags
and original camera geometry before writing one per-probe success. Neither
case serializes a distinct correlated native *home request/reply* identity
or proves a reusable A/B/C group abort/recovery protocol. Similarly, the
named-pipe backend's cancellation token can abandon a pending RPC await
(`LWBridgeControlPipeHostState.cs:292-336`); there is no game-facing
`cancelNativeGroup` command in the actual Lua dispatcher.

The audit pins the current pipe receiver's exact allowed envelope types,
function-name condition, status-result fields, probe's pump registry,
managed backend allowlist and product factory default. A future native
group handler, extra Lua pump, altered result payload or source selector
causes a **requalification error** instead of a stale declaration that
no alternate transport exists. This source inventory does not claim to
prove a negative about inaccessible native code or future implementations.

### Read-only native archive comparison

| Archive | What the original recording establishes | Why it cannot pass this group boundary |
| --- | --- | --- |
| `artifacts/map-013-live/native-causal-20261014-004239` | Three privately observed consecutive region receipts, native stage measurements and independent control restoration | Region files predate run/group reply ownership and the per-AOI/per-block/home native correlation schema. |
| `artifacts/map-013-live/native-causal-20261014-004916` | **119** diagnostic probes, **10,000/10,000** native AOI footprint union, **49.203 seconds** in `full-sequence-result.json`, A/B/C files and final camera restoration | Diagnostic source rows/footprint union are not 10,000 native reply-owned individual proofs, 2,500 independently received typed block outcomes, or an authenticated, distinct final home ACK. |

The first archive's observations remain valid, and the second archive's
footprint count remains an authentic **diagnostic** result. The audit does
not re-run those native sequences or infer whether absent production-grade
fields could be recovered by a future supported inbound transport.
For each archive it re-extracts the actual saved
`experimental-resource-probe.lua` writer contract: that observer-instrumented
snapshot declares **159** result fields (versus **158** in the present
checked-in baseline). Its research instrumentation fields are accounted for before
rejecting the group; the rejection does **not** rely on treating genuine
experimental instrumentation as forged Lua output.
Historical packet tracer `sequence-trace.json` includes passive
`world.get.block` observations, but native command labels, timestamps and
logical region echoes in a passive trace do not authenticate one complete
owned group and its cleanup by themselves.

## Executable audit and adversarial controls

```powershell
python -m unittest discover -s tools/map_014_shared_transport_audit -v
python tools/map_014_shared_transport_audit/audit_group_transport.py
```

`audit_group_transport.py` extracts whitelist and writer fields from the
**actual source functions**, checks managed file paths/version/poll, verifies
the Lua lease validation anchors and asserts absence of group writer fields.
`audit_one_probe_correlation` admits a controlled one-shot request's
supported **correlation subset** only: identity, version, mode, request ID,
and bounded `capturedAt`. It intentionally does not replace the managed
native footprint/record validator and never states real native packets
were received. `audit_current_producer_group_request/result` rejects
unsupported `group` mode, forged selected-stage extras and any claim
requiring keys the Lua writer cannot emit. The archive comparator inspects
the original A/B/C files read-only and cannot accept them as current group.

`audit_future_shape_only` tests a **fully controlled** example of a
possible future contract with three monotonic matched native stage replies,
fixed owner and group, 10,000 nonduplicated AOIs, four same-reply AOIs per
block, 2,500 City block outcomes and a distinct home reply. Its success
means **structurally coherent fixture only**, not native receipt
authentication, usable performance or engine publication. Inverses cover
forged echoed stage, stale per-probe file even with correct request ID,
swapped profile/session/challenge/PID, changed request IDs, missing/duplicate
or stale native stages, duplicate/9999 AOIs, cross-block reply collision,
2499/duplicated typed blocks and missing/reused/wrong-owner final home.
The newer adversarial tests exposed **three actual false accepts in this
audit's controlled future-receipt validator**: a missing
`WorldPointManager._curViewIndex` source label, one forged reply ID
certifying all 10,000 AOIs, and one reply ID with inconsistent native
observation times. All three initially failed the new tests (**RED**).
The validator now requires the exact native source and enforces the
`Map014NativeSharedReceiptSource.ValidateReceivedEnvelope` limit of
**at most 160 native AOIs per native reply identity with one stable time**,
and the tests pass (**GREEN**). A source guard also rejects changes to
that upstream 160-AOI limit before claiming continued parity. This
tightening changes only this offline auditor, not game/product behavior.
Separate source-drift tests stop an obsolete conclusion if Lua gains a new
mode or output field, the named pipe admits a new native command/envelope,
the live source adds a group pump or Map317 begins selecting a group
factory.

### Later source-only ownership and composition recheck

This continuation re-read the checked-in Lua `process_pipe_inbound`
(`tools/current_overview_bridge.lua:3470-3545`), actual adapter mailboxes,
bulk-AOI reader (`tools/current_live_resource_probe.lua:2469-2577`), pump
dispatch, managed transport and the received-only source. **No additional
supported received native-group ingress was found in those checked-in paths.**
The existence of a framed, authenticated named pipe is not native group
packet reception, and the original per-probe `fail_bulk_aoi` restore path
is not a group cancellation/home-ACK protocol.

The prior audit's **ownership drift checks were too weak**: they merely
required field/condition strings to exist. Controlled edits changing the
bulk-AOI identity guard's disjunction to conjunction or neutralizing its
`bulk_aoi_diagnostic_identity_mismatch` result, and changing the pipe's
owner or `getStatus` command reject to conjunction, incorrectly passed the
old source checks (**RED: four false accepts**). The audit now requires
source-backed exact short-circuit rejection predicates, with the explicit
error branch, for all four pinned identity dimensions
(profile, session, challenge, game PID) on the bulk probe and for protocol
version/profile/session/request ID plus exact command ID, call kind,
function and empty arguments on the pipe (**GREEN**). A corresponding
future-envelope inverse also caught an omitted home/point correlation
rule: the synthetic checker accepted a final-home reply ID reused to
attest four regular AOIs (**RED**). It now rejects that reuse, matching
`Map014NativeSharedReceiptSource.ValidateReceivedEnvelope`'s existing
separate-home restriction (**GREEN**). These were **offline audit
weaknesses**, not demonstrated defects in the unmodified producer.

To prevent a production source arriving outside the existing provider's
`createSourceForAcceptedRun = null` seam, the audit also surveys all
production `src/LWBridge.Desktop/**/*.cs` files (excluding generated
`bin`/`obj`) for a
`Map014NativeSharedReceiptSource` call/reference or a new
`IMap014ReceivedNativeSharedGroupIngress` implementation. The declaration
file is excluded from this **other-production-call-site** check. Synthetic
new composition/implementation inverses require requalification instead
of reporting absent integration on the strength of a comment alone; the
current census is empty. This source scan is deliberately conservative,
not a compiler, hidden-native-code detector, or proof of what a future
binary runs. Tests and source CLI run only under Python; no .NET runner,
Git/recovery action, Lua change or game interaction was used.

The exact missing production step remains a source-supported **received**
native group transport that independently binds request and reply identity,
world/server/owned session, physical AOIs, per-kind freshness and native
home ACK, plus genuine cancellation/restore and desktop package
qualification. No live group provider is installed or enabled by this
audit. FOV20 City-only production qualification and conservative guarded
other-kind selection are unaffected; **MAP-014 stays PARTIAL**.
