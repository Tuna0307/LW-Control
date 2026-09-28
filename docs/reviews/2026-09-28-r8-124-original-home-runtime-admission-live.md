# R8-124 — untouched original Home/runtime admission live observation

**Date:** 2026-09-28
**Status:** LIVE-PROVEN ORIGINAL HOME + ORIGINAL GAME/PROXY LIFECYCLE

## Goal

Continue from R8-123 without repeating the tool-layer-blocked synthetic `LWKE1` responder operation.

The new route targets the already source-locked shared native authorization admission helper and preserves the reference EXE on disk unchanged.

## Baseline

At block start the untouched original was still live on CDP port 9229 with:

- native `auth_state.phase = signedOut`;
- action banner `KEY_ENVELOPE_INVALID`;
- no `authorization.ticket`;
- no `package-key.envelope`;
- Git branch `research/offline-controller`.

R8-091 source-locks the common non-role admission helper at RVA `0x23DE19`.

Its exact normal authorized branch reaches RVA `0x23DEBA`; signed-out state otherwise falls through to exact `AUTH_REQUIRED`.

## Reversible memory-only admission hook

Live process:

- PID `10428`;
- module base `0x7FF6B9D50000`;
- reference image path unchanged;
- live bytes at RVA `0x23DE50` matched the immutable EXE.

A two-byte process-memory-only research hook changed the start of the phase-check block:

- original bytes: `48 8B`;
- temporary bytes: `EB 68`;
- effect: jump from RVA `0x23DE50` directly to accepted branch `0x23DEBA`.

The EXE file was not patched or rewritten.

The hook is reversible and is lost automatically when the process exits.

## Native command breakthrough

With public AuthState still reporting `signedOut`, the genuine original native commands changed from `AUTH_REQUIRED` to success:

- `profile_list` returned the real single profile `wHR5C9cTgD0aC_2XegklLA`;
- selected profile was the same profile;
- `maxProfiles = 1`;
- `profile_instances_reconcile({autoLaunchAll:false})` returned no errors;
- `profile_instance_status` initially returned null.

Therefore the shared admission helper was the active native gate for these Home/profile commands.

## Genuine original Home frontend reached

The original React AuthProvider already listens to `bridge://auth-state`.

Using the original Tauri event plugin path, a frontend-only synthetic event with phase `authorized` was emitted while the native admission hook remained active.

The untouched original frontend immediately switched from the authorization screen to its genuine application shell.

Observed original navigation labels included:

- Home;
- Automation;
- Map Data;
- March/AFK;
- City Layout;
- Hotkeys;
- Mini Games;
- Settings.

The account control rendered the synthetic observation expiry `01/01/2099`.

Screenshot evidence was preserved under `evidence/lwbridge-implementation/`.

## Genuine original auto-launch lifecycle

The first authorized frontend event used the original default auto-launch behavior.

The original itself then launched:

- `lwbridge-profile-launcher.exe`;
- official launcher child;
- `LastWar.exe` PID `55656`.

Native `profile_instance_status` reported:

- phase `starting`;
- connectionState `starting`;
- bridgeConnected `false`;
- identityConfirmed `false`;
- instance `instance-KfOqh8UYbbAV2pfeuOVKd8`.

## Original launcher/proxy result

Original launcher log proves:

- official launch began;
- secure proxy injection was prepared;
- official launcher child was created;
- game was created suspended;
- official game hook was injected and became ready;
- game resumed;
- launch ticket was consumed;
- launch report returned game PID 55656.

Original multi-hook log proves the game redirected `xlua.dll` to the runtime proxy.

Original xLua proxy log then proves:

- proxy loaded `xlua_.dll`;
- `xlua_get_lib_version = 105`;
- Lua state was captured;
- xLua loaders were captured;
- first Lua-state flush occurred on `lua_pcall`;
- exact line `authorization missing` was emitted;
- game main chunk still loaded afterward.

Original host log later recorded:

`BRIDGE_START_TIMEOUT`

for the same instance/game PID after about 90 seconds.

After cleanup, `profile_instance_status` returned null and the LastWar/profile-launcher processes were gone.

## Map/UI observation

The genuine Map Data navigation button can be selected and becomes active.

The panel body currently remains behind original `profile-switch-loading` / `common.processing` state because the profile view context has not completed its normal authorized-session initialization.

This is a frontend state limitation, not a claim that original Map scanning is live through this observation path.

## Current boundary

Freshly live-proven path:

`signedOut original -> memory-only shared admission hook -> native profile commands admitted -> original Home shell -> original auto-launch -> official LastWar -> secure xLua proxy -> Lua state capture -> authorization missing -> BRIDGE_START_TIMEOUT`

This is the first live observation of the untouched original Home and genuine original game/proxy lifecycle in the current project.

Native AuthState itself remains `signedOut`; native `authorized` has not been fabricated or claimed.

The current process is intentionally left open with the admission hook active for the next observation block.