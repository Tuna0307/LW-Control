# LWB317-RE-MAP-001 — Map Goal closeout audit

**Date:** 2026-10-01  
**Branch:** `research/offline-controller`  
**Campaign starting SHA:** `086757e36562b76d7e45b857a282c51267e16171`  
**State:** `AWAITING_REVIEW`

## Ordered continuation checkpoints

1. Restart/reopen recovery began from `086757e36562b76d7e45b857a282c51267e16171`
   and its first checkpoint was committed at
   `5092959b9c7a3bb6204ec7b23370933dcbb9e781`.
   Independent review then identified that production uses Map317 rather than the
   retained Manual service; the canonical ownership/reconciliation correction was
   included in the next checkpoint.
2. Server jump/history plus canonical Map317 restart ownership were committed at
   `75515a5a853ba489d74661f0a84a378432639ff4`.
3. Remaining-v22 category acquisition and safe Map actions were committed at
   `4ccb9870e1f64894162abfc6f8c2809056bbe710`.
4. Auto Scan scheduler/canonical frontend recovery was committed at
   `b5e786d3e32235949cd111b815de66a51596ec29`.

Every checkpoint above was pushed to `origin/research/offline-controller` and
verified equal locally before advancing.

## Completion-criteria audit

The 0.3.17 Map frontend/host command surface, scan lifecycle, schema-v4
per-profile persistence, query/filter/sort/pagination, export, marks, history,
options, server jump, action boundaries and Auto scheduler have durable recovered
contracts. Historical 0.3.1 material is retained only where revalidated; the
0.3.17 frontend package is the authority for current UI claims. The versioned
`LWBridge.Map317` control/data plane is the production authority and the canonical
`src/LWBridge.UI-0.3.17` frontend is wired only to recovered commands.

Current-client v22 compatibility and bounded live validation now cover Resource,
City, Monster, Truck and Dispatch acquisition; Manual lifecycle including active
Stop/Clear; query/options/summary; coordinate navigation; server jump/return; and
a current-server Auto Scan cycle. Durable evidence exists under
`evidence/lwbridge-0.3.17/map/`. No attack, march, gather, purchase, alliance
share, plunder or reward claim was used by this campaign.

The Goal's research/control-plane deliverables are complete enough for
project-lead review, but this closeout does not claim every Map UI/provider path
LIVE_PROVEN. The remaining bounded Map work is explicit below. Therefore the Goal
is `AWAITING_REVIEW`; the project lead can either accept closure with these
documented validation/provider boundaries or authorize the bounded follow-ups.

## Final classifications

- canonical production frontend/host Map surface: `EXACT_CONTRACT`; Resource
  normal-launch UI flow `LIVE_PROVEN`;
- Manual scan lifecycle, active Stop and Clear: `LIVE_PROVEN`;
- Resource observable current-v22 route: `CURRENT_PATH_COMPLETE` / `LIVE_PROVEN`;
- Resource `GAME_UNIVERSE_COMPLETE`: `UNKNOWN`;
- exact original protected/private traversal equivalence: `UNKNOWN`;
- server jump same-server no-op and `2212 -> 2198 -> 2212` transition/return:
  `LIVE_PROVEN`; history contract `EXACT_CONTRACT`;
- restart orphan reconciliation/exclusive ownership: rebuild implementation
  policy with deterministic coverage, `IMPLEMENTED_NOT_VALIDATED` at live
  process-termination scope; exact public resume remains unavailable because no
  recovered `resumeAvailable=true` producer exists;
- saved Map rows across database reopen: deterministic persistence proven;
  selected category/result tab/browse-server UI persistence is `OUT_OF_SCOPE` for
  strict 0.3.17 parity because the recovered frontend does not persist it;
- City/Monster/Truck/Dispatch v22 acquisition/summary/options/search:
  `LIVE_PROVEN`; direct canonical-WebView positive-row rendering remains
  `IMPLEMENTED_NOT_VALIDATED`;
- Railway/Ghost/Treasure positive-row acquisition/rendering:
  `BLOCKED_BY_LIVE_STATE` in the campaign snapshot;
- coordinate navigation provider: `LIVE_PROVEN`; canonical row-button click scope
  `IMPLEMENTED_NOT_VALIDATED`;
- player marks/local persistence/query and City export format/native command:
  `EXACT_CONTRACT` with deterministic coverage; live UI-level execution remains
  `IMPLEMENTED_NOT_VALIDATED`;
- Treasure claim/status provider: `BLOCKED`; no claim attempted;
- Ghost plunder preparation provider: `BLOCKED`;
- march-follow, Dispatch alliance share and scheduled Dispatch/Truck plunder:
  `IMPLEMENTED_NOT_VALIDATED` live and intentionally not executed because they can
  affect gameplay/alliance state;
- Auto Scan recovered scheduler/config: `EXACT_CONTRACT`; bounded current-server
  Auto cycle and disable/clear/health cleanup: `LIVE_PROVEN`; integrated
  multi-server Auto cycle: `IMPLEMENTED_NOT_VALIDATED` (server jump/return itself
  is separately `LIVE_PROVEN`).

## Exact remaining Map work

If the project lead requires live UI proof beyond the Goal's recovered contract
and bounded-provider evidence, the next tasks are narrowly limited to:

1. direct normal-WebView rendering/filter/sort/pagination checks for live
   City/Monster/Truck/Dispatch rows in one bounded combined scan;
2. direct safe UI mark/unmark persistence/query and City export interaction;
3. optional assistant-owned mid-scan Desktop termination/reopen proof for the
   rebuild restart-safety policy (not a claim about exact 0.3.17 resume);
4. positive Railway/Ghost/Treasure acquisition/rendering only when live content
   exists;
5. provider recovery for Treasure claim/status and Ghost preparation if new exact
   source evidence becomes available, without live reward/plunder execution.

No other subsystem is authorized by this closeout.
