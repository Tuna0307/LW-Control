MAP-019 offline request planner
==============================

`plan_requests.py` contains no RPC, networking, native runner, process control or
production integration. It writes inspectable LOD0 request plans only:

```
python tools/map_019_review/plan_requests.py --out artifacts/map019/request-plan
python -m unittest discover -s tools/map_019_review -p test_plan_requests.py -v
```

`summary.json` includes source hashes/line anchors and explicitly records that
native speed, byte counts, accuracy, FPS and memory were not measured. Six JSON
files contain each complete plan, including indices and native encoded bounds.
Geometry checks prove only coverage of the planned AOI index space. They do not
prove that the game/server returns all records or complete fields.

The useful changed hypothesis is combining a mixed Truck+point sweep. Current
source sends a 625-rectangle Truck sweep, one central AOI index per rectangle,
then a 63-request, 160-index point sweep. The combined plan retains the exact
625 Truck centers/bounds but gives each request its rectangle's sixteen AOIs.
It therefore plans 625 rather than 688 initial world.get.block requests (63
fewer, 9.16%) and 10,000 rather than 10,625 index entries (625 fewer, 5.88%).
This is a request-count reduction, **not a speed or accuracy positive**.
In particular 625 point capture callbacks replace 63, a 9.92x increase; expensive
point collectors might cost more than the saved requests. Actual native reply
capture and correlation must be implemented and qualified before a live trial.
The earlier pipelined cache sampler failed because overwritten state was polled;
another identical sampler or a larger-than-160 request would not test this new
hypothesis. The plan rejects areas above 160.

Risk must be reported: AOIs per Truck request rise from one to sixteen, even
though total mixed indices fall. The plan keeps the existing 20/s maximum,
rectangles, LOD and current-server context. The march push has no echoed
rectangle/request ID, so preserve existing bounded response fences and
serialized repairs; do not assign lost pushes to requests by FIFO guesses.
Capturing point replies requires a source-bound hook before deletion, complete
indices/body/fields and independent server/world/LOD attribution. Two same-session
rounds against both current and camera scans, with drift and field comparisons,
remain mandatory. Current C# independently requires exactly 63 replies, so the
prototype cannot be passed through production admission without a phase-2 change.

Fewer-area and rectangle plans also quantify tradeoffs: 80 linear AOIs require
125 requests; 10x16 compact AOIs require 70; 8x20 require 65. With the observed
160-index cap, 63 is the arithmetic minimum for enumerating 10,000 distinct AOIs
once. It does not establish minimum elapsed time. Rectangle geometry may affect
march delivery and response cost, which only a native experiment can measure.

All counts exclude repairs, retries, camera fallback, startup and kind-specific
details/enrichment/list requests. This planner does not replace dedicated
kind-specific routes such as the Zombie-Boss-only coarse path.
