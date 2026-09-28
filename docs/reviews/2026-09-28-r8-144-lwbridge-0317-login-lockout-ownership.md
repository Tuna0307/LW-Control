# R8-144 — LWBridge 0.3.17 login lockout ownership

**Date:** 2026-09-28
**Status:** RECOVERED LOCKOUT OWNERSHIP / NO LIVE BRUTE FORCE

## Question

The visible login policy says repeated failures can trigger a 15-minute lock. This checkpoint determines whether that wait is merely a frontend timer or is backed by native/server state.

## Frontend behavior

In the recovered 0.3.17 frontend, helper `Hn` at character offset `214041` handles exact error codes:

- `ACCOUNT_LOCKED`
- `TOO_MANY_REQUESTS`

When either code is accompanied by `auth_state.lockedUntil`, the UI computes the remaining seconds from that timestamp and renders a countdown.

The auth screen at character offset `215120` independently derives a Login-button lock:

`login mode && lockedUntil != null && Date.parse(lockedUntil) > now`

The submit button is disabled while that condition is true.

Therefore the browser UI owns the countdown display and a client-side submit-button fence, but it does not manufacture the lock timestamp itself.

## Native evidence

The 0.3.17 executable retains native auth vocabulary including:

- `TOO_MANY_REQUESTS` at RVA `0x829580`
- `lockedUntil` at RVAs `0x82E602` and `0xD558B4`
- `retryAfter` at RVA `0x82E60D`
- `/api/login` at RVA `0x82E5F8`

`lockedUntil` and `retryAfter` are in the native auth request/response schema neighborhood beside `/api/login`. This is strong evidence that lock timing is communicated through the native/service layer rather than being only a browser-side stopwatch.

## 0.3.1 parity

The immutable recovered 0.3.1 frontend also contains both `ACCOUNT_LOCKED` and `TOO_MANY_REQUESTS` with the same countdown/error-rendering design. This behavior therefore predates 0.3.17.

## Security interpretation

Removing or altering the frontend submit-button condition would only remove a local convenience fence. It would not remove the native/service `TOO_MANY_REQUESTS`, `lockedUntil`, or `retryAfter` response handling.

A live brute-force or rate-limit-evasion attempt was not performed. The safe way to validate the full behavior is a controlled local/mock auth endpoint that returns lock responses and lets the client/native layers be observed without repeated guesses against the production service.

## Result

The visible 15-minute wait is **not the sole enforcement point**. It is a frontend representation of lock state provided through native authentication state, with native response vocabulary showing service-oriented rate-limit handling.
