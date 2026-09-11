# Team workflow and current prompts

Updated 2026-09-11. This replaces the Web/Sol split. [AGENTS.md](../AGENTS.md) governs every worker; [task.md](../task.md) retains all 47 acceptance cases.

## Roles and current task

| Role | Responsibility |
|---|---|
| Owner | Sets priorities and judges results. Follows plain permitted UI steps, describes visible results and supplies screenshots. No commands or technical diagnosis. |
| PM | Audits findings/evidence and maintains tasks/instructions. Does not implement scripts/app code or operate the game. |
| ChatGPT Web | Single primary research, implementation and technical verification worker. Builds/fixes, automates evidence collection, prepares the owner guide and interprets results. |
| Separate Sol worker | No active assignment. Historical contributions remain credited. |
| Daybreak | No task until a specific reviewed research escalation is assigned. |

Web delivered preparation at `9c20896`; the former Sol-labelled worker delivered its blocked attempt at `ccb74b0` and existing-log diagnosis at `537a5b9`. That route was codex-chatgpt-web 5.0.6 / chatgpt-web/high. Native navigation worked, but a get_window_state call was rejected upstream before local dispatch; no fresh Start was sent. Reviewer-specific cause remains unknown. Do not repeat diagnosis without new evidence or invent an application fix.

Current Web reports files/shell/existing-log access but no native screenshot/click tool. **Current task: Web fixes PM15-01/02 in the implemented recorder.** The owner already completed the no-saved-context check; no repeat is needed. The collector exists but future saved-reopen readiness is withheld. Read [review 15](reviews/2026-09-11-review-15-owner-evidence.md). Resource live acceptance remains open; no monster work follows from this role change.

## Required owner-testing package

Use [live-test-handoff.md](live-test-handoff.md) for technical identity/results and [user-test-checklist.md](user-test-checklist.md) for plain UI instructions.

Web must provide:

1. An identified runnable build and current-client compatibility result. Web checks hashes, dependencies, selected profile/store and process/recovery state; the owner does not.
2. Tested scripts that automatically collect the required permitted evidence. Prefer Web starting collection itself; if local user initiation is necessary, provide an actual tested double-click entry point with understandable status. Never ask the owner to paste commands, install runtimes, copy output, inspect JSON or find databases.
3. One UI action per step, exact Chinese/English labels as applicable, expected visible result, completion/failure indication, screenshot checkpoint and stop condition. Explain what must be open/closed; do not assume Overview Launch is implemented.
4. A durable bundle per attempt: actual app/client/profile identity, timestamps, scoped logs/result references and hashes, actual request/query/result evidence when available, errors, and cleanup/restoration results. Save partial failures and preserve earlier attempts. Do not fabricate missing fields.
5. Meaningful verification for missing prerequisites, changed identities, incomplete evidence, interruption/resume and cleanup reporting. Collection must not silently trigger scans, modify stored records, clear journals or overwrite prior proof.
6. An explicit readiness decision for the permitted scope. READY_FOR_OWNER_CHECKS means actual scripts and instructions work; it does not clear restrictions or imply live success.

If normal Search request/result or render correlation is unavailable, document the instrumentation gap. Implement only independently permitted passive collection if feasible, label new instrumentation IMPLEMENTATION POLICY and validate it without replaying the denied workflow. SQLite reads are not the actual UI Search request/result. Screenshots complement technical evidence; do not ask the owner for developer-console or network traces.

Web interprets automatically collected evidence with owner screenshots/descriptions and records PASS/FAIL/BLOCKED/NOT_RUN. A screenshot or saved row alone is not fresh acquisition proof. Fix the first demonstrated failure and prepare a focused retest. PM audits combined evidence before accepting the function.

## Restrictions and alternative methods

- Missing capability: discover supported tools. An available documented API/CLI or read-only log inspection may help only within environment/tool/skill rules. Terminal access is not automatic permission for custom desktop control.
- Technical error: diagnose and use supported recovery without duplicating side effects.
- Explicit rejection: preserve exact call/time/reason. Do not recreate it through shell clicks, helpers, another tool/model or an owner-run script. Do not disable safeguards. Use existing diagnostics or an independent materially safer permitted action; otherwise record the required external condition.

SB-97 remains unresolved. Owner assistance is not a workaround for the rejected automated operation. Keep available saved-data/UI checks separate from blocked fresh acquisition. PM cannot waive platform restrictions. Do not disguise a live-action trigger as an evidence collector.

## Coordination and interruption

During an owner test Web must not rebuild/replace the app, edit its profile/store or unexpectedly control the same session. Save each completed step and cleanup obligations. After interruption Web inspects actual/saved state before giving further instructions; the owner is not asked to diagnose recovery. Keep secrets and unrelated private data out of shared evidence. Commit/push/verify coherent checkpoints under AGENTS.md.

## Prompt to send Web now

```text
Work in LW-Control. Read AGENTS.md, task.md, BACKLOG.md, docs/reviews/2026-09-11-review-15-owner-evidence.md and docs/live-test-handoff.md. Inspect HEAD/worktree and preserve other work.

PM accepts the owner's no-saved-context observation and the bounded LWB-PM13-009 repair. Do not ask the owner to repeat the empty-profile check.

Fix PM15-01: reopen evidence must belong to the current app session. Session one must never satisfy session two's Search/result/render gate. Add isolated tests for no second Search, stale/uncorrelated/mismatching second evidence, file/PID ordering or reuse, and a valid distinct second session with the same saved row.

Fix PM15-02: process-query failure/invalid output is UNKNOWN, not an empty process list or clean cleanup. Carry evidence-health failures into incomplete/blocked status, preserving partial logs. Test command failure, malformed output, legitimate empty output, and app/evidence failures after an earlier successful session.

Use offline isolated regressions; do not run a fresh scan, reroute SB-97, seed owner data, request an owner retest or start unrelated work. Keep collection automatic and owner instructions beginner-friendly. Update the exact build/evidence/handoff, run relevant checks, commit/push/verify, and return to PM. A collector fix is not live resource acceptance.
```

## Repeat prompt after owner feedback

```text
Continue the same resource function in LW-Control. Read AGENTS.md, docs/team-workflow.md and the current technical packet. Collect and interpret automatic evidence with the owner's screenshots/description; do not ask for command output or technical diagnosis. Fix only the first demonstrated supported defect, verify it and update the exact build, collection scripts and simple retest instructions. Preserve previous evidence and restrictions, commit/push/verify and return to PM. If evidence is missing, repair permitted collection before asking the owner to repeat work. No Sol or Daybreak assignment.
```
