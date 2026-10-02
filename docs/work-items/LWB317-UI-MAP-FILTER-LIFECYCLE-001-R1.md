# LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1

Status: **AWAITING_REVIEW**, 2026-10-02.
Worker correction delivered; parent remains CHANGES_REQUIRED pending project-lead review.

## Goal and inputs

Correct one options-response/server synchronization mismatch while preserving
the delivered filter/Clear/Treasure work. Do not restart the parent campaign.

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Branch: `research/offline-controller`.
Submitted product baseline: `c99c7613c6320fe5659e96e4a7235896e9ad9770`.
Assignment/review commit is newer; inspect HEAD and do not reset the checkout.
Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this work item, the parent
work item and `docs/reviews/2026-10-02-LWB317-PM-027-map-filter-lifecycle-review.md`.

Reference EXE: `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Primary original source is `MapDataPanel-B4GXEND2.js` under the frontend-package
assets; SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
Canonical frontend is `src/LWBridge.UI-0.3.17`.

## Reproduction and required correction

Run `node evidence/lwbridge-0.3.17/ui/LWB317-PM-027/independent-cases.mjs`.
At the submitted baseline it exits 1 with one failure and five passes.
Do not overwrite the preserved PM-027 JSON or modify its assertion into a pass.

Initial scan/data server 321; current options request for 321 returns serverId
322. The original discards that payload's lists, briefly requests 322, then
synchronizes back to scan server 321 and requests 321. Settled result:
server 321, options request servers `[321,322,321]`. Current production settles
at 322 with `[321,322]`.

Trace the interaction of original options effect (UTF-8 bytes 34871-35650) with
server effect (33502-33682; dependencies `[R,C.serverId]`). Implement the
source-backed combined lifecycle using current canonical state producers.
Do not hardcode server IDs, blindly ignore mismatches, rewrite scan polling or
break intentional stored-data/availability behavior. Recover and document
which current state corresponds to original C.serverId, including normal
scan/server events versus successful Clear parent-state acknowledgements.

## Acceptance checks

- The lead's six-case checker passes on corrected production without test edits.
- Add independent original/current cases for normal same-server reply, redirected
  reply, obsolete redirect response, an actual server-state transition, and loss
  of a positive server where the original contract supports it. Verify requests,
  settled server, options, rows/cache/page/loading ownership with deferred replies.
- Preserve delayed Clear(321), transition 322, acknowledgement 321: the already
  recovered post-transition options sequence remains `[322,321]`. Same-server
  Clear still reloads options once. Keep failed/pending Clear retention and
  stale search/options/finally fences.
- Rerun the parent filter/source checkers and maintained request-lifetime,
  navigation, interaction and strict integration assertions; focused historical
  filter/Checking/row/table replay. Keep old evidence/identity manifests intact.
  The parent filter checker writes its result JSON: use a new output-redirecting
  read-only replay adapter to store fresh results under R1, preserving its
  actual assertions and the submitted parent's historical result record.
- Run frontend check/build/production-build, new evidence validation, protected-WIP
  guard and diff checks. Add new actual-callback results tied to current source
  hashes; keep baseline failure evidence. A real offline browser smoke check
  should confirm normal server-321 fixture filters and Treasure preferences.

## Scope and delivery

This is a small UI state correction only. No native integration/gameplay, scan
polling campaign, export/start feedback, Treasure picker redesign or full pixel
claim. Do not add fallback UI or fake successful preview operations.

Keep protected `previewAfkFixtures.js`, `.scratch-lwb317/`, and parent CORRECT-003
screenshots unchanged/unstaged. Main worker owns production/docs. No subagents
are required for this small correction.

Write new evidence under `LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1` in the UI tree,
create a dated review, update relevant current docs conservatively, commit/push
and verify direct remote SHA. Continue until the checks pass or a concrete
blocker is established; no timed stop or intermediate approval pause.
Return AWAITING_REVIEW with the correction, exact original/current cases,
regressions/browser/limits, commit and remote SHA. Parent acceptance belongs
to the project lead; do not upgrade overall UI/native/pixel status.

## Worker delivery

The exact source mapping is `C.serverId` -> canonical `scanState.serverId` and
original local `R` -> canonical `browseServerId` / derived `dataServerId`.
Canonical now resynchronizes an options-only redirect to the parent scan server,
so options(321) -> payload server 322 produces `[321,322,321]` and settles on
321 like the original. Server-transition option invalidation now also matches the
source distinction between a positive-server transition and loss to server 0;
backend-availability and unmount disposal fences remain separate and intact.

New R1 evidence executes five original/current cases and redirects the unchanged
parent filter checker output into the R1 directory. The lead PM-027 checker is
6/6, the R1 comparison is 5/5, delayed Clear remains `[322,321]`, maintained
request/navigation/interaction/integration and historical replay pass, canonical
check/build/package pass, offline browser smoke passes with zero console errors,
and protected WIP remains unchanged/unstaged. See the dated R1 review and evidence
directory. Parent/global UI/native/pixel acceptance is unchanged.
