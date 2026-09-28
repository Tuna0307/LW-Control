# R8-140 — LWBridge 0.3.17 auth/profile frontend parity

**Date:** 2026-09-28
**Status:** RECOVERED 0.3.17 AUTH/PROFILE FRONTEND PARITY

## Goal

Compare the recovered live 0.3.17 frontend auth/profile orchestration against the immutable 0.3.1 frontend without submitting any account action or changing native state.

## Source identity

0.3.17 executable SHA-256:
`4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`

Recovered live 0.3.17 main bundle:
- `index-BVfnK1wp.js`
- 377,972 bytes
- SHA-256 `44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6`

0.3.1 comparison sources:
- `index-sfL2sT3K.js` SHA-256 `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`
- `api-ClPPi2JT.js` SHA-256 `062ebd2362885af05e07bf4f0de00ed833293a83fefa6b54c26975fa1db9df47`

## Auth request wrappers

The following payload contracts are present identically in both versions:

- `auth_login` -> `{username,password}`
- `auth_activate` -> `{username,password,licenseCode}`
- `auth_renew` -> `{licenseCode}`
- `profile_instances_reconcile` -> `{autoLaunchAll}`
- `multi_entitlement_get` remains a separate zero-argument command.

Therefore no frontend request-shape change was found at the basic login/activate/renew/entitlement boundary.

## ProfileProvider ordering

0.3.17 source contains the exact helper at character offset `195019`:

`return{entitlement:await e(),profiles:await t()}`

The same exact helper exists in 0.3.1 `api-ClPPi2JT.js` at character offset `9785`.

This proves both versions perform initial profile refresh in the same order:

`multi_entitlement_get -> profile_list`

The event helper is also identical:

`return{entitlement:e,profiles:await t()}`

It occurs at 0.3.17 character offset `195091` and 0.3.1 API offset `9856`.

The 0.3.17 ProfileProvider around `bridge://profile-identity` (character offset `329420`) confirms:
- authorized/grace is required before provider refresh;
- `bridge://profile-identity` calls `profile_list` directly;
- `bridge://multi-entitlement` combines event entitlement with a fresh `profile_list`;
- leaving authorized/grace clears provider/profile selection state.

The corresponding 0.3.1 provider has the same structure around character offset `306704`.

## Correction to the rough version diff

`ACCOUNT_EXPIRED`, `DEVICE_MISMATCH`, `CLIENT_BUILD_UNAUTHORIZED`, and `CLIENT_UPDATE_REQUIRED` are all already present in the recovered 0.3.1 frontend. Their presence in 0.3.17 is therefore not, by itself, evidence of a new auth decision branch.

## Nearby real version delta

The recovered 0.3.17 API wrapper for `call_lua` accepts an optional profile scope and injects `profileId` when supplied. The recovered 0.3.1 wrapper calls `call_lua` with only `fnName` and `args`. Several automation wrappers show similar newer profile scoping.

This is a concrete frontend/API evolution, but it does not alter the auth/profile admission sequence recovered above.

## Native-analysis status

Read-only 0.3.17 string locations were found, including `SESSION_INVALID` at RVA `0x82E63A`, the account/build/device error cluster around RVA `0xD54EFA` through `0xD54F3F`, and `bridge://auth-state` at RVA `0xD55506`.

A full executable Capstone xref sweep did not return a reliable result before the scratch session became unresponsive. The scratch Python session was terminated. No native producer/branch address is claimed from that attempt.

## Limits / continuation

No login, activation, renew, or unbind request was sent. No credential file was read. No EXE or DLL bytes were modified.

Next: locate the 0.3.17 native AuthState producer with a bounded read-only disassembly strategy instead of another full-image sweep, then compare the resulting producer/transition sites to the already source-locked 0.3.1 functions.
