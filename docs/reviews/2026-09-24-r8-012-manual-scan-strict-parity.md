# R8-012 — restore original Manual Scan public contract

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** Manual `map_scan_start` kinds, mode parsing, mode persistence/UI, recovered concurrency semantics, and observable defaults.

## Authority

Primary recovered authority is:
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`;
- `docs/reviews/2026-09-24-r8-012-invalid-scanmode-helper-evidence.md` (repository copy of helper report, SHA-256 `A6E908198763A6395FD0ADE9D2C373F25F681F14176249A1D37E30053FA3A5FA`);
- immutable original `MapDataPanel-C1HVeNHr.js`;
- original executable `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.

The final invalid-`scanMode` gap was closed from the original executable worker at RVA `0xF9333-0xFB8D8`.
The exact strings are present at raw offsets:
- `0x826431` — `scanMode`;
- `0x826439` — `normal`;
- `0x82643F` — `fast`;
- `0x826443` — `INVALID_SCAN_MODE`;
- `0x826454` — `map scan mode must be normal or fast`.
## Exact Manual public contract

The original Manual Scan exposes exactly eight Map kinds, in this order:

`city, resource, monster, truck, railway, dispatch, ghost, treasure`.

There is no original public `zombie_boss` scan kind.

Manual Start sends:

```javascript
map_scan_start({
  selectedTypes,
  scanMode
})
```

Selected-type normalization is:
- missing or non-array `selectedTypes` => all eight;
- accept exact strings only from the eight-kind allowlist;
- discard unknown and non-string entries;
- deduplicate while preserving first accepted occurrence;
- no accepted entries => `INVALID_SCAN_TYPES / no valid map scan types selected`.
## Exact scanMode parsing

The original native parser begins with mode `normal`.

| Input | Original behavior |
|---|---|
| missing | `normal`, concurrency 8 |
| `null` | `normal`, concurrency 8 |
| any non-string | `normal`, concurrency 8 |
| `""` | `INVALID_SCAN_MODE / map scan mode must be normal or fast` |
| `"normal"` | accepted, concurrency 8 |
| `"fast"` | accepted, concurrency 20 |
| any other string | exact `INVALID_SCAN_MODE` pair |

String matching is exact and case-sensitive. There is no trimming or string coercion.

The exact native normal branch writes concurrency 8 around `0xF9DC4`.
The exact fast branch compares `fast` and writes concurrency 20 around `0xFB731-0xFB749`.
Invalid-string validation occurs after earlier game/active-scan/world preparation and before the protected `startMapScan` request.
Therefore an earlier connection, active-scan, or world-readiness failure can win over `INVALID_SCAN_MODE`.
R8-012 preserves that ordering at the recovered host boundary rather than rejecting invalid strings during payload normalization.

## Original Manual UI

The immutable original panel contains:
- storage key `lwbridge.mapScanMode`;
- exact stored values `normal` and `fast`;
- one `.map-speed-toggle`;
- two radio inputs named `map-scan-speed`;
- persisted Manual mode selection;
- Manual Start payload `{selectedTypes,scanMode}`.

If the stored preference is absent/invalid, the frontend uses the current scan-state mode or `normal`.

R8-012 restores these Manual controls and locale strings through `tools/build_lwbridge_frontend.py`.
Generated bundles remain generator-owned; they are not maintained by independent hand edits.
## Recovered observable defaults

A fresh/default Manual scan state now preserves the recovered original values:
- all eight selected kinds;
- `scanMode = normal`;
- `concurrency = 8`;
- `retryCount = 2`.

The Manual service no longer publishes the rebuild-only `auto` mode or zero concurrency as its reset/default vocabulary.

Current-client compatibility strategies may still differ internally.
They may not rewrite the caller-visible recovered `normal`/`fast` mode or its 8/20 concurrency semantics.

The historical current-client Zombie Boss acquisition/query compatibility code remains internal where needed for later checkpoint work.
It is not selectable through the R8-012 Manual Scan public allowlist.
`map_search` parity is intentionally not completed here.
## Deviations removed

R8-012 removes these Manual-surface rebuild deviations:
- ninth public `zombie_boss` scan kind;
- removal of the Manual Normal/Fast control;
- backend-owned rewriting of requested Manual mode;
- rebuild-only feature-owned `targetServerId` input semantics;
- default/reset `scanMode=auto`;
- default/reset concurrency 0;
- null `retryCount` in the Manual service status;
- rejection of `null`/non-string `scanMode` values that original 0.3.1 defaults to Normal.

The exact invalid-string pair is now evidence-backed rather than assumed.

## Regression coverage

Deterministic checks cover:
- exact eight-kind default and allowlist;
- filtering/deduplication/order of selected types;
- exact `INVALID_SCAN_TYPES` behavior;
- Zombie Boss-only rejection and mixed valid/unknown filtering;
- Normal preserved at concurrency 8;
- Fast preserved at concurrency 20;
- current-client strategy selection cannot rewrite public mode;
- fresh/default status uses all eight + Normal/8/retry2;
- invalid string mode reaches exact `INVALID_SCAN_MODE` only after live/context admission;
- earlier active-scan and context failures retain precedence over invalid mode;
- null/non-string mode inputs default to Normal.
Browser/frontend regression covers:
- one original Manual speed toggle;
- two Manual Normal/Fast radios;
- `lwbridge.mapScanMode` persistence;
- Manual Start retaining `selectedTypes + scanMode`;
- original speed locale strings;
- Auto Scan mode rollback remaining explicitly pending.

## Validation

Passed on 2026-09-24:
- `git diff --check`;
- Release C# build: **0 warnings / 0 errors**;
- full deterministic checks: `ok=true`, `failures=[]`;
- frontend generator reproducibility check;
- R8-008 Clear frontend contract regression;
- R7-147 owner-workflow browser regression;
- R7-131 persistence / saved-server / Stop / Clear-race browser regression;
- R7-156 Auto navigation / refresh / reconnect + jump-first regression;
- R7-136 Auto app/process restart regression;
- R8-012 Manual Normal/Fast browser regression.
## Not claimed by R8-012

R8-012 does **not** claim recovery of:
- protected game-side traversal/acquisition order;
- exact mode-specific pacing beyond recovered concurrency;
- exact mode-specific retry differences beyond the recovered default `retryCount=2`;
- protected acknowledgement/drain/extraction internals;
- the complete `map_scan_status` serializer or all state transitions;
- complete `map_scan_stop` uncommon error behavior;
- strict `map_search` parity;
- the original Auto Scan scheduler/state machine;
- Scheduled Plunder;
- any Login / Account / Authentication behavior.

The next Map checkpoint is `map_scan_status` / `map_scan_stop`.
