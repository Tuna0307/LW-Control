# LWBridge 0.3.1 — invalid `scanMode` evidence check

**Date:** 2026-09-24
**Assignment:** R8-012 Manual Scan invalid-`scanMode` evidence check
**Classification:** **EXACT / PROVEN**

## 1. Conclusion

Original LWBridge 0.3.1 does have the exact invalid-string error pair:

```text
INVALID_SCAN_MODE
map scan mode must be normal or fast
```

These strings are present verbatim in the original reference executable and are directly referenced by the original `map_scan_start` worker.

However, the original does **not** reject every non-`normal`/`fast` JSON value.

Exact behavior:

- missing `scanMode` => defaults to `normal`;
- `null` => defaults to `normal`;
- any non-string value => defaults to `normal`;
- empty string => rejected with the exact error pair above;
- exact `"normal"` => accepted as mode, concurrency 8;
- exact `"fast"` => accepted as mode, concurrency 20;
- any other string => rejected with the exact error pair above.

Therefore the current R8-012 implementation is **not exact as written** if it rejects `null` or other non-string values.

The exact code/message pair itself **is original and may be claimed as exact for invalid string values**.

## 2. Exact original native branch

Reference executable:

```text
C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe
SHA-256:
2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff
```

Original `map_scan_start` shared worker:

```text
RVA 0xF9333–0xFB8D8
```

Relevant original string raw offsets:

```text
0x826431  scanMode
0x826439  normal
0x82643F  fast
0x826443  INVALID_SCAN_MODE
0x826454  map scan mode must be normal or fast
```

Corresponding image RVAs are raw offsets mapped through the original PE; direct xrefs in the worker are:

```text
0xF9C9E  default "normal" pointer
0xF9CB3  lookup "scanMode"
0xF9CD0  string-value pointer selection
0xF9D18  "INVALID_SCAN_MODE"
0xF9D1F  "map scan mode must be normal or fast"
0xFB742  "fast"
```

### Exact branch behavior

At approximately `0xF9C9E–0xF9D34`:

1. mode pointer starts as exact `"normal"`;
2. if command payload is an object, lookup property `scanMode`;
3. if the property is absent, keep default `"normal"`;
4. if the property exists but is **not the handler's string JSON variant**, keep default `"normal"`;
5. only a string value replaces the default pointer/length;
6. string length 4 is accepted only when bytes equal exact `fast`;
7. string length 6 is accepted only when bytes equal exact `normal`;
8. every other string length or failed exact comparison reaches:
   `INVALID_SCAN_MODE / map scan mode must be normal or fast`.

Normal branch:

```text
0xF9DC4  concurrency = 8
0xF9DC9  mode length = 6
```

Fast branch:

```text
0xFB731  exact "fast" comparison
0xFB73D  concurrency = 20
0xFB749  mode length = 4
```

The selected mode pointer/length and concurrency are then stored into scan state/request fields at `0xF9DCE+`.

## 3. Input matrix

The table below describes **mode parsing once the handler reaches the scanMode branch**. Earlier connection/world-admission errors can win first; see section 4.

| Input | Proven original mode behavior |
|---|---|
| missing | defaults to `normal`; concurrency 8 |
| `null` | defaults to `normal`; concurrency 8 |
| `""` | rejects with `INVALID_SCAN_MODE / map scan mode must be normal or fast` |
| `"normal"` | accepts; concurrency 8 |
| `"fast"` | accepts; concurrency 20 |
| unknown string | rejects with exact invalid-mode error pair |
| non-string | defaults to `normal`; concurrency 8 |

Additional exact consequences:

- matching is case-sensitive;
- no trimming/coercion occurs for strings;
- e.g. `"NORMAL"`, `"slow"`, whitespace strings, and other non-exact strings reject;
- object/array/number/boolean/null values do not get string-coerced; they use the default `normal` path.

## 4. Rejection timing / precedence

The invalid-string error is **before actual scan start**, but **not before all scan admission/preparation**.

Original worker ordering proves these earlier operations can occur first:

