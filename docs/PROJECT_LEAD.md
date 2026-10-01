# Project lead control sheet

The main project lead owns integration and should keep this file small and current.

## Current target

LWBridge 0.3.17

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Current parity scope

Target one-for-one parity for the **in-scope post-auth product experience**.

The original login/account/licensing/entitlement system is intentionally
`OUT_OF_SCOPE` for reconstruction.

Owner clarification, 2026-10-01: necessary auth-related local dependency
research is allowed within a named in-scope feature, using supplied artifacts
and authorized access; the login UI remains excluded. See `AGENTS.md` section 6.
Use one canonical production path and fix defects there. No legacy product
fallback is wanted; the existing `--legacy-ui` switch has a separate pending
retirement task. Preserve its source as historical evidence.

## Current phase

**Phase 2 — function recovery**

Phase 1A static UI recovery/reconstruction has been project-lead reviewed and
accepted. Direct post-auth runtime visual comparison remains legitimately
blocked by the original auth boundary and stays `BLOCKED`, not fabricated.

The first Phase 2 Goal is the complete Map Data subsystem recovery:

`docs/GOAL_CAMPAIGN_PHASE2_MAP.md`

## Current work items

The 2026-10-01 incoming handoff was stale at `13b25f6`; takeover inspected the
actual clean/pushed campaign baseline `389df37`. Preserve existing implementation.
Static UI baseline acceptance does not imply complete Home state coverage or
original runtime pixel parity. Complete all recoverable UI states/interactions,
starting with Home and Map, under the full UI task below while
the Map closeout awaits a separate independent acceptance review.

