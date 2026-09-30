# LWB317-RE-MAP-002 — 0.3.17 Map scan lifecycle/state machine

Date: 2026-09-30

## Result

State: `EXACT_CONTRACT` for the host-visible lifecycle and recovered SQLite
transaction boundaries. Game traversal/native-capture internals that cannot yet
be established are explicitly left `UNKNOWN` and require the current-client
compatibility/live phases.

Reference executable SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary machine-readable evidence:

- `state-surface.json` —
  `36A3CCE9F90F75C8CAA471FFEEC71CD11C80B21FE3D838692EDADFC31E09011F`;
- `concurrency-handler-discovery.json` —
  `B1CD918206ECB392C241E775D7DEC18CF70FD3FC763E920D6C4A1800EA148D43`;
- `concurrency-handler-summary.json` —
  `F97DC21F04C8D242F35DBBC7248D0EB7A0B44717BA9FBCE6E31D88F918B4499A`;
- `state-handler-discovery.json` —
  `AB8A55B16BC3559B447E7520CF96C7A5459906B62A5B3CF68C8E94EFF8F532E4`;
- `progress-static-contract.json` —
  `6BECD72E83EEBC30D943BAC4C7739AEB51D6B94F5EBD59CC9D071DE1B374E44B`;
- `storage-flow-surface.json` —
  `1838DFF376C7883D701472347D25B8FD9E813A1AE6268DC1794C97E3C5EC9D89`;
- `storage-flow-handler-discovery.json` —
  `CC01FA4A377DBB07C7F2ADDC48BE92C7EECDB23B19AD7E027449EE9D125DCB04`;
- `storage-flow-handler-summary.json` —
  `1B442E9D3250BD1452B9C91BCF6011082ECDDAA653CA7A9CD1686483FC4F66B0`.

## Current function map

The public command handlers and the deeper current services are distinct:

- `map_scan_start`: `0x143D67-0x144C41`;
- start service: `0xFA09E-0xFC643`;
- `map_scan_status`: `0x159925-0x15A27B`;
- status/state service: `0xF8CB2-0xFA09E`;
- shared state normalizer: `0x42612D-0x426A05`;
- related state constructors: `0x426A05-0x426D07` and
  `0x426D67-0x427069`;
- capture/progress/terminal worker: `0x427120-0x428A92`;
- `bridge://map-scan-status` emitter: `0x428A92-0x428D90`;
- stop service: `0x42AB3A-0x42B1C1`;
- clear service: `0x42B4FF-0x42B787`;
- cancel transaction: `0x3C4964-0x3C50FE`;
- clear-server transaction: `0x3C61BE-0x3C68DF`;
- progress persistence: `0x3D1662-0x3D1A59`;
- successful direct publication: `0x3CF96B-0x3D0A03`;
- failed-scan preservation: `0x3D98FA-0x3DA298`.

These are current 0.3.17 RVAs. Historical 0.3.1 addresses are not reused.

## Start gates

The start service first tests game/runtime availability. Current control flow
at `0xFA105` branches to the exact backend error
`GAME_CONNECTION_UNAVAILABLE` / `game connection unavailable` when the game
connection cannot be used.

With a connection present it calls the active-scan predicate at `0xFA112`.
When a scan is already reading, current code returns
`SCAN_RUNNING` / `map scan already running` at `0xFA128`.

The same current service contains and reaches exact validation/errors for:

- `scanMode`, accepting the current `normal` / `fast` vocabulary;
- `INVALID_SCAN_MODE` / `map scan mode must be normal or fast`;
- `INVALID_SCAN_TYPES` / `no valid map scan types selected`;
- `SERVER_UNAVAILABLE` / `current server id unavailable`;
- `MAP_SIZE_UNAVAILABLE` / `world map dimensions are unavailable`;
- `WORLD_MAP_FAILED` / `failed to enter world map`;
- `MAP_SCAN_START_FAILED`;
- `MAP_SCAN_REJECTED`.

The provider boundary is current and explicit: the start path references and
invokes `enterWorldMap`, then constructs the scan request and invokes
`startMapScan` (the latter at the current service site around `0xFBB2B`). A
rejected provider result and a returned block-count mismatch are not treated as
successful scan starts.

The host state constructed around a successful start carries the current
server/world/tile information, selected types, scan run id, block counters,
mode/concurrency/rate/progress and native-capture readiness/pending/dropped
fields evidenced by the current binary.

The mode-to-concurrency mapping is now independently exact in current bytes:
the normal path loads `8` at `0xFAB2F`; the `fast` branch compares exact
`fast` at `0xFC49C` and loads `0x14` / `20` at `0xFC4A8` before rejoining the
same state construction path. Therefore `normal=8`, `fast=20` is current
0.3.17 host behavior, not a frontend-only default.

## Resume/restart

The 0.3.17 public start handler still implements the old two-factor resume gate,
but this was revalidated from current instructions rather than inherited.

At `0x14431A` the handler reads request field `resume` and accepts the resume
branch only when it is a JSON boolean whose value is `true`. False, missing or
another JSON type takes the fresh-start branch at `0x144705`.

