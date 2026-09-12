# Team workflow — PM review 16 Overview corrections

PM audited `0758006` and returned two source-confirmed defects. Successful live lifecycle paths and prior owner acceptance remain credited. [Detailed audit and acceptance criteria](reviews/2026-09-12-review-16-overview-zero-open.md). [AGENTS.md](../AGENTS.md) is mandatory.

## Ownership and stop gate

Web is the single implementer/researcher/technical verification owner. PM audits and maintains instructions; the owner chooses features and supplies only simple UI observations/screenshots. Web captures all technical evidence automatically. No separate Sol assignment. No Daybreak assignment: these are implementation defects, not exhausted binary-analysis questions or a transfer of restricted operations.

**Do not begin Player City or any Map Data work.** PM16-02 and PM16-01 are delivered/offline-tested as `LWB-PM16-001` and `LWB-PM16-002`; PM16-03 reconciliation is recorded as `LWB-PM16-003`. Corrected code revision `c3d77e2` is remote-verified and CI `34696597174` passed on that exact SHA. Return to PM/owner and stop. No automatic transition to cross-server, other shared controls or the historical queue. Whole Overview remains incomplete while S02/S03/S06 are unfinished; S05 runtime-name scope depends on actual visible consumers.

## Main prompt for ChatGPT Web

```text
Review 16 correction work is delivered. In C:\Users\chimw\OneDrive\Desktop\Github\LW-Control on research/offline-controller, preserve LWB-PM16-001/002/003 and the corrected code revision c3d77e2. PM16-02 process-incarnation ownership and PM16-01 selected-root integration are corrected/offline-tested; GitHub Actions 34696597174 passed on c3d77e2. PM16-03 reconciles the O01/O06 evidence, saved LWB-OVR-012 reproduction excerpts, corrected build/helper identity and remaining limits.

Return this package to PM/owner for audit. Do not run another owner/game test unless PM/owner explicitly requests one. Do not begin Player City, Map Data, cross-server implementation, broad original-parity work or another feature. Whole Overview is not 100% complete: S02 pending semantics, S03 full runtime Refresh Status and S06 actual cross-server travel remain open. Only the owner resumes subsequent scope.
```

## Repeatable continuation prompt

```text
Inspect the latest PM/owner response and actual HEAD/worktree first. If review 16 is still awaiting audit, do not redo PM16-01/02/03; preserve LWB-PM16-001/002/003 and return the package. Start no new feature until the owner explicitly resumes scope. If the owner assigns a new scope, re-read AGENTS.md and the updated planning documents before acting.
```
