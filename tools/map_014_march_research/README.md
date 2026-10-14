# MAP-014 native march acquisition — offline research

## Worker-7 pinned native handler/rect/clock audit, 2026-10-14

Read-only SHA-256-gated installed metadata and archived `CSHandleResponse`
IL identify the separate `WorldMarchDataManager.HandleWorldGetRectMarchInfos`
method (`0x0600327b`); `CSHandleResponse` (`0x060019a7`) can return before
dispatch. The offline `audit_receipt` now requires a source-labelled,
same-correlated **request → reply → manager handler → process** quartet
with correct class/token, runtime and native tick order, timestamps near
the capture clock, true March-manager `viewRect` and stable retained manager
rectangles. The typed compare-ready cohorts are bounded to at most one
second per sample interval to prevent slow stale populations appearing
simultaneous. Synthetic RED acceptance cases before changes are GREEN now;
**55 Python unit tests pass**. The later native 60-UUID snapshot is still
missing these necessary stages; six earlier mixed rounds remain too far
apart for a same-time source conclusion. See the qualification document for
exact paths, source limitations and archived losses.

## ce9fedf2 stale terminal / cross-stage collision fix

The synthetic checker initially **falsely passed** a future-arrival Truck
that was present in the point-ready native snapshot but missing from both
post-processing manager and independent oracle, and a `trainUuid` rebound
under a different `marchUuid`. Adversarial tests first reproduced these
failures on the old implementation. `audit_compare_ready_march.py` now
requires every point-ready typed March to be present at the terminal
stage (until a separate genuine terminal lifecycle source can justify its
departure), prohibits cross-stage train-key rebinding, and verifies typed
cohort counts, source manager membership and stage capture intervals.
It also pins `WorldMarch.GetMarchCurPosIndex():int32` token
`0x06003208` and `WorldScene.IndexToTilePos(int32)` token
`0x060039c3`, binding raw point indices and claimed tile positions,
and rejects final `arriveTs` within a one-second source-clock margin.
These are **fail-closed structural checks**, not genuine mobile positives.

The `102638` native receipt still has 60 unclassified point-ready March
UUIDs, without a before/processed source or trustworthy queue/reply/clock
barrier. Independent re-audit of the six Resource rounds confirms capture
timestamps lie within real camera windows, but 19 baseline-only versus
25 candidate-only points in pair 0→1 are **sequential** observations, not
simultaneous proof of spawn/expiry/misses. The WinError5 hard denial
remains in force. No game, installed scripts or production files were used.

## 342b3737 source-grounded typed compare-ready gate

`audit_compare_ready_march.py` adds **offline-only** `audit_compare_ready(receipt)`
for a *future* single-session independently source-observed Truck/Train
comparison. It cannot create real receipts, verify an observer's provenance,
or convert older snapshots into simultaneous evidence. It requires the
existing full March request/response/process/queue/timestamp receipt first,
then independently typed and overlapping position/identity cohorts, an
observed native server clock and final `TrainData.arriveTs` after that
clock. It rejects old `viewRect`, untyped TrainType, stale native runtime
intervals, reused March UUID for a different owner/Train/arrival, current
manager cache collisions and unexplained moving UUIDs. An admissible
movement must be source-observed from hash-gated
`WorldMarch.GetMarchCurPosIndex():int32` token `0x06003208`.

`sameSessionCompareContractPass=true` is possible only for the **synthetic**
offline positive in `test_audit_compare_ready_march.py`; independent
source authenticity and global accuracy flags remain false **unconditionally**.
The real 60-unclassified-UUID `102638` receipt remains nonqualifying, and
`103048` is a WinError5 journal-write denial with no new native comparison
receipt. Do not perform any recovery/game/script activity from this research
lane. Details and pinned IL evidence:
`docs/MAP_014_MARCH_FRESHNESS_QUALIFICATION.md`.

```text
python -m unittest discover -s tools/map_014_march_research -p 'test_*.py' -v
python tools/map_014_march_research/trace_installed_metadata.py
```

## New archived real-native October 14 passive receipt gate (checkout 62f45143)

Use `audit_passive_march_receipt.py` against
`artifacts/map014-lead-live/native-march-source-20261014-102638/live-resource/bulk-aoi-diagnostic-result.json`
to validate the real point-ready March manager snapshot without upgrading a
missing source-causal request chain into a pass. The actual receipt includes
60 uncapped unique native `WorldMarchDataManager` UUIDs, but **omits**
`marchSourceBefore`; the separate `march-source-before-camera.json` is only
a differently keyed camera checkpoint. It also lacks exact March rect network
request/response/processed events, source-backed StepProcessQueue.Count getter
provenance, native DateTime.Ticks and independent same-time active March oracle.
Both its saved queue counts being zero is not a completion proof. The checker
returns `mobileFreshnessQualified=false`, keeping baseline Truck/Train policy.

Optional `--archived-mixed artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149`
reports privacy-safe *counts* of exact UUID intersections against all six
earlier mixed SQLite receipts. Observed overlap is **zero** across Truck and
Railway in each archived round, but time gap is **8.06–8.23 hours** and new
manager IDs are unclassified: no expiry, missing-population or recall inference.
See `docs/MAP_014_MARCH_FRESHNESS_QUALIFICATION.md` for input schema and
exact native source references. The existing
`tools/map_014_lead_native/test_march_source_telemetry.py` source guard passes,
but repaired producer source does not retrospectively repair an old receipt.

```text
python tools/map_014_march_research/audit_passive_march_receipt.py artifacts/map014-lead-live/native-march-source-20261014-102638/live-resource/bulk-aoi-diagnostic-result.json --archived-mixed artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149
python -m unittest discover -s tools/map_014_march_research -p 'test_*.py' -v
```

