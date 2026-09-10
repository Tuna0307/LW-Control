# Regular AI task — review 13

Read [AGENTS.md](../AGENTS.md), [task.md](../task.md), [BACKLOG.md](../BACKLOG.md), [current audit](lwbridge-project-status.md), [first-live-result contract](first-live-result.md) and the relevant evidence. Audit base: `b16fb9a`; inspect actual HEAD/worktree before editing. Preserve concurrent work. All 47 acceptance cases remain required.

## One active function

**Resource scan -> storage -> normal Resource Search/display -> second fresh refresh -> reopen.** This function remains partial. Do not switch to monster research, export, auto-update, broad original-pipe parity or unrelated fixes while it remains active. The next function is monster acquisition plus search, after resource acceptance; monster search alone cannot create missing game records.

Accepted: bounded current-client acquisition, PM12-A restoration/ownership, PM12-C immutable result/session correlation, and source-backed idle for the observed point. Do not repeat those recoveries. PM12-B is reopened only for the newly identified PM13-03 races. PM12-D normal-window completion remains open. Read the audit before treating contributor checkbox claims as full acceptance.

Post-review checkpoint LWB-PM13-001 implements and offline-tests the saved-context repair: exactly one published server in the selected profile store can seed browsing as saved_profile_index/unavailable; zero or multiple saved servers fail explicitly. The historical production DB used by review 13 is not currently present at its recorded path, so PM13-01 remains open at the ordinary-window reopen proof gate. Do not recreate that row from fixtures or historical JSON.

## Execute in this order within the resource function

1. **PM13-01: make the existing real saved row visible on reopen.** PM verified one persisted resource in the selected profile but the normal UI shows zero. Trace UI query -> missing server context -> summary/storage and fix saved-context browsing without pretending the game is connected. Preserve provenance, profile/server isolation, saved versus live state, unknown fields and explicit errors. Verify ordinary Resource Search after app restart; no fixture/manual record or new scan may substitute for the saved row.
2. **PM13-01b: make the button's limitations understandable.** All eight categories currently appear selected; default Start fails with a generic message. Explain resource-only support through localized product wording, keeping layout faithful. No silent category fallback. Distinguish unsupported scan, no records, missing context and query errors.
3. **PM13-03: close cancellation/commit and helper-ownership races.** Add deterministic barriers during import and between process start/registration, not just before fake-helper completion. Define an atomic commit/cancel ordering so cancelled/closed work cannot sneak a row into storage. Any started helper stays owned through cleanup. Preserve passing PM12-A/C tests.
4. **PM13-02: repair the proof harness before relying on it.** Use the audit's three failing DOM counterexamples. Bind rendered identity and freshness to the exact result/query for each acquisition; reject placeholders/loading/errors and previous-run rows. Exercise the normal Search/refresh path, including the second read. Preserve correlation metadata and prepare screenshot directories before writing.
5. **PM13-04: verify the complete function through the normal window when permitted.** Real resource-only Start, truthful state/cleanup, correlated source/result/storage/query/render, a second newer acquisition, Search/refresh and saved row after app restart. Do not imply full-world scanning or persistent connection. A screen capture by itself is insufficient.

If a required operation is actually restricted, document its exact tool/reason/target and the permitted alternative or required external change. Keep the function ACTIVE/BLOCKED and continue only its direct permitted dependencies. Do not endlessly generate unrelated research checkpoints, self-approve an escalation, or reroute a denied action through another executor/model. Existing user permissions do not need renewal.

## Computer Use capability correction

On review 13 the PM successfully used the installed `computer-use` skill via `mcp__node_repl__js` and `@oai/sky` to inspect and click the real Windows app. Read the installed SKILL.md and guidance/API first; discover whether that tool is callable in **your** environment. Browser-only `mcp__cua_repl` native limitations do not establish that all installed native capabilities are absent. If sky is unavailable, record the exact capability gap and use permitted offline checks/prepare the UI handoff. Do not invoke the native helper executable or custom helper protocol as a workaround. SB-97's rejected live proof remains a separate restriction and is not cleared by native capability discovery.

