# Milestone C Auto Scan frontend/config recovery

Evidence state: **EXACT_BYTES + EXECUTED ORIGINAL FRONTEND**.

This folder recovers the Auto Scan configuration and Map-panel interaction contract from the hash-pinned LWBridge 0.3.17 frontend assets. It does not use the current clone's storage/config implementation as an oracle and does not execute native scan or server-jump operations.

Pinned assets:

- `index-BVfnK1wp.js` SHA-256 `44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6`
- `MapDataPanel-B4GXEND2.js` SHA-256 `ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`
- `en-BisSXcTB.js` SHA-256 `0bf43d180eb93692a93b830b5d984e9d01ddeea524340f2334fc63cba527a731`

`recover-auto-contract.mjs` re-reads those immutable bytes, asserts the structural matches, and writes UTF-8 byte locators to `source-locators.json`. `auto-contract-cases.mjs` executes the original pure helpers and actual original `MapDataPanel` component with inert native-facing stubs; it writes `case-results.json`.

Run:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-b/recover-auto-contract.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-CLOSEOUT-002/agent-b/auto-contract-cases.mjs
```

The executable suite currently passes **12/12** cases.

## Recovered contract

### Manual / Auto switching and shared status

- The mode is local `MapDataPanel` state and starts as `manual`. Manual and Auto buttons only set that state. Switching mode does not save Auto config.
- The Auto target-server draft is separate local state initialized to `""`; it survives Manual → Auto → Manual → Auto while the component remains mounted.
- The scan timing header and scan summary/status/progress are outside the Manual/Auto conditional branches. They remain visible in either mode when their normal scan-state conditions are met.
- Exact panel locators: mode state `31879-31910`, mode tabs `43893-44321`, scan timing `44472-45035`, scan summary `48951-49590` in `MapDataPanel-B4GXEND2.js`.

English labels are exactly `Manual Scan` and `Auto Scan`.

### Master switch and status text

- Master checkbox emits `{...currentConfig, enabled: checked}` through the panel callback.
- Status label precedence is `autoScanRunning ? Running : enabled ? Waiting for schedule : Disabled`. `Running` therefore wins even if the config has already been disabled while the current cycle is still winding down.
- Original Auto configuration controls are not disabled merely because `autoScanRunning` is true. Run-now is the control that explicitly gates on it; scan-state reading also gates run-now.
- English text is exactly `Enable automatic scanning`, `Running`, `Waiting for schedule`, and `Disabled`.
- Complete original Auto card: bytes `46001-48950` in `MapDataPanel-B4GXEND2.js`.

### Target server parsing, Add / Enter, chips and fallback

Original parser `Ci`, bytes `359071-359234` in `index-BVfnK1wp.js`:

- Splits on one-or-more whitespace, ASCII comma `,`, fullwidth comma `，`, ASCII semicolon `;`, or fullwidth semicolon `；`.
- Converts each token with JavaScript `Number`, then accepts only integers in `1..99999`.
- Because it uses `Number`, integer-valued forms such as `001`, `1.0`, `1e2`, and `0x10` are accepted and normalized numerically. Non-integers, non-numbers, zero, negatives and values above `99999` are rejected.
- Deduplicates by first occurrence, preserves order, and keeps at most 20 IDs.

`wi` appends by reparsing `existing.join(",") + "," + draft`, so existing order wins during dedupe and the total remains capped at 20. `Ti` removal is a filter and preserves the remaining order. Their locators are `359234-359284` and `359284-359327`.

Panel Add callback `lr`, bytes `40544-40620`:

- If the draft parses to zero valid IDs, it does nothing and leaves the draft unchanged.
- If at least one valid ID parses, it emits the appended list and clears the draft.
- The Add button is disabled exactly when parsing the draft yields zero IDs, not merely when the text is empty.
- Pressing exact key `Enter` always calls `preventDefault()` and invokes the same Add callback. Invalid Enter therefore prevents the default but leaves the text and config unchanged.
- A syntactically valid new ID at the 20-ID cap still causes an emission of the unchanged first-20 list and clears the draft.

The input is controlled, has `inputMode="numeric"`, and uses the current scan-state server ID as its placeholder when `serverId > 0`; otherwise it shows literal `8, 15, 120`. The placeholder is informational: an empty configured server list falls back to the current server only when the scheduler produces its target list (`Di`).

Chips render in `config.serverIds` order. Each chip's remove button filters that ID. The English hint is `Enter server IDs and click Add. Commas add several at once; × removes one. No entries scans the current server.` The displayed hint under-describes the parser because whitespace and both semicolon variants also work.

### Defaults and config normalization

Default `Si`, bytes `358908-359070`:

```text
enabled: false
intervalMinutes: 60
serverIds: []
selectedTypes: [truck, railway, dispatch, ghost, treasure]
scanMode: fast
returnToOriginalServer: true
nextRunAt: 0
```

Normalizer `Ei`, bytes `359327-359801`:

- `enabled` is true only for literal boolean `true`.
- `intervalMinutes` is `Math.trunc(Number(value))`, clamped to `20..1440`; missing/null uses default 60. Examples: `19 → 20`, `20.9 → 20`, `1440.9 → 1440`, `5000 → 1440`, empty string `→ 20`.
- A nonnumeric non-null interval such as `"bad"` becomes `NaN`; the original normalizer does not repair that to 60. If later JSON-serialized, JavaScript serializes `NaN` as `null`, and a subsequent reload then yields default 60.
- `serverIds` are normalized by joining the stored array and sending it through the exact parser, giving the same range/dedupe/order/20-ID rules.
- `selectedTypes` keeps only the eight known Map kinds, deduplicates in first occurrence order, and falls back to the five defaults if none remain.
- `scanMode` is `normal` only for exact string `"normal"`; every other stored value becomes `fast`.
- `returnToOriginalServer` is true unless the stored value is exact boolean `false`.
- `nextRunAt` is numeric/truncated with a minimum of zero; invalid numeric text becomes zero.

Malformed structural values can throw inside `Ei`: for example a truthy non-array `serverIds` has no `.join`, and a truthy non-array `selectedTypes` has no `.filter`. Profile loader `Ai` catches those throws and returns the full defaults. Valid JSON with only a bad interval does not throw, so it preserves the `NaN` behavior above.

### Scan types, speed and return toggle

- Auto renders all eight known types: city, resource, monster, truck, railway, dispatch, ghost and treasure. The 0.3.17 catalog marks all eight enabled.
- Default Auto selection is the five-type ordered list `truck, railway, dispatch, ghost, treasure`.
- Checking a type appends it to the current selection. Unchecking filters it. If a single sole type remains, that checkbox is disabled so the UI cannot remove the last selection.
- Parent normalization also guarantees a nonempty stored selection by falling back to the five defaults.
- Auto speed select emits `fast` only when the event value is exact `fast`; any other event value maps to `normal`. This differs from stored-value normalization, where anything other than exact `normal` becomes `fast`.
- Return-to-original is a direct boolean checkbox edit and defaults true.

### Next-run state and display

The panel does not itself normalize or persist config. Its helper `$`, bytes `40514-40544`, emits a shallow merge `{...currentConfig, ...patch}`. Parent App callback `Ct`, bytes `364217-364377` in `index-BVfnK1wp.js`, is the normalization/persistence boundary:

1. Normalize the panel-emitted object with `Ei`.
2. If this edit transitions disabled → enabled, replace `nextRunAt` with `Date.now()` so the scheduler can run immediately.
3. If the resulting config is disabled, force `nextRunAt = 0`.
4. Update the live config ref and React state.
5. Save under the selected profile through `ji`, which normalizes again before `JSON.stringify`.

For enabled → enabled edits, the supplied/normalized `nextRunAt` is preserved. Run-now is therefore only a config edit `{nextRunAt: Date.now()}`; it is enabled only when online, Auto is enabled, Auto is not already running, and the scan state is not reading.

Next-run UI is shown only when `enabled && nextRunAt > 0`; otherwise it is `-`. Formatter `N`, bytes `7885-8022` in `MapDataPanel-B4GXEND2.js`, treats positive values below `1e12` as seconds and larger values as milliseconds, then uses `new Date(...).toLocaleString(language)`.

English labels are exactly `Run now` and `Next scan`.

### Profile-scoped load/save and reset-like behavior

- Storage key is exact `lwbridge.mapAutoScan.${profileId}`. There is no `default` fallback suffix in the recovered App helper.
- Loader `Ai`, bytes `360013-360170`, reads only the selected profile key, JSON-parses it and normalizes through `Ei`. Missing storage normalizes to defaults. JSON parse errors or structural normalization errors return a fresh default object with a cloned default `selectedTypes` array.
- Saver `ji`, bytes `360170-360259`, always persists `JSON.stringify(Ei(config))` to the selected profile key.
- On profile ID change, a `useLayoutEffect` at bytes `370501-370600` immediately loads that profile into the live ref and React state. The load itself does not write storage.
- There is no explicit Auto Scan Reset control in the original Auto card. Reset-like state transitions are: missing/malformed profile storage → defaults on load; disabling → `nextRunAt = 0`; profile change → replace the live config with that profile's loaded/default config.

This ownership prevents a prior profile's in-memory config from being written into a newly selected profile before that profile has been loaded.

## User edits versus normalized stored state

The component and parent have deliberately separate responsibilities. The panel may emit a raw user value, then the parent normalizes it before state/persistence. The executable interval case demonstrates `"19"` from the number input becoming raw numeric `19` in the panel callback and stored/displayed `20` after the parent callback. Empty number input emits raw `0` and likewise becomes `20`.

Some panel handlers apply their own UI transformation before the parent: server Add uses the exact parser/appender, Auto speed maps to `fast|normal`, scan-type toggles append/filter, and checkboxes emit booleans. Those objects still pass through `Ei` at the parent boundary.

## Native/runtime-produced values intentionally not claimed here

This recovery does not establish native scan execution or server-jump correctness. The Auto UI consumes scan/runtime values including `scanState.serverId`, `isReading`, phase/progress/timestamps/errors and the App-level online state. Their producers and live timing are outside this Milestone C frontend/config evidence.

`autoScanRunning` is an App-owned frontend scheduler flag passed into the panel; this recovery establishes only how that prop affects presentation/gating. The scheduler's native calls, per-server completion, timeout behavior and actual return-to-server outcome remain outside this evidence.

## Current clone comparison and suggested implementation shape

The current working copy was inspected only to identify migration work; it is not an oracle. At inspection time `MapDataPage.jsx` still owns `autoConfig` and localStorage directly. Its one-time `useState` load plus `[autoConfig, autoStorageKey]` save effect can carry the previous profile's in-memory config into a new profile key because changing the key does not first reload the new profile's config.

Other observed differences at inspection time include comma-only Add parsing; clearing invalid drafts; no Enter handler/recovered placeholder/input mode; Add enabled by nonempty text rather than parseability; interval out-of-range fallback to 60 instead of clamping; Auto fields disabled during `autoRunning`; and next-run display not explicitly gated by `enabled`.

The closest recovered implementation shape is:

1. Put the pure defaults/parser/add/remove/normalize/load/save helpers at the App/config ownership layer.
2. Own normalized Auto config state plus a live ref in the profile-aware parent.
3. On selected-profile change, synchronously load `lwbridge.mapAutoScan.${profileId}` into that ref/state before any save path for the new profile.
4. Pass `autoScanConfig`, `autoScanRunning`, and one `onAutoScanConfig` callback into the Map page/panel.
5. Keep only Manual/Auto mode and target-server draft as panel-local state.
6. Let every panel config edit shallow-merge and flow through the single parent callback; normalize there, apply the enable/disable `nextRunAt` rule, then persist the normalized object.
7. Reproduce the exact server parser/Add/Enter/chip behavior and next-run formatter/gating. Do not add `autoRunning` disablement to configuration controls unless later evidence establishes a version-specific change.

This shape also gives profile switching one clear load boundary and user editing one clear normalization/save boundary, matching the recovered 0.3.17 frontend.
