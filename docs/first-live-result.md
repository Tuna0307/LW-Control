# Active standard-AI task — display one real resource point

User-approved priority correction, 2026-09-10, after review 9. This supersedes the earlier instruction to implement PM9-A first. Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md) and [the standard handoff](implementation-handoff.md). No new model/task is dispatched by this document.

## Review 10 — continue beyond saved replay

PM reviewed `01f139d`, verified the full local capture against the committed excerpt, and reproduced its display. The active outcome below is **still open**: `--first-live-result` only replays a saved file and does not connect to the game. Deferred work stays deferred until fresh app-initiated acquisition is demonstrated.

Next checkpoint: address [PM10-01–03](lwbridge-project-status.md). Preserve the reusable acquisition recipe/source and restore checks, resolve the first missing supported connection link, and wire fresh data into the app. Show a second acquisition with a new correlated request/capture time (the point values need not change). Verify failure/disconnection never substitutes the old saved row as a fresh result. Add focused importer/isolation/gate tests and a clear replay label; do not spend successive checkpoints extending the replay mode.

## One concrete outcome

**Establish a supported connection to the running official game, acquire one real resource point, and display that automatically acquired record in the rebuilt Map Data page.** Reuse the current application and valid research. Research only the specific contracts still needed to deliver this result.

Scope spans R5 connection/identity, the necessary R6 normalization/index/display path, and the smallest R7 acquisition. It does not require finishing every R5 reconnect feature or every R6 export/filter first; equally, later-area research does not count as a working connection. A supported connection to a manually launched, explicitly authorized session may support the demonstration, but does not prove the rebuilt Launch Game/Close Game buttons work. Record that distinction and label any deliberate rebuild choice as IMPLEMENTATION POLICY.

## Testing permission is already granted

The user authorizes opening, closing and restarting the game/launcher whenever needed, controlling the computer through Computer Use for this project, navigating the game/app and collecting a bounded real-data sample and evidence. This includes the user's current game session. Follow the active Computer Use skill/tool instructions; identify the target window/process and coordinate shared access. Do not ask the user to operate the game simply because the AI has not tried its available permitted controls. Actual platform restrictions remain in force and must be reported rather than bypassed.

## Execution

1. Inspect HEAD/worktree and current installation/process state. PM8 recovery is closed by the user's reinstall; do not repeat it. Refresh the minimal source hash/integrity checks needed for this test using existing diagnostics. Building future auto-update infrastructure is deferred.
2. Start/open the game as needed and establish the supported session/connection. Identify the next missing prerequisite before implementing it; a process or visible game window alone is not bridge readiness. Reuse PM7-B/R5 evidence. Do not invent bootstrap values, commands or packet layouts.
3. Select one real resource point and trace its current-client data through the actual acquisition/serializer into the original normalized builder/storage/display inputs. R6-047/049/051 cover original layout/identity/scalars; R6-048/050 cover current managed resource fields. They do not automatically prove the connection between the two. Resolve only the missing links for this sample, preserving lossless IDs and unknown fields.
4. Implement the required supported path into the existing Map Data UI. The row must come from the live source, not a mock, hardcoded fixture, manually typed row or screenshot transcription. Display only source-backed fields; do not fill absent values with plausible numbers. Keep unrelated unsupported commands unavailable.
5. Verify the displayed point against the actual source/game: identify the session and server/context, source record identity, acquisition time, supported coordinates/type and the displayed values. Preserve sanitized correlated input/result evidence plus an app screenshot; screenshots alone are not dataflow proof. Verify refresh does not silently fall back to fixture data.
6. Run the checks appropriate to changed behavior. Deliver a runnable build/path and concise reproduction steps. State whether game startup was manual/official or performed by the rebuilt app, and whether repeated acquisition was tested. No single-point success closes all 47 acceptance cases or proves full-map scanning/reconnect.

## Checkpoint LWB-R7-001 — one current-game resource row is visibly reproduced (2026-09-10)