Only after request `resume === true` does the handler read current state field
`resumeAvailable` at `0x14434C`. If that state value is absent, not boolean or
false, the handler again takes the fresh-start path. Therefore:

`resume path = request.resume === true && state.resumeAvailable === true`.

The current state constructors/reset/terminal paths inspected at `0xFB694`,
`0x4269CB`, `0x426C83`, `0x426F5E`, `0x42869F`, and `0x42AFCF` serialize
`resumeAvailable=false`. No current static true producer has been found.

That is intentionally narrower than saying resume can never occur: the public
branch is present, but `resumeAvailable=true` remains `UNKNOWN`/unobserved in
the recovered current static paths and must be treated unavailable unless a
current provider/live path proves otherwise.

## Status and derived progress

The shared normalizer revalidated the historical arithmetic in current
0.3.17. Exact current constants are:

- completed percent: `100.0`;
- active scale: `1000.0`;
- tenths divisor: `10.0`;
- active clamp: `98.0`.

The shared presentation normalizer independently contains the recovered base
remainder relation:

`unreadBlocks = max(totalBlocks - completedBlocks - failedBlocks, 0)`

For progress:

- `totalBlocks <= 0` -> `0.0`;
- a completed terminal state with a positive total -> `100.0`;
- otherwise the current instruction sequence computes the bounded
  `(completedBlocks + failedBlocks) / totalBlocks` fraction, applies the
  1000/tenths rounding structure and clamps active progress to `98.0`.

The exact arithmetic and constants are fail-closed in
`inspect_map_progress_contract.py`; the artifact deliberately calls the helper
target `roundLike` unless its imported/runtime identity is separately needed.

`retryCount` is notable: it is a frontend default field (`2`) but the exact
0.3.17 executable has no recovered `retryCount` string. It must not be promoted
to a host persistence/state guarantee from the frontend default alone.

### Current live-progress update path

A separate current update path at `0x4296C7-0x429CE9` supplies the actually
published in-progress counters before the common serializer. It is stricter
than the base normalizer and was recovered after the first MAP-002 pass:

- `completedBlocks` and `failedBlocks` are individually bounded to
  `0..totalBlocks`;
- if their sum exceeds the total, the host returns exact
  `INVALID_SCAN_PROGRESS / completed and failed map blocks exceed the scan total`
  at `0x429781-0x4297A1` rather than silently accepting impossible progress;
- `remaining = total - completed - failed`;
- `inflightBlocks` is bounded by both configured concurrency and `remaining`;
- the emitted live `unreadBlocks` is
  `max(total - completed - failed - inflight, 0)`;
- the emitted `readBlocks` is the normalized completed-block count;
- the progress transaction `0x3D1662-0x3D1A59` is called before the updated
  public state fields are assembled/published;
- active `progressPercent` still uses the recovered completed+failed fraction,
  tenths rounding and `98.0` clamp;
- `scanRate` is derived from completed blocks over
  `max(now - startedAt, 1)`, rounded to two decimal places. It is not an
  arbitrary provider-supplied rate.

The local 0.3.17 control plane therefore persists normalized progress before
raising its state-changed event and uses the live-update unread formula while a
scan is active.

## Stop / cancel

The current stop service begins from shared scan state. When a live scan/run is
present it calls current cancel transaction `0x3C4964-0x3C50FE`.

That transaction contains exact current SQL:

`UPDATE scan_runs SET status='cancelled',error=?1,updated_at=?2 WHERE id=?3 AND status IN ('running','paused')`

followed by:

`DELETE FROM scan_records WHERE run_id=?1`.

The same function contains the ordered transaction/log markers
`begin cancel map scan`, `cancel map scan`, `clear cancelled scan staging`, and
`commit cancel map scan`.

The host stop path constructs a non-reading/idle state, serializes
`resumeAvailable=false`, and, when the game connection/provider is available,
reaches `stopMapScan`. It emits the resulting state through the current
`bridge://map-scan-status` emitter before returning it. Provider stop therefore
belongs behind the provider adapter; local cancel/persistence behavior does not
require pretending that the game stop succeeded when it is unavailable.

## Clear/reset and ownership

Current clear service `0x42B4FF-0x42B787` first inspects shared state. If
`isReading=true`, it returns the exact current
`SCAN_RUNNING` / `stop the map scan first` error before touching server data.

The service requires a positive requested server and verifies it against the
current shared-state server/source conditions before entering the storage
clear. Requests that do not satisfy the current server ownership checks take
the current server-unavailable error path rather than clearing another
server's data.

The accepted clear path calls current storage transaction
`0x3C61BE-0x3C68DF`, whose exact SQL contains, in transaction order:

- `DELETE FROM scan_runs WHERE server_id=?1`;
- `DELETE FROM map_records WHERE server_id=?1`;
- commit.

`scan_records` and `scan_blocks` are owned by `scan_runs` through foreign keys
with cascade delete in the recovered schema, so deleting the server's runs is
the staging/checkpoint ownership boundary rather than a separate fabricated
public delete.

After successful clear the service resets shared Map state to an idle shape and
emits `bridge://map-scan-status`.

