# R8-054 - audit lastwar_localize strict parity boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** reclassify the existing R7 Last War localization reconstruction under the strict R8 one-to-one goal, closing native admission/default behavior and preserving only evidence-backed equivalent cache plumbing.

## Native authority

Primary evidence:

- `lastwar_localize` handler `0x140199316-0x140199C30`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- native localization helper `0x140235ED5-0x1402361FE`;
- native locale loader `0x14023509E-0x140235AE7`;
- retained frontend wrapper `lastwar_localize({language,keys})`;
- prior R7 localization recovery in `docs/lwbridge-map-scan.md`.

## Native public admission/defaults

Native first awaits shared authorization state. Unavailable state is exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

This admission is owner-excluded and is not implemented by the rebuild.

Native payload parsing is permissive:

- non-object payload behaves like no fields supplied;
- missing or wrong-type `language` falls back to `"en"`;
- missing or wrong-type `keys` container becomes an empty key list;
- an array-valued `keys` field is cloned for downstream typed conversion.

The exact error behavior for non-string elements inside an otherwise valid `keys` array is not closed in R8-054 and is not claimed.

## Exact localization contract already recovered

Fresh native helper evidence confirms the prior R7 contract:

- maximum accepted key count: **200**;
- 201+ keys fail with code `TOO_MANY_LOCALE_KEYS` and native message prefix `too many LastWar locale keys: ` plus the count;
- lookup order is requested locale first;
- when requested locale is not `en`, English is the second lookup;
- if neither locale resolves the key, the key string itself is returned.

Native locale-loader vocabulary includes:

- `STATE_UNAVAILABLE` / `locale cache is unavailable`;
- `UNSUPPORTED_LOCALE` / `unsupported LastWar locale: ...`;
- `LOCALE_VERIFY_FAILED` / `LastWar locale verification failed: ...`;
- `INVALID_LOCALE_MANIFEST` / `read locale manifest`;
- `INVALID_LOCALE` / `read locale file`, `decompress locale file`, and `locale payload must be an object` paths.

Prior R7 extraction proved the embedded manifest is version 1, source version 23443, `json-gzip`, 1009 keys, with nine locales: `en`, `zh-CN`, `zh-TW`, `ja`, `ko`, `vi`, `id`, `ru`, `pt`.

## Current rebuild comparison

`LastWarLocaleService` correctly preserves the recovered normal lookup/fallback behavior, exact 200-key boundary, nine-locale verified cache model, key echo, and matching loader/error vocabulary. `%LOCALAPPDATA%\\LWBridgeRebuild\\locales` remains explicit equivalent plumbing because game-owned locale payloads are not committed.

Two strict-parity deviations remain:

1. the rebuild exposes localization without native authorization-state admission;
2. `ParseRequest` currently throws rebuild-only `INVALID_PAYLOAD` errors for non-object payloads, wrong-type `language`, wrong-type `keys`, and non-string key elements, while native is permissive for the outer payload/language/keys-container cases.

R8-054 does not change production parsing because the exact non-string array-element path is not fully closed and changing only part of parser behavior would leave a mixed contract. It also does not synthesize authorization state.

## Evidence classification

- max 200 / `TOO_MANY_LOCALE_KEYS`: `EXACT_NATIVE`;
- requested→English→key fallback: `EXACT_NATIVE`;
- locale cache verification/error vocabulary: `EXACT_NATIVE`;
- rebuild cache location/file plumbing: `EQUIVALENT_REIMPLEMENTATION`;
- native permissive outer payload/language/keys-container defaults: `EXACT_NATIVE`;
- non-string key-array element behavior: `UNKNOWN`;
- authorization-state admission implementation: `OWNER_EXCLUDED_UNKNOWN`;
- current rebuild `INVALID_PAYLOAD` parser branches: `DEVIATION`;
- current public command overall: `PARTIAL / EQUIVALENT_REIMPLEMENTATION`, not exact parity.