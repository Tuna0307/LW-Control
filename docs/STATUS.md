# Product status

Last updated 2026-10-29. Latest release: **lw-control-v1.2-2026-10-27** (product source
`f93c3b9a`, merged to `main` through PR #17). Launch update recovery is ready for review
on `codex/launch-update-recovery`; this is not a new release.

Status meanings:

- **Done**: qualified with real packaged-app tests against the live game, owner-accepted.
- **Done (limits)**: works and is qualified, with known limits listed.
- **Inert only**: implemented and covered by controlled (no-game) tests, but never observed working
  live yet. Treat these as untested in the real game.
- **Removed**: deliberately taken out by the owner. Do not bring back without owner direction.

## Tabs

| Tab | Status | Notes |
|---|---|---|
| **Home** (launch, connect, close, profiles, reconnect, repair) | Done | Accepted 2026-10-24 after soak, fault and reconnect campaigns ([note](design-notes/2026-10-23-home-final-acceptance.md)). Real simultaneous two-game support is **not** verified (only one installed game; shared per-user Lua package blocks a second real owner by design, see D-H18). |
| **Map Data** (scan, browse, filters, Jump, Clear, Auto, all servers, City export) | Done | All ten kinds, Manual/Auto, Stop, Clear/reopen qualified 2026-10-25 ([note](design-notes/2026-10-25-map-final-qualification.md)). Remote servers are scanned by switching server, fast-scanning and returning; camera-free remote scanning is not possible yet. Plunder, schedule and share were removed. |
| **Automation**: alliance help, donations, gifts, gathering dispatch, center/stronghold resources, trade station, official application, treatment, close popups, reconnect, Secret Task (+ Dispatch Assist) | Done (limits) | Engines qualified in R30 and later rounds. Individual events (Secret Task, trade stock) depend on what the game offers that day. |
| **Automation: Chat rewards** (Red Packet, Fireworks, Gold Egg, cross-server) | Inert only | Outcome counting, separate Gold Egg and bounded cross-server discovery implemented ([note](design-notes/2026-10-25-chat-rewards.md)). No live claim was ever observed; activity post 710 is unsupported. |
| **Automation: Treasure** (radar digs, Test Flight Failure) | Inert only | Follows LwTool's Dig Treasure logic ([note](design-notes/2026-10-26-treasure-lwtool-logic.md)). The owner will test it live; four open questions are listed in the note. |
| **Squads / AFK**: Monster AFK, shared AFK profiles, stamina items, Gathering | Done (limits) | Gathering send/recall qualified with four squads, including recall after a 12 s authority pause ([note](design-notes/2026-10-26-gather-recall-retention.md)). No guarantee for arbitrarily long authority gaps. |
| **Squads: Alliance Drill join** | Inert only | Corrected native create path ([note](design-notes/2026-10-25-gather-drill-fix.md)); the event ended before a live positive. |
| **Startup of saved features** | Done | Saved background features start from Home without visiting each tab ([note](design-notes/2026-10-26-startup-synchronization.md)). |
| **City Layout** | Done | Single move, swap, in-flight cancel and full restore qualified (111 anchors). |
| **Hotkeys**: Q/W/E/R attack, recall, F6/F7/F8 shield, Alt+1-4 equipment, F9/F10 relocation, frontline reinforcement, G troops | Done (limits) | Each key qualified in R14-R29. The 2026-10-25 run found no eligible targets, so it did not re-prove a live attack. |
| **Equipment** (presets, rename, drag) | Done | Qualified 2026-10-25 with exact restoration. |
| **Mini Games**: Food House auto clear, Black Market chests | Done (limits) | Accepted 2026-10-18. Both need their in-game event to be active; none was active in the last run. |
| **Settings** (language, theme, FPS/ping overlay) | Done | Nine UI languages. The game itself stays in Chinese. |

## Removed by owner direction

March speed-ups (items/diamonds), the Space shield countdown, Auto Garrison and Zombie Bus Garrison,
Ghost Ops automation, Map plunder, scheduled plunder, Add to schedule, Share to alliance,
Normal/Fast scan modes, and the six worker diagnostic panels. **Unlock Land Cell must never be pressed or tested** (it breaks
an account whose tutorial is unfinished). Details: [BEHAVIOR_DIFFERENCES.md](BEHAVIOR_DIFFERENCES.md).

## Known issues and follow-ups

1. **Live checks still owed**: Treasure dig/redirect/claim, chat reward claims, Alliance Drill
   join and a fresh Hotkey attack. Each needs a natural in-game opportunity.
2. Intermittent `HomeR2` controlled assertion in the Desktop checks; an isolated rerun passes.
3. A retired-pipe `CancelIoEx` retry loop could spin if a write never unblocks (minor, not seen live).
4. Two real games at once (F-07): needs a second independent game installation to verify.
5. **Little headroom in the Map probe.** The packaged `current_live_resource_probe.lua` module is
   64,573 bytes against the 65,535-byte limit for one packaged module, and its main chunk uses
   200 of Lua 5.3's 200 local variables. New probe code must free space first (see DEVELOPMENT.md).

Code notes:

6. `src/LWBridge.Desktop/MapDataStore*.cs` remains only as the staged-scan sink that the Map014
   and manual-scan checks drive (`MapDataStoreScanSink`) and to clear an old per-profile
   `map-data.db` at startup. The Map tab itself uses `LWBridge.Map-0.3.17`'s `MapStore`.
7. The 2026-10-28 cleanup removed every proof switch without a driver, the legacy Map store
   paths and the retired plunder provider surface. `LWBridgeWindow.cs` went from about 370 KB to
   about 120 KB. Retired Map commands still answer `FEATURE_REMOVED`.

## Ideas not done

Deferred, not rejected, during the LwTool comparison
([matrix](competitor-research/lwtool/FULL_PRODUCT_METHOD_MATRIX.md)). Each needs a real problem
or measurement before it is worth the extra state:

- Late connect: let a slow game finish connecting without a relaunch (needs a new recovery
  journal stage).
- Cross-engine squad reservation, so two engines cannot plan the same idle squad in one tick.
- A hotkey capability manifest to catch drift between UI and game-side handlers.
- `PRAGMA user_version` schema versioning for the SQLite stores, at the next schema change.
- Rotation of the local diagnostic log.
- A direct adapter-to-Lua in-memory queue instead of the file mailbox (must drain on the game
  thread).

## Compatibility

The app checks the installed Last War client against known hashes (`CurrentClientCompatibility.cs`,
`src/LWBridge.GameScripts/current_client_compat.py`). After a game update, the hashes and any
changed game Lua interfaces must be re-verified before live use. See
[DEVELOPMENT.md](DEVELOPMENT.md#after-a-game-update).

The 2026-10-29 launch repair moves pending official Lua-update recovery into the shared
start transaction, covering manual Launch, startup, reconnect and repair. It invalidates
stale settlement, lets the official launcher update restored originals, and permits one
bridge retry with a fresh readiness window. Stop/Close remains effective throughout.
Unknown runtime/critical Lua changes remain blocked. See the
[design note](design-notes/2026-10-29-launch-update-recovery.md).

Validation: focused controlled lifecycle checks and the full Windows Desktop suite passed;
all UI checks, all 53 strict game-script checks (zero skips), four release packager tests,
an inert packaged Home capture and a separate v29 candidate roundtrip passed. A genuine
isolated run reproduced the delta failure, applied the official v27-to-v29 update, connected
the bridge and closed/restored the exact post-update originals. Its final Home proof account
gate rejected the selected account, so that first run is qualified only for update recovery,
connection and restoration. A second genuine isolated Home proof on the identified account
passed launch, authenticated connection, Home initialization and exact Close/restoration.
Both runs left no game/launcher processes or recovery journals and preserved the existing
owner app. Gameplay tasks were disabled; no gameplay qualification is claimed.
