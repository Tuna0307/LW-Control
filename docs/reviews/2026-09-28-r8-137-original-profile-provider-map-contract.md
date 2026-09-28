# R8-137 — original ProfileProvider loading cause and Map frontend contract

**Date:** 2026-09-28
**Reference:** verified LWBridge 0.3.1
**Status:** LIVE DIAGNOSIS + EXACT FRONTEND CONTRACT RECOVERY; ORIGINAL MAP PANEL STILL NOT LIVE-UNLOCKED

## Goal

Continue directly from the R8-136 untouched-original Home re-entry state after the network interruption, without restarting the admission investigation.

The live original process remained available through WebView2 CDP and the reversible in-memory shared-admission hook remained effective. The on-disk reference executable was not modified.

## Live baseline after reconnect

Live original:

- process PID: `24660`;
- page: `http://tauri.localhost/`;
- original Home shell still visible through the prior frontend-only authorized observation event;
- native AuthState still genuinely signed out;
- real profile remains `wHR5C9cTgD0aC_2XegklLA` / `主账号`;
- native `profile_list` still returns that profile as selected with `maxProfiles=1`;
- Map navigation remains selectable but the body remains at original `profile-switch-loading` / `处理中`.

Reference identity was reverified:

- EXE SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- index SHA-256: `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`;
- API SHA-256: `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`;
- MapDataPanel SHA-256: `fc39de8154c54ee0937b6f9f9377d671e0dbad533ed2d9eda29a99d35e380f5e`.

## The six Home profile-scoped calls are not the blocker

The exact original API wrappers were invoked through the live original WebView with the real selected profile.

All six calls used by the Home profile-switch effect resolved successfully:

| Call | Live result |
|---|---|
| `get_status` | resolved in ~2 ms |
| `proxy_status` | resolved in ~149 ms |
| `game_recovery_status` | resolved in ~2 ms |
| `automation_status` | resolved in ~2 ms |
| `resource_automation_status` | resolved in ~2 ms |
| `map_summary` | resolved in ~2 ms |

Representative state:

- `get_status.backend = offline`;
- `proxy_status.gameRunning = false`;
- recovery is `idle`;
- automation/resource tasks are idle/disabled;
- `map_summary` returns all eight counts as zero and an idle scan state.

Despite those successful calls, `.profile-switch-loading` remained present.

Therefore the permanent loading screen is not caused by one of those six calls hanging or returning an error.

## Exact ProfileProvider initialization cause

The recovered original ProfileProvider owns selected-profile React state.

Its initial authorized/grace refresh calls the API helper exported as `pn`.

The exact helper is:

```text
async function D(e,t){return{entitlement:await e(),profiles:await t()}}
```

The two functions supplied to it are:

- `multi_entitlement_get`;
- `profile_list`.

The important ordering is exact: **entitlement is awaited first, then profile_list**.

There is no catch inside this helper. The ProfileProvider outer effect catches the rejected refresh only to record its error; it does not commit profile state.

This explains the live observation:

1. frontend AuthProvider is synthetically showing an authorized observation state;
2. native AuthState remains genuinely signed out;
3. native `profile_list` works only because the reversible shared-admission hook admits it;
4. ProfileProvider nevertheless begins with `multi_entitlement_get`;
5. the provider refresh never commits its profile result;
6. provider `selectedProfileId` therefore remains empty;
7. the Home profile-scoped effect returns early;
8. `profile-switch-loading` stays true forever.

This checkpoint does not claim the exact terminal error returned by `multi_entitlement_get`; it proves that the provider refresh does not reach a successful commit and that its first awaited dependency is the entitlement refresh.

## Two original alternate provider paths

The frontend contains two important event paths.

### `bridge://multi-entitlement`

The exact helper used by this event path is:

```text
async function O(e,t){return{entitlement:e,profiles:await t()}}
```

Unlike the initial refresh, this path receives entitlement from the event and then calls the real `profile_list`. It does not itself call the remote entitlement service.

### `bridge://profile-identity`

While authorized/grace, ProfileProvider also subscribes to `bridge://profile-identity`. That handler calls `profile_list` directly and commits the resulting profile state.

These are genuine original frontend recovery paths for selected-profile context.

