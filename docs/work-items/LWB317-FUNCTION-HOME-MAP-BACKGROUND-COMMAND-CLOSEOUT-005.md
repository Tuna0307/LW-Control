# LWB317-FUNCTION-HOME-MAP-BACKGROUND-COMMAND-CLOSEOUT-005

Status: **MERGED INTO HOME-MAP-A-TO-A-CAMPAIGN-007**, 2026-10-08.
Its complete obligations remain required under campaign checkpoint E; preserve
any completed results. Do not dispatch it as a second simultaneous worker task.
Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
Branch: `research/offline-controller`.
Audited starting ancestor: `ba97b8bf8fa8438004ae8d69ff95b9a4a1d8695d`;
begin from the lead's assignment commit containing this file, not an older checkout.
Parent: `LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001`, still PARTIAL.

## Goal

Close the remaining positive **production command -> native/frontend payload ->
profile reopen** proof for already captured Resource data. Use the real 8008-row
completed dataset, not fixtures. The previous live scan succeeded but its first
post-completion `map_search` call used the wrong envelope. Direct MapStore proof
alone does not close that command/application boundary.

Work alone: no subagents, other AI-chat delegation, GPT Work or Codex execution.
Read current AGENTS.md, docs/AI_WORK_PROTOCOL.md, the background-only owner direction,
the parent pilot, and docs/reviews/2026-10-08-LWB317-BACKGROUND-WORLD-RESOURCE-004-LEAD.md.
Read the 004 checkpoint notes and reports; preserve their negative history.

## Checkpoint A — actual positive commands and reopen

1. Inspect the actual 004 reports to locate the completed isolated database/run
   `b439bfa57cd54405a14e803d21964c9d`. Independently verify its provenance:
   server 2212, completed 2500/2500, zero failed, Resource total 8008.
   Preserve the source database, WAL/SHM if present, report files and historical hashes.
   Use SQLite read-only backup to a uniquely named task-owned temporary root.
   Record source identity/content hashes before and after; do not copy an inconsistent
   live DB or initialize production stores against the historical source.
2. Drive the actual production Map317CommandService/native command dispatcher.
   Do not substitute MapStore.Search for map_search. Use the recovered envelope
   `{kind:"resource",query:{serverId:2212,page:1,pageSize:50,...}}`.
   Compare returned total/records with a separate read-only query of the copied
   real dataset. Prove first/second page non-overlap, a positive filter derived
   from a real returned row, an impossible keyword returning zero, supported sort/
   page normalization from actual source, and repeated close/reopen persistence.
   Avoid invented filter fields or validation rules.
3. Exercise map_data_options and map_summary through production commands.
   Distinguish persisted local rows from live scan/context readiness:
   offline summary may legitimately have server 0 until context is known.
   An explicitly inert context seam can prove deterministic command composition
   but must be labelled inert, never LIVE_PROVEN or an authenticated game session.
   Do not force offline summary to claim a live server to make the check pass.
4. Preserve the original malformed-envelope negative record. Add a distinguishing
   check that the old envelope is rejected and the correct envelope returns
   the positive data. Check separate cancelled dataset still has zero publication.
   Queries must not modify accepted rows or trigger scan/action commands.

Prefer the normal production composition on a task-owned root. If using the existing
explicit test constructor, state the exact external seam and prove the production
dispatcher/control/store remain actual. Verify no scheduled jobs exist before
composition; prevent background workers from dispatching any gameplay. Do not
copy unrelated owner profiles/jobs into this task.

Commit a coherent A checkpoint, then continue B without asking for another relay.

## Checkpoint B — actual frontend boundary and profile lifetime

1. Inspect canonical src/LWBridge.UI-0.3.17/src/mapBackend.js, MapDataPage,
   App/native transport and profile routing. Execute actual production callbacks/
   adapter code, not a reimplementation of their payload builder.
2. Prove the frontend sends the same correct envelope and consumes the production
   command result from the copied real dataset. Verify Resource total, first/second
   page rows, filter changes, tab leave/return and reload/reopen against those
   positive rows. A headless mounted DOM/controlled transport harness is permitted;
   it is frontend integration evidence, not live desktop/canonical WebView proof.
   No owner desktop snapshots, screenshots, focus, keyboard or mouse.
3. Verify profile A (copied completed dataset), independent empty B, then A again
   through actual profile/runtime ownership. B must not inherit A rows/errors/
   stale replies; A must retain its persisted completed rows. Use an actual
   deferred response and obsolete-profile inverse where relevant.
4. Keep source/local UIUX, query normalization, event/readiness ownership,
   auth/session/run/lease/Stop behavior and provider-unavailable fences intact.
   Fix only a demonstrated source-backed defect within this command/profile scope.
   A passing clone-only test does not establish original parity. No product
   change is required merely to produce a commit.

If the normal command/context boundary genuinely requires a current authenticated
game context, one fresh isolated **query-only** Home launch/connect/Stop session
is authorized within the existing background pilot, with preflight compatibility/
backup/identity gates and exact restoration. The owner backgrounds the game.
Reuse the copied completed data; do not repeat a full scan just to fill time.
Record that data were captured in 004, not recaptured by the query-only session.
Never silently switch to a fixture when the real boundary fails.

## Checkpoint C — independent check, preservation and delivery

- Write a repeatable validator against actual new outputs, source/artifact hashes,
  real dataset provenance, query/profile results and cleanup. Include distinguishing
  malformed-envelope, empty-B, stale-response and cancellation-data negatives.
- Run applicable focused command/native-boundary/ownership tests and the 004 inverses,
  heartbeat/delegate/Lua ownership checks. If product/test source changed, build
  affected Release projects; if frontend/product code changed, run canonical check,
  fresh production build and check:production-build. Record exact commands/results.
- Inspect the complete task diff and inverse tests yourself. Do not count unreturned
  reviewer work, unexecuted commands or historical hashes as fresh validation.
- No pending requests/subscriptions/processes or recovery journal; exact originals
  restored if a game was launched. Retain meaningful data/evidence and delete only
  verified task-owned expendable roots. Preserve all historical 004 attempts,
  both live run IDs, installed originals, original encrypted payload artifacts,
  accepted fixes and unrelated work.
- Update this work item, relevant current masters, feature ledger/parity matrix,
  handoff/queue and a dated WORKER review. Mark proof types separately:
  current-client live capture, local command replay, inert context/frontend,
  live query-only witness (if actually performed), and remaining UNKNOWN.
- Review/commit/push coherent checkpoints to origin/research/offline-controller,
  verify direct remote SHA and clean/unrelated-only worktree.
- Return once at **AWAITING_REVIEW** with executed distinguishing checks, commits,
  cleanup, genuine remaining limits and exact continuation. Project-lead acceptance
  and full original Home/Map parity remain separate.

## Boundaries

No desktop capture/input/focus or routing around Windows-MCP restrictions.
No new scan, forced city/world navigation, Resource export, claims, plunder,
sharing, marches, spending, cross-server movement, recurring Auto, updater,
protected-original service access, login/licensing bypass or guessed Lua controllers.
No legacy/fixture fallback or manufactured success. Technical ownership gates remain.
Do not rewrite historical negative evidence. If another project chat/process is
active on these files or a pilot session, inspect fresh identities and coordinate
through durable ownership/owner relay before concurrent work; do not reset its work.
