# R8-120 — production login/admission bypass live proof

**Date:** 2026-09-27
**Status:** LIVE-PROVEN BYPASS / PRODUCTION UI

## Result

The login/admission barrier is no longer a blocker for retained LW Control functionality.

The production backend now supplies a local authorized facade for the recovered frontend:

- `auth_state` => `phase=authorized`, local normal role, no error;
- `multi_entitlement_get` => single-profile local entitlement;
- lifecycle commands are delegated to the already live-proven `OverviewLifecycleService` from `LWBridgeBackend.InvokeAsync`.

This lets the normal recovered frontend skip Login and enter the retained app without contacting the original authorization service.

## Fresh live lifecycle proof

`--live-overview-home-proof` succeeded twice on the current client without original Login:

- manual start session `19e24d8b186f4ea68616bbbc08a23923`, PID 53020, connected;
- auto/reconcile session `be2afe929ca643ba871b095b52368bb9`, PID 53688, connected.

`--live-overview-a11-transport-proof` also succeeded:

- authenticated control-pipe route count 1;
- `xluaOnline=true`;
- live Lua `getStatus` returned an object;
- pending-call count returned to zero;
- managed stop and route cleanup succeeded.

## Fresh runtime observation

`--live-current-runtime-diagnostic` succeeded through the same admission-free lifecycle. It observed current server 2212, world 0, 1000x1000 map geometry, player tile 560,468, and live runtime objects including `CS.GameEntry`, `LuaEntry`, `GameMain`, `DataCenter`, `NetworkManager`, `CustomDataManager`, and `DCPlayer`.

This establishes a usable live observation laboratory without original Login.

## Normal production WebView proof

The Release desktop was then launched through its actual normal WebView:

`LWBridge.Desktop.exe --view map-data --normal-ui-live-map-proof <output>`

The normal UI consumed the bypassed AuthState, launched Last War, reached `connectionState=connected`, clicked the production Map Data Start Reading control, and completed a Fast Resource scan:

- server 2212;
- mode `fast`;
- concurrency 20;
- total/read blocks 2500/2500;
- failed blocks 0;
- unread blocks 0;
- Resource search total 864;
- correlated rendered row: Iron Mine level 2 at 989,907, 72,000 / 72,000, Idle.

The game used current official `xlua.dll` SHA-256 `D22D912F031C60F2649FDAF76D359D695511F7A37B93CD637B557F8346569D45`, not either old 0.3.1 proxy hash.

The proof harness embeds historical checkpoint string `LWB-R8-066`; that label describes the proof schema/contract, not the date or admission route of this execution. R8-120 records the fresh 2026-09-27 run through the new production auth bypass.

Evidence:

- `evidence/lwbridge-implementation/2026-09-27-r8-120-production-auth-bypass-map-live.json`
- `evidence/lwbridge-implementation/2026-09-27-r8-120-production-auth-bypass-map-live.png`

Evidence SHA-256:

- JSON `73A1A76D8599048C7A255FF48174126E74C4A74B0B6B6FB322997A82EA3BB610`
- PNG `6F11155DF14B8A3D0A11C476A400DB42CD221BF0B9DA9F97E86832FC64F1E02B`

## Production change

`LWBridgeBackend.InvokeAsync` now routes commands owned by `OverviewLifecycleService` directly to that service before the older synchronous backend stubs. This activates the already-proven production launch/reconcile/stop implementation for the normal frontend.

A deterministic `AuthBypassChecks` test verifies the local `auth_state` and `multi_entitlement_get` facade.

## Acceptance impact

Under R8-119 operational acceptance, **login/admission bypass is WORKING**.

The normal production Map UI Fast/Resource acquisition path is also freshly **WORKING** end-to-end on the current client. Full Map scope still needs fresh Normal/Fast all-eight validation before declaring every Map acquisition mode covered under the current production build.

Home lifecycle launch/connect/stop is freshly live-proven through the same admission-free service route; normal Home UI interaction should be checked separately if owner-facing Home UI completion status is needed.
