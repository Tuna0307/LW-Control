# R8-138 — original profile-identity / pipe boundary

**Date:** 2026-09-28
**Reference:** verified LWBridge 0.3.1
**Status:** EXACT IDENTITY CONTRACT + LIVE SESSION BLOCKER + CONTROLLED-PIPE PREREQUISITE RECOVERED

## Goal

Continue from R8-137 and determine why the untouched original ProfileProvider cannot obtain its selected profile, then identify an original-owned route to clear `profile-switch-loading` without fabricating React state.

The live original remained PID 24660 with CDP attached. The reversible shared-admission hook remained active. Native AuthState remained genuinely `signedOut`.

## Exact entitlement failure

A direct read-only live probe proved:

- `auth_state` -> `signedOut`;
- `multi_entitlement_get` -> exact `SESSION_INVALID` in about 16 ms;
- `profile_list` -> success with the real selected profile;
- `.profile-switch-loading` -> still present.

This closes the R8-137 ambiguity. ProfileProvider fails on its first awaited entitlement dependency before it can commit the otherwise-healthy profile list.

## Ordinary profile mutations do not refresh the provider
Three idempotent genuine original commands were tested against the already-current profile:

- `profile_note_set` with the existing empty note;
- `profile_primary_set` on the already-primary profile;
- singleton `profile_reorder` to the existing order.

All succeeded and returned refreshed native profile state. None cleared `profile-switch-loading`.

Therefore these ordinary registry-return paths are not useful producers of the frontend `bridge://profile-identity` refresh.

## Exact identity-binding contract

Immutable-EXE strings expose the native identity transaction:

`UPDATE profiles SET game_uid = ?, role_name = ?, server_id = ?, updated_at = ? WHERE id = ?`

Nearby exact vocabulary includes:

- `profile identity accepted`;
- `profile identity rejected`;
- `bridge://profile-identity`;
- `INVALID_GAME_UID`;
- `INVALID_ROLE_NAME`;
- `INVALID_SERVER_ID`;
- `PROFILE_UID_CONFLICT`;
- `INVALID_PROFILE_IDENTITY`;
- `profile.identity`.

The already-source-backed identity handler around RVA `0x417FE6` extracts exact payload keys `gameUid`, `roleName`, and `serverId`.
The known success transition at RVA `0x4183B3` sets instance `identityConfirmed = true` and normally advances the instance phase to `running`.

The recovered generic control-pipe envelope remains exactly:

`version, type, profileId, instanceId, requestId, timestamp, payload`

Together these facts narrow the inbound game-side message to the standard original envelope with type `profile.identity` and an identity payload containing `gameUid`, `roleName`, and `serverId`.

## Natural-route negative proof

Current original runtime logs contain no `profile.identity` or `profile identity accepted`.

The current launch instead reaches:

- secure xLua proxy load;
- Lua-state capture;
- `authorization missing`;
- game main chunk load;
- host `BRIDGE_START_TIMEOUT`.

So the natural identity publisher is downstream of bridge authorization/package startup that the current signed-out host does not reach.

## Original proxy outbound primitive

The immutable secure proxy contains exact Lua global:

`__XluaBridgePipeSend`

alongside `__XluaBridgeLoad`, `__XluaBridgeEvalHook`, `XluaBridgeHandlePipeMessage`, `XluaBridgeNativeStart`, and the original bridge runtime vocabulary.
This provides a concrete original-owned path for a controlled bootstrap to send a serialized original envelope back through the proxy pipe.

## Heartbeat experiment

A fresh controlled format-2 package was built with a bootstrap that attempts one non-destructive `heartbeat` envelope through `__XluaBridgePipeSend`.

Controlled package:
- size: 539 bytes;
- SHA-256: `8d08dcc16d59d7bb2d3a5c8e2f6e071eed14fb6eb41b5d461646e27e97e685aa`;
- authentic on-disk `bridge-scripts.dat`: unchanged.

The already-proven RAM-only ticket-signature, envelope-signature, and post-manifest package-swap hooks armed successfully in LastWar PID 4572.

However the secure proxy logged `authorization missing` before controlled package load. Therefore the experiment did not reach the bootstrap and does **not** yet prove the `__XluaBridgePipeSend` calling convention.

Cleanup completed; LastWar and launcher processes are no longer running.

## Missing prerequisite and exact next continuation

The normal signed-out original launch runtime currently contains:
- `authorization.challenge`;
- `build.manifest`;
- synthetic `package-key.envelope`;
- **no `authorization.ticket`** after cleanup.

The previously live-proven R8-130 standalone path explicitly creates a source-locked synthetic LWAT2 `authorization.ticket` from the current challenge + machine binding before launch.

Next block: combine that already-proven ticket installation with the normal original-host launch (so its real pipe server/instance exists), keep the R8-138 heartbeat controlled package, and verify one host heartbeat before attempting `profile.identity`.

Do not reopen ProfileProvider, the six Home calls, or ordinary registry mutations.
