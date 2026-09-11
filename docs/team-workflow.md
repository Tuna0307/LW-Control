# Team roles, current assignments and prompts

Owner-approved division of responsibility, 2026-09-11. This is a coordination supplement to [AGENTS.md](../AGENTS.md) and [task.md](../task.md); it does not replace the full 47 acceptance cases. The current technical audit remains review 14. No new live acceptance is implied by this role change.

## Current dispatch order

| Order | Worker | Assigned deliverable | Return to |
|---|---|---|---|
| 1 — send now | ChatGPT Web | Prepare the exact latest resource-only build and complete the live test packet. Reuse verified repairs; fix only a concrete prerequisite. State READY_FOR_SOL or the precise preparation blocker. | Owner forwards completion to Sol; PM review is not needed just to start a ready test. |
| 2 — after Web's packet | Codex Sol 5.6 | Verify the exact handed-off build through the normal app against the current game when permitted. Capture first fresh result, newer second result, reopen persistence and cleanup. Return PASS/FAIL/BLOCKED per step. | Web for a reproducible defect; PM for complete evidence or an external blocker requiring a decision. |
| 3 — if a step fails | ChatGPT Web, then Sol | Web diagnoses/fixes that specific failure and delivers a new build; Sol retests the affected step and relevant resource exit criteria. | Repeat without fresh PM permission; keep the resource function active. |
| No current task | Codex Daybreak | Wait for an explicitly assigned, reviewed ESC question. Tool availability, missing live proof or an automatic-review rejection alone is not a specialist research assignment. | PM. |
| After test evidence | Project manager | Audit combined findings and test proof; accept/reject the bounded resource result and assign the next function. | Owner: what works live, what fails, next owner/deliverable. |

The owner can relay the prompts and completion messages; nothing here claims cross-task messaging is automatic. Do not create tasks or send messages on the owner's behalf without the relevant request. Workers may complete the Web/Sol loop via the committed packet and user-forwarded messages without requesting routine permission again.

## Handoff discipline

Use [live-test-handoff.md](live-test-handoff.md). Web fills its preparation section, including the exact tested build identity. Sol fills the result section and saves sanitized evidence under an identified evidence directory. Keep previous run evidence immutable; summarize/link it rather than overwriting failure history. On each attempt record the app build, actual client fingerprint, profile/data root and step status so an old row cannot pass as a fresh scan.

During Sol testing, Web must not rebuild the shared output, replace the executable, change the tested source, edit the profile/database or operate the game. Release ownership explicitly on return. Do not stage another worker's unfinished work. This coordination rule does not require destructive resets or discarding local changes.

Before a potentially interruptible segment, save next action and cleanup ownership. After each observation, save the result promptly. After reconnection, inspect actual state before deciding whether to continue or clean up. An incomplete attempt stays incomplete; do not invent PASS to resume quickly.

SB-97 remains an operation-specific restriction on the prepared live proof. Capability discovery and the owner-approved role split do not clear it. Neither Web nor Sol nor Daybreak may reproduce a denied operation through a different executor merely to evade the restriction. Report exact scope and any genuinely independent permitted path; otherwise retain BLOCKED and identify the required external change. This is not a blanket claim that all ordinary UI checks are prohibited. PM cannot waive platform restrictions.

## Prompt 1 — ChatGPT Web, send first

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control as the main implementation/research worker. Read AGENTS.md, task.md, BACKLOG.md, docs/team-workflow.md, docs/implementation-handoff.md, docs/lwbridge-project-status.md, docs/user-test-checklist.md and docs/live-test-handoff.md. Inspect actual HEAD/worktree and preserve other work.

Your current task is to prepare PM13-04 for Sol's real-game validation, not repeat completed research. PM13-01/01b/02/03 repairs are already accepted at their recorded scopes. Verify the latest runnable resource-only build and necessary offline checks; complete docs/live-test-handoff.md with exact implementation commit, executable path/hash, current-client compatibility evidence, profile/data-root discovery, prerequisites, steps/expected results, evidence collection and cleanup instructions. Record all actual restrictions, including SB-97; do not package a denied operation for another executor. Mark READY_FOR_SOL only for a concrete test packet with a permitted executable test scope; otherwise identify the precise blocker and useful permitted checks.

You own code fixes and research. Sol owns native Computer Use/live verification. Do not start monsters, exports or unrelated research. Commit/push and verify each coherent checkpoint. End with readiness, what Sol must test, remaining restrictions, packet path and commit. Do not call this live success.
```

## Prompt 2 — Codex Sol 5.6, send after Web finishes its packet

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control as the live-testing and Computer Use worker. Read AGENTS.md, docs/team-workflow.md, task.md, docs/lwbridge-project-status.md, docs/user-test-checklist.md and docs/live-test-handoff.md. Use Web's committed packet and verify the actual build/client/profile identities. Do not test an obsolete preview or replace Web's build silently.

Check your supported Computer Use capability, then perform the packet's permitted real-game resource test through the normal rebuilt app: first fresh scan and Search/display; second newer scan and Search/display; close/reopen and saved result. Correlate source/request/storage/query/render evidence and verify cleanup. Computer Use setup, an old saved row, a screenshot or 100% progress alone is not success. Preserve SB-97 and other actual restrictions; do not reroute a denied operation. If the packet is incomplete or execution is restricted, report the exact missing requirement instead of guessing.

Use short resumable segments. Save each completed step, evidence and cleanup state; after a disconnect inspect current state before resuming. Stop at the first failed step, reproduce/diagnose it as permitted and return exact evidence to Web. Do not undertake broad code changes. Fill the result section of docs/live-test-handoff.md with PASS/FAIL/BLOCKED per step, commit/push/verify, and identify the next owner. All passing steps go to PM for acceptance; a code failure goes to Web for repair.
```

## Repeat prompt — Web after Sol finds a failure

```text
Continue as ChatGPT Web in LW-Control. Read AGENTS.md, docs/team-workflow.md and the latest docs/live-test-handoff.md plus Sol's evidence. Verify HEAD and diagnose the first failed resource step. Recover any missing contract rather than guess, implement the smallest supported fix, run relevant checks and prepare a new exact build for Sol. Do not repeat closed research or switch functions. Update the packet with the fix, new build identity and required retest; retain prior evidence. Commit/push/verify. If the issue is external or restricted, record that accurately rather than claiming a code fix or sending a denied operation elsewhere.
```

## Repeat prompt — Sol after a fix or disconnect

```text
Continue as Codex Sol 5.6 in LW-Control. Read AGENTS.md, docs/team-workflow.md and the latest docs/live-test-handoff.md. Check actual HEAD/build/client/process state and saved cleanup obligations. Resume the first unverified step or retest Web's specific fix using supported Computer Use and permitted actions. Do not replay a completed live action blindly. Save per-step evidence and PASS/FAIL/BLOCKED, commit/push/verify, and return the first failure to Web or complete evidence to PM. Do not claim overall completion from tool setup or saved data alone.
```

## Daybreak — do not send a task now

There is no assigned specialist question. If Web exhausts relevant permitted methods for a specific missing contract, it must complete an ESC packet with the evidence, attempts, alternatives, scope and return criteria. PM reviews it and supplies a question-specific prompt if assigned. [deep-binary-handoff.md](deep-binary-handoff.md) is the specialist's standing guide, not an instruction to begin unassigned work.
