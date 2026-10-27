# Behavior differences from the original LWBridge 0.3.17

Since 2026-10-10 the goal is a faithful, reliable, fully working replacement, not a byte-exact clone.
Differences are allowed when they keep the feature's purpose and are verified. Each deliberate
difference is recorded here.

Labels: **OWNER_DIRECTION**: the owner asked for it. **OWN_DESIGN**: our method, where the original
was unknown or ours is better. **CURRENT_CLIENT_ADAPTATION**: needed for today's game build.
Removing a feature or changing its purpose needs OWNER_DIRECTION. Never relabel a gap as done.

The full original ledgers, with every evidence path, are in the archive tag
`archive/pre-cleanup-2026-10-27` (`docs/HOME_004_BEHAVIOR_DIFFERENCES.md`,
`docs/MAP_015_BEHAVIOR_DIFFERENCES.md`, `docs/MAP_016_BEHAVIOR_DIFFERENCES.md`,
`docs/FEATURE_018_BEHAVIOR_DIFFERENCES.md`).

## Excluded from the product

- The original login, licensing and commercial account system, including its protected
  services. No entitlement bypass.

## Home

| ID | Type | Behavior | Verified |
|---|---|---|---|
| D-H01 | OWN_DESIGN | Local session/profile ownership replaces the original commercial admission/lease. | Native lifecycle tests, genuine Launch/Close |
| D-H02 | CURRENT_CLIENT_ADAPTATION | Our own authenticated named pipe, heartbeat, readiness and installation adapter for the current client. | Genuine Launch/Connected/Close, 1800 s soak |
| D-H03 | OWN_DESIGN | Finite retry/timing/error policies with user Stop; no blind retries. | Controlled + fault campaigns |
| D-H04 | OWN_DESIGN | Reconnect/repair keeps the original purpose and the OFF/ON preference, using our own mechanism. | 20/20 local fault reconnects, 5/5 from final ZIP |
| D-H05 | OWN_DESIGN | Profile capacity is not copied from the commercial limit; local profiles are offered (see D-H07). | n/a |
| D-H06 | OWN_DESIGN | Home Close sends Stop for the captured profile even before a recovery instance ID exists. | Controlled |
| D-H07 | OWN_DESIGN | Four local profile slots with Add/Delete (SQLite); the folder picker is shown even when a root is valid. | Controlled + packaged WebView |
| D-H08 | OWN_DESIGN | When no repair journal exists, Update-and-Launch falls back to a real owned Start and requires Connected. Never calls the official updater. | Controlled |
| D-H09 | OWN_DESIGN | Per-profile delete admission; another profile's Start/Stop stays usable while one is busy. | Packaged WebView |
| D-H10 | OWN_DESIGN | An active recovery without an instance ID shows as `recovering`; sidebar Stop / Stop All work during it. | Native controlled |
| D-H11 | OWN_DESIGN | A per-profile Enabled toggle. Disabled profiles cannot be started or auto-reconciled; running ones are not killed. | Native + packaged |
| D-H12 | CURRENT_CLIENT_ADAPTATION | "Bridge online" requires both a fresh heartbeat and a live authenticated pipe route (60 s offline threshold). | Native controlled |
| D-H13 | OWN_DESIGN | An empty journal-repair result is reported as an error, not as success. | Controlled |
| D-H14 | CURRENT_CLIENT_ADAPTATION | Add/Delete are controller-global commands (an earlier routing bug is fixed). | Packaged WebView |
| D-H15 | CURRENT_CLIENT_ADAPTATION | Committed Add/Delete/primary-set get their own acknowledgement and survive a profile switch. | Packaged WebView |
| D-H16 | CURRENT_CLIENT_ADAPTATION | Opt-in isolated route-fault switch for testing transport loss (`--isolated-root` + env flag only). | Genuine game |
| D-H17 | CURRENT_CLIENT_ADAPTATION | Journal-based Stop works without the plaintext challenge after adoption failure. | Genuine game |
| D-H18 | OWN_DESIGN | The game's Lua package is per Windows user, so only one real game owner runs at a time (`BRIDGE_HOST_BUSY`). Two real games need a second independent installation; this is unverified. | Native controlled |