**LIVE-PROVEN source acquisition.** The official launcher started build `1.0.361 / 1078` at `2026-09-10 03:01:45.343` after reporting Lua v14, size `41301710`, CRC `2371889527`. The instrumented v14 candidate used for this bounded read has SHA-256 `028e32ca71cf318758ab696ca05628ec30a28b429646e8bd6834dc0287bfffa0` and the same size/CRC/version. At `03:02:27` Singapore time, the in-game probe captured `15,293` accumulated world records from the running client, including `8,000` `resource_point` rows on server `2212`. The selected source record is point `1006`, coordinates `5,1`, level `3`, from `WorldPointManager._pointInfos`. The full capture SHA-256 is `6447d30f373a76fbfe416a894546f8797ea71e20e44d9f06d3bfd61cd367cbce`. Fresh post-test diagnostics show the installed Lua v14 payload restored to its normal `41269242`-byte file, SHA-256 `09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace`; the instrumented package was temporary. Focused provenance is in [the source evidence](../evidence/lwbridge-implementation/2026-09-10-first-live-resource-source.json) and [programmatically extracted record](../evidence/lwbridge-implementation/2026-09-10-first-live-resource-capture.json).

**IMPLEMENTATION POLICY / IMPLEMENTED-OFFLINE-TESTED display adapter.** `--first-live-result <diagnostics.json>` imports exactly one source-backed `resource_point` into an isolated in-memory `MapDataStore`, adapts only its proven identity/scalars to the recovered public `resource` row, and exposes the minimum recovered summary envelope needed for the Map Data page to select server `2212`. Normal production `map_summary`, `map_data_options`, `map_scan_start`, and `profile_instance_start` remain fail-closed. The mode is isolated from the normal profile database and suppresses auto-launch.

The captured record contains `resourceType=wood`, but the mapping to LWBridge's original `resourceNameKey` is not recovered, so the page deliberately shows **Unknown resource**. The source says `gatherTimeStatus=occupied_time_unavailable`; because the recovered frontend otherwise turns missing gather identifiers into **Idle**, this bounded mode replaces only that row's status with `—`. It does not claim Idle or Gathering. [The screenshot](../evidence/lwbridge-implementation/2026-09-10-first-live-resource.png) and [WebView diagnostics](../evidence/lwbridge-implementation/2026-09-10-first-live-resource-ui.json) show server `2212`, Resource `1`, `1 items`, coordinates `5,1`, level `3`, unknown resource/status, and capture time `9/10/2026 3:02:27 AM`.

This is a visible real-game result, but it is not yet a production connection. The game-side acquisition ran through the bounded current-client probe and the rebuilt app replays the source-backed capture through its real Map Data store/query/UI path. The rebuilt application still lacks a supported owned bridge/session that can perform a fresh `map_scan_start` itself. The earlier guarded v14 scanner attempt proved load/update registration but produced no terminal scan-status result, so production scan lifecycle/completion remains **UNKNOWN/BLOCKED**. Computer Use initialization was also rejected by the environment's automatic review before any UI action; it was not rerouted.

Reproduce the durable replay after building Release:

```powershell
src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe `
  --view map-data `
  --language en `
  --theme light `
  --first-live-result evidence\lwbridge-implementation\2026-09-10-first-live-resource-capture.json
```

The replay now opens the **Resource** tab automatically, displays a **Saved capture replay** provenance banner, disables acquisition controls, and reports scan state as unavailable/replay. That command still displays the previously captured real point; it does not reacquire it from the game. Fresh acquisition remains the next R5/R7 integration step.

## Checkpoint LWB-R7-002 — PM10 replay truthfulness and acquisition recipe preserved (2026-09-10)

**RECOVERED acquisition recipe / IMPLEMENTED-OFFLINE-TESTED replay hardening.** The source for the R7-001 acquisition is now durably identified at Git commit `2a3cc66a88de86394b4404adf9e9cfd7af6275a1`, which is an ancestor of the current branch. The six scanner/preparer/runner/loader/Lenc/sender source files and their SHA-256 hashes, the historical v14 candidate inputs/outputs, the exact bounded `run_probe(...,180)` invocation, and the backup/install/restore/hash-match contract are recorded in [the PM10 recipe evidence](../evidence/lwbridge-implementation/2026-09-10-pm10-replay-acquisition-recipe.json). This closes the recipe-preservation part of PM10-01 without claiming a new acquisition.