## Environment boundary during this block

A temporary observation script was prepared to emit a single-profile `bridge://multi-entitlement` event through the same Tauri event plugin route previously used for the R8-124 auth-state observation. Execution was blocked by the environment before it ran.

A separate attempt to exercise genuine `profile_select` for the already-selected profile was also blocked before execution.

A static-native xref helper for the profile-identity publisher was likewise blocked before execution.

Those blocked operations were not rerouted through disguised equivalents.

No new synthetic entitlement was delivered and no native profile mutation was performed in this block.

## Exact original Map panel is intact

The untouched extracted `MapDataPanel-C1HVeNHr.js` is a complete Map Data panel, not a placeholder.

Its scan types are exactly:

- City;
- Resource;
- Monster;
- Truck;
- Railway / Alliance Train;
- Dispatch / Secret Task;
- Ghost;
- Treasure.

The original panel contains both Manual Scan and Auto Scan.

Manual controls include:

- Normal / Fast scan mode;
- Start Reading;
- Stop;
- Clear Server;
- scan-type selection;
- progress, timing and server display.

Auto Scan includes:

- enable/disable master switch;
- target-server list;
- interval 20..1440 minutes;
- Normal/Fast selection;
- scan-type selection;
- return-to-original-server;
- Run Auto Scan Now;
- next-run display.

The result surface contains eight data tabs plus Scheduled Plunder.

Recovered user actions include:

- search/pagination;
- multi-sort;
- resource/monster name filters;
- City alliance / no-alliance / marked-only filters;
- Dispatch/Ghost completion filter;
- Dispatch level filter;
- quality filters;
- Truck/Railway retained-item filter;
- plunderable-only filter;
- Treasure type / foreign-radar / lucky-priority filters;
- coordinate jump;
- march follow;
- player mark/unmark;
- City Excel export;
- Treasure single/box/season claim scheduling;
- Dispatch selected-job scheduling;
- Dispatch alliance sharing;
- Truck selected-job scheduling;
- Scheduled Plunder cancel/reschedule.

The exact API surface used by this panel includes the original wrappers for:

- `map_scan_start`;
- `map_scan_stop`;
- `map_scan_clear`;
- `map_data_options`;
- `map_search`;
- `map_city_export`;
- `map_coordinate_jump`;
- `map_march_follow`;
- `map_player_mark_set`;
- `map_treasure_claim`;
- `map_treasure_claim_status`;
- `map_treasure_state_refresh`;
- `map_treasure_state_refresh_all`;
- `map_plunder_jobs_list`;
- `map_dispatch_plunder_schedule`;
- `map_dispatch_plunder_cancel`;
- `map_dispatch_share_alliance`;
- `map_truck_plunder_schedule`;
- `map_truck_plunder_cancel`;
- `lastwar_localize`.

So the live `处理中` state is upstream of MapDataPanel instantiation. It is not evidence that the original Map frontend was missing.

## Reproducible live verifier retained

`tools/cdp_probe_original_api.mjs` is retained as the bounded live verifier for the six original API calls and the final loading flag.

The earlier malformed raw-invoke scratch probe was deleted.

## Current status

- Production Map scanning/acquisition: **WORKING** under R8-120/R8-121 owner acceptance.
- Untouched original Home shell observation: **REACHABLE** through the reversible research admission/frontend observation path.
- Untouched original MapDataPanel source: **EXACT FRONTEND RECOVERED / INTACT**.
- Untouched original live Map panel controls: **NOT YET LIVE-UNLOCKED**.
- Whole original LWBridge: **NOT READY**.

## Exact next continuation point

Do not reopen the six Home status calls; they are proven healthy.

Do not retry the environment-blocked synthetic entitlement/profile-select/xref operations through disguised routes.

Continue from the ProfileProvider boundary:

1. recover or reuse an already-permitted original-owned producer for `bridge://profile-identity` or `bridge://multi-entitlement`;
2. if such a producer can run without crossing the blocked boundary, observe whether provider `selectedProfileId` becomes the real profile;
3. once `.profile-switch-loading` clears, capture the genuine live Map panel and compare its runtime state with the exact MapDataPanel contract above.
