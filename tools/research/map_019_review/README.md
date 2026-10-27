# MAP-019 diagnostic harness

All files in this directory and the changes to `tools/live-harness/` are
explicit diagnostics. They do not change production scanner defaults. Only the
primary worker operates a live session; do not run a second owner concurrently.
The native runner preserves its existing exact ownership/admission/restoration
gates. It does not authorize account actions or desktop input.
The two MAP-019 scenarios force the existing background-only launcher option.

Build after the previous task-owned native host and game have stopped:

```powershell
dotnet build tools/live-harness/Native.csproj -c Release --no-restore
```

All ten individual kinds, two current-method observations each, one owned session:

```powershell
$env:LWBRIDGE_MAP019_WORKLOADS='city,resource,monster,zombie_boss,dispatch,ghost,treasure,other,railway,truck'
$env:LWBRIDGE_MAP019_METHODS='direct,direct'
$env:LWBRIDGE_MAP019_ROUNDS='2'
$env:LWBRIDGE_MAP019_MAX_MINUTES='60'
$env:LWBRIDGE_MAP019_TELEMETRY='1'
python tools/live-harness/run_owned.py benchmark-review
```

Typical mixed and all-ten selections, same-session current/camera/camera/current:

```powershell
$env:LWBRIDGE_MAP019_WORKLOADS='city-resource,all-ten'
$env:LWBRIDGE_MAP019_METHODS='direct,camera,camera,direct'
$env:LWBRIDGE_MAP019_ROUNDS='4'
$env:LWBRIDGE_MAP019_MAX_MINUTES='180'
$env:LWBRIDGE_MAP019_TELEMETRY='1'
python tools/live-harness/run_owned.py benchmark-review
```

Optional source-supported coarse Zombie Boss candidate, only after reviewing
historical rejection reasons and current admission. This turns on an existing
internal source hook in the diagnostic host only; the game-v27 candidate has no
accepted accuracy or speed qualification:

```powershell
$env:LWBRIDGE_MAP019_WORKLOADS='zombie_boss'
$env:LWBRIDGE_MAP019_METHODS='direct,coarse-boss,coarse-boss,direct'
$env:LWBRIDGE_MAP019_ROUNDS='4'
$env:LWBRIDGE_MAP019_MAX_MINUTES='60'
$env:LWBRIDGE_MAP019_TELEMETRY='1'
$env:LWBRIDGE_MAP019_SERVER='2175'
python tools/live-harness/run_owned.py benchmark-review
```

The optional server view is limited to the owner's supplied 2175,2180,2198,2203.
It uses the existing read-only Map view transition, records transition timing,
returns to the task-owned original server/home view in a separate cleanup token,
and reports exact camera equality independently. Remove `LWBRIDGE_MAP019_SERVER`
for a home-server benchmark. A view transition is not a march or relocation.

Raw source acquisition shape experiment (160,80,80,160 areas in one session):

```powershell
Remove-Item Env:LWBRIDGE_MAP019_SERVER -ErrorAction SilentlyContinue
python tools/live-harness/run_owned.py shape-review
```

Only this scenario transforms an isolated copy of the probe with unique exact
source seams. It retains serial requests and the production typed City/Resource
collectors, validates 63/125 replies and the 10,000-AOI union, and retains each
raw receipt. Resource detail hydration and product publication are excluded;
its speed and field comparisons are raw discovery only. The production C#
63-reply admission remains unchanged. The private source and original/private
digests are saved with the session. No larger-than-160 variant is generated.

The matrix alone allows a 1–180 minute bound; the runner rejects larger values
before native startup. Existing scenarios retain historical deadlines and
without matrix environment variables retain historical workload/method behavior.
`benchmark-all-ten` and `benchmark-city-resource` are also individual scenarios. `benchmark-all-nine` remains accepted only so historical nine-kind evidence can be reproduced; current qualification uses all ten kinds including `other`.
Round ordinals are global across workloads so databases and receipts are unique.
The default review is 12 workloads × 4 alternating direct/camera rounds: all ten current kinds plus City+Resource and all-ten. Configure
only a bounded workload subset for the intended experiment.

`benchmark.json` records requested/effective method, workload, round, global
ordinal, actual command-plane terminal state, summary and independent camera
snapshots. `benchmark-N.db` contains the genuine published data. Correlated point
and mobile native receipts are copied per round; complete mobile admission
accepts bounded serialized repairs up to two passes and all 625 verified
rectangles, rather than requiring exactly 625 sends. Direct Railway-only uses
the mobile receipt, including a valid empty train set. Mixed direct acquisition
retains both mobile and point evidence. Per-kind detail requests remain part of
the actual production path, not replaced by fixtures.

Negative review rounds are saved. The harness may continue only after a settled
failed/cancelled direct scan, exact managed Stop, a still-admitted owner, an
independent matching camera checkpoint, and explicit camera/cache restoration
flags on every observed same-run native receipt. Missing cleanup proof,
validation errors after completion, candidate failure, owner drift and changed
camera abort the owned matrix. `failure-cleanup.json` is mandatory evidence for
any continued negative. Coarse candidate receipts omit native scanRunId/serverId;
their weaker diagnostic binding is explicitly recorded as exact owner plus
capture window, with requestId admission still performed by the native reader.

`diagnostic-launch-hashes.json` identifies actual runtime DLL hashes and source
files present at launch. Source-at-launch hashes do not establish compilation
provenance; inspect the build command and actual DLL hash. Completion source
hashes are separately named because later permitted harness edits may differ.
This distinction also applies to attempts launched before this hash extension.

Process telemetry is sampled from the exact game PID. Before/after CPU seconds,
working/private bytes and one-second sampled memory maxima are native process
observations. `peakWorkingSetBytesProcessLifetime` is a lifetime peak, never a
per-round peak. Average machine CPU divides observed process CPU by elapsed scan
time and advertised logical processor count. These counters do not measure
network bytes or FPS.

The opt-in Lua wrapper observes unique `UnityEngine.Time.frameCount` values and
`unscaledDeltaTime`; it writes cumulative counters at most once per second.
Snapshots include `idle-before`, a three-second idle interval, `before` and
`after`. Difference cumulative sample/count/sum counters for each interval.
The cumulative maximum is not subtractable and cannot supply a per-round
maximum. This is timing of frames observed by the probe, not an assertion of
complete presentation FPS. A missing/stale snapshot is missing telemetry.
The instrumentation adds modest observer work, which should be disclosed in
performance comparisons. Exact wire bytes require separate packet/transport
measurement; planned AOI counts and JSON file sizes are not wire-byte receipts.

Offline request shape/combined-sweep plans, their limits and their commands are
in [REQUEST_PLAN.md](REQUEST_PLAN.md). They never send requests. Current point
plans cap at 160 AOIs; the combined hypothesis has 16 AOIs per existing Truck
rectangle and does not increase the accepted pacing. Neither its geometry
tests nor controlled Lua tests are native accuracy positives.

```powershell
python -m unittest discover -s tools/research/map_019_review -p 'test_*.py' -v
```
