# R8-053 - fence profile_settings_get at authorization-state boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained read-only `profile_settings_get` persistence/public-result contract while refusing to bypass its owner-excluded authorization-state admission.

## Native authority

Primary evidence:

- `profile_settings_get` handler `0x140136EC4-0x14013779B`;
- shared authorization-state future `0x1400DC79E-0x1400DC995`;
- required JSON-string helper `0x1403AC6C7`;
- profile-settings read helper `0x1403DBC87-0x1403DC188`;
- public settings serializer `0x1403DC575-0x1403DC661`;
- handler result wrapper/projector `0x1403EB4EF-0x1403EB5DB`;
- R8-028 profile-settings persistence evidence.

## Public payload

`profile_settings_get` requires a JSON-string field:

- `profileId`

Missing or wrong-type `profileId` uses exact code `INVALID_REQUEST`. No separate human-readable detail string was established from this helper, so R8-053 does not invent one.

## Authorization-state admission

Before performing the settings read, native awaits the same shared authorization-state future used by other retained commands.

Unavailable authorization state returns exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

Authorization/account-state implementation is owner-excluded. R8-053 therefore does not substitute an always-available state or remove this admission rule.

## Persistence/read contract

After authorization admission, the native path reads the target profile's singleton `settings` row from `profile.db` using the already recovered R8-028 query:

```sql
SELECT revision, value_json FROM settings WHERE id = 1
```

The read helper uses the same per-profile settings storage family recovered in R8-028. Malformed stored settings can surface native `PROFILE_DATA_INVALID` from the read path.

## Exact public result

The serializer at `0x1403DC575` projects exactly these keys:

```json
{
  "profileId": "<requested profile id>",
  "revision": 0,
  "value": {}
}
```

`revision` is the persisted integer revision. `value` is the parsed persisted JSON value. The getter therefore differs from the R8-028 save result documentation by explicitly including `profileId` in its public projection.

## Why no production implementation is added

The rebuild already owns an equivalent local `ProfileSettingsStore.Read()` from R8-028, so the storage operation itself is implementable. However, native `profile_settings_get` is not simply a storage read: it first requires authorization state.

Wiring the current store directly to a public getter would silently remove native admission behavior. Reconstructing or synthesizing authorization/account state is outside owner scope. R8-053 therefore makes no runtime-code change.

## Evidence classification

- required `profileId` field/type and `INVALID_REQUEST`: `EXACT_NATIVE`;
- singleton settings SQL/read ownership: `EXACT_NATIVE` / previously recovered R8-028;
- public `{profileId,revision,value}` projection: `EXACT_NATIVE`;
- `PROFILE_DATA_INVALID` read-path vocabulary: `EXACT_NATIVE`;
- authorization unavailable error: `EXACT_NATIVE`;
- authorization-state implementation: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.