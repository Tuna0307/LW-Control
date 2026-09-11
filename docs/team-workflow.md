# Team workflow — one live Overview delivery

Owner priority reset, 2026-09-11. [AGENTS.md](../AGENTS.md) is mandatory; [task.md](../task.md) retains all 47 cases. [overview-live-delivery.md](overview-live-delivery.md) is the active bounded plan and overrides old resource-first/monster-next instructions.

## Roles

- Owner sets priorities, verifies actual live results, and explicitly selects the next feature. The owner supplies only guided UI actions, descriptions and screenshots.
- PM combines findings, audits evidence and maintains tasks. PM does not implement application code/scripts or operate the game.
- Web is the single implementation/research/technical-verification worker. It automatically collects all needed technical evidence and provides a simple actual entry point/guide. Never ask the owner for commands, terminal output, JSON/database inspection, hashes or recovery diagnosis.
- No separate Sol role. Daybreak is unassigned unless a specific reviewed escalation is assigned.

## Current result required

Normal Overview **Launch Game** opens the current official game, establishes a verified injected/loaded bridge, displays exactly **LWbridge is running** at the top centre inside the game, and Overview **Close Game** closes the correct game/session with clean state. A process or overlay alone cannot prove injection readiness. The owner must verify this complete sequence before any next feature is started.

## Ordered work and continuity

Web owns all OVL-00–06 in [the plan](overview-live-delivery.md): finish the interrupted PM15 evidence commit, trace only necessary launch/session contracts, implement normal launch and bridge readiness, render the truthful in-game message, implement close/cleanup, prepare automatic capture and the beginner guide, then verify the complete current-game outcome with the owner when permitted.

Do not return after every subtask merely to ask whether to continue. Commit/push/verify coherent checkpoints, save exact resume state and continue the next direct dependency. CI success and research completion do not equal live acceptance. Report meaningful results rather than checkpoint volume. Stop for a real blocking external condition or necessary owner observation, not because a small commit ended.

Map Data work, monsters, exports, automatic reconnect/startup and updater work are deferred. Preserve the already-written PM15 evidence and finish its delivery housekeeping without extending that project. Web must inspect actual dirty/staged files and preserve the PM's priority changes. The owner chooses what comes after this Overview feature.

## Main prompt — send Web now

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control. Read AGENTS.md, the priority banner in task.md/BACKLOG.md, docs/overview-live-delivery.md and docs/lwbridge-project-status.md. Inspect actual HEAD/worktree and preserve pending work.

The owner has explicitly replaced resource-first work. Your only active feature is the normal Overview sequence: Launch Game opens the current game, the bridge is successfully injected/loaded and genuinely ready, the exact text "LWbridge is running" appears top-centre inside the game, and Close Game closes that owned session with verified cleanup. No fake overlay, process-only success or guessed protocol.

All OVL-00–06 subtasks in the plan are assigned to you. First finish the already-prepared PM15 evidence/handoff commit and verify delivery without expanding recorder work. Then proceed through the Overview dependency chain, implementation, in-game message, close/cleanup, automatic evidence collection and beginner-friendly test guide. Reuse established findings; research only direct blockers. Continue through coherent commit/push checkpoints without waiting for PM after each subtask.

You own technical capture and diagnosis. The owner can only follow clearly explained UI actions, describe what appears and supply screenshots. Provide actual tested scripts/entry points; never ask the owner for commands/logs/hashes/JSON/database inspection. Preserve operation-specific restrictions; do not recreate rejected actions through another executor or the owner. Assess new Overview operations by their actual scope. Document exact external blockers and permitted next actions rather than generating filler.

Keep resources/monsters and other features deferred. No Daybreak assignment unless an exact reviewed ESC is assigned. Do not claim this feature complete until the owner verifies Launch -> real bridge/message -> Close works live. After owner acceptance, wait for the owner's next feature instruction. Save resumable checkpoints and report which of those four visible outcomes works, the remaining broken step and the next direct action.
```

## Repeat prompt — use after interruption or a partial checkpoint

```text
Continue the Overview-only delivery in docs/overview-live-delivery.md. Read AGENTS.md and the latest saved checkpoint; inspect HEAD/worktree, current state and cleanup obligations before resuming. Preserve concurrent work. Finish the next incomplete OVL step and continue direct dependencies without a routine PM stop after every commit. No resource/monster detour or repeated empty-profile test. Collect technical evidence automatically, fix the first failed step and keep owner instructions simple. Commit/push/verify checkpoints. Do not replay completed live actions blindly or bypass restrictions. Only the owner's verified Launch -> injected bridge with "LWbridge is running" top-centre -> Close result closes this feature; wait for the owner's next instruction afterward.
```

## Handling blockers

Unknown contracts require bounded source-led recovery. Missing tools require discovery/setup of permitted tools. Explicit denials require precise scope/reason and no reroute. Daybreak is not an automatic destination for a denied action. A complete reviewed question can be escalated; continue direct permitted Overview dependencies while waiting. Do not repeatedly request permissions already granted or claim the owner withheld access.

During an owner test, do not replace the running build or unexpectedly control/edit its session/profile. Capture partial results, preserve journals and inspect state after interruption. A record of failure is useful; changing evidence to turn it into success is not.
