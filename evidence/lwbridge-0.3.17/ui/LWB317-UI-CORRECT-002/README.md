# CORRECT-002 recovery evidence

The owner requested completion of an interrupted worker. Baseline `a19905d`
already contained PM-007 and the assignment; uncommitted Pages/Map/test changes
and two worker screenshots were preserved. Fresh continuation captures are JPEG;
the two PNGs are interrupted worker artifacts, not newly verified screenshots.

`source-contracts.json` contains 17 exact asset contracts, source hashes and
zero-based UTF-8 anchors. `collect-contracts.mjs` regenerates these records and
the source/screenshot manifest. `verify-evidence.mjs` checks hashes and byte
anchors. `browser-qa.json` records 50 assertions/visible branch observations;
it is evidence from actual CUA browser interactions, not a replay script.
`coverage-matrix.md` states the affected branches and remaining implementation gaps.
`verification.json` records checks and build fingerprints for this checkpoint.

Repeat the local preview with:

```powershell
npm.cmd run dev --prefix src/LWBridge.UI-0.3.17 -- --host 127.0.0.1 --port 4319 --strictPort
```

Use the canonical app's preview-state URL parameter (see App.jsx). Fixture states
used here: `automation-config`, `automation-save-error`,
`automation-training-unavailable-level`, `automation-training-no-camps`,
`automation-training-data-unavailable`, `automation-training-error`,
`automation-training-no-order`, `squads-profile`, `squads-profile-save-error`
and `map-city`. Navigate through the actual UI. Construction defaults open;
Training is under Tasks, Gather has its own category, Train is under Alliance,
and AFK is under Squads. Detailed interaction sequences and exact values appear
in browser-qa.json and the coverage matrix. Fresh captures cover desktop/light/en,
390x844/dark/zh-CN, and desktop/dark/ja.

All inventories, camps, builders, reward rows, targets, member identities and Map
rows in preview are synthetic local QA data. They are never current-game facts.
The config adapter acknowledges in memory only; it does not persist to disk or
write native/game state. Map fixtures stay offline and all mutation APIs reject.
Native modes cannot choose the fixture. No original protected-service access,
game launch, native provider work or live gameplay was performed.

Physical HTML5 drag, direct original post-auth pixels, native config persistence
and full all-page/runtime parity are not proved. Other pages and existing Map
campaign live-proof dispositions remain unchanged.
