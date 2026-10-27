# Product status

Last updated 2026-10-27, release **lw-control-v1.2-2026-10-27** (product source `f93c3b9a`,
merged to `main` through PR #17). No work is in progress; the owner is taking a break.

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

Code cleanups that need a Windows machine (build + Desktop checks after each step):

6. Proof-only code still lives in the product: the `LWBridgeWindow.*Proof.cs` partial classes,
   `--*-proof` switches in `Program.cs` and `LWBRIDGE_*` proof environment variables. Many have
   no driver left in `tools/live-harness/` (see LIVE_TESTING.md). `LWBridgeWindow.cs` is
   about 370 KB, mostly because of these.
7. The removed plunder feature still has an interface surface: `IMapActionProvider` /
   `IGhostPlunderProvider` methods that only return `FEATURE_REMOVED`, the
   `dispatch-plunder-changed` / `truck-plunder-changed` events and `startPlunderWorkers`
   parameters, plus their test fakes.
8. `src/LWBridge.Desktop/MapDataStore*.cs` is the older Map store. Only legacy live proofs and
   `FirstLiveResultImporter` use it; the product Map tab uses `LWBridge.Map-0.3.17`'s `MapStore`.

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
