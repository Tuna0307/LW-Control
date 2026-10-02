# PM-025 — navigation review and next large UI assignment

Project-lead decision: **CHANGES_REQUIRED** for NAVIGATION-001 at
`305240e22c963f64fe4f6522696792acbed2db91`. Preserve its useful tab-cache correction;
one request-lifetime regression must be corrected before focused acceptance.

## Reproduced finding

Current `MapDataPage.jsx` search effect replaces its previous local cancellation
flag/cleanup with a request generation and returns no cleanup. A generation is
advanced for a new search, a tab switch or a server transition. When
`backendAvailable` becomes false, the effect returns before advancing it.
The pending request therefore remains authorized to apply rows/total/error and
clear loading after its originating effect has been disposed.

The independent lead case reuses only the delivered persistent hook runtime and
adds a props transition while the initial actual production search is deferred.
The same test runs the immutable pre-navigation component and current production:

| Case | Pre-navigation baseline | Delivery 305240e |
|---|---|---|
| Pending success after backendAvailable false | Reply ignored; state unchanged | Obsolete row and total applied; loading cleared |
| Pending rejection after backendAvailable false | Reply ignored; state unchanged | Obsolete error applied; loading cleared |

Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-PM-025/check-availability.mjs` and
`availability-results.json`. Results are actual-component synthetic local
callback/effect evidence, not browser/native observations. The current-only
regression is established independently of the original's backend connection
contract. Request/effect ownership must remain fenced without removing correct
tab-generation ordering. Disposal/unmount and provider replacement also need
distinguishing coverage in the correction; their failures are not yet claimed
as separately executed lead cases.

## Checks and retained scope

Lead replay passed navigation (six baseline defects, zero campaign failures,
17 searches), its evidence validator, 472 filter comparisons, 216 Checking
display comparisons, 384 input fences and eight original producer cases.
Canonical `npm.cmd run check` and `check:production-build` pass at the submitted
fingerprints. Lead inspected the production diff, exact saved source contract,
browser record and populated Truck screenshot. Browser interactions were not
independently rerun; worker browser evidence remains attributed to the worker.

Original cache/search/server locators and pinned asset identity are unchanged.
No accepted filter/table scope is revoked. No game/native action was executed.
The lead did not change production code or rewrite historical evidence.

## Next assignment

Owner requests a large worker task. Assign `LWB317-UI-MAP-INTERACTIONS-001`:
request-lifetime correction first, then exact local search/name interactions,
per-kind selection lifecycle and Scheduled Plunder presentation, followed by
integrated UI verification. A bounded campaign permits coherent sub-milestones
without restarting unrelated Automation/AFK or native functionality.

The worker may use two subagents with exclusive file ownership. Main worker
integrates code and writes masters; project lead retains final acceptance.
