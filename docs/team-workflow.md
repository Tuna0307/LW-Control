# Team workflow - accepted Overview milestone

Owner acceptance checkpoint, 2026-09-11. [AGENTS.md](../AGENTS.md) is mandatory; [task.md](../task.md) retains all 47 cases. The selected Overview Launch/message/Close milestone is complete as `LWB-OVL-003`. **There is no active next feature until the owner explicitly chooses one.** Existing operation-specific restrictions and deferred queues remain unchanged.

## Roles

- Owner sets priorities, verifies actual live results, and explicitly selects the next feature. The owner supplies only guided UI actions, descriptions and screenshots.
- PM combines findings, audits evidence and maintains tasks. PM does not implement application code/scripts or operate the game.
- Web is the single implementation/research/technical-verification worker. It automatically collects all needed technical evidence and provides a simple actual entry point/guide. Never ask the owner for commands, terminal output, JSON/database inspection, hashes or recovery diagnosis.
- No separate Sol role. Daybreak is unassigned unless a specific reviewed escalation is assigned.

## Current accepted result

`LWB-OVL-003` is **OWNER ACCEPTED**. Normal Overview **Launch Game** opened the current official game, the correlated injected bridge became ready, the owner screenshot visibly showed exactly **LWbridge is running** near the top centre inside the game, and Overview **Close Game** closed the exact owned session with automatic byte-exact restoration/cleanup evidence. **Stop here until the owner explicitly selects the next feature.**

## Ordered work and continuity

OVL-00-06 are closed for the selected milestone. Do not repeat the owner run, reopen Overview implementation, resume Map Data/resources/monsters, or choose another feature on a generic continuation prompt. Preserve `LWB-OVL-003`, its automatic evidence and restrictions.

When the owner explicitly names the next feature, inspect HEAD/worktree and then update this workflow/priority banner for that feature before implementation. Until then, only housekeeping needed to preserve/deliver this accepted checkpoint is in scope.

## Main prompt - send Web now

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control. Read AGENTS.md and the latest checkpoint. `LWB-OVL-003` is owner-accepted: normal Overview Launch -> correlated injected bridge -> visible "LWbridge is running" -> normal Close/cleanup worked live. Do not repeat that test or start another feature. Preserve existing restrictions and deferred work. If the owner has not explicitly named a next feature, report that the project is waiting for the owner's selection and make no product changes.
```

## Repeat prompt - use after interruption or a partial checkpoint

```text
Resume only acceptance-checkpoint housekeeping for `LWB-OVL-003` if something remains undelivered. Do not rerun the accepted Overview sequence and do not resume resources/monsters or another feature unless the owner explicitly selects it. Verify HEAD/worktree and preserve existing restrictions.
```

## Handling blockers

Unknown contracts require bounded source-led recovery. Missing tools require discovery/setup of permitted tools. Explicit denials require precise scope/reason and no reroute. Daybreak is not an automatic destination for a denied action. A complete reviewed question can be escalated; continue direct permitted Overview dependencies while waiting. Do not repeatedly request permissions already granted or claim the owner withheld access.

During an owner test, do not replace the running build or unexpectedly control/edit its session/profile. Capture partial results, preserve journals and inspect state after interruption. A record of failure is useful; changing evidence to turn it into success is not.