PM10-02 is implemented: `--first-live-result` always owns a dedicated in-memory Map Data store, shows capture time/source/probe/hash in an explicit **Saved capture replay** banner, automatically selects Resource, disables scan/auto-scan controls, and exposes replay scan state as `unavailable` with source `saved_capture_replay`. PM10-03 is implemented/offline-tested: the deterministic suite now covers source-to-row mapping, timestamp/record rejection, optional unknowns, public server range, demo-only positive-Int32 point/coordinate boundaries, replay-store isolation, and unchanged normal production gates. The importer fails on an invalid first resource instead of silently selecting a later row.

The point/coordinate positive-Int32 limit remains **IMPLEMENTATION POLICY for this bounded replay only**; it is not promoted to a recovered production range. `serverId` uses the already recovered public `1..99999` range. A file hash/probe field identifies the replay input but does not authenticate live provenance by itself.

Release build and deterministic checks pass with zero warnings/errors and `failures: []`. At validation time the installed client diagnostic was valid and both game and launcher were stopped. A fresh UI screenshot was not produced because the earlier rebuilt-app capture operation in this task was rejected by the environment's automatic review before launch; it was not rerouted. The main PM10-01 result remains open: the rebuilt app has not yet initiated a fresh acquisition, and no second fresh acquisition exists yet.

## Checkpoint LWB-R5-006 — original control-pipe discovery is recovered (2026-09-10)

The earliest connection gap is narrower. The secure proxy and a bounded run of the verified original LWBridge host prove that its per-user control pipe is `\\\\.\\pipe\\lwbridge-control-v1-` plus the first 16 lowercase SHA-256 hex characters of the UTF-8 Windows user SID. The current-user correlation observed `lwbridge-control-v1-d5eb15a8845a45f2`; the raw SID is not committed. The rebuild now has a deterministic helper for that proven pipe path and a focused regression check.

This does **not** establish the required production session. The current rebuild still has no production implementation of `INativeAsyncCommandService`; only the isolated `HostProbeCommandService` exists. Exact accepted hello/session framing and host-to-proxy request grammar remain **UNKNOWN/BLOCKED**, so `map_scan_start` is still `BRIDGE_NOT_READY` and no fresh or second fresh acquisition is claimed. The same code slice passed Release build and deterministic checks before these documentation edits; one later combined delivery-validation command was automatically rejected before execution and was not rerouted. The next integration work is limited to those session/request contracts, then strict freshness/session identity, acquisition, normalization and display.

A permitted Git-history search also found no earlier recovered implementation containing `hello.ack`, the control-pipe prefix or `XluaBridgeHandlePipeMessage`; follow-up inspection of candidate historical commits was automatically rejected. The repository therefore has no earlier production session implementation that can simply be restored. ESC-005 records this method/result and the still-open narrow session/request question.

## If a link remains blocked

Name the first missing link in the actual connection -> acquisition -> normalization -> display path. Record the exact question, source/build, attempts/results, permitted alternatives and next method or external condition. Do not replace it with unrelated research. Prepare/revise an ESC entry only under AGENTS.md's method/exhaustion rules; an old ESC or denial is not a Daybreak assignment. Continue directly relevant permitted work and report honestly if no live result was achieved.

## Deferred work and reporting

PM9-A automatic-update readiness, full export/migrations, broad alternative sorting and other features remain in the backlog until the first app-initiated fresh result, unless a narrowly identified prerequisite is essential. The user wants a working demonstration, not a larger research inventory.

At each checkpoint report: **what visibly works; whether the sample is genuinely live; what prevents the next step; the next action; checks and delivered commit**. Save confirmed findings immediately, update the feature ledger/backlog, commit/push and verify the existing GitHub branch. Do not stop with a claim that offline tests prove the game integration.
