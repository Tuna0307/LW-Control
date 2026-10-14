# MAP-014 native march acquisition — offline research

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