## In-progress staging and active query ownership

An active scan is query-visible through staging only when all current conditions
hold:

- effective server is positive;
- shared state `isReading` is true;
- shared state `serverId` equals that effective server;
- `scanRunId` is a nonempty string.

Current helper `0x42BCBE-0x42BDD1` implements this test. Search, summary and
options use it. If the predicate fails they read the published `map_records`;
if it succeeds they read `scan_records` for that run/server. This prevents an
unrelated server or stale run from becoming the active query source.

## Successful terminal publication

Current completion transaction `0x3CF96B-0x3D0A03` reads current run
`totalBlocks`, `completedBlocks` and `failedBlocks`. It exposes exact
`INCOMPLETE_SCAN` failures for a direct scan with failed batches or incomplete
block coverage; those paths do not get promoted to successful publication.

For an eligible completed run, the current function's ordered instruction/
string sites establish this transaction sequence:

1. `begin direct scan completion` (`0x3CFD80`);
2. for each selected kind, delete the existing published kind/server records
   using `DELETE FROM map_records WHERE kind=?1 AND server_id=?2`
   (`0x3CFE8C`);
3. insert the selected run's staged rows from `scan_records` into
   `map_records` (`0x3D013D`);
4. publish marker (`0x3D0363`);
5. atomically update the still-running run to
   `status='completed', error=NULL` (`0x3D03E5`), failing if it disappeared or
   is no longer running;
6. clear that run's staging rows (`0x3D04FF`);
7. prune other scan runs for the same server using
   `DELETE FROM scan_runs WHERE server_id=?1 AND id<>?2` (`0x3D055B`);
8. commit (`0x3D07D6`).

This proves that published data is replaced per selected kind only at a
successful terminal publication boundary; in-progress staging is not the same
as durable published state.

## Failure preservation

Current failure transaction `0x3D98FA-0x3DA298` updates a still-running run to
`status='failed'` with an error and performs
`INSERT OR REPLACE INTO map_records ... SELECT ... FROM scan_records` before
clearing that failed run's staging and committing. The associated exact markers
are `begin preserve failed map scan`, `preserve failed map scan records`,
`clear preserved map scan staging`, and `commit preserved map scan`.

This is a different contract from successful per-kind replacement: rows already
captured by a failed scan are preserved/upserted without first deleting the
server/kind's previous published set. The local implementation must retain this
distinction.

## Progress persistence and current-server failure

Progress transaction `0x3D1662-0x3D1A59` updates
`completed_blocks`, `failed_blocks`, an optional error and `updated_at` only
while the run is still `running`; otherwise it exposes `map scan is not
running`.

The current status service contains the exact failure text
`current server changed during map scan`, and the capture worker contains
`map scan failed` plus `native world capture session stopped`. These are
current lifecycle failure boundaries; live behavior must still establish which
current-client circumstances reach each one.

## Native capture/provider boundary

Exact current bytes contain and actively reference:

- `map.native.capture`;
- `nativeCaptureReady`;
- `nativePendingRecords`;
- `nativeDroppedRecords`;
- `native world capture session stopped`.

The capture worker ingests staged records, updates progress, performs the
terminal transactions above and emits status events. Provider/Lua-style names
such as `XluaBridgeNativeStart`, `__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN`,
`__XluaBridgeNativeWorldCapture`, `XluaBridgeMapScanTick`,
`XluaBridgeNativeUpdate`, and `XluaBridgePoll` are also present in exact bytes,
but the current string-xref pass did not recover direct code references for
those names. Their lexical presence is not promoted into an invented current
wire protocol.

## Revalidated vs historical 0.3.1

The following historical behaviors are now classified **unchanged/present in
0.3.17** on current evidence:

- duplicate-start `SCAN_RUNNING` gate;
- missing-game `GAME_CONNECTION_UNAVAILABLE` gate;
- request `resume` plus state `resumeAvailable` two-factor gate;
- no currently recovered true `resumeAvailable` constructor;
- base and live-update unread/progress derivation, exact progress overflow
  rejection and the 98% active clamp;
- cancel transaction clears staged rows and marks the run cancelled;
- clear refuses while scanning and owns data by server;
- success replaces selected published kinds from staging at the terminal
  transaction;
- failed scans preserve captured rows without the successful replacement step;
- active query source is the matching nonempty current scan run only.

Historical RVAs themselves are rejected and were not copied.

## Still unknown after static recovery

The exact host establishes the control/data plane but does not yet justify
inventing these game-side details:

- current-client block geometry/traversal generation beyond the recovered
  world/tile size inputs;
- retry/backoff policy and whether the frontend's `retryCount:2` is meaningful
  to the current host;
- exact normal-vs-fast provider execution mechanics;
- game/provider transport grammar beneath `enterWorldMap`, `startMapScan`, and
  `stopMapScan`;
- native capture queue/budget implementation details beyond the recovered
  status counters and ingestion boundary;
- a live producer of `resumeAvailable=true`;
- current-client conditions that produce every failure/terminal branch.

These are inputs to the compatibility and bounded live-validation stages, not
reasons to fabricate provider behavior in the local implementation.
