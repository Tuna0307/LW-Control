# R8-014 — restore original `map_search` kind/filter ownership

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** public `map_search` kind admission, original frontend query builder, filter ownership, and generic keyword behavior. Alternate-sort internals remain a separate partial area.

## Authority

Primary authority:
- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`, especially section 5;
- immutable original `evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js`.

The original frontend is authoritative for which query keys are actually emitted by each result kind. The backend remains fail-closed for recovered named fields used by the wrong kind.

## Restored public kind set

`map_search` now accepts exactly the original eight kinds:
`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`.

Rebuild-only public `zombie_boss` search is removed. Internal Zombie Boss acquisition/storage compatibility remains outside this public command and is not claimed as original product behavior.

## Restored filter ownership

The generated frontend query builder again matches the original ownership:
- Resource: `resourceNameKey` only;
- Monster: `monsterNameKey` only;
- City: alliance / no-alliance / marked filters;
- Truck/Railway: quality, item and plunderable families as recovered;
- Dispatch/Ghost: completion/status families as recovered;
- Treasure: treasure/supplies and viewer/lucky/radar fields as recovered;
- `minLevel` / `maxLevel`: Dispatch only.

R8-014 removes the rebuild-added Resource `resourceLevel`, `resourceIdleOnly`, `resourceFullOnly`, and `excludeBlackTile` public semantics and the Monster level selector / localized `monsterNameKeys` side channel.

Unknown rebuild-only extra keys such as the Resource truth flags are not assigned invented original behavior; they are inert at normalization. By contrast, recovered named fields such as `minLevel` / `maxLevel` are still recognized and fail closed when supplied for the wrong kind.

## Keyword behavior

Original `keyword` search uses the generic persisted predicate over name, alliance name, UUID and the JSON blob. R8 had replaced Monster keyword search with a special localized-name path to avoid raw JSON schema-key matches. R8-014 removes that public special case and restores the generic original predicate.

This means literal text present in `data_json` participates in Monster keyword matching, including schema text such as `zombieRushId`. That behavior is preserved because strict parity takes precedence over rebuild-specific search cleanup.

The old localized Monster helper remains only as an internal proof compatibility path in `MapDataStore`; public `LWBridgeBackend.map_search` no longer calls it or accepts `monsterNameKeys` as a product extension.

## Frontend restoration

The maintained generator applies the R8-014 rollback after historical hash-locked deltas, preserving those deltas as evidence while emitting a strict final panel. The generated `MapDataPanel-C1HVeNHr.js` now has:
- the original eight result tabs/kinds;
- Resource/Monster name selectors only;
- no Zombie Boss result tab;
- no Resource truth or Resource level controls;
- no Monster level selector;
- no `monsterNameKeys` locale rerun path;
- Dispatch-only `minLevel` / `maxLevel` payload emission.

`tools/check_map_search_r8014.cjs` independently locks those final generated properties without requiring Playwright.

## Regression coverage

Deterministic coverage proves:
- public `zombie_boss` search returns `INVALID_MAP_KIND`;
- rebuild-only `monsterNameKeys` does not alter public Monster keyword results;
- generic Monster keyword search again includes `data_json`;
- Monster `maxLevel` and Monster level ranges fail closed;
- Resource truth keys are inert extras and do not filter results;
- Resource `minLevel` / `maxLevel` fail closed;
- original Resource/Monster name-key equality filters still work;
- the generated panel contains none of the removed R7 search vocabulary.

## Validation

Checkpoint validation includes:
- Release production build: 0 warnings / 0 errors;
- full deterministic checks: `ok=true`, `failures=[]`;
- focused `tools/check_map_search_r8014.cjs`: PASS;
- frontend generator reproducibility check: PASS.

Final whitespace/JSON integrity and staged-diff checks are recorded in the checkpoint evidence JSON after the final run.

## Remaining boundaries

R8-014 does not claim:
- every alternate-sort implementation branch is fully recovered;
- protected Map acquisition/traversal/extraction parity;
- original Auto Scan scheduler/state machine parity;
- Scheduled Plunder restoration;
- Login / Account / Authentication behavior.

The next main Map checkpoint is the original Auto Scan frontend scheduler/state machine. Remaining `map_search` alternate-sort internals stay explicitly partial and must not be promoted without stronger evidence.
