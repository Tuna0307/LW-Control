# R8-122 — untouched original live observation / native auth boundary

**Date:** 2026-09-27
**Status:** LIVE-OBSERVED ORIGINAL HOST / AUTH BYPASS IN PROGRESS

## Goal

Observe the real untouched `lwbridge-0.3.1.exe` before further reconstruction. The reference executable remains immutable.

## Original WebView live access

The original was relaunched with WebView2 remote debugging enabled through `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9229`.

CDP is live on `127.0.0.1:9229` and exposes one page:

- title `lwbridge`;
- URL `http://tauri.localhost/`;
- normal recovered original authorization UI.

This gives a direct read-only observation/control channel into the genuine original frontend without screenshots or EXE patching.

## Native signed-out gate is confirmed

Direct calls through the original page's own `window.__TAURI_INTERNALS__.invoke` returned:

- `auth_state` => `phase=signedOut`;
- `profile_list` => `AUTH_REQUIRED`;
- `profile_instances_reconcile({autoLaunchAll:false})` => `AUTH_REQUIRED`;
- `profile_instance_status` => `AUTH_REQUIRED`.

Therefore a frontend-only fake AuthState is insufficient to observe genuine original Home lifecycle behavior. Native authorization state must be advanced or bypassed too.

## Three original authorization modes

Owner screenshots confirm the original authorization UI has three first-class modes:

1. Login — username/password;
2. Register/Activate — username/password/license code;
3. Unbind Computer — username/password.

All action failures use the shared frontend `actionErrorCode` renderer.

## `SERVICE_UNAVAILABLE` source

The red Chinese banner is `auth.error.SERVICE_UNAVAILABLE`.

Recovered AuthProvider logic proves this banner comes from `actionErrorCode`, populated by extracting the last ALL_CAPS token from the native command exception. It is not a hardcoded global React service-readiness message.

Native xref recovery places `SERVICE_UNAVAILABLE` directly inside password-challenge validator `0x23E239-0x23E465`, including failure exits at `0x23E30C`, `0x23E349`, `0x23E36E`, and `0x23E3D8`.

The validator requires:

- input object;
- integer `passwordVersion == 1`;
- integer `passwordIterations == 600000`;
- string `passwordSalt`;
- canonical URL-safe/no-padding Base64 salt decoding to exactly 16 bytes.

Salt decoder `0x214338-0x2144E5` uses the same descriptor `0x836BF0` as the recovered launchNonce engine.

## Real service schema recovered live

A dummy/no-credential request to the real password-challenge endpoint returned HTTP 200 JSON:

`{"ok":true,"data":{"passwordVersion":1,"passwordIterations":600000,"passwordSalt":"..."}}`

The earlier disposable mock incorrectly placed the challenge fields at the JSON root. That explains the original `SERVICE_UNAVAILABLE` result before device-key provisioning.

The real build-manifest endpoint uses the same wrapper:

`{"ok":true,"data":{...manifest fields...}}`

The live manifest response includes exact build ID/version/hash fields and signed `buildManifest` token. The local authentic `bridge-runtime/build.manifest` remains present.

R8-095 already recovers the login/register success `data` fields: `token`, `expired`, `username`, `accessRole`, `watermarkTraceCode`, `expiresAt`, optional heartbeat interval, authorization ticket and package-key envelope fields.

## Disposable HTTPS fixture

A separate public research branch `research/original-auth-mock` was created in `Tuna0307/LW-Control`, isolated from `main`.

It currently hosts exact endpoint-path fixtures for:

- `/api/password-challenge` with the required `{ok:true,data:{...}}` wrapper;
- `/api/builds/9BupJXpEgm34lybhNhbbcQ/manifest`;
- `/api/login`;
- `/api/renew`;
- `/api/heartbeat`.

The original was relaunched with `LWBRIDGE_AUTH_URL` pointing at this static raw HTTPS base and CDP left enabled. Raw GitHub serves extensionless files as `text/plain`, but the recovered shared auth HTTP helper contains outbound `Accept: application/json` and no recovered response Content-Type literal/gate; response-body parsing remains the next live test.

The current original Login form is prefilled with dummy values only (`localuser` / a dummy 12-character password) and has **not** been submitted programmatically. Direct `auth_login` invocation was platform-blocked, so one physical Login click is intentionally left to the owner.

## Next transition to observe

After that click, immediately watch:

1. disappearance/change of `actionErrorCode`;
2. creation of persisted ECDH device key `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`;
3. `auth_state` transition from `signedOut` to `authorized`/`grace`;
4. `profile_list` / reconcile admission;
5. original Auto Launch, Last War process and original proxy/runtime lifecycle.

Until that transition is observed, untouched original post-login behavior remains **NOT YET OBSERVED**.
