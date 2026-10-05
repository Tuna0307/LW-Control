# Milestone 5 final independent review

Date: 2026-10-05 SGT
Reviewed production baseline: `1a7d863b536f8da67c2abc1b7dfbec483ec36d81`
Verdict: **NOT READY**

The current browser/integration result does not show a remaining UI-behavior gap. The blocking items are in the final evidence contract and validator.

## Required fixes

1. **Make `validate-milestone-5.mjs` genuinely read-only.** It currently spawns the accepted parent Unit B `check-browser-pixels.py`. That script writes `unit-b/pixel-results.json` and saves `*-diff-mask.png` files when differences exist. This directly contradicts the work item's requirement to preserve historical evidence and separate evidence creation from read-only validation; it also contradicts `milestone-5/map-replay-inventory.md`, which correctly classifies this exact script as an evidence producer. Replace this invocation with a Milestone-5-local read-only pixel adapter that opens the immutable parent captures/measurements, recomputes the 16 pair differences and bounded regions in memory, asserts the expected marker/counts and `outsideAcceptedPixels === 0`, and writes nothing. Then rerun the actual M5 validator read-only. Do not modify or repin Unit B.

2. **Replace the curated 32-file `productionSources` list with the actual served-App dependency closure and pin exact material render-tool versions.** `run-full-app-current.mjs` manually lists 32 files, so the validator can remain green after a material unlisted dependency changes. Concrete current omissions include the actual entry/style inputs `src/main.jsx`, `reference.css`, and `styles.css`; direct `App.jsx` dependencies such as `HomePage.jsx`, `ShellPresentation.jsx`, `shellTheme.js`, `GameAssetImage.jsx`, `backendBridge.js`, `mapBackend.js`, `mapAutoConfig.js`, `NavIcon.jsx`, and `autoLaunchPreference.js`; and page/fixture dependencies including `previewRemainingPagesContracts.js`, `previewConfigHook.jsx`, `AutomationMeta.jsx`, `DispatchAssistManual.jsx`, `tradePurchaseHistory.js`, `RallyJoinSettings.jsx`, `previewAfkContracts.js`, `EquipmentMotion.jsx`, `mapPlunderFixtures.js`, and `mapScanHeaderFixtures.js`. Generate or otherwise verify the recursive local import/asset/CSS closure from the real Vite entry used by the captured App, include the fixture inputs exercised by M5, and make validation fail if any member changes. Pin the package/lock inputs or exact resolved renderer versions as well: the manifest currently records Vite as the semver request `^7.1.7`, while the installed Vite used here is `7.3.6`; it also omits Pillow `12.3.0`, which the validator uses to decode all PNGs. React and React DOM are both actually `19.3.0` and are material to the `Activity` behavior being accepted, so their resolved versions should be pinned directly or through a verified exact lock/install check.

## No additional functional fix required

Do **not** add a fresh interval-owner browser case solely to satisfy this review. The assignment requires suspended hidden effects, not a new interval-specific measurement. The current real-App M5 packet proves a concrete current hidden-effect lifecycle with City Layout's window keydown owner dropping on hidden `Activity` and returning to exactly one owner, while the inherited accepted shell-retention packet already proves the actual Mini Games / Settings interval owners at visible/hidden/returned `1/0/1`. Together these satisfy the stated integrated effect-lifetime gate without inventing another requirement.

The other requested integrated gates are present in the current packet: four modes each have 16 source-order/return route assertions (64 total), retained DOM identity is measured, App-owned Automation/Map/Squads selections survive the keyed profile replacement while the Map-local keyword resets, uncached and cached profile transitions are distinguished, representative Map/Automation/Squads/City local state survives ordinary route hiding, Cross-server/profile-note/exit behavior is exercised with provider/native actions fenced, live locale/theme and narrow layouts are covered, 205 assertions and 11 PNG captures report zero console/page issues, and the inherited Unit B, E-H, M4 shell, and archive replays remain green. Historical stale hash fences are documented rather than repinned.

## Independent replay performed

- `mutation-check.mjs`: **PASS**, all 3/3 mutations detected.
- Unit B saved-packet validator: **PASS**, 10,482 Scheduled renders, 51/51 mutations, 75 integrated cases, 16 browser pairs, 25 mounted cases.
- Units E-H `recover-pages.mjs --verify`: **PASS**, 30 / 18 / 78 / 63 actual paired render states.
- M4 whole-shell `validate-read-only.mjs`: **PASS**, 16 cases / 16 browser pairs / 0 console issues / 2 mutation detections.
- Legacy archive guard: **PASS**, `exactFiles=10`.
- Independent read-only M5 hash check: **PASS**, 4 reference files / 32 currently listed sources / 13 evidence files / 11 screenshots all match the frozen manifest.
- Independent PNG decode: **PASS**, all 11 manifest captures decode as non-empty PNGs at their recorded dimensions.
- `validate-milestone-5.mjs` itself was **not executed**, because inspection proves its current invocation would write into accepted Unit B evidence. That is the first required fix above, not a validation failure to work around.
