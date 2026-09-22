# LWB-R7-138 — Duplicate Start ownership and Stop timing matrix

**Date:** 2026-09-22
**Base revision:** `a12f54b0d76cdb3a807d942f3d544250a222dc37`
**Scope:** close B05 and B06 acceptance without changing production behavior.

## Result

Committed-checkout deterministic test SHA-256:

`FD9BB70E2E607A2B7EC6DCC5371F4F88787FBB303C1399F39520F699FD866CD9`

No production change was required. The shipped scan owner and cancellation engine already enforce the required behavior; R7-138 adds the missing deterministic acceptance matrix.

## B05 — Duplicate Start and mid-scan type changes

The existing active-scan ownership check previously retried the same type. R7-138 changes the conflict attempt so the active run is:

- selected type: `resource`
- backend-selected mode: `normal`
- concurrency: 8
- phase: `scanning`

while the conflicting Start requests:

- selected type: `city`
- caller mode field: `fast`

The second Start must fail with the existing active-scan ownership contract:

- code: `SCAN_RUNNING`
- message: `A map scan is already in progress.`

After that rejection the acceptance test requires:

- identical `scanRunId`;
- identical active `selectedTypes=[resource]`;
- identical backend-selected `scanMode`;
- identical `scanStrategy`;
- identical concurrency 8;
- exactly one live-context acquisition.

Therefore a changed-type Start cannot replace, retune or partially mutate the owned scan.

**B05 -> PASS_CURRENT_OFFLINE**

## B06 — Stop early / mid / near completion

R7-138 adds a controllable five-block scan source over a deterministic 100x20 synthetic world. One capture is deliberately held in-flight while the preceding blocks, if any, are allowed to checkpoint.

### Early

- completed before Stop: 0
- source calls at Stop: 1
- durable checkpoints: 0
- final readBlocks: 0
- final unreadBlocks: 5

### Mid

- completed before Stop: 2
- source calls at Stop: 3
- durable checkpoints: 2
- final readBlocks: 2
- final unreadBlocks: 3

### Near completion

- completed before Stop: 4
- source calls at Stop: 5
- durable checkpoints: 4
- final readBlocks: 4
- final unreadBlocks: 1

At every timing boundary:

1. exactly one capture is blocked in-flight;
2. `map_scan_stop` waits for terminal cancellation;
3. returned status is `phase=idle`, `isReading=false`, `failedBlocks=0`, `inflightBlocks=0`;
4. the canceled capture never becomes a checkpoint;
5. all retained checkpoints use the recovered store value `status=completed`;
6. after Stop returns, neither source-call count nor checkpoint count can increase;
7. previously published City data is still present, proving the stopped partial run did not publish/replace it;
8. `resumeAvailable=false` remains truthful.

R7-130 remains the live authority for the public Stop boundary: a real active Fast scan was stopped while still at 0/2500 and returned terminal idle.

**B06 -> PASS_CURRENT_PLUS_HISTORICAL**

## Test-harness failure-first notes

Two temporary test-harness issues were found while building the matrix and were removed before the final candidate:

- a manual cancellation registration around a gate TCS did not unwind reliably in this module-initializer runner; the final source uses `Task.WaitAsync(cancellationToken)`;
- asynchronous observation waits were brittle under the module-initializer runner; the final acceptance uses bounded `SpinWait` for capture entry and a short synchronous post-terminal observation period.

No production defect or production code change resulted from these harness issues. All temporary trace output was removed before final validation.

## Validation

Clean candidate:

- `git diff --check` — PASS
- Release build — 0 warnings / 0 errors
- deterministic groups:
  - profileRouting = true
  - persistence = true
  - requestLifetime = true
  - mapPersistence = true
  - mapContract = true
  - bridgeControlPipeContract = true
- `failures=[]`
- deterministic runtime: 40.86 s
- final gameRunning = false
- final launcherRunning = false

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-22-r7-scan-ownership-stop-timing.json`

## Remaining technically actionable acceptance gaps

After B05/B06, the main read-only technical gaps are:

- B07 — deliberate bridge loss/recovery during an active scan;
- B11 — native add/update/remove/movement transition matrix;
- C04 — mark/unmark -> rescan -> restart -> relocate;
- C07 — vanished/replaced target failure branches.

Population-, authorization-, multi-account- and human-GUI-dependent gates remain separate.
