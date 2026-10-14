# MAP-014 — available-scope owner review, 2026-10-14

**Review verdict: available passive Map scope READY FOR INDEPENDENT LEAD REVIEW; whole Map PARTIAL.**
This supersedes the pending work in `MAP_014_WORKER_RESUME_HANDOFF_2026-10-14.md`,
but does not rewrite its historical failed observations. Branch:
`codex/map-owner-fixes-014`; review in existing draft PR #10 only. No merge,
release publication, account claim, plunder, sharing, combat, paid action or
relocation was performed. The accepted single-game Home scope remains separate.

## Real problem and correction

The complete genuine mixed scan in
`artifacts/map014-takeover/native-ui-20261014-125647/` published 4,132 City,
8,010 Resource, 14,028 Monster, 193 Truck and 842 Dispatch records, zero
Railway, with all 2,500 logical blocks/10,000 native AOIs and zero failures.
The initial packaged Secret Task table failed despite independent SQLite
reopen/export and direct owner lookup working. The exact first-generation
`MapStore.Query.Search` Dispatch/Ghost SQL ran two correlated City owner/name
lookups across the entire eligible task population **before** its 50-row page.
On the genuine database the original query exceeded a nine-second bounded
diagnostic; a `MATERIALIZED` selection-page CTE performs the expensive lookups
only after sorting/filtering/pagination, returning 50/50 resolved names in
~1.19 seconds for each of the first two pages. Actual packaged native success
now independently shows accepted `map_search` request/reply and a loaded table;
the missing command-error log was never treated as a success signal.

Source: `src/LWBridge.Map-0.3.17/MapStore.Query.cs`; distinguishing regression:
`tests/LWBridge.Desktop.Checks/Map014OwnerFixChecks.cs` seeds 4,132 City plus
842 Dispatch identities and asserts owner/name, sort/page, total and a deadline.

## Genuine packaged-app and native evidence

- `artifacts/map014-takeover/native-ui-20261014-152426/`: the actual canonical
  Windows publish running the production WebView against a **copy** of the
  original 27,205-row genuine dataset. `native-ui-map.json` reports
  `real_native_ui_browse_existing_genuine_dataset`, `newMapScans:0`; the
  `map014-ui-extras.json` receipts all pass: Other tab/subtype presentation,
  stable Connected/server popover, combined Resource level/amount/black-tile
  filters, 50 real Dispatch owner names, genuine Truck row/menu icons, native
  Truck Follow/Stop, and <=25 million Truck power with multiple item ALL-match
  filtering. Screenshots and `native-ui-map.json.command-trace.jsonl` are retained.
  The game and app exited, exact original v26 scripts restored, and no recovery
  journal survived. A prior reproducible failed replay remains at
  `native-ui-20261014-150852/`.
- `artifacts/map014-lead-live/native-stop-20261014-232737/`: first **fresh
  current-source, genuine production-provider** active scan Stop at 17.4%
  camera-acquisition progress, zero completed logical blocks. Separate native
  before/after camera checkpoints match exactly, including server 2212,
  anchors, world geometry, projection matrix, FOV, zoom, position and rotation.
  Terminal is idle, no in-flight/pending native blocks; exact PID exit and
  original scripts restored. This is a diagnostic host, not WebView.
- `artifacts/map014-lead-live/native-stop-20261014-233300/`: repeated genuine
  active Stop after cloning retained 27,205 published records into a different
  isolated owner. `stop-published-retention.json` verifies identical before/
  after sorted row SHA-256 `6542176cb004587f2866e29848294842a2f3572890039d9db168320e58b27336`,
  durable `scan_runs.status=cancelled`, **zero** staged rows, zero active runs,
  and zero Dispatch/Truck/assist jobs. Original source DB hash unchanged;
  exact camera geometry and original scripts restored. No positive staging was
  observed prior to Stop: this is a genuine active acquisition Stop and saved
  publication retention, **not** a genuine positive-staged Stop.
- Retained earlier genuine positive witnesses remain relevant only where
  production paths were unchanged: remote Auto browse/Jump/Clear
  (`native-ui-20261014-122631`), City FOV20 source identity/relocation and
  exact camera return, historical positive Scattered Supplies
  (`native-ui-20261014-013823`), and moving Truck UUID-follow/expiry. New
  strict Other migration was tested against real SQLite reopen; no new
  natural Party spawn was observed.

Each task was isolated, with restored official v26 script SHA-256 values:
`LWScripts.data` `49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d`,
`LWScripts.txt` `52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0`,
`version.txt` `5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca`.

## Other correctness repairs and tested scope