| Work item / campaign | Owner | State | Scope |
|---|---|---|---|
| LWB317-PM-001 | Project lead | COMPLETE | Repository/documentation reset for 0.3.17 |
| LWB317-PM-003 | Project lead | COMPLETE | Takeover source/evidence audit, current path map and exact CSS checkout repair; no new live test/full Map acceptance |
| LWB317-UI-HOME-STATES-001 | None | SUPERSEDED | Historical Home-only proposal; replaced by full UI coverage |
| LWB317-UI-COMPLETE-001 | Previous worker | CHANGES_REQUIRED | Useful partial UI checkpoint at `a253cce`; PM-006 found missing Home preference scope, incorrect Automation forms and incomplete Equipment interactions; full source-backed coverage not accepted |
| LWB317-UI-CORRECT-001 | Previous worker | CHANGES_REQUIRED | PM-007 accepts Home profile-scope fix and retains useful nested UI work; Automation controls remain omitted, AFK drafts leak and Map fixture QA ignores query semantics |
| LWB317-UI-CORRECT-002 | Interrupted worker; project lead continuation requested by owner | AWAITING_REVIEW | Focused Automation controls, isolated AFK drafts/save states and applied Map fixture queries completed with source/state/browser evidence; coverage matrix records remaining UI gaps; no new native/game integration |
| LWB317-UI-CORRECT-003 | Previous worker; split by lead at owner's request | PARTIAL | Pushed 5204f67 adds substantial Automation/AFK code and baseline review; checks pass but Stage B browser evidence/master closeout incomplete; broad execution replaced by smaller continuations |
| LWB317-UI-CORRECT-003A | Fresh/continued worker chat, manually dispatched by owner | READY | Finish only Trucks/Secret Task weekly quality draft/save/error/retry/discard and focused evidence; preserve other implementation and WIP |
| LWB317-UI-001A | Worker AI | COMPLETE / ACCEPTED | Static frontend package inventory/extraction |
| LWB317-UI-CAMPAIGN-8H | Goal worker | ACCEPTED | Static UI recovery/reconstruction accepted; direct post-auth visual validation remains blocked |
| LWB317-COMPAT-MAP-V22-001 | Goal worker | COMPLETE | Installed Last War v22 Map compatibility revalidated statically/source-first; no production Map change required |
| LWB317-LIVE-MAP-V22-001 | Goal worker | AWAITING_REVIEW | Fresh v22 owned-session readiness/world metadata, coordinate navigation, a 2500/2500 Resource scan, query/filter/sort/pagination and clear/reset live-proven; its 678-row population was later shown not to be a trustworthy completeness census |
| LWB317-LIVE-MAP-V22-002 | Goal worker | AWAITING_REVIEW | Fresh same-session v22 proof covers genuinely active Resource scan stop/cancel, readiness after stop, clear/reset continuity, second scan/start-stop-clear in the same profile/instance/PID/server, false connection-loss root cause/fix, and structured orphan-free cleanup |
| LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001 | Goal worker | AWAITING_REVIEW | Offline-only Resource completeness observability prepared: raw point/candidate/rejection/dedupe accounting, accepted-state/spatial distributions, and lossless source/published-row evidence for a separately authorized future live run; no live game/session launched |
| LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001 | Goal worker | AWAITING_REVIEW | Baseline completeness diagnostics proved same-tick camera restoration lost remote Resource association; Resource-scoped deferred restoration is LIVE_PROVEN by two corrected full-world scans (8008/8007 unique, zero rejected, full 0..999 bounds). `CURRENT_PATH_COMPLETE`; `GAME_UNIVERSE_COMPLETE` and original private traversal equivalence remain `UNKNOWN` |
| LWB317-MAP-UI-INTEGRATION-001 | Goal worker | AWAITING_REVIEW | Clean reconstructed `LWBridge.UI-0.3.17` Map page now uses the production native bridge. Resource-only Start/progress/completion, 7,960-row live query, page-2 pagination, Resource-name filtering, Clear/zero render and same-session connected readiness are `LIVE_PROVEN`; old Desktop WebUI is preserved |
| LWB317-MAP-UI-PRODUCTIONIZE-001 | Goal worker | ACCEPTED | Project-lead accepted at `086757e36562b76d7e45b857a282c51267e16171`; `src/LWBridge.UI-0.3.17` is the canonical/default packaged Desktop frontend; zero-argument normal launch, native bridge, all eight primary pages, and Resource Start/progress/completion/page-2/filter/Clear/post-clear health are LIVE_PROVEN. Legacy `WebUi` remains an explicit recovery/reference path |
| LWB317-RE-MAP-001 | Goal worker | AWAITING_REVIEW | Ordered Map continuation completed through restart/reopen recovery, live server jump/return, combined v22 category acquisition, safe action wiring and current-server Auto Scan. Direct canonical-WebView positive-row rendering for City/Monster/Truck/Dispatch and live UI marks/export remain IMPLEMENTED_NOT_VALIDATED; Railway/Ghost/Treasure positive rows were BLOCKED_BY_LIVE_STATE; Treasure claim/status and Ghost preparation remain BLOCKED; Resource game-universe/private-traversal completeness remains UNKNOWN |
| Other LWB317-RE-* | None | BLOCKED | Do not start another subsystem until Map Goal review |
| Login/auth/licensing reconstruction | None | OUT_OF_SCOPE | Boundary/dependency only |

Loop-mode workers must follow `docs/LOOP_WORKER_PROTOCOL.md`,
`docs/LOOP_QUEUE.md`, and the active campaign file.

## Project-lead responsibilities

Before assigning a worker/campaign:

1. define explicit goals/non-goals;
2. name the reference artifact/state;
3. name required evidence/output paths;
4. define acceptance/stop checks;
5. prevent two workers from editing the same master workstream unsafely.

After a worker/campaign returns:

1. review evidence/source identity;
2. reject unsupported inferences;
3. inspect each campaign checkpoint/commit;
4. update parity matrix/feature ledger conservatively;
5. run applicable checks;
6. accept/fix/revert only coherent changes;
7. decide whether the next function-family Goal may open.

## Status discipline

The project lead should distinguish:

- what the reference demonstrably contains;
- what has been observed at runtime;
- what has been copied visually;
- what has been reverse engineered;
- what has been implemented;
- what has been live-proven.

Do not compress those into a single vague “done” percentage.

## Desktop constraint

Desktop-control tooling remains available. The current Map Goal is
static/headless first and then explicitly authorizes **bounded live Last War Map
validation** using an assistant-owned session where possible.

It does not authorize auth bypass or unrelated gameplay actions.
