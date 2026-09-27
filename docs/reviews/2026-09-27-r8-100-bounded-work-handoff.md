# R8-100 — bounded work cadence and full new-chat handoff

**Date:** 2026-09-27
**Scope:** operating cadence, continuity documentation and stale instruction cleanup; no production behavior change.

## Owner requirement

The owner reports that long uninterrupted ChatGPT work can be cut off or appear stuck around the ~25-minute range.

The durable operating rule is now:

- target no more than roughly 20 minutes from first technical action to owner summary;
- around minute 17–18, stop starting new investigation branches/long-running jobs;
- preserve evidence and current state;
- close a coherent checkpoint when warranted;
- summarize actual discoveries/changes, evidence/tests, remaining NOT WORKING/UNKNOWN items, Git state and exact resume point;
- wait for owner `continue` / `ok continue` before the next block;
- summarize earlier on disconnect/blocker;
- never lower evidence standards or promote a fallback because a work block is ending.

Recorded in:
- `AGENTS.md`;
- `docs/team-workflow.md`;
- `docs/implementation-handoff.md`;
- `docs/lwbridge-project-status.md`;
- `docs/handoffs/2026-09-27-r8-100-chat-handoff.md`.

## Full chat handoff

The new handoff records:
- owner binary WORKING rule;
- strict one-to-one LWBridge 0.3.1 product goal;
- Map-first/Home-second priority;
- no-fallback rule;
- no-concurrent-worker / owner-game-process handling;
- branch/worktree discipline;
- historical R8-065/R8-066 live evidence classification;
- Map recovery through R8-088;
- R8-098 original proxy Lua compiler/source boundary;
- R8-099 decrypted format-2 module-table/source-assembly contract;
- known LWBP2/AES and package-key/envelope context;
- preserved protected consumer boundary;
- negative artifact searches already completed;
- exact next Map source-byte objective;
- useful source/tool paths;
- Home/auth/entitlement state;
- required validation/Git delivery workflow;
- exact new-chat startup procedure.

## Stale instruction correction

A previous repository rule broadly allowed closing an already-open owner game session. This conflicted with the later owner clarification that there are no concurrent AI workers and the owner may simply have Last War open for play.

Current rule:
- inspect process ownership before live testing;
- treat unexplained pre-existing Last War as owner activity unless evidence shows assistant ownership;
- do not close/kill/repurpose the owner play session unless it is clearly assistant-launched or the owner explicitly hands it over.

## Status impact

No production code changed.

Technical recovery baseline remains **R8-099**.

Owner-facing status remains:
- **Map: NOT WORKING**
- **Home: NOT WORKING**

Next technical work remains authentic decrypted bridge-module/source recovery leading directly to `XluaBridgeMapScanTick`.
