# R7-148 direct Train-list speed path

## Scope

Owner asked to make map scanning materially faster, especially for time-sensitive targets. This checkpoint changes **Truck/Railway-only** acquisition from a 10,000-AOI full-world walk to the current-v20 game-owned `LWTrainDataManager.TryGetTrainList(true)` list response. It does not change mixed scans or Secret Task yet.

## Recovered current-v20 contract

`GetTrainListMessage` returns `message.ls` and `message.allianceTrainList`; `LWTrainDataManager:OnTrainListGet` replaces `enemyTrucks` from `ls` and `enemyTrains` from `allianceTrainList`. The same response carries `trainServers`; `LWMyStationDataManager:SetMatchServer` stores them in `matchServers`, and `CheckTrainInMatchServer(serverId)` uses that exact set. This is sufficient to distinguish a covered server with zero rows from a server outside the current match set.

## Implementation

- `current_fast_train_list_v1` is now the planner strategy for standard-world selections containing only Truck/Railway.
- `CurrentClientMapBlockSource` performs one correlated Train-list refresh and publishes the returned rows into the existing 2,500 logical block contract without issuing AOI requests.
- The live probe now serializes both `enemyTrucks` and `enemyTrains`, preserving exact source identity, moving positions, quality/power, and Truck loot metadata.
- The probe also records `truckServerIds`, `railwayServerIds`, and game-owned `matchServerIds` for cross-server coverage diagnostics.
- Mixed scans still use `current_fast_full_world_v2`; this checkpoint does not yet make Auto Scan skip server travel.

## Live result

On current v20, a Truck source acquisition from server 2212 completed in **0.4463395 s** with 2,500 logical captures and zero AOI requests. A prior same-path measurement was **0.573658 s**. The 2212 response contained 15 Truck rows across its global source list and one Railway row; the game-owned match set was `2182, 2193, 2197, 2198, 2204, 2207, 2208, 2209, 2212`. Current-server 2212 had zero Truck rows at that moment, correctly publishing an empty current-server dataset.

A separate live run entered server 2182 and returned two positive Truck rows through the same direct-list path, including coordinates, owner/power/quality, current goods, and exact max-loot metadata. The older full acceptance harness then stopped only because that live population contained no ordinary-UR Truck; that is a population-specific filter gate, not an acquisition failure.

## Validation

- Lua parse: PASS.
- Release build: PASS, 0 warnings / 0 errors.
- Deterministic executable: `ok=true`, all six groups true, `failures=[]`.
- Installed script package restored exactly to v20 hashes after live proofs.
- No LWBridge/Desktop game/launcher process remained after validation.

Machine-readable evidence: `evidence/lwbridge-implementation/2026-09-23-r7-direct-train-list-speed.json`.
