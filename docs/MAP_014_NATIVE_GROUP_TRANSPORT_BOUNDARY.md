# MAP-014: actual bulk-AOI file transport versus authenticated native group

**Result: source boundary proven; native A/B/C group acquisition remains unproved.** The
checked-in v24 experimental producer and the current managed FastCity client
implement a supported **one-command/one-result probe**. Neither currently
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
Separate source-drift tests stop an obsolete conclusion if Lua gains a new
mode or new output field.

The exact missing production step remains a source-supported **received**
native group transport that independently binds request and reply identity,
world/server/owned session, physical AOIs, per-kind freshness and native
home ACK, plus genuine cancellation/restore and desktop package
qualification. No live group provider is installed or enabled by this
audit. FOV20 City-only production qualification and conservative guarded
other-kind selection are unaffected; **MAP-014 stays PARTIAL**.
