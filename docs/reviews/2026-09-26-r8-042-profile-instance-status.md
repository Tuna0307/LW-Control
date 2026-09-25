# R8-042 — restore profile_instance_status public contract

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the native profile-instance status result, active-state projection and retained single-profile connection classification without changing launch/stop/reconcile process control.

## Authority

Primary native evidence:

- `profile_instance_status` handler `0x1401662AD-0x140167645`;
- selected-profile resolver `0x1402AE43C-0x1402AE4E6`;
- runtime resolver `0x14041C8AB-0x14041CBB1`;
- instance-record provider `0x14041CBB1-0x14041D722`;
- optional-record clone `0x14038118D-0x14038128E`;
- base-record serializer `0x14041DB57`;
- connection-state classifier `0x1403D28C3-0x1403D2A01`;
- bridge supplement provider `0x1403CEA9A`;
- Unix-millisecond clock helper `0x14023F1C0`;
- identity-confirm transition around `0x1404183B3`;
- lease-required setter `0x14041BAA4-0x14041BC3F`;
- lease-active predicate `0x140408F2C`.

The retained frontend API wrapper and Overview stop flows were also checked: stopped entries are represented as null rather than a synthetic stopped status. This checkpoint does not enter the protected package-key RVA lane.

## Exact result shape

When an active/recoverable native instance record exists, the serializer emits exactly these fields in order:

1. `profileId`
2. `instanceId`
3. `phase`
4. `pid`
5. `startedAt`
6. `lastError`
7. `identityConfirmed`
8. `leaseRequired`
9. `connectionState`
10. `bridgeConnected`
11. `lastHeartbeatAt`
12. `leaseActive`

`profileId`, `instanceId`, `phase`, and `connectionState` are strings. `pid` is integer-or-null. `startedAt` is a scalar signed integer in Unix milliseconds. `lastError` is string-or-null. The identity/lease/bridge flags are booleans. `lastHeartbeatAt` is integer-or-null.

## Native absence and phase semantics

The provider returns `Result<Option<InstanceRecord>, ...>`. If no active or recoverable instance exists, the native handler serializes the successful `None` result as JSON `null`. It does not fabricate `phase="stopped"`, an unmanaged-process error object, or a PID-only status.

Recovered instance phases in this lane are:

- `starting`;
- `awaitingIdentity`;
- `running`;
- `recovering`;
- `error`.

No native `stopped` or `stopping` instance-record phase string was found. The rebuild therefore keeps its internal stopping state private and projects an owned stopping record as `running` until the record is removed.

At record creation native stamps `startedAt` in Unix milliseconds, sets `pid=null`, `lastError=null`, `identityConfirmed=false`, and `leaseRequired=false`. Identity confirmation later sets the identity flag and normally advances the phase to `running`.

## Connection-state projection

Native first maps `lastError` to `connectionState="error"`. Special phases map `starting -> starting`, `recovering -> recovering`, and `awaitingIdentity -> awaitingLogin`.

For the retained single-profile path, native multi-entitlement leasing is not required. With `leaseRequired=false`, the ordinary state is `connected` exactly when the instance has a named-pipe route, its heartbeat is fresh, and identity is confirmed; otherwise it is `reconnecting`. Native heartbeat freshness is `now - lastHeartbeatAt < 15001` milliseconds.

The rebuild now uses its exact per-instance control-pipe registry for `bridgeConnected`, its same-profile/session/challenge/PID heartbeat evidence for `lastHeartbeatAt`, and its wall-clock Unix-millisecond timestamp captured at instance reservation for `startedAt`. These are equivalent plumbing for the recovered public semantics.

Native `leaseRequired` and `leaseActive` belong to the multi-instance entitlement lease subsystem. Account/entitlement activation remains outside retained scope, so R8-042 does not synthesize that state: the retained single-profile projection reports both flags false.

## Command errors

The selected-profile resolver emits `PROFILE_ID_REQUIRED` when `profileId` is missing or invalid. An ID with no retained runtime entry emits `PROFILE_RUNTIME_UNAVAILABLE`. Runtime/state lock failures use `STATE_UNAVAILABLE`.

R8-042 replaces the rebuild-only `PROFILE_REQUIRED` / `INVALID_PAYLOAD` / `PROFILE_SCOPE_MISMATCH` behavior for this command only. Other command scopes are unchanged. Native error prose beyond the stable codes is not claimed where the recovered helper does not expose an independently distinguishable message.

## Rebuild boundary and validation

The prior rebuild public status returned five synthetic fields and could fabricate `stopped/offline` or `UNMANAGED_GAME_RUNNING`. R8-042 moves only `profile_instance_status` to a separate native projection. Existing internal lifecycle diagnostics and start/stop/reconcile return objects remain unchanged. Native cross-restart instance adoption/recovery is not claimed fully identical by this checkpoint; R8-042 closes the observable status projection for the retained instance state the rebuild owns.

Deterministic coverage now proves:

- JSON null for no managed/recoverable instance, including an unrelated unmanaged LastWar process;
- exact 12-key order for an active record;
- starting defaults and Unix-millisecond `startedAt`;
- exact per-instance bridge-route classification;
- fresh same-session heartbeat publication and connected-state classification;
- retained single-profile lease flags remain false;
- `PROFILE_ID_REQUIRED` and `PROFILE_RUNTIME_UNAVAILABLE` command-boundary behavior.

Release/checks builds, full deterministic suite, recovered-frontend hash check, JSON syntax and staged diff checks are required before checkpoint commit.
