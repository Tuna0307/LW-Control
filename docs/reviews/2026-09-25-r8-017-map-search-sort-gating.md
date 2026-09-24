# R8-017 — gate unrecovered map_search sort branches

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1 Map contract
**Scope:** remove three speculative backend sort expressions while preserving original frontend sort vocabulary.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`, section 5;
- `docs/reviews/2026-09-24-r8-014-map-search-filter-parity.md`;
- immutable original Map Data frontend assets.

The reference UI remains authoritative for public sort vocabulary. R8-017 changes only backend execution authority.
## Correction

The R8 control-plane review explicitly leaves three native sort branches partial:

- Monster `distance`: public key recovered, but exact native distance expression/source and the observed descending-direction inversion remain partial;
- City `shield`: public key recovered, but exact native shield expression/data flow remains partial;
- Railway `quality`: public key recovered, but the exact native Railway quality branch remains partial.

Earlier R7 code executed guesses for all three. R8-017 removes those guesses.

A query containing any of those keys now normalizes normally but is marked with unsupported `sorts`; `RequireRecoveredIndexedSearch` then returns `MAP_QUERY_UNRECOVERED` before SQL assembly.

## Still-admitted evidenced expressions

The backend continues to admit the sort expressions whose native source/expression is supported by current evidence:

- Monster: `level`, `updatedAt`;
- City: `level`, `health`, `updatedAt`;
- Railway: `power`, `itemCount` when `itemKey` is present, `protectTime`, `updatedAt`;
- Truck, Resource, Dispatch/Ghost and Treasure retain their previously recovered expression sets.

This checkpoint does **not** promote the complete native ordered multi-sort/null assembly to exact parity. Null-order fragments and the stable `record_key ASC` tie are recovered, but the full native assembly remains partial where the R8 contract report says it is partial.

## Frontend behavior

No frontend transform or locale changes are made.

The original UI may still emit Monster distance, City shield, and Railway quality because those are authentic 0.3.1 controls. Until the corresponding native expressions are recovered, selecting one produces the existing fail-closed `MAP_QUERY_UNRECOVERED` backend behavior rather than silently using an invented ordering.

This is intentionally a parity-safety correction, not a claim that those visible controls are end-to-end complete.

## Regression coverage

`MapSearchSortParityChecks` proves:

- Monster distance is classified unrecovered and fails with `MAP_QUERY_UNRECOVERED`;
- City shield is classified unrecovered and fails with `MAP_QUERY_UNRECOVERED`;
- Railway quality is classified unrecovered and fails with `MAP_QUERY_UNRECOVERED`;
- neighboring Monster level/updatedAt, City level/health/updatedAt, and Railway power/protectTime/updatedAt/itemCount paths remain admitted and execute against SQLite.

Historical R7 proof blocks were narrowed so they no longer describe the three partial branches as hash-locked recovered behavior.

## Validation

Passed during R8-017:

- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0.

Final frontend-generator, diff, JSON and staged-diff checks are recorded in the machine-readable checkpoint evidence.

## Remaining sort gap

R8-017 does not recover Monster distance, City shield, Railway quality, or the complete native ordered multi-sort/null assembly where the R8 report remains partial. Those branches stay evidence-gated; do not replace `MAP_QUERY_UNRECOVERED` with a guessed implementation.
