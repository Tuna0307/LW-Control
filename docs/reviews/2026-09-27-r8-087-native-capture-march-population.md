# R8-087 — recover native-capture march/train population

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** safe-region march record population, raw-field fallbacks and nested train composition. No production scanner change.

## Result

Both verified embedded proxies build the march producer from the same 23-field cached IL2CPP offset bundle:

`_uuid`, `type`, `targetPos`, `startPos`, `homePos`, `ownerUid`, `ownerName`, `ownerServer`, `ownerCurServerId`, `allianceUid`, `allianceName`, `allianceAbbr`, `power`, `startTime`, `endTime`, `worldId`, `status`, `_curHp`, `monsterId`, `monsterType`, `monsterSpecialType`, `monsterRallyNum`, and `train`.

The builder walks parent classes when needed and stores unresolved fields as -1, so producer nullability is tied to actual field-resolution success rather than assumed class layout.
## Direct march-field routes

The raw bundle feeds the R8-083 native march record exactly as follows:

- `_uuid -> uuid`; the producer requires a positive qword before admitting the record.
- `type -> marchType`.
- `ownerServer -> ownerServer`.
- `ownerCurServerId -> ownerCurServerId`.
- `worldId -> worldId`.
- `status -> status`.
- `monsterId -> monsterId`.
- `monsterType -> monsterType`.
- `monsterSpecialType -> monsterSpecialType`.
- `monsterRallyNum -> monsterRallyNum`.
- `power -> power`.
- `startTime -> startTime`.
- `endTime -> endTime`.
- `_curHp -> curHp`.

The five string fields `ownerUid`, `ownerName`, `allianceUid`, `allianceName`, and `allianceAbbr` are each passed through the same IL2CPP-string conversion helper before entering the exact R8-083 native string slots.
## pointIndex and isMonster fallbacks

R8-086 established the preferred `GetMarchCurPosIndex` route. R8-087 closes its fallback chain:

1. positive `GetMarchCurPosIndex`
2. positive raw `targetPos`
3. positive raw `startPos`
4. nullable raw `homePos`

The first positive source wins. If no preferred source succeeds, `homePos` supplies the final nullable value/presence pair.

R8-087 also closes the previously partial `isMonster` fallback. When `IsMonsterOrOrdinaryBoss` is available, the producer stores `result > 0`. When that getter is unavailable, it uses `monsterId > 0` if the raw `monsterId` field is present; otherwise the derived value is false. The `isMonster` field itself is then marked present.
## Nested train composition

The march `train` source resolves four outer fields in order: `uuid`, `cfgId`, `type`, and `config`.

When `config` is present, a second field resolver obtains exactly `id`, `quality`, and `carriageNum`.

These map to the six R8-083 train fields:

- `uuid -> train.uuid`
- `cfgId -> train.cfgId`
- `type -> train.type`
- `config.id -> train.configId`
- `config.quality -> train.quality`
- `config.carriageNum -> train.carriageNum`

The composed train record occupies march offset `0x170`; its outer nullable-object presence byte is at `0x1A8`.
## Boundary and remaining gaps

This checkpoint does not inspect or cross protected native region `0x3F8E0-0x40A6D`.

It closes the safe march/train population side, not the larger point producer. Remaining Map gaps include point raw-field/helper population, protected `XluaBridgeMapScanTick` traversal/order/coordinates, per-tick work/request pacing, retry/backoff, and any separate protected acknowledgement behavior.

## Verification

Hash-gated verifier: `tools/inspect_lwbridge_native_capture_march_population.py`

Durable evidence: `evidence/lwbridge-implementation/2026-09-27-r8-087-native-capture-march-population.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner behavior is changed.
