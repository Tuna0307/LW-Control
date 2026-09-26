# LWB-R7-130 — Map Data correctness, multi-server control, and scan-speed corrections

**Date:** 2026-09-22
**Scope:** seven owner-reported Map Data defects after LWB-R7-129.
**Overall status:** code complete at this checkpoint; live proof exists for Player City HP, fast full-world/all-eight acquisition, two-server acquisition/storage, transient Overview-admission recovery, and active-scan Stop. Auto scheduler failure isolation, UI persistence, saved-server selector, and Clear/search race suppression are implemented and deterministic/source-contract tested; normal-window click/reopen UI acceptance remains an owner-visible follow-up.

## Source identity and provenance

The installed official LocalLow Lua package reports version.txt = 20 and was restored after every live proof:
- LWScripts.data SHA-256 FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9
- LWScripts.txt SHA-256 FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F
- version.txt SHA-256 F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B

The HP formula was recovered before the v20 live re-proof from preserved current-v19 Lua artifacts:
- .codex-live/WorldPlayerDes-current.lua SHA-256 2F3908BAE09394F61B82EBB355C4A1E1D89D31A009884E6D20ABD92AECECC645; lastHpTime/recoverSpeed/curHp effective display path at lines 492–505.
- .codex-live/BuildFireEffectManager-current.lua SHA-256 0709F03B25ED08BD96751F8228EF6EDF62887265D9B7C0709DADC26B5F446F11; MAX_CITY_DEFENCE = 10000 at line 8, normal-city max assignment around line 459, unavailableTime / 1000 use around line 515.
- .codex-live/BuildingUtils-current.lua SHA-256 C59545AA81E9922FD4F81BCADF40093E8E66B0D1439F240E989C8156FB32650B; status 500300 fire speed around line 2420 and GetDefenceWallCoverSpeed() recovery around line 2427.

These artifacts are source evidence for the formula. The v20 live proof below establishes current-client compatibility rather than silently relabelling the v19 recovery as v20 static parity.
## Finding R7-130-F1 — Player City HP is an effective value, not raw curHp

**RECOVERED + LIVE-PROVEN.** tools/current_live_resource_probe.lua::player_city_health_snapshot(info) now retains the raw HP diagnostics but publishes the effective value. For a normal main city it uses the recovered 10,000 cap, elapsed server time, wall recovery speed, fire expiry/speed when present, then floors/clamps to [0, 10000].

Fresh v20 proof used the production current-client source against the exact reported block containing (67,858). That row returned:
- raw curHp = 1222
- recoverSpeed = 0.38100001215935
- lastHpTime = 1789856054
- healthMaxHp = 10000
- published health = 10000
- calculation mode normal_main_fire_then_recover

The live row therefore reproduces the stale raw baseline and proves the shipped publication path now reports the effective durability.

## Finding R7-130-F2 — Unselected City/Resource extraction was a major scan-cost multiplier

**IMPLEMENTATION POLICY + LIVE-PROVEN.** The exact 10,000-cell LOD0 admission requirement is unchanged. The optimization is only to avoid serializing expensive retained City/Resource sources when those categories are not selected. CurrentClientMapBlockSource.FastCity.cs now sends correlated includeCity / includeResource flags; the Lua probe validates/echoes them and calls city_aoi_records(...) / resource_aoi_records(...) only when selected.

Measured on server 2212:
- R7-129 Truck baseline: 135.165346 s; R7-130 Truck: 74.7213523 s (~44.7% lower scan wall).
- R7-129 Monster baseline: 137.0355255 s; R7-130 Monster: 77.7809171 s (~43.2% lower scan wall).
- all original eight categories together: 77.8903951 s, 2500/2500, 0 failed, 0 unread; reopened counts exactly matched publication.
- mixed City+Truck+Dispatch+Treasure immediately beforehand: 78.0272254 s.

The all-eight result shows selected-category breadth no longer multiplies the native traversal wall time. Aspect 8/16 experiments remained capped at 50 native AOI cells and were not promoted to production.
## Finding R7-130-F3 — one-response Overview admission gaps must not enter the long Home health window

