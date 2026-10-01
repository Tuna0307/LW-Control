# Current implementation/research handoff

**Target:** LWBridge 0.3.17  
**Branch:** `research/offline-controller`  
**Phase:** Phase 2 function recovery / Map Data clean-UI live-integration checkpoint

## 2026-10-01 takeover continuation — read first

The pasted handoff ending at `13b25f6` is superseded. The takeover inspected
clean HEAD and remote `389df373ba3a25af3b61d1a3fc75f2e741eb23bc`; productionization
and the ordered Map continuation already exist. No fresh live test or full
Map campaign acceptance was performed by the takeover.

Current source/evidence audit:
`reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md`.
Current path map: `PROJECT_STRUCTURE.md`.
`LWB317-UI-COMPLETE-001` remains historically `CHANGES_REQUIRED` after PM-006.
PM-007 reviewed follow-on worker HEAD `543b6eb` and returned
`LWB317-UI-CORRECT-001` as `CHANGES_REQUIRED`. Home profile-scoped reconnect
is accepted at the source/transport boundary. Preserve useful `239254a`/`591409d`
Equipment/dialog/locale/preview work. Those findings led to
`work-items/LWB317-UI-CORRECT-002.md`.
Historical lead review: `reviews/2026-10-01-LWB317-PM-007-ui-correction-lead-review.md`.
Correction evidence:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/`. Worker review:
`reviews/2026-10-01-LWB317-UI-CORRECT-001.md`. Lead review that motivated the
work remains `reviews/2026-10-01-LWB317-PM-006-ui-completion-lead-review.md`.

Home now consumes existing read-only root/proxy/recovery/config state and
implements the recovered presentation/precedence, but native game launch/close/
repair remains deliberately unimplemented for the separate lifecycle task.
Map's existing production adapter/data path was preserved and no new live Map
campaign was started. Direct post-auth reference visual comparison is still
`BLOCKED`; clone screenshots are not pixel-parity proof.

The owner requested recovery of the interrupted CORRECT-002 worker. At clean
lead baseline `a19905d`, three worker-modified files and two screenshots existed
without a delivery commit. They were preserved and completed into a focused
`AWAITING_REVIEW` checkpoint. Construction/Training/Gather/Train local controls,
whole per-ID AFK drafts, exact join normalization/validation, local save-state
handling and applied read-only Map fixture queries now have targeted evidence.
Review: `reviews/2026-10-01-LWB317-UI-CORRECT-002.md`.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-002/`, especially
`source-contracts.json`, `browser-qa.json`, `coverage-matrix.md` and
`verification.json`. Native Map/other pages and exact reference assets were preserved.

The owner requested the next worker assignment after recovery. Exact continuation:
`work-items/LWB317-UI-CORRECT-003.md` is READY for a manually opened fresh chat.
First independently review CORRECT-002 at `de6c075`, then complete the recorded
Automation runtime/Trade/Assist/weekly-quality and AFK target/member/toolbar UI gaps.
Physical drag, native config persistence and original pixels remain unproved.
Do not restart the completed correction or resume gameplay/native Home/another
backend campaign automatically. The lead supplies a prompt for the owner to paste;
no separate chat or subagent was automatically dispatched.

The exact recovered CSS checkout conversion remains hash-protected; keep the
checker intact.

## Important reset

The previous bot rebuild was deliberately deleted.

The repository has returned to reverse-engineering-first work.

The current plan is:

1. reproduce the in-scope post-auth 0.3.17 UI one-for-one;
2. then recover in-scope functions slowly and individually;
3. then map them to the current Last War client;
4. live-prove them.

## Explicit auth exception

The clone will **not** recreate the original LWBridge login/account/licensing/entitlement system.

Owner clarification, 2026-10-01: login UI remains excluded, but necessary
auth-related local dependency research is permitted within an assigned in-scope
feature. See `AGENTS.md` section 6; do not stop solely at such a dependency, and
do not circumvent original access controls or obtain others' credentials.

No product fallback is wanted. Preserve the old WebUI source as evidence;
retirement of the currently selectable `--legacy-ui` host option is pending a
separate bounded assignment. The UI task does not perform that host change.

## Current reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Old research

Extensive 0.3.1 research is preserved in Git history, reviews, evidence and archived management docs.

Do not blindly resume the old R8/R9 next step. The target has changed.

Use old findings as hypotheses and tooling references only until revalidated against 0.3.17.

## Accepted 0.3.17 baseline

`LWB317-UI-001A` is accepted at commit:

`8d71cce99a6daf522950b63f1d790e5a5a49b7ae`

It established the exact static frontend package and recovered/hash-locked 24 embedded frontend assets.

## Desktop-control state

Desktop-control tooling was used for the UI-parity campaign's clone-side smoke
checks and captures.

