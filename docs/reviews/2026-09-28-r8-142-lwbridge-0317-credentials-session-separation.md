# R8-142 — LWBridge 0.3.17 saved credentials vs native session

**Date:** 2026-09-28
**Status:** LIVE-PROVEN CREDENTIAL/SESSION SEPARATION

## Result

LWBridge 0.3.17 can have saved username/password material available to the login form while the native authorization state remains fully signed out.

Current live PID `44748` still reports:

- `auth_state.phase = signedOut`
- `accessRole = normal`
- no expiry / heartbeat / lock / public auth error
- `profile_instance_status -> AUTH_REQUIRED`
- `multi_entitlement_get -> SESSION_INVALID`

At the same time, the login form contains saved username and password values. Their contents were not read or recorded.

## Frontend proof

The recovered 0.3.17 frontend at character offset `214832` calls `credentials()` on auth-screen mount and copies the returned `username` and `password` into otherwise-empty form state.

The AuthProvider separately defines:

- `credentials` through the `auth_credentials` command at character offset `248492`;
- `login` through the distinct `auth_login` command at character offset `248572`.

The form submit handler invokes `login(username,password)` only when the user submits the Login mode. Loading saved credentials alone does not call login.

The same three source patterns exist in the recovered 0.3.1 frontend at offsets `192909`, `226545`, and `226624` respectively.

## Filesystem state

Current roaming state contains `auth-credentials.v2.json` but no `auth-session.v2.json` and no legacy `auth-session.json`.

Credential file metadata only:

- size: 367 bytes
- modified: `2026-09-27T16:19:28.020Z`

The pre-0.3.17 backup contains the same credential filename with the same size and modification timestamp, while also lacking both session filenames. This proves the saved-credential state predates the 0.3.17 launch; 0.3.17 did not create this distinction by deleting a previously-present session during startup.

No credential-file contents were read.

## Correction to R8-117

R8-117's then-current machine observation said no `auth-credentials.v2.json` was present. That observation is now obsolete because the machine state changed afterward. The architectural result from R8-117 remains valid: credentials and sessions are separate persisted objects.

## Implication

Saved credentials are a login convenience, not native authorization authority. A user can have a prefilled login screen while the native supervisor has no usable persisted session and therefore exposes `signedOut`, `AUTH_REQUIRED`, and `SESSION_INVALID`.

This rules out the theory that simply recovering the saved credential file should make 0.3.17 start authorized. The next useful target remains the startup session/supervisor and downstream session/runtime-material lifecycle, not the form-prefill path.

No authorization bypass is claimed by this checkpoint.
