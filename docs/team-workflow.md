# Team workflow - Player City PM return

Player City is the active Map Data delivery. PM17 remains delivered at `f24fef39bbe41a8655595da2c9c9da1bcb9e9811` with GitHub Actions `34711920482` SUCCESS. S03/S06 remain deferred; S02 remains unfinished/unassigned; the corrected-build Overview live regression remains prepared but unperformed.

## Current PM return

`LWB-PC-001` LIVE-PROVES the technical core on the current client: two fresh city-only acquisitions, correct active-profile/server persistence, normal `map_search`/summary, a strictly newer second capture, exact Lua-package restoration, and clean owned-process/recovery shutdown. `LWB-PC-002` LIVE-PROVES a separate fresh-process same-profile reopen/search of the saved city. Shared evidence is identity-redacted and preserves immutable raw-result hashes.

The remaining selected-path gate is the **owner-visible normal Map Data row**. Do not ask the owner to perform it until PM approves this package. The prepared check is read-only: no scan, game launch, cross-server travel, Resource action, export, or store mutation.

## Prompt to send PM now

```text
Audit the Player City return on research/offline-controller. Read AGENTS.md, task.md, BACKLOG.md, docs/map-data-delivery.md, docs/live-test-handoff.md, docs/user-test-checklist.md, docs/lwbridge-feature-ledger.md and the two LWB-PC evidence files.

Verify that LWB-PC-001 supports two distinct fresh city-only current-client acquisitions with authoritative source/request identities, strictly newer second capture, active-profile persistence, normal map_search/summary, exact restoration and clean ownership; verify LWB-PC-002 independently reopens the same profile/store and returns the saved city through normal map_search.

Audit the new read-only owner package: Start Owner Player City Check.cmd, tools/collect_owner_player_city_evidence.py and --owner-evidence instrumentation. Confirm startup auto-launch is suppressed, state-changing commands are blocked, City Search evidence is sanitized, and render correlation reads only coordinates/level/updated time rather than player/alliance identity.

If accepted, dispatch only the owner-visible normal-page check in docs/user-test-checklist.md. Do not request a fresh scan or game action. Keep S03/S06 deferred, S02 unfinished, corrected-build Overview regression unperformed, and all non-Player-City Map Data work out of this checkpoint.
```
