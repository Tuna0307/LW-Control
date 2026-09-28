# R8-121 — current-client Map all-eight Normal/Fast live proof

**Date:** 2026-09-27
**Status:** MAP ACQUISITION WORKING / LIVE PROVEN

## Scope

This checkpoint runs the existing production current-client Map acquisition implementation after the R8-120 Login/admission bypass. One owned Last War session executes the exact eight retained Map kinds in Normal mode, then the same eight in Fast mode:

- city
- resource
- monster
- truck
- railway
- dispatch
- ghost
- treasure

The proof requires each mode to complete all 2500 logical blocks with zero failed and zero unread blocks. Fast-mode published counts must also survive database reopen unchanged.

## Fresh live result

Normal completed:

- mode `normal`
- concurrency 8
- total/read blocks 2500/2500
- failed 0
- unread 0
- wall time 119.7333401 seconds

Normal published counts:

- city 6801
- resource 683
- monster 4432
- truck 168
- railway 1
- dispatch 77
- ghost 0
- treasure 1

Fast completed:

- mode `fast`
- concurrency 20
- total/read blocks 2500/2500
- failed 0
- unread 0
- wall time 111.1577866 seconds

Fast published counts:

- city 6801
- resource 854
- monster 3315
- truck 147
- railway 0
- dispatch 59
- ghost 0
- treasure 5

The reopened database reproduced every Fast count exactly.

Zero live rows for a kind are not treated as a failed acquisition route. `ghost` had no rows in either run and `railway` had no rows in Fast, but both kinds were part of the selected eight-type request, traversed the complete 2500-block world, and completed with zero failed/unread blocks. Normal independently observed one railway record.

The Last War and proof processes were fully cleaned up afterward.

## Acceptance impact

Under the owner operational acceptance rule, the retained Map acquisition surface is now **WORKING** against the current Last War client:

- normal production frontend path and Start Reading control were already freshly proven in R8-120;
- current-client all-eight acquisition now passes live in Normal and Fast;
- both modes complete 2500/2500 with zero failed/unread;
- persisted Fast results survive database reopen.

This does not claim the current scanner is the original LWBridge 0.3.1 internal algorithm. That provenance is no longer required for owner-facing WORKING status.

## Evidence

`evidence/lwbridge-implementation/2026-09-27-r8-121-map-all-eight-normal-fast-live.json`

SHA-256: `4045092FFB8216E101894061C57B6A7BD2C7611CBC275235867A444DD0D67156`