**IMPLEMENTED/OFFLINE-TESTED + LIVE-PROVEN under naturally occurring gaps.** CaptureFastFullWorldAsync recognizes only the exact correlated error Fast world batch failed: overview_session_unavailable as a short admission gap. It revalidates the same owned session and waits 500 ms before bounded retry. Other failures retain the existing healthy-session path and 150 ms retry delay.

A two-server v20 run naturally hit multiple first-attempt gaps, including one second-attempt gap, yet both scans still completed 2500/2500 with no failed/unread blocks. Server 2213 finished in 78.1995665 s. FastFullMapRetriesOverviewAdmissionGapWithoutHealthWindow pins the branch deterministically.

## Finding R7-130-F4 — Auto Scan must isolate each target server and saved data must remain browseable

**IMPLEMENTED/OFFLINE-TESTED; acquisition/storage LIVE-PROVEN.** The generated top-level Auto Scan loop now owns a try/catch per configured target instead of one outer failure boundary. A failed jump/scan is logged in failed and later configured servers continue unless Stop has disabled the scheduler.

LWBridgeBackend.map_summary now exposes complete savedServerIds and chooses a deterministic saved/offline browse context instead of throwing solely because several servers are stored. Map Data renders a server selector whenever more than one saved server exists and, while idle, no longer forces browse state back to the live server.

Live bounded proof:
- owned-session jump 2212 -> 2213 -> 2212 succeeded;
- all-eight server 2212 scan: 82.6769287 s, 2500/2500, zero failed/unread;
- all-eight server 2213 scan: 78.1995665 s, 2500/2500, zero failed/unread;
- the same store retained both published server IDs;
- reopened 2213 counts exactly matched publication;
- the session returned to 2212.

This proves the backend/navigation/storage prerequisites with two real servers. A deliberately failing target was not injected through the normal UI; per-target continuation itself is covered by generated-source deterministic guards.
## Finding R7-130-F5 — Stop, persistence, and Clear/search race

**Stop: IMPLEMENTED/OFFLINE-TESTED + live backend cancellation.** The Map Data Stop action first calls the parent Auto Scan config callback with enabled:false; that callback synchronously writes the scheduler ref before map_scan_stop is awaited. The scheduler checks the ref before every next target. A current fast scan was live-stopped while still at 0/2500; final phase was idle and isReading=false.

**Persistence: IMPLEMENTED/OFFLINE-TESTED.** Per-profile localStorage keys now preserve Manual selected categories, Manual/Auto tab, result tab, and saved browse server. Existing filter/Auto Scan persistence remains intact. The remote shell interface available for this checkpoint has no native click/keyboard/screenshot control, so changing these values through the real WebView and visually confirming them after an executable reopen was not exercised here.

**Clear/search race: IMPLEMENTED/OFFLINE-TESTED.** Clear increments the saved-search and Treasure-refresh generations before mutating SQLite, resets loading/search state, and therefore makes any older request/result/error stale. The exact owner-visible false “Saved map search failed” race is guarded by generated-source regression; repeated normal-window click timing was not available through this remote interface.

## Validation

- python tools/build_lwbridge_frontend.py --check — pass.
- node --check on index-sfL2sT3K.js and MapDataPanel-C1HVeNHr.js — pass.
- Release build of LWBridge.Desktop.Checks — 0 warnings / 0 errors.
- full deterministic executable — all six groups true, failures=[].
- fresh v20 (67,858) HP proof — pass.
- all-eight fast full-world/reopen proof — pass.
- two-server full-world/store/reopen/return proof — pass.
- active fast-scan Stop proof — pass.
- private 160-cell SendAoiRequest experiment failed closed with target_aoi_still_current; it was not promoted and exact production coverage remains unchanged.

## Implementation impact and limits

Production changes are in CurrentClientMapBlockSource.FastCity.cs, LWBridgeBackend.cs, tools/current_live_resource_probe.lua, the canonical frontend generator and its generated bundles. Deterministic contracts are in CurrentClientMapBlockSourceChecks.cs, ManualMapScanCommandServiceChecks.cs, and Program.cs.

No state-changing game action was performed. Scan correctness still requires exact 10,000-cell admission/publication. The remaining owner-visible follow-up is ordinary UI confirmation of persisted controls/server browsing and the Clear/Search timing behavior; those are not relabelled as live-proven by this checkpoint.
