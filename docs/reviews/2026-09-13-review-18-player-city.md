# Review 18 — Player City technical return

Reviewed 2026-09-13 at implementation commit `7da866595089698a00d89b14cd650b849a5bac90` on `research/offline-controller`. The remote branch matched that SHA and GitHub Actions run `34726315539` completed SUCCESS; its Windows job `103640930629` completed every reported step successfully.

## Decision

**APPROVED for the prepared read-only owner-visible saved-row check.** No PM-blocking defect was found in the selected Player City path or its owner-evidence package.

This approval is intentionally narrow. It accepts the technical Player City core plus the passive owner-check tooling; it does **not** mark all Map Data complete, does not live-prove the targeted `SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)` fallback, does not clear Resource SB-97, and does not complete deferred S02/S03/S06 or the corrected-build Overview regression.

## Audited live evidence

`LWB-PC-001` was parsed directly from the committed evidence. PM verified two distinct request IDs, launch-session IDs and game PIDs; a strictly newer second `capturedAt`; exact `mapKind=city`, `state=proven`, source `WorldPointManager._pointInfos`, ordinary `StartViewRequest+UpdateViewRequest(true)` route, runtime `BuildPointInfo`, `pointType=6`, server 2212, normal `map_search`, city summary, exact package restoration, no installed-file drift and stopped owned game state.

The immutable result SHA-256 values remain `4cc90604f14c5450f9c0bc9318f9dc367d6a7c47114bbff2ef31869dec1b5d3f` and `cb4f14b955786b92c9d815ed4daff5218a033f04b577ba47f5e96b29ad738c07`. Shared proof flags and content verify player/alliance identity, raw local profile ID and Windows user path are redacted.

`LWB-PC-002` independently starts from a fresh proof process, reopens the active saved profile/store, reports server 2212 with a positive city count, and normal `map_search` returns the persisted city. This closes the technical same-profile reopen/search step without claiming owner-visible rendering.

## Audited implementation boundaries

PM inspected the exact committed source. Owner-evidence bootstrap suppresses game auto-launch. State-changing commands including scan start/stop/clear, export, server/coordinate jump and `call_lua` are blocked before backend dispatch. City Search is recognized separately from Resource Search; City payload and result evidence are sanitized before writing.

The City DOM observer reads only coordinates, level and updated time from `.map-table--city`; it deliberately excludes Player and Alliance cells. The collector binds evidence to the exact launched PID/session, refuses active LWBridge/Last War and pending recovery ownership, requires an existing saved City row, and requires same-session Search->render correlation with no detected identity leakage.

The production live service accepts exactly one `resource` or `city` kind, validates current source, city `pointType=6`, expected server, and only the ordinary recovered route or the recovered targeted City fallback. City record identity uses the recovered signed-decimal point index. Failure restoration closes only the exact helper-owned game PID by normal close before a restoration retry when necessary.

## Validation and limits

GitHub Actions run `34726315539` is green on the exact implementation SHA. The prior Web package also passed Release build with zero warnings/errors, deterministic backend/transport checks, Python compilation, Lua parsing, collector self-test/preflight, shortcut self-test, current-client `--check-only`, privacy checks and clean recovery/process state.

The successful live runs used the ordinary current-view route; the targeted PlayerWorldPointId fallback remains IMPLEMENTED/OFFLINE-TESTED only. The final selected-path gate is the owner-visible normal Map Data City row in the exact tested build.

## Owner dispatch

PM approves only the read-only check in `docs/user-test-checklist.md`: owner opens `Start Owner Player City Check.cmd`, uses Player City + Search once, sends one screenshot of the visible saved row, then closes LWBridge normally. No fresh scan, game launch, cross-server travel, Resource action, export, clear or jump is requested.
