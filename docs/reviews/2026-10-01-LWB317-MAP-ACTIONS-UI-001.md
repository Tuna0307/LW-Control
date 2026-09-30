# LWB317-MAP-ACTIONS-UI-001 — recovered safe Map actions and canonical UI

**Date:** 2026-10-01  
**Branch:** `research/offline-controller`  
**State:** `AWAITING_REVIEW`

## Recovered UI surface

The hash-locked 0.3.17 `MapDataPanel-B4GXEND2.js` renders coordinate buttons,
City player-mark toggles and City Excel export. It also contains march-follow,
Dispatch sharing/scheduling, Truck scheduling and Treasure claim controls. The
last group is gameplay-affecting and is not promoted to live proof here.

The canonical React Map table now wires safe coordinate navigation for rows with
integer coordinates, City mark/unmark, and City export to the existing native
commands. Truck/Railway coordinate cells remain disabled in this safe UI slice
because the recovered panel can route march-like rows through march-follow; this
campaign does not live-exercise a potentially gameplay-affecting march.

City export uses the recovered 12-column header order and the Desktop native save
dialog path. Player marks remain local Map database state and search refreshes
after a mark change.

## Live safe action proof

`LiveCoordinateJumpProof` ran on a fresh assistant-owned v22 session at server
2212/world 0 and exercised the recovered current-client coordinate-navigation
callback with no scan, attack, plunder, claim/collect or messaging action. It
reported `attempted=true`, `proven=true`; this safe provider boundary is
`LIVE_PROVEN`.

The live category scan contained no Treasure rows, so read-only Treasure row
inspection is `BLOCKED_BY_LIVE_STATE` for this campaign snapshot. No Treasure
claim was attempted. Ghost preparation remains provider-blocked. Dispatch share,
scheduled Dispatch/Truck plunder and march-follow were not live-executed.

## Classification

- coordinate navigation provider: `LIVE_PROVEN`;
- canonical coordinate button wiring: `IMPLEMENTED_NOT_VALIDATED` at direct
  normal-WebView click scope;
- player marks/local persistence/query: `EXACT_CONTRACT`, deterministic coverage;
- City export format/native dialog path: `EXACT_CONTRACT`, deterministic coverage;
- Treasure inspection with a live row: `BLOCKED_BY_LIVE_STATE`;
- Treasure claim/status provider: `BLOCKED`;
- Ghost plunder preparation provider: `BLOCKED`;
- march-follow, Dispatch alliance share, scheduled Dispatch/Truck plunder live
  execution: `IMPLEMENTED_NOT_VALIDATED` and intentionally not exercised because
  they can affect gameplay/alliance state.

Evidence: `evidence/lwbridge-0.3.17/map/LWB317-MAP-ACTIONS-UI-001-live-coordinate-proof.json`.

