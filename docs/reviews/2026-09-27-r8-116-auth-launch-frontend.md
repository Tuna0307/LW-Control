# R8-116 — recover reference frontend auth-to-launch orchestration

**Date:** 2026-09-27
**Reference:** recovered exact LWBridge 0.3.1 frontend assets
**Status:** RECOVERED FRONTEND ORCHESTRATION
**Scope:** reference frontend login form, credential prefill, auth-state transition and post-auth profile reconciliation. No credential contents are read, no auth request is sent, and no production behavior is changed.

## Result

R8-115 proved which host response paths can populate `package-key.envelope`. R8-116 closes the user-facing transition that legitimately reaches those paths and then proceeds into game launch.

The exact reference API wrapper is:

`auth_login({username,password})`.

The signed-out/locked auth screen submits username/password through that wrapper. Its HTML admission bounds are:

- username: `minLength=3`, `maxLength=50`, required;
- password: `minLength=8`, `maxLength=72`, required.

On mount, the auth screen calls `auth_credentials` and fills username/password only when the corresponding local React state is still empty. R8-116 does not read or recover any stored credential value.

## Post-auth launch bridge

The AuthProvider marks normal `login` and `activate` actions as post-auth-launch actions. After a returned/published AuthState is `authorized` or `grace`, the reference frontend runs the exact sequence:

1. best-effort `multi_entitlement_get`;
2. `profile_instances_reconcile({autoLaunchAll:<setting>})`.

The entitlement call is explicitly best-effort: failure is ignored before profile reconciliation.

The auto-launch setting is owned by localStorage key:

`lwbridge.autoLaunchGame`

Its default is **true**. It is false only when the stored string is exactly `"false"`.

Therefore a normal successful owner login is not an isolated account operation or a research-only service probe. In the reference product it is intentionally wired to proceed into the existing profile/game lifecycle, normally with auto-launch enabled.

## Current-machine reachability

A filename-only search of the relevant Local/Roaming AppData roots in this block found no:

- `auth-session.v2.json`;
- `auth-session.json`;
- `auth-credentials.v2.json`;
- `auth-credentials.json`;
- `authorization.ticket`;
- `package-key.envelope`;
- `device-key-cleanup.pending`.

The current bridge runtime still contains only `authorization.challenge` and matching `build.manifest` among the recovered auth/package prerequisites.

This machine therefore has no at-rest session/credential restore route available through those original filenames. The next authentic transition is an **owner-driven normal login (or activation where appropriate)** through the reference product, not fabricated session material or research-only direct service probing.

## Map-source impact

Combining R8-115 and R8-116 gives the intended original product chain:

`auth screen -> auth_login -> normal host login future -> ticket/envelope ingest -> authorized/grace -> entitlement refresh -> profile_instances_reconcile(autoLaunchAll) -> original game/proxy lifecycle -> R8-104 assembled-source window`.

If the owner performs the normal login in the reference UI and authorization succeeds, the immediate research target is no longer auth semantics. Re-check for `authorization.ticket` / `package-key.envelope`, observe the reference-owned game/proxy launch, and return directly to the R8-104 source window rather than exploring another fallback.

This checkpoint does not enter credentials, invoke `auth_login`, recover the Lua source, or prove Map working.

At the end of the block the verified reference `lwbridge-0.3.1.exe` was launched normally and left open for owner interaction. The reference process remained running while `LastWar.exe` stayed absent; the bridge runtime still contained no ticket/envelope/session/credential artifact. No credential-bearing action was performed by research tooling. The owner can now use the visible reference Login form locally; credentials should not be pasted into chat or tooling.

## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_auth_launch_frontend.py`

Evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-116-auth-launch-frontend.json`

Reference asset hashes:

- API: `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`;
- index: `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`.

## Status

**MAP: NOT WORKING. HOME: NOT WORKING. WHOLE LWBRIDGE: NOT READY.**
