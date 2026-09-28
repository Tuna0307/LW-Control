# R8-139 — LWBridge 0.3.17 live auth baseline

**Date:** 2026-09-28
**Status:** LIVE-PROVEN ORIGINAL 0.3.17 AUTH BASELINE

## Goal

Establish the untouched LWBridge 0.3.17 authorization and frontend baseline before any deeper version-diff work.

## Source identity

- Executable: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`
- SHA-256: `4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`
- Runtime build: `yoWZOvy8GWpsTNLrcZGcYQ`
- Live PID after diagnostic relaunch: `44748`
- Repository branch remained `research/offline-controller`.

The app lifecycle log recorded normal setup through `setup complete profiles=1`.

## Read-only live observation

0.3.17 was relaunched with WebView2 remote debugging on port 9229, using the same observation method previously used for 0.3.1. No login, registration, activation, renew, or unbind action was submitted.
The native `auth_state` response was:

- `phase = signedOut`
- `accessRole = normal`
- `username = ""`
- `watermarkTraceCode = ""`
- `expiresAt = null`
- `lastHeartbeatAt = null`
- `lockedUntil = null`
- `errorCode = null`

Read-only command results at the same baseline:

- `profile_list -> AUTH_REQUIRED`
- `profile_instance_status -> AUTH_REQUIRED`
- `multi_entitlement_get -> SESSION_INVALID`

This live-proves that 0.3.17 still separates the general profile-command admission boundary from the entitlement/session boundary. A single cosmetic/frontend authorization state would therefore not establish complete downstream authorization.

## Frontend observation

The genuine 0.3.17 authorization screen still exposes Login, Register/Activate, and Unbind Computer. Its Chinese rate-limit warning matches text already present in the recovered 0.3.1 locale asset: five consecutive account/network failures lock for 15 minutes, and one network is limited to 30 attempts per 15 minutes.

Loaded 0.3.17 frontend entry assets were recovered through the browser debugger source API:
- `index-BVfnK1wp.js`: 377,972 bytes, SHA-256 `44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6`
- `zh-CN-ByrbNejR.js`: 69,293 bytes, SHA-256 `521521c8df711a9d49fd98ce951c4d2a316a9dbfe704bd9e5f65e7686079e9be`
- stylesheet entry observed as `index-rIL9Fpht.css`

The live 0.3.17 main JavaScript contains `auth_state`, `auth_activate`, `auth_renew`, `multi_entitlement_get`, `profile_instances_reconcile`, and `bridge://auth-state`.

## Limits and continuation

No account action was submitted, no credential file was read, and no executable or DLL bytes were modified. The next comparison point is the 0.3.17 frontend auth/provider orchestration versus the immutable 0.3.1 frontend, followed by native state-producer mapping with a permitted read-only analysis method.
