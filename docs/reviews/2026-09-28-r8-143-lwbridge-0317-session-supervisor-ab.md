# R8-143 — LWBridge 0.3.17 session supervisor A/B

**Date:** 2026-09-28
**Status:** LIVE-PROVEN 0.3.17 SESSION SUPERVISOR A/B

## Goal

Test one narrow startup distinction locally: no persisted session versus a syntactically valid but unusable local `auth-session.v2.json`. No vendor authentication request, license activation, ticket, envelope, or executable modification was used.

## Control: no persisted session

With neither `auth-session.v2.json` nor legacy `auth-session.json` present, the live 0.3.17 host reports:

- `auth_state.phase = signedOut`
- `auth_state.errorCode = null`
- `accessRole = normal`
- `profile_list -> AUTH_REQUIRED`
- `multi_entitlement_get -> SESSION_INVALID`

This reproduces the R8-139 baseline.

## Controlled malformed SessionV2 fixture

After backing up the roaming state, 0.3.17 was restarted with a local fixture containing only `{}` at the normal `auth-session.v2.json` path.

The live native result changed to:

- `auth_state.phase = signedOut`
- `auth_state.errorCode = SESSION_INVALID`
- `accessRole = normal`
- `profile_list -> SESSION_INVALID`
- `multi_entitlement_get -> SESSION_INVALID`

This proves 0.3.17 distinguishes “no session file” from “a v2 session file exists but cannot be admitted.”

## Restore / post-check

The fixture instance was terminated, the malformed session file was removed, and 0.3.17 was relaunched normally as PID `46628`.

The original control behavior returned exactly:

- `signedOut` with `errorCode = null`
- `profile_list -> AUTH_REQUIRED`
- `multi_entitlement_get -> SESSION_INVALID`

The roaming auth-data root after restoration contains `auth-credentials.v2.json` but contains none of:

- `auth-session.v2.json`
- `auth-session.json`
- `authorization.ticket`
- `package-key.envelope`

## Interpretation

This is live behavioral parity with the 0.3.1 supervisor contract recovered in R8-093/R8-092: persisted-session failure is normalized to `SESSION_INVALID`, while a clean absence of any usable session settles into normal `signedOut` state.

It also explains the different profile error surfaces:

- clean signed-out/no-session state -> `AUTH_REQUIRED`;
- invalid-restored-session state -> `SESSION_INVALID`.

The multi-entitlement path returns `SESSION_INVALID` in both cases because it independently requires usable session/service authorization.

No authorization bypass is claimed.

## Invalid-session retention

A second controlled run checked the malformed v2 file itself after startup. The 3-byte `{}` fixture was still present after 0.3.17 had settled into `signedOut / SESSION_INVALID`.

Therefore startup rejection does **not** automatically delete the invalid `auth-session.v2.json` in this observed path. A stale invalid local session can persist across launches and keep producing the invalid-session surface until another lifecycle removes or replaces it.

The fixture was then deleted by the research harness, 0.3.17 was relaunched normally as PID `45648`, and the clean baseline (`signedOut`, public `errorCode=null`, entitlement `SESSION_INVALID`) returned.

## Legacy-session fallback

With v2 absent, a controlled legacy `auth-session.json` fixture containing only `{}` was also tested. 0.3.17 settled into the same invalid-session surface:

- `auth_state.phase = signedOut`
- `auth_state.errorCode = SESSION_INVALID`
- `profile_list -> SESSION_INVALID`
- `multi_entitlement_get -> SESSION_INVALID`

The fixture was removed afterward and the normal no-session baseline restored. This is behavioral evidence that 0.3.17 still checks the legacy session path when v2 is absent, consistent with the recovered 0.3.1 v2-first / legacy-fallback design.
