# R8-058 - fence app_exit_confirm at native shutdown/restore lifecycle

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the observable main-window close-request and `app_exit_confirm` shutdown contract without inventing the still-missing original proxy install/backup/restore mutation lifecycle.

## Native authority

Primary evidence:

- retained frontend wrapper `app_exit_confirm` with no payload;
- `app_exit_confirm` handler `0x14017DA63-0x14017EA20`;
- main-window close hook `0x1404122EE-0x14041263A`;
- exact event string `app://close-requested`;
- exact payload key `instanceCount` constructed inline by the close hook;
- per-runtime `bridge_exit` dispatch in the confirm handler;
- game-close helper `0x1400E563D-0x1400E579C`;
- original-proxy restore helper `0x1403A0B48-0x1403A0E71`;
- success JSON helper `0x1402BB774-0x1402BB816`;
- R8-040 proxy-status evidence for native proxy/original paths and the still-unrecovered mutation lifecycle.

## Close-request behavior

The native window-event hook special-cases the main window. Rather than immediately completing the ordinary close path, it gathers the managed runtime/instance collection and emits:

`app://close-requested`

with an object containing exact numeric field:

`instanceCount`

The retained frontend uses this event as the confirmation boundary before calling `app_exit_confirm`.

R8-058 classifies the event name and payload key/type as exact native evidence. The hook is part of the native close interception path; no rebuild-only direct-close substitute is added.

## app_exit_confirm admission

The frontend invokes `app_exit_confirm` with no feature payload.

Unlike several profile-scoped commands, this handler is app-global. Static recovery does not show a public `profileId` or authorization-state admission for the command.

## Native shutdown ordering

The confirm handler owns a multi-step shutdown lifecycle over managed runtime instances.

Recovered observable/semantic ordering includes:

1. enumerate the managed runtime/instance collection;
2. for each applicable managed runtime, dispatch exact method `bridge_exit`;
3. perform per-instance shutdown/close handling;
4. run lifecycle cleanup for the managed instance;
5. restore original proxy state before final application exit;
6. complete the application-exit path.

The exact restore helper contains native strings:

- `originalExists`
- `app exit restored original proxy`

This directly proves that proxy restoration is part of the confirmed-exit lifecycle rather than optional UI cleanup.

The game-close helper exposes exact:

- code: `GAME_CLOSE_TIMEOUT`
- message: `The game did not close in time.`

## Failure mapping

The confirm handler contains exact public failure code:

`APP_EXIT_FAILED`

The failure path wraps underlying shutdown/restore/app-exit failure information. Static evidence does not close one universal fixed public message/detail string for every failure branch, so R8-058 does not invent one.

## Success result

After successful shutdown/restore and final exit preparation, the handler serializes the success result through helper `0x1402BB774`.

That helper emits JSON:

`null`

So the recovered public success value is exactly JSON null.

## Rebuild comparison

The rebuild currently has no production `app_exit_confirm` handler and does not own the original proxy install/backup/restore mutation lifecycle.

R8-040 intentionally implemented read-only proxy-state discovery only. It explicitly does not:

- install/replace the original proxy;
- create the original backup lifecycle;
- restore the original proxy;
- claim the native launch/close mutation lifecycle.

Implementing `app_exit_confirm` as a simple window/process `Close()` would therefore be observably wrong: it would skip `bridge_exit`, managed-game shutdown behavior, and the native original-proxy restoration step.

## Why no production implementation is added

The command is not blocked by account/authentication scope. It is blocked by a missing retained host lifecycle primitive: the original proxy install/backup/restore ownership and its coupling to managed runtime shutdown.

Until that primitive is recovered and implemented one-for-one, the safest parity behavior is to keep `app_exit_confirm` unavailable rather than expose a partial direct-close path that can leave native-managed proxy state behind.

R8-058 therefore makes no runtime-code change.

## Evidence classification

- frontend no-payload command: `EXACT_NATIVE`;
- `app://close-requested` event: `EXACT_NATIVE`;
- `instanceCount` payload key/numeric value ownership: `EXACT_NATIVE`;
- app-global confirm admission: `EXACT_NATIVE`;
- per-runtime `bridge_exit`: `EXACT_NATIVE`;
- game-close timeout code/message: `EXACT_NATIVE`;
- original-proxy restoration requirement: `EXACT_NATIVE`;
- success JSON null: `EXACT_NATIVE`;
- `APP_EXIT_FAILED` code: `EXACT_NATIVE`;
- universal failure message/detail mapping: `UNKNOWN`;
- original proxy install/backup/restore implementation: `UNKNOWN / FENCED`;
- runtime implementation: `FENCED`.