Static/headless function recovery and the bounded v22 live Map acquisition plus
stop/clear continuity checkpoints are complete. `LWB317-LIVE-MAP-V22-001` used an
assistant-owned session to live-prove world readiness/metadata, coordinate
navigation, a 2500/2500 Resource scan, fresh query behavior and clear/reset;
later completeness diagnostics established that its 678-row population was not
a trustworthy full Resource census. `LWB317-LIVE-MAP-V22-002` then live-proved
controlled stop/cancel while Resource acquisition was genuinely active,
readiness across stop and clear, and a second start/stop/clear cycle in the exact
same owned session. `LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` finally
live-proved the Resource response-association defect and a Resource-scoped
deferred-restoration correction with two corrected full-world scans at 8,008 and
8,007 unique Resources. Auth bypass and unrelated gameplay actions remain
unauthorized. The tasks cleaned up their owned sessions and restored the
pristine v22 package.

## Phase 1 campaign handoff

The project-lead-authorized campaign:

`docs/LOOP_CAMPAIGN_8H.md`

has returned `AWAITING_REVIEW`.

The separate 0.3.17 UI project is `src/LWBridge.UI-0.3.17/`. Its static shell,
eight effective navigation entries and inventoried page surfaces are
implemented from exact recovered frontend evidence. UI-007 completed the
available static contract/fix pass; direct post-auth reference visual
comparison remains auth-blocked.

UI stage checkpoints end at `fc760bc`. See
`docs/reviews/2026-09-29-LWB317-UI-CAMPAIGN-8H-handoff.md` for the final worker
handoff.

The Phase 1A static UI recovery/reconstruction is accepted. Direct post-auth
reference visual validation remains blocked at the excluded auth boundary.

## Active continuation

The active Goal remains:

`docs/GOAL_CAMPAIGN_PHASE2_MAP.md`

`LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` is now `AWAITING_REVIEW`. Its
evidence is in
`evidence/lwbridge-0.3.17/map/LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001/`
and its review is
`docs/reviews/2026-09-30-LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001.md`.
The baseline completed all 2,500 logical blocks but accepted only 296 unique
Resources while rejecting 1,407 candidates as `outside_selected_aoi`; live spot
checks proved same-tick camera restoration preceded remote response capture.
The corrected Resource-only path waits for the correlated remote response and
serialization before restoring the camera, then waits for a normal restored-home
response. Two corrected full scans produced 8,008 and 8,007 unique Resources,
zero rejected candidates and full `(0,0)..(999,999)` coverage. This establishes
`CURRENT_PATH_COMPLETE`; `GAME_UNIVERSE_COMPLETE` and original LWBridge private
traversal equivalence remain `UNKNOWN`.

`LWB317-MAP-UI-INTEGRATION-001` is also `AWAITING_REVIEW`. Its review is
`docs/reviews/2026-09-30-LWB317-MAP-UI-INTEGRATION-001.md` and evidence is under
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-INTEGRATION-001/`. The clean
`src/LWBridge.UI-0.3.17` Map page now speaks the production Desktop native bridge
through a readable frontend adapter. A final owned v22 Resource-only UI run
completed `2500/2500`, zero failed/unread, queried/rendered 7,960 real Resource
rows, proved page-2 pagination and Resource-name filtering, cleared the backend
and rendered zero rows/count, and retained the same healthy Map session/server
with the UI back at `Connected`.

`LWB317-MAP-UI-PRODUCTIONIZE-001` was project-lead `ACCEPTED` at
`086757e36562b76d7e45b857a282c51267e16171`; see
`docs/reviews/2026-10-01-LWB317-MAP-UI-PRODUCTIONIZE-001.md` and
`evidence/lwbridge-0.3.17/map/LWB317-MAP-UI-PRODUCTIONIZE-001/`.
`src/LWBridge.UI-0.3.17` is now the canonical frontend. Desktop Release builds
create and verify a fresh `ProductionUi` package from it, and an ordinary
zero-argument launch selects that package. The preserved
`src/LWBridge.Desktop/WebUi` is available only through the deliberate
`--legacy-ui` recovery path; arbitrary `--ui-root` remains proof-gated. The
normal-launch v22 acceptance smoke-rendered all eight primary pages and
LIVE_PROVED Resource Start/progress/completion, a 7,994-row snapshot, page 2,
Resource-name filtering, Clear and same-instance connected health with no
runtime integration errors.

Future product frontend work targets `src/LWBridge.UI-0.3.17`. Do not add new
frontend behavior only to `src/LWBridge.Desktop/WebUi` unless historical
compatibility/reference explicitly requires it. The ordered Map continuation is
complete through restart/reopen, live server jump/return, a combined remaining-
category v22 acquisition, safe action wiring and a live current-server Auto Scan
cycle. `LWB317-RE-MAP-001` is `AWAITING_REVIEW`; direct canonical-WebView positive
row rendering for City/Monster/Truck/Dispatch and live UI-level marks/export are
still `IMPLEMENTED_NOT_VALIDATED`. Railway/Ghost/Treasure positive rows were
`BLOCKED_BY_LIVE_STATE`; Treasure claim/status and Ghost preparation remain
`BLOCKED`; Resource game-universe/private-traversal completeness remains
`UNKNOWN`. Do not start another subsystem until the project lead closes the Goal
or assigns the remaining bounded Map validation.

## Required worker/campaign output

The campaign file defines per-stage evidence/checkpoint requirements and final handoff format.

See:

- `docs/LOOP_WORKER_PROTOCOL.md`
- `docs/LOOP_QUEUE.md`
- `docs/LOOP_CAMPAIGN_8H.md`