Actual production SQLite Other migration now requires exact native integer
types/canonical decimal strings, required fields and exact class-case suffix;
malformed historical Treasure cannot become actionable Other. UI selection
keys and scheduled payloads preserve exact full-precision trimmed native UUID;
missing Truck power/HP/reward amounts appear unknown instead of fabricated 0.
Ghost preparation rejects invalid signed-Int64 UUIDs, duplicate batches and
protection overflow before any command; valid terminal transport still fails
closed. The cross-locale UI completeness check now applies the actual Other
locale additions, preserving the original generated locale sources.

Controlled regression sweep passed: full Desktop/Checks Release (Home,
MapStore, scanner, cancellation, owner actions, Auto, Other), frontend
`npm run check` (including mounted mobile and profile-switch inverses), Other
frontend check, 46 native diagnostic helper tests and Windows production UI
build/package identity validation. These checks prove controlled scenarios,
not a live claim or a naturally spawning event.

## Scanner decision by selection

On the supported standard world 0 with a 1000×1000 tile grid, City alone uses
native-qualified FOV20: six source-identity-qualified paired
trials found the same 4,114 actual owner IDs; median 227.912 seconds baseline
versus 86.179 seconds FOV20 (62.19% reduction in those trials). Never enforce
a fixed City population quota. Resource, Monster, Truck, Railway, Dispatch,
Ghost, Treasure, Other and **all mixed selections** use guarded **serial LOD0**
acquisition with one shared full-world discovery, 2,500 logical blocks / 10,000
AOIs; Other shares Treasure point discovery. Nonstandard world geometry is
admitted only through the narrower guarded City/Resource legacy capability;
unsupported kind/geometry combinations fail closed. Requested concurrency labels 8/20
are scheduling metadata, not multiple simultaneous cameras. Existing Resource
FOV20 trials have unexplained live identity mismatches; mixed FOV20 omitted
92/92/83 baseline Truck UUIDs, of which 90/88/82 had recorded final arrivals
after approximately timed candidate completion. These arrival timestamps
do not independently establish same-time source presence or complete March
freshness, so neither faster route is promoted. Individual experimental
one-shot camera request/replies are correlated; the consecutive-region
shortcut still lacks a run-bound native A/B/C group with per-AOI reply
ownership, all 2,500 typed blocks, source-backed March queue/identity coverage
and an independent authenticated home-return acknowledgement. This is a
qualified currently working route, **not**
a globally fastest or universal-recall claim. See
`MAP_014_SCANNER_METHOD_INDEPENDENT_AUDIT_2026-10-14.md` for exact evidence
and the known stale `current_fast_train_list_v1` strategy label.

## Still unfinished and reasons

1. **Missing current-v26 provider:** Treasure aggregate daily claim
   quota/status, reset/cooldown and actual single/box/season claim terminal and
   reward delta are unavailable (`GAME_PROVIDER_UNAVAILABLE`). Existing
   inspection, eligibility status and claim preview are read-only. Dedicated
   Ghost plunder send/result correlation is not authenticated for this client;
   preparation works, terminal scheduling/execution remains
   `GHOST_PLUNDER_PROVIDER_UNAVAILABLE`. No blind send or fake success.
2. **Unavailable genuine live population:** source-supported Celebration Party
   event `27000`, moving Doom Walker/BossVO and Ghost were absent from the
   captured owned windows; Railway final Train lifetime/`TrainData.arriveTs`
   positive expiry and naturally occurring Monster Follow remain unproven.
   A WorldMarch movement `endTime` is not the Train's terminal lifetime.
   Historical positive moving Truck and Supplies are retained separately.
3. **Only controlled:** pre-existing positive-stage late Stop/staging race,
   Auto cancellation that prevents the next target, action-owner guards,
   profile retirement and malformed legacy Other rejection passed rigorous
   injected/inert production tests. Native active Stop and preservation were
   positive, but no new **positive-staged** live Stop was observed.
4. **Owner-only live actions:** Dispatch sharing and plunder, Truck plunder,
   actual marches, combat, claims, spending and relocation have **not** been
   attempted. A one-action-at-a-time, identity/cooldown and cleanup checklist
   is already in `MAP_006_OWNER_ONLY_VERIFICATION_CHECKLIST.md`. Never enable
   a currently missing Ghost/Treasure terminal for its sake.

The complete feature is **not** accepted as fully working until the missing
provider terminals and real or appropriately deferred positive event/action
witnesses are resolved. The available passive Map browsing, acquisition,
filtering, navigation, Stop and durable data scopes can be independently
reviewed now. Final ZIP integrity/source SHA and PR review details are retained
separately under the ignored `artifacts/` root; they are not a published release.