## Map

| ID | Type | Behavior | Verified |
|---|---|---|---|
| D-M01 | OWN_DESIGN | Camera-free scanning on the current server: the 10,000 AOIs are requested directly through `WorldGetBlockMessage` in batches, instead of moving the game camera. Guarded camera fallback only after a native cleanup receipt. | Genuine: City ~24 s vs ~196 s by camera |
| D-M02 | OWN_DESIGN | Trucks use a dedicated typed march sweep. Later qualification replaced the original camera-only Truck policy. | Genuine packaged all-ten |
| D-M03 | OWN_DESIGN | One guarded retry when the camera settles during the first scan after startup. | Genuine packaged |
| D-M04 | OWNER_DIRECTION | Scheduled/auto plunder, Random delay and saved plunder jobs removed. Old jobs are retired on open. | Controlled + packaged |
| D-M05 | OWNER_DIRECTION | Add to schedule and Share to alliance removed (`FEATURE_REMOVED`). | Controlled |
| D-M06 | OWNER_DIRECTION | Normal/Fast choice removed; the fastest qualified scanner always runs. An old `scanMode=normal` setting still loads. | Controlled + packaged |
| D-M07 | OWN_DESIGN | When reconnecting, Auto waits one full interval instead of firing an overdue run. Run now stays immediate. | Controlled |
| D-M08 | OWN_DESIGN | "All servers" browse with the source server on each row. Aggregate Clear is unavailable. | Controlled + genuine data |
| D-M09 | OWN_DESIGN | Railway comes from a native 625-rectangle type-2 march sweep and can prove an empty result. | Genuine 2212/2211 |
| D-M10 | OWN_DESIGN | A cached marker for an unchanged official client skips repeating the first-use launcher settle. | Controlled |
| D-M11 | OWN_DESIGN | UI reads show the real backend error instead of an empty result. | Mounted UI |
| D-M12 | not qualified | Remote servers are scanned by switching server, fast-scanning and returning. Camera-free remote acquisition is unsupported, because the game drops off-server replies. | Research only |

## Automation, Squads, Hotkeys, City, Mini Games

| ID | Type | Behavior | Verified |
|---|---|---|---|
| D-F01 | OWNER_DIRECTION | March speed-ups removed from Hotkeys. Q/W/E/R never spends items or diamonds; old saved settings are ignored. | Controlled + live Q→A |
| D-F02 | OWNER_DIRECTION | The six worker diagnostic panels are removed from Hotkeys/Mini Games. | Mounted UI |
| D-F03 | OWNER_DIRECTION | Auto Garrison and Zombie Bus Garrison removed; old saved entries are dropped on the next write. | Controlled |
| D-F04 | OWNER_DIRECTION | Map scan data is session-only: it is cleared on every app start. Profiles and settings stay. | Packaged |
| D-F05 | OWN_DESIGN | City Layout completes from the authoritative building positions; it never replays sent moves. | Live move/swap/cancel |
| D-F06 | OWNER_DIRECTION | Space shield countdown removed completely. F6/F7/F8 shield use stays. | Controlled + mounted |
| D-F07 | OWNER_DIRECTION | Treasure follows LwTool's Dig Treasure settings and defaults, with gathering-squad redirect and resume added. The time-of-day claim schedule is no longer used. Radius 3 and the Random unit are OWN_DESIGN. | Inert only |
| D-F08 | OWN_DESIGN | Chat rewards: exact native outcome counting, separate Gold Egg card, bounded cross-server room discovery, and one shared status poll for the four Chat cards. | Inert + read-only native |
| D-F09 | OWN_DESIGN | Saved background features start from the per-profile runtime at launch, so no tab visits are needed. | Genuine Home-only run |
| D-F10 | OWN_DESIGN | Gathering keeps its exact march records through a temporary loss of authority in the same session, and recalls them on Off. | Genuine 4-squad run |
| D-F11 | OWNER_DIRECTION | Ghost Ops automation removed (2026-10-22): no card, saved settings or game-side engine. Map's Ghost scan is unaffected. | Controlled |