## Resource function exit criteria

- Existing source-backed saved row survives app restart and displays with correct profile/server/coordinates/level/time and truthful saved status.
- Unsupported selection and unavailable context give clear, accurate UI feedback; no synthetic data appears in production.
- Stop/timeout/Close/duplicate Start tests cover late commit and cleanup ownership, not only request cancellation.
- A permitted fresh Start and a second fresh read reach the normal table with durable source/request/result/query/render correlation. The second read may return the same point; its acquisition time and query/render evidence must be newer.
- Required cleanup/integrity checks pass; missing names and unobserved Gathering remain explicit limitations. No full-map or all-category claim follows from this bounded result.
- Documentation, focused checks, coherent commit, push and remote verification complete. Only then select the next user function. An external blocker is recorded, not counted as passing this gate.

## Next function, queued only: monster scan and search

Recover current-client monster acquisition/type/identity/field mappings and original LWBridge normalization/query semantics. Trace Start -> real response -> monster records -> persisted index -> normal Monster Search. Distinguish an empty scanned area from absent acquisition and from filter/context errors. Prove a real monster appears, matching filters find it, nonmatching filters exclude it, refresh updates it, and scope is maintained; never manufacture a mob or claim an empty result proves acquisition. Stay on this function until its stated scope works or an exact external blocker is recorded. Regular AI owns it; Daybreak only receives a complete reviewed ESC packet for a specific unresolved question.

## Checkpoints and reporting

Run checks appropriate to changes. Sequence builds sharing output. Baseline: Release desktop build; desktop checks with `--verify-real-config-unchanged`; three PM12 isolated recovery/session/scoped-close regressions; frontend `--check`; preference and both transport checks; browser verifier using the existing Playwright installation and Edge channel. The audit's proof-row reproducer is a **known-failure demonstration**, not a passing acceptance test after its source predicate changes; replace it with the new meaningful regression when fixing the defect, retaining historical output.

At each checkpoint report: current function; what the user can actually do; what changed; live versus offline evidence; precise remaining failure; next action within this same function; commit and verified remote. Save confirmed findings immediately. Commit/push coherent checkpoints without a fresh PM permission gate; a research checkpoint does not close the function.

## Main prompt to give the regular AI

```text
Work in LW-Control. Read AGENTS.md, task.md, BACKLOG.md, docs/lwbridge-project-status.md, docs/implementation-handoff.md and docs/first-live-result.md. Inspect current HEAD/worktree and resume the current function from saved evidence.

Follow review 13: finish the resource scan/search/display function one step at a time. Start with PM13-01: the real saved resource exists but reopening the normal app shows zero rows. Fix its saved browsing context and clear error feedback, then the identified cancellation/ownership races and false-positive UI proof. Verify the real normal buttons, second fresh refresh and reopen when permitted. Do not replace missing results with fixtures or move to monsters/unrelated research before this function passes its exit criteria. If externally blocked, document the exact cause and continue only this function's permitted prerequisites.

Use available tools and the installed Computer Use skill; native sky worked for the PM but you must verify your own capabilities. Preserve actual restrictions and never reroute denied operations. Reverse-engineer missing contracts, document each confirmed finding, run meaningful checks, and commit/push/verify each coherent checkpoint. Report results in plain language. No Daybreak task is assigned unless a complete escalation receives PM review.
```

## Repeatable continuation prompt

```text
Continue from the latest committed checkpoint in LW-Control. Re-read AGENTS.md and the current audit, backlog and implementation handoff; inspect HEAD/worktree so you preserve other work. Stay on the current user-visible function and fix its next failed step. Reproduce the cause before changing it, verify the actual result, document evidence and remaining limits, then commit/push and verify GitHub. Do not repeat completed research or jump to another function because a research checkpoint ended. Move to the next queued function only after the current exit criteria pass. If an external restriction prevents completion, retain the open gate, record the exact condition and permitted next action, and avoid filler work or rerouting. End with what now works, what still fails, and the next step.
```