## October 14 hash-pinned freshness qualification

See [the current source-causal qualification](../../docs/MAP_014_MARCH_FRESHNESS_QUALIFICATION.md).
The installed `Assembly-CSharp.rdl` SHA-256 is
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`,
matched by `trace_installed_metadata.py` with **no** game launch. Its pinned
fields include `viewRect` (`0x0400254f`), `cameraViewRect`
(`0x04002551`), `_messages` (`0x04002561`), `_messagesMergeDict`
(`0x04002562`), `_lastGetBlockMsgTime` (`0x04002566`) and
`_lastHandleBlockMsgTime` (`0x04002567`). These are inspected member
names/types, **not** proof that a specific camera movement sent or completed a
march request: opaque IL call operands remain unresolved.

`march_freshness_gate.py` supplies a strictly offline, source-attributed
**proposed** receipt evaluator for the prime's future owned passive native
experiment. It deliberately distinguishes March reply and message processing
from point AOI response; single-region success is never global mobile accuracy.
The added `test_march_freshness_gate.py` rejects old-region response, nonempty
queue, snapshot-before-process, cross-server attribution, and missing UUIDs
even when all point AOIs appear covered. No raw player IDs are written to Git.

## Source and causal boundary

The shipped `Assembly-CSharp.rdl` hash-gated metadata archive
(`artifacts/map-009-offline/map014-march-request-il.json`) identifies
`WorldMarchDataManager.OnUpdate` (`0x0600326c`),
`HandleWorldGetRectMarchInfos` (`0x0600327b`), and
`WorldGetMarchInfos` (`0x060032aa`). The independent
`UpdateViewRect` (`0x0600326e`) appears in
`map014-march-viewrect-il.json`; the separate
`WorldGetRectMarchInfosMessage.CSHandleResponse` route appears in
`map014-rect-march-protocol.json`. These are actual installed-game
metadata observations, although unresolved obfuscated tokens do not
establish all call-graph edges or imply that a particular request completed.

The product's bulk camera probe observes the *point* manager's
`isRecvViewPoints` and `hasReceiveViewPointsReply` flags at
`tools/current_live_resource_probe.lua:6347-6373`, then snapshots
`WorldScene.MarchDataManager.GetAllMarchesByCS` and filters current march
positions through the *point* `_curViewIndex` AOI set at lines 3532-3563
and 6255-6264. There is no march rectangle/queue-response admission check
in that path. C# accepts the March snapshot at
`src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:1950-1984`
after validating each included position, but does not verify the native
march manager has delivered all active marches for that camera rectangle.

Thus a full 10,000-cell **point** AOI union can coexist with incomplete
March reception. The saved six-round mixed benchmark contains Truck counts
of baseline 103/95/89 and FOV20 39/39/36, with zero block failures and all
10,000 point AOIs on every run. Exact baseline→candidate UUID comparison yields:

| Pair | Shared Truck UUIDs | Missing baseline Truck UUIDs | Missing with future native `arriveTs` | Extra candidate UUIDs |
| --- | ---: | ---: | ---: | ---: |
| 0→1 | 11 | 92 | 90 | 28 |
| 2→3 | 3 | 92 | 88 | 36 |
| 4→5 | 6 | 83 | 82 | 30 |

This is a reproducible deficiency of the candidate snapshot, not proof that
every omitted UUID is currently active. The separate Railway comparisons also
lose 2/1/1 baseline UUIDs with future source arrival timestamps.

## Archived replay

Run only on the saved receipts, without a game session:

```text
python tools/map_014_march_research/audit_march_pairs.py artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149
python -m unittest discover -s tools/map_014_march_research -p test_audit_march_pairs.py
```

The audit uses `record_key` and `server_id` from SQLite, compares UUIDs
against `data_json.uuid`, and normalizes plausible Unix seconds/milliseconds
before checking baseline `TrainData.arriveTs` relative to approximate candidate
completion. A future `arriveTs` is evidence against simple arrival-time expiry;
it does **not** independently prove the corresponding march was still
published in the candidate's *native* manager. The march `endTime` is a
movement-leg field and is deliberately excluded from expiry classification.

## Distinct passive native experiment for the prime

On a separately owned, protected native session, collect *passive snapshots*
around a single **existing camera navigation** with both baseline and FOV20:

1. Before navigation: server/world/clock, exact camera geometry, march
   `UpdateViewRect` bounds, the manager's current and previous rectangle,
   pending rect-request queue length, last receive/process timestamps,
   `GetAllMarchesByCS` exact UUIDs and source `TrainData.arriveTs`.
2. After **point** `isRecvViewPoints` + `hasReceiveViewPointsReply` transition:
   capture these same march fields *before* camera/home restoration. Attribute
   observed `WorldGetRectMarchInfosMessage` outgoing request and incoming
   `HandleWorldGetRectMarchInfos` response to the rectangle and server;
   record whether processing completes after point readiness.
3. Record one more state on the manager's **own** queue/reply/processing
   transition (if present), with the camera still at the target. Compare
   March UUIDs, rectangle intersection, and native timestamps. Restore
   exact camera/scripts under the sole session owner after completion.

Acceptance depends on a source-attributed march reply/manager processing
barrier, exact rectangle coverage (potentially including traversing line
segments), per-UUID identity/server, and final camera restoration. No
guessed network request, arbitrary delay, or global mouse/keyboard is needed.
If the native manager's queue finishes after point readiness and gains
unexpired UUIDs, the concrete product fix is to gate Train/Monster snapshot
publication on that validated **march** completion before restoring the
camera. If no march request occurs under FOV20, investigate `UpdateViewRect`
rectangle-change/subscription policy and widened view requirements first.

FOV20 mobile-kind default must remain disabled until the distinguishing
native experiment and a full paired product inverse pass.
