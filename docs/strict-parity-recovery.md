# LWBridge 0.3.17 parity directive

**Effective:** 2026-09-29

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Goal

Reproduce the observable **in-scope post-auth product behavior** of LWBridge 0.3.17 one-for-one, then make that recovered behavior work against the current Last War client.

## Explicit scope exception

The original LWBridge login/account/licensing/entitlement system is **out of scope for reconstruction**.

We do not need parity for:

- login/account-management UX;
- credential exchange;
- token refresh/auth protocols;
- subscription/purchase flows;
- license validation/activation;
- entitlement bypass.

If a login/locked screen is encountered, it may be documented as an access boundary, but it is not a clone target.

If an in-scope feature later proves it consumes state produced by auth, only that downstream dependency contract should be traced.

## Authority order

1. verified 0.3.17 reference bytes/runtime observations;
2. exact assets extracted from that reference;
3. current official Last War artifacts for compatibility mapping;
4. 0.3.1 findings as hypotheses/history only;
5. implementation choices only when explicitly labeled and not confused with recovered behavior.

## Rules

- Do not redesign before recovering.
- Do not invent missing values.
- Do not silently substitute a 0.3.1 behavior for 0.3.17.
- Do not call an in-scope feature parity-complete from UI appearance alone.
- Preserve exact recovered bytes unchanged where practical.
- Record unknowns explicitly.
- Separate static recovery from live proof.
- Keep user-visible in-scope behavior as the parity target even when current-client compatibility requires different internals.
- Do not bypass login/auth/entitlement.
- Do not spend project time reconstructing the original login/licensing system unless the project lead explicitly reopens that scope.

## Required trace for an in-scope function

Where evidence permits, recover:

`UI -> frontend/API -> host command -> provider/runtime -> state/storage -> visible result`

If the trace reaches an auth-produced dependency, document the consumed state and stop at that boundary unless the project lead explicitly authorizes a narrower dependency investigation.

## Current order

UI parity comes first. Function recovery begins after the in-scope 0.3.17 UI baseline is established.
