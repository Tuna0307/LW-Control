# LWB317-MAP-UI-INTEGRATION-001 — reconstructed Map UI / live backend integration

**Date:** 2026-09-30
**Branch:** `research/offline-controller`
**Starting HEAD:** `6e64cd1c8b1674ddf0c87929a89dc77f9175996e`
**Result:** `LIVE_PROVEN` for the clean reconstructed UI's Resource manual-scan/query/clear path

## Scope and result

This task connected the clean `src/LWBridge.UI-0.3.17` Map Data page to the
existing production Desktop Map backend without replacing
`src/LWBridge.Desktop/WebUi` and without changing current-client Map acquisition
semantics.

The clean UI now uses the production WebView2 request/response/event bridge and
the existing Map commands:

- `map_scan_start`
- `map_scan_stop`
- `map_scan_clear`
- `map_scan_status`
- `map_summary`
- `map_data_options`
- `map_search`

The eight recovered scan-content labels map to the established backend kinds:

| Reconstructed label | Backend kind |
|---|---|
| Player City | `city` |
| Resource Point | `resource` |
| Monster | `monster` |
| Truck | `truck` |
| Train | `railway` |
| Secret Task | `dispatch` |
| Ghost Ops | `ghost` |
| Treasure | `treasure` |

The production bridge/backend contract remains `EXACT_CONTRACT`. The static
Map visual inventory remains based on `EXACT_BYTES` recovered frontend assets;
this task changed runtime behavior inside that reconstruction rather than
redesigning the page.

## Integration architecture

`src/LWBridge.UI-0.3.17/src/backendBridge.js` implements a small readable
adapter over the already-established WebView2 envelopes. It requires the
Desktop-injected live bootstrap/session, sends `invoke`/`listen`/`unlisten`
messages, correlates native responses by request ID and session ID, and
propagates native error code/message/details rather than manufacturing success.

`src/LWBridge.UI-0.3.17/src/mapBackend.js` owns the Map command names, exact
eight-kind mapping, profile scoping, scan/search payload construction, recovered
50-row page size, sort cycling, status/count normalization and event unwrapping.

`src/LWBridge.UI-0.3.17/src/MapDataPage.jsx` owns Map presentation/state. It
polls at the existing five-second scale, subscribes to
`bridge://map-scan-status`, invalidates stale summary responses when newer scan
events arrive, advances the browse server when a newer positive live server is
observed, uses backend pagination, and exposes native failures in the page.

`src/LWBridge.UI-0.3.17/src/App.jsx` reads real `get_status` and `proxy_status`
connection state independently of Map status. A Map status failure therefore
does not falsely mark the whole application unavailable. The header's pending
count uses the backend's real `pending` field.

The Desktop host gained a proof-only `--ui-root` path so the Vite build can be
served by the real production WebView2 host during integration validation. The
option is accepted only with `--map-ui-integration-proof`; arbitrary normal
Desktop runs cannot replace the privileged `https://lwbridge.local` content
root. The existing bundled `src/LWBridge.Desktop/WebUi` remains the default and
was not deleted or replaced.

Ordinary browser/Vite preview has no native transport. The bridge stays in
explicit `preview` mode and rejects native actions instead of silently supplying
fake production Map data.

## Controls and current classification

The clean Map page now has real manual Start, Stop, Clear, status/progress,
per-kind counts and backend searches. Start/scan/status/Resource query/filter/
pagination/Clear are `LIVE_PROVEN` through this task's final owned session.

The Stop button is wired to the already-recovered `map_scan_stop` contract and
its enablement follows live scan state. Stop semantics remain separately
`LIVE_PROVEN` by `LWB317-LIVE-MAP-V22-002`; this task did not interrupt its final
full Resource acceptance scan solely to repeat that campaign through the clean
UI.

City, Monster, Truck, Train, Secret Task, Ghost Ops and Treasure use the same
backend count/query plumbing where the recovered query contract already exists.
That wiring is `IMPLEMENTED_NOT_VALIDATED` for those categories in this task;
no new category-by-category live campaign was opened.

Auto Scan remains present visually and disabled. Cross-server/server-jump
execution remains `IMPLEMENTED_NOT_VALIDATED` and was not enabled or exercised.
Scheduled Plunder remains a visible zero/empty surface here; its separate
Dispatch/Truck worker/control-plane recovery is not revalidated by this UI task.
City Export remains disabled in the clean page because its native save-dialog
host integration was not part of this bounded task. Coordinate row actions are
also left disabled in this clean integration checkpoint.

Treasure claim/status remains `BLOCKED`. Ghost preparation remains `BLOCKED`.
No unavailable action is presented as successful.

## Final live Resource acceptance — `LIVE_PROVEN`

The authoritative successful artifact is:

`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-INTEGRATION-001/final-live-acceptance-v2.json`

The clean reconstructed UI was served by the production Desktop WebView host,
detected `bridgeMode="native"`, and ran Resource Point only in an assistant-owned
Last War v22 session on server `2212`.

The scan completed:

- mode: `normal`
- concurrency: `8`
- logical blocks: `2500/2500`
- failed blocks: `0`
- unread blocks: `0`
- terminal phase: `completed`
- live Resource total: **7,960**

