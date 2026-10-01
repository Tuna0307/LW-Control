# Loop work queue

Only the project lead should normally change what is authorized here.

## Current authorization

| Campaign / work item | State | File | Notes |
|---|---|---|---|
| LWB317-UI-HOME-STATES-001 | READY | `docs/work-items/LWB317-UI-HOME-STATES-001.md` | Owner manually dispatches a fresh worker chat; one bounded UI-only block, not Loop authorization; no game/native lifecycle work |
| LWB317-UI-CAMPAIGN-8H | ACCEPTED | `docs/LOOP_CAMPAIGN_8H.md` | Static UI recovery accepted; direct original post-auth pixel validation remains blocked by the original auth boundary |
| LWB317-COMPAT-MAP-V22-001 | COMPLETE | `docs/reviews/2026-09-30-LWB317-COMPAT-MAP-V22-001.md` | Installed v22 Map compatibility statically revalidated; no production Map change required |
| LWB317-LIVE-MAP-V22-001 | AWAITING_REVIEW | `docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-001.md` | Fresh v22 acquisition/navigation/query/clear proof complete; later completeness work shows the 678-row population was not a trustworthy full Resource census |
| LWB317-LIVE-MAP-V22-002 | AWAITING_REVIEW | `docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-002.md` | Same-session active stop/clear continuity and second Resource start/stop/clear are live-proven after the minimal current-client world-state serialization fix; no next Map task is opened here |
| LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001 | AWAITING_REVIEW | `docs/reviews/2026-09-30-LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001.md` | Strictly offline preparation for the next Resource completeness proof; diagnostic accounting/full-row capture is implemented but not live-validated, and no next live task is authorized by this row |
| LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001 | AWAITING_REVIEW | `docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001.md` | Resource same-tick restore defect and Resource-scoped correction are LIVE_PROVEN by two corrected full-world scans; corrected current path reconciles at 8008/8007 unique with zero rejected candidates and full bounds; game-universe/original-private-traversal completeness remain UNKNOWN |
| LWB317-MAP-UI-INTEGRATION-001 | AWAITING_REVIEW | `docs/reviews/2026-09-30-LWB317-MAP-UI-INTEGRATION-001.md` | Clean reconstructed Map UI is connected to the production backend; Resource manual Start/progress/completion/query/pagination/filter/Clear and post-clear connected readiness are LIVE_PROVEN with a fresh 7,960-row snapshot |
| LWB317-MAP-UI-PRODUCTIONIZE-001 | ACCEPTED | `docs/reviews/2026-10-01-LWB317-MAP-UI-PRODUCTIONIZE-001.md` | Project-lead accepted at `086757e36562b76d7e45b857a282c51267e16171`; reconstructed `LWBridge.UI-0.3.17` is the normal packaged Desktop frontend; zero-argument launch identity/native bridge/all primary pages and Resource flow are LIVE_PROVEN, with legacy WebUI retained only for deliberate recovery/reference |
| LWB317-RE-MAP-001 | AWAITING_REVIEW | `docs/GOAL_CAMPAIGN_PHASE2_MAP.md` | Ordered Map continuation complete through restart/reopen, live server jump/return, remaining-v22 category acquisition, safe actions and current-server Auto Scan. Closeout retains direct canonical-WebView non-Resource row rendering and live UI marks/export as IMPLEMENTED_NOT_VALIDATED; Railway/Ghost/Treasure positive rows are BLOCKED_BY_LIVE_STATE; Treasure claim/status and Ghost preparation are BLOCKED; Resource game-universe/private-traversal completeness remains UNKNOWN |
| Other LWB317-RE-* | BLOCKED | not assigned | Do not begin another subsystem until the Map Goal is closed |
| Auth/login/licensing reconstruction | OUT_OF_SCOPE | n/a | Boundary/dependency only; no recreation or bypass |

When the 8-hour campaign stops, the worker must change its campaign state from
`ACTIVE` to `AWAITING_REVIEW`, commit/push that checkpoint, and report
`WAITING_FOR_PROJECT_LEAD`.

## State meanings

- `ACTIVE` — project-lead-authored campaign may execute its internal ordered stages.
- `READY` — one bounded work item may execute now.
- `IN_PROGRESS` — informational state while a worker is active.
- `AWAITING_REVIEW` — worker finished; project lead must review.
- `BLOCKED` — worker must not start.
- `ACCEPTED` — project lead accepted the result.
- `OUT_OF_SCOPE` — intentionally not part of the reconstruction target.
