# R7-150 ? cross-server Train-list no-jump Auto scan

**Date:** 2026-09-23
**Parent revision:** `e594455ff43abe38d657f66a4eec3eb243e3585c`

## Result

Truck/Railway-only Auto Scan can now skip physical server travel when the official current-v20 `LWTrainDataManager.TryGetTrainList(true)` coverage says the target server is in the current live server's `matchServers` set. Mixed selections and uncovered Train-list targets keep the existing jump-first path.

The backend now separates the published dataset server (`serverId`) from the physical game server (`liveServerId`). A remote direct-list dataset reports `serverIdSource=remote_train_list`; top-bar current-server and restart-recovery decisions use `liveServerId`, so publishing server 2182 while physically on 2212 cannot masquerade as a server jump.

## Live proof

On the current installed v20 package, the official client was physically on **2212**. One read-only coverage refresh returned:

- `matchServerIds`: 2182, 2193, 2197, 2198, 2204, 2207, 2208, 2209, 2212
- `truckServerIds`: 2182, 2193, 2197, 2198, 2204, 2207, 2208, 2209
- `railwayServerIds`: 2207
- coverage refresh wall time: **0.629 s**

The proof then started a Truck+Railway scan with `targetServerId=2182` and did **not** call `server_jump`. It completed in **0.809 s** with:

- dataset server 2182; physical `liveServerId` 2212; source `remote_train_list`;
- 2,500/2,500 logical blocks, 0 failed, 0 unread;
- 2 Truck rows and 0 Railway rows published for 2182;
- physical server 2212 before and after the scan.

This changes the multi-server cost model for covered Truck/Railway-only targets from one full server jump plus scan per target to one lightweight coverage read followed by fresh direct-list reads. The implementation deliberately refreshes the official list for each target rather than reusing a stale row snapshot; this live run measured about 0.8 seconds for the remote target. No fixed 10-server duration is claimed because network/server conditions vary.

## Auto routing proof

The browser Auto harness proves both branches:

- two covered Truck/Railway targets reuse one coverage decision and issue **zero** `server_jump` calls;
- an uncovered Truck target performs the existing `server_jump` and starts the post-travel scan without a remote `targetServerId`;
- City/mixed-selection scheduler ownership, navigation/Refresh/reconnect behavior, restart recovery, failure isolation, ordering and return-to-origin contracts remain intact.

## Validation

- current-v20 live no-jump proof: pass;
- Release build: 0 warnings / 0 errors;
- deterministic executable: all six groups true, no failures;
- frontend generator check: pass;
- 36-view browser parity: pass;
- automatic scan strategy browser check: pass after updating its old unconditional-jump assertion;
- R7-131 Map Data browser check: pass;
- R7-150 Auto no-jump/fallback browser check: pass;
- R7-136 restart-ownership browser check: pass;
- normal production Release smoke: pass; final Desktop/game/launcher/helper counts zero. Working-tree apphost SHA `212DDD57D3C1917787FA108D217C186A80ECCF434D63E9082E4B6CBD40729FEA`; exported staged-snapshot apphost SHA `8A228D9AD619BB9406855A8CEBAECA3E10E397102B5153569CA43E50DE24A489`; as in R7-145, no universal build-context-independent apphost hash is inferred.

No claim, plunder, share, attack, spend, collection, or other state-changing gameplay action was executed.