The population is dynamic; `7,960` is this fresh UI acceptance snapshot and is
not a hard-coded expected census. The earlier Resource completeness task remains
the authority for acquisition completeness classification.

The page-1 native search used `pageSize=50`, `updatedAt desc`, server `2212` and
the active profile. The first native row and rendered row correlated exactly:
record key `902952`, coordinate `(951,902)`, Resource name key `129027`, level
`1`, amount `21,600 / 21,600`, status `Idle`.

Pagination then issued a real page-2 native query for the same server/profile,
returned 50 rows from the same total `7,960`, and had **0 record-key overlap**
with page 1.

The recovered Resource-name filter selected key `100281`; the native query
returned total **2,726** and the proof verified the returned page rows matched
that Resource name key.

Clear was invoked through the clean UI and `map_scan_clear`. The backend returned
the default idle/zero scan state, the post-clear Resource query returned total
`0`, and the rendered Resource tab showed count `0`, an empty table and
`aria-busy=false`.

Post-clear continuity used the same exact Map-readiness predicate previously
proven in `LWB317-LIVE-MAP-V22-002`: the owned Map session still matched, the live
server remained `2212`, the healthy heartbeat returned, and the clean UI header
settled at `Connected`.

Screenshots are preserved as:

- `final-live-acceptance-v2.png`
- `final-live-acceptance-v2-post-clear.png`

Structured JSON is the primary acceptance evidence; the screenshots are
supplemental.

## Preserved failed proof attempts

Earlier evidence in the same directory is intentionally retained. Those attempts
completed substantial live UI work but exposed proof-harness defects: first the
new proof mode was not admitted by the native `map_search` recorder, then the
verifier used the wrong locale for the recovered table timestamp, and later it
sampled a broader lifecycle-status projection instead of the established Map
readiness predicate. These failures are not discarded because they document why
the proof harness changed. The final `final-live-acceptance-v2.json` supersedes
them as the acceptance result.

One attempted cross-process post-scan continuation also showed that a fresh
`MapControlPlane` begins with idle/server `0` even when persisted Map rows/runs
exist. That is restart/resume/saved-browse-context work and remains intentionally
outside this task rather than being silently folded into UI integration.

## Deterministic verification

The final implementation was exercised with:

- `npm.cmd run check --prefix src/LWBridge.UI-0.3.17` — passed, including
  `LWB317_MAP_UI_INTEGRATION_CHECKS_OK`.
- `npm.cmd run build --prefix src/LWBridge.UI-0.3.17` — Vite production build
  passed.
- `dotnet run --project tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj -c Release`
  — `LWB317_MAP_CHECKS_OK`.
- `dotnet build src/LWBridge.Map-0.3.17/LWBridge.Map-0.3.17.csproj -c Release --nologo`
  — passed with zero warnings/errors.
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --nologo`
  — passed with zero warnings/errors.
- full `LWBridge.Desktop.Checks.exe` deterministic checker — `ok=true`, all six
  deterministic groups true, no failures.
- `tools/check_map_search_r8014.cjs`, `check_map_auto_r8015.cjs`, and
  `check_scheduled_plunder_r8016.cjs` — passed.
- `git diff --check` — passed; only Git's existing LF/CRLF conversion notices
  were printed.

## Cleanup

`cleanup-after.json` records the final task-owned cleanup state. There are no
owned Last War/launcher/LWBridge processes, no recovery journal, no task-named
runtime artifacts and no profile Map WAL/SHM files. The validated official v22
package SHA-256 is
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.

The final UI Clear left zero `map_records` and zero `scan_runs` for server `2212`.
The normal per-profile `map-data.db` remains because it is production persistence,
not a disposable proof database. Older unrelated Map-campaign WAL/SHM evidence
elsewhere under `LWBridgeRebuild` was preserved.

## Classification and remaining work

| Item | Status after this task |
|---|---|
| Clean reconstructed UI native bridge integration | `LIVE_PROVEN` for Resource manual flow |
| Resource Start / progress / completed state through clean UI | `LIVE_PROVEN` |
| Resource count / first-page rows | `LIVE_PROVEN` |
| Resource pagination | `LIVE_PROVEN` |
| Resource-name filter | `LIVE_PROVEN` |
| Clear / zero UI state / same-session readiness | `LIVE_PROVEN` |
| Stop button wiring | `EXACT_CONTRACT`; backend stop already `LIVE_PROVEN`; clean-UI invocation not repeated live here |
| Other Map tab query/count wiring | `IMPLEMENTED_NOT_VALIDATED` |
| Auto Scan / server jump | `IMPLEMENTED_NOT_VALIDATED` |
| Restart/resume and cross-process saved browse context | `IMPLEMENTED_NOT_VALIDATED` |
| Treasure claim/status | `BLOCKED` |
| Ghost preparation | `BLOCKED` |
| Resource corrected observable acquisition route | `LIVE_PROVEN` / `CURRENT_PATH_COMPLETE` from the prior completeness task |
| `GAME_UNIVERSE_COMPLETE` | `UNKNOWN` |
| Original protected/private LWBridge traversal equivalence | `UNKNOWN` |

The Map Goal therefore remains `IN_PROGRESS`. Remaining bounded work includes
server jump, restart/resume/saved-browse recovery, live validation of other Map
categories and remaining supported actions, followed by final Map closure.