```text
0xF93xx  game connection / active-scan admission
0xF95D9  enterWorldMap path when needed
0xF9Axx  world-map readiness failure path
0xF9Bxx  current server / selectedTypes preparation
0xF9C9E  scanMode parsing begins
0xF9D0F  invalid scanMode branch
...
0xFADC0  startMapScan method installation/request assembly
```

Therefore:

- an invalid string is rejected **before `startMapScan` is sent**;
- no scan is started from the invalid-mode branch;
- but connection checks, active-scan checks, world-map state/readiness work, and potentially `enterWorldMap` happen before mode validation;
- if one of those earlier gates fails, that earlier error can be returned instead of `INVALID_SCAN_MODE`.

So the statement "invalid scanMode is rejected before scan start" is **EXACT**.

The stronger statement "invalid scanMode is rejected before scan admission/world preparation" is **false**.

## 5. Original frontend behavior

Original asset:

```text
evidence\lwbridge-0.3.1\frontend\assets\MapDataPanel-C1HVeNHr.js
```

Exact preference key:

```text
lwbridge.mapScanMode
```

Recovered initialization around character offset ~29640:

```javascript
let e=localStorage.getItem(ke);
return e==="normal"||e==="fast" ? e : w.scanMode||"normal"
```

Manual Start around character offset ~36079 calls:

```javascript
map_scan_start({
  selectedTypes: e,
  scanMode: P
})
```

Therefore normal original UI flow intentionally sends a normalized `normal` or `fast` value.

The malformed-input behavior recovered above is a native/public host contract for direct or abnormal callers.

Existing exact-contract document:

```text
docs\reviews\2026-09-24-r8-map-control-plane-exact-contract.md
```

already established:

- valid modes `normal`, `fast`;
- `normal -> 8`;
- `fast -> 20`;
- invalid stored frontend preference falls back to current mode or `normal`.

The new evidence in this report closes the previously unresolved **native invalid-input behavior and exact error strings**.

## 6. Preserved evidence search

A targeted search of:

```text
evidence\lwbridge-implementation\*.json
evidence\lwbridge-implementation\*.md
evidence\lwbridge-implementation\*.txt
```

found no preserved artifact containing either exact invalid-mode string.

That absence does not weaken the result because the original executable contains and directly references both strings in the exact `map_scan_start` worker.

## 7. Current rebuild comparison — not authority

Current file:

```text
src\LWBridge.Desktop\MapScanContract.cs
```

Current `NormalizeStart` rejects when a present `scanMode` is not a JSON string:

```text
modeValue.ValueKind != JsonValueKind.String
=> INVALID_SCAN_MODE
```

That conflicts with original 0.3.1.

Original behavior for present non-string values is:

```text
default to "normal"
```

Current routing also calls:

```text
MapScanContract.NormalizeStart(payload)
```

before `StartAsync(...)`.

Original mode parsing happens only after earlier game-connection, active-scan, world-map readiness, and selected-types preparation work.

Thus current R8-012 differs in two observable ways:

1. it rejects `null`/non-string modes that original defaults to `normal`;
2. it can return `INVALID_SCAN_MODE` earlier than original relative to game/world admission errors.

The current exact code/message strings themselves match original.

## 8. Recommendation to main researcher

**Recommendation: C. Current implementation conflicts with original evidence.**

The exact pair:

```text
INVALID_SCAN_MODE
map scan mode must be normal or fast
```

is **EXACT / PROVEN original LWBridge 0.3.1 behavior for invalid string values**.

But do not commit the current parser as strict parity unchanged.

To match original behavior:

- absent `scanMode` => `normal`;
- any present non-string `scanMode`, including `null` => `normal`;
- only present string values are validated against exact `normal` / `fast`;
- other strings throw the exact pair above;
- preserve original precedence if strict error-order parity matters: mode validation occurs after the earlier connection/running/world-readiness preparation and before `startMapScan`.

No LW-Control file was modified for this investigation.


## Final concurrent-worktree note

A final read-only `git status --short` showed active R8-012/main-researcher modifications on `research/offline-controller`, including `MapScanContract.cs`, `ManualMapScanCommandService.cs`, Map Data frontend assets, tests, and frontend tooling.

No listed LW-Control change was created or modified by this helper.
