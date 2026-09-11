# Mandatory project rules for every AI and contributor

These are explicit user requirements for this entire repository, regardless of AI vendor, model, editor, session or task. Read this file before research, implementation, review or cleanup. Carry these rules into every delegated task and handoff. Do not weaken or silently waive them to make progress appear complete. Later explicit user instructions and the operating environment's higher-priority rules still apply; report an actual conflict instead of concealing it.

## 1. Reverse-engineer first; do not invent behavior or values

**MUST prioritize evidence from the verified LWBridge reference and official Last War artifacts over guesses, recollection, generic game assumptions or plausible-looking implementations.**

- LWBridge feature authority: `../LW/lwbridge-0.3.1.exe` and its immutable recovered assets under `evidence/lwbridge-0.3.1/`. Verify the reference hash against `task.md` before new recovery work.
- Official-runtime authority: the current installed Last War launcher, executable, DLLs, managed/native code, Lua/bytecode, packaged resources, configuration and relevant logs. Discover and fingerprint the actual installation; do not assume a historical path/version is still current. Use `docs/official-runtime-architecture.md` and existing inspectors as starting points.
- Existing documentation is an evidence index, not automatic proof. Check its source identity and whether it applies to the current artifact/build. Reuse valid findings instead of repeating extraction; revisit them when binaries, contracts or contradictory observations change.
- Use LWBridge to establish original feature/UI/host behavior and official Last War artifacts to establish current game/runtime contracts. Where they differ, document the discrepancy and recover the necessary mapping. Never silently treat one build's offsets, ABI or packet schema as another build's contract.
- Before implementing a feature or changing a constant, trace the applicable UI trigger, arguments, host handler, runtime request/response, state update and visible result as far as the available evidence permits. Record where that trace stops.

**MUST NOT invent or silently substitute** command names, IDs, offsets, addresses, field meanings/types, defaults, limits, retry counts, timeouts, concurrency, geometry, intervals, formulas, eligibility rules, counts or success conditions. A value that makes a test pass is not evidence that the original uses it. Copying a number from another AI's answer is not recovery.

- Every behavior-affecting recovered value/contract must have a traceable finding/source reference in the relevant documentation and, where practical, at its code definition. Derived values need the formula, inputs and provenance.
- If unsupported, label it `UNKNOWN/BLOCKED`, preserve missing fields as unknown rather than fabricated zeroes, and keep dependent production operations explicitly unavailable. Specify the next concrete source trace or experiment. Continue independent work whose contracts are known.
- A deliberate rebuild engineering choice must be labelled **IMPLEMENTATION POLICY**, with its rationale, scope and validation, rather than presented as recovered parity. Do not use this label to disguise a guessed original contract or bypass an unresolved prerequisite.
- Synthetic fixtures may use explicitly labelled deterministic test data in isolated test/capture paths. They must never become production facts, defaults or evidence of live correctness.

## 2. Document every successful recovery immediately

**A reverse-engineering result is not complete until another AI can locate, understand and reproduce it from durable repository evidence.** Do not keep a successful finding only in chat, scratch files, terminal output or personal memory.

For each confirmed contract/value/behavior, add or update a finding in the appropriate existing document under `docs/`, with supporting material under `evidence/` when needed. Record it as soon as it is confirmed, before relying on it in implementation or moving to an unrelated investigation. Incrementally save long-running work so interruptions do not erase the recovery path.

Each finding MUST contain:

1. **Stable finding ID, date and scope:** the feature/command and what was established.
2. **Source identity:** artifact path/name, version/build if available, SHA-256; distinguish LWBridge reference from official current-client evidence.
3. **Exact locator:** symbol/function/method, file and line, resource key, table/SQL, RVA/file offset, bytecode location, or correlated log/event identifier. State the coordinate system for addresses/offsets.
4. **Reproduction:** tool/script and exact command or bounded analysis steps; link durable output or a focused excerpt. Do not commit credentials, private tokens or unrelated personal data.
5. **Result:** exact fields/types/units/defaults/constants, behavior and preconditions; separate observed facts from interpretations and derived values.
6. **Validation and limits:** static-only versus runtime observation, tests performed, current-build applicability, contradictory evidence, unresolved questions and next experiment.
7. **Implementation impact:** affected files and S/O/M/R or PM defect IDs, what can now be implemented, and what remains gated.

Use these evidence labels consistently:

- **RECOVERED:** established from identified original/static artifacts; not automatically proven against the current live game.
- **IMPLEMENTED/OFFLINE-TESTED:** rebuild code and stated offline tests; not a claim of a live outcome.
- **LIVE-PROVEN:** observed on the identified current client with correlated authoritative before/after evidence.
- **UNKNOWN/BLOCKED:** unresolved; include the exact missing contract or proof.
- **IMPLEMENTATION POLICY:** an explicitly documented rebuild design choice, not a recovered original fact.

Preserve meaningful superseded findings with their source/build and explain why they no longer apply. Do not overwrite historical evidence to make it agree with the latest result. Index new material in `docs/README.md`, update `docs/lwbridge-feature-ledger.md` and the relevant `BACKLOG.md` entries, and link the subject document instead of duplicating conflicting specifications.

## 3. Tool choice and installation are pre-authorized

The user grants standing permission to use, obtain, install and configure whatever tools are needed for this project's reverse-engineering, reconstruction and validation. **Do not ask again for routine project-related tool discovery, downloads, installation or configuration.** The user is not expected to know which tools are already installed. The objective is working, evidence-backed functionality, not limiting the investigation to tools exposed as AI integrations.

- Inspect available executables, package managers, runtimes, libraries, installed-tool directories and existing scripts. A tool missing from `PATH` or from the AI tool list is not proof it is absent from the machine.
- **“No Ghidra integration is available” is not a research blocker.** If Ghidra or another suitable analyzer is needed, locate/install it and its required runtime, then use its standalone GUI, CLI, headless scripting or exported analysis. Apply the same approach to disassemblers, decompilers, debuggers, PE/IL/Lua/resource inspectors and custom scripts. Select tools according to the unresolved contract, not vendor preference.
- Use available capabilities directly when no connector/plugin exists. Follow the current environment's tool-use rules. Do not confuse missing MCP integration with inability to invoke a local tool.
- Prefer official project/vendor distributions or trusted package-manager sources. Verify the download/version and published checksum/signature where available. Use an isolated environment or user-scoped tool directory when practical; keep installers, binaries, caches and generated bulk output out of source commits.
- Record the tool name/version, source/download URL or package ID, installation location, runtime dependencies and exact invocation in the relevant recovery finding. Verify it runs before claiming setup complete. Another AI must be able to reuse the installation and reproduce the analysis.
- Install tools when they materially enable the next investigation; do not spend a checkpoint installing an unrelated collection. Reuse capable installed tools and valid prior evidence. If a necessary tool is unavailable, diagnose/install it or continue through another suitable permitted analysis method rather than guessing the missing contract.

### Report blockers accurately; do not attribute them to the user

The user has not withheld permission for the project-related technical work described above. Separate these cases explicitly:

- **Tool/setup gap:** identify what is missing, what discovery/install/configuration was attempted, the concrete error and the next remedy. Missing integration alone is not a final blocker.
- **Unrecovered contract:** identify the missing fact, source/build and next analysis. This is a research gap, not denied permission. Continue independent recovery/implementation while investigating it.
- **Unavailable validation target:** identify the missing runtime/world state or authoritative observation; do not relabel offline evidence as live proof.
- **Environment/platform restriction:** identify the exact denied operation, the tool/system that rejected it and its stated reason. Say that the restriction is imposed by that environment, not by the user. Record the restriction and pursue a permitted alternative when one exists.

User consent does not change system/developer policy, OS permissions or unavailable tool capabilities. Do not claim otherwise or repackage a denied operation through another executor merely to evade a restriction. Use the permissions actually available and explain the precise remaining requirement only when it genuinely prevents progress. Do not repeatedly request authorization already granted here.

### Standing live-testing and Computer Use authorization — 2026-09-10

The user explicitly authorizes every AI working on this project to **open, close and restart the official game and launcher whenever needed for testing**, including the user's already-open game session, and to **control the computer through the Computer Use plugin for project testing**. This includes navigating the game and rebuilt app, observing their state, running the bounded scan/data-read demonstration, and capturing relevant verification evidence. Do not repeatedly ask the user to launch/close the game or reconfirm routine project-related computer control.

Identify the actual target session/process/window before controlling it; do not stop unrelated applications or another task's tools. Prefer normal close/restart and coordinate shared computer access. User permission to control an existing game session does not prove the rebuild owns that session or has established its bridge connection; preserve those distinct production acceptance gates.

Use the Computer Use skill/plugin when available and follow its tool instructions. Missing integration may be addressed under the standing tool-setup authorization, but permission does not create unavailable native-control capabilities or override system/developer rules. Record actual capability/restriction failures accurately, continue independent permitted work, and never reroute a denied operation. Existing explicit messaging/spending boundaries still apply; this grant is project testing permission, not unrestricted unrelated computer activity.

## 4. Completion requires evidence, documentation and GitHub delivery

The user explicitly requires **commit and push to GitHub after each completed task or coherent checkpoint**. This is standing project authorization for the relevant work; do not ask for the same routine commit/push permission again.

A task/checkpoint is complete only after ALL applicable steps are done:

1. Review the actual diff and verify that the work matches recovered contracts or clearly labelled implementation policy.
2. Run the checks appropriate to the changed behavior. Report failures, skipped checks and blocked live acceptance separately. Passing fixture screenshots, a promise, a command send or process presence does not establish real feature success.
3. Document every new recovery and update the feature ledger, progress checklist and handoff. Mark only the verified slice complete. A research checkpoint may be complete while a feature remains blocked; preserve that distinction.
4. Inspect the current branch, remote, worktree and staged files. Stage the coherent checkpoint, including code, tests and durable evidence; preserve unrelated unfinished work. Exclude local settings/databases, credentials, binaries, bulk captures and scratch output unless explicitly required and appropriate for the repository.
5. Create a descriptive Git commit and push it to the verified existing working branch on the configured GitHub remote, unless the user specified another destination. Never force-push, rewrite history, switch the destination or discard changes merely to finish delivery.
6. Verify the remote branch contains the committed revision. Report the commit ID, branch, checks and remaining blockers. A local commit alone is not GitHub delivery.

If checks, permissions, authentication, remote divergence or connectivity prevent a step, preserve the work and record the exact failure and next action. Do not claim it was pushed or the feature is complete. A checkpoint intentionally preserving known failures must say so in its docs and commit message; it is not a release or a waiver of those failures. Do not defer all documentation/commits until the entire reconstruction is finished.

## 5. Required reading and handoff structure

- `AGENTS.md` — mandatory project rules; applies to all work and future sessions.
- `task.md` — primary AI handoff, full Overview + Map Data requirements and acceptance contract.
- `BACKLOG.md` — current priorities and progress checkboxes. Do not recreate the former `TASKS.md`.
- `docs/lwbridge-project-status.md` — dated project-manager audit, reproduced defects and remaining work.
- `docs/README.md` and `docs/lwbridge-feature-ledger.md` — evidence index and per-feature proof.

Always start from the saved checkpoint, inspect the current code/evidence and continue the unresolved work. Preserve the login-free recovered UI and completed legacy cleanup. These rules require evidence-led progress, not another cosmetic implementation or a guessed approximation.

## 6. Regular AI first; evidence-backed Daybreak escalation only

**Explicit user requirement, updated 2026-09-11:** ChatGPT Web is the single primary implementation/research/verification worker. The owner supplies guided manual observations and screenshots when native control is unavailable. There is no separate Sol assignment. Follow section 8 for ownership and automated evidence collection. Daybreak is a specialist escalation destination, not the automatic owner of all binary analysis or difficult tasks. The DB-01–06 labels describe subjects; they do not assign a model.

- Follow `docs/implementation-handoff.md` for the regular task and `docs/deep-binary-handoff.md` for the specialist task. Both inherit `task.md`; do not duplicate or reduce its 47 acceptance cases.
- Before requesting Daybreak, exhaust the relevant permitted methods you can reasonably identify. Review existing findings, readable assets/current-client code, scripts and available local tools; diagnose setup failures and correct invalid searches. Install a needed tool under section 3 when it materially helps. Document why any relevant alternative cannot answer the question. There is no arbitrary attempt count and no requirement to install unrelated tools or repeat failed commands indefinitely.
- **MUST create a durable request in `docs/daybreak-escalations.md` before handing off.** Use a stable ESC ID and record the exact question, affected feature, source/build/hash, already-known facts, each attempted method/tool/version/locator and result, evidence paths, exact error or restriction, alternatives considered, remaining uncertainty, why specialist analysis could help, bounded permitted scope and acceptance/return criteria. A bare "blocked", "too hard", "no Ghidra integration" or "needs deeper binary analysis" is insufficient.
- Separate capability/research exhaustion from tool setup, missing live targets, external service dependencies and environment restrictions. A safety denial alone is not evidence that Daybreak can or may do the denied action. Never use a different model, tool, task or CI job to reroute a prohibited operation. Record restrictions and continue independent permitted work.
- The project manager reviews requests as `NEEDS_INFORMATION`, `READY_FOR_PM_REVIEW`, `APPROVED`, `ASSIGNED`, `RETURNED_FOR_INTEGRATION`, `CLOSED` or `NOT_ASSIGNED`. Do not self-approve or dispatch a specialist task just by adding a DB tag. Explicit user assignment can select scope, subject to higher-priority restrictions.
- A specialist takes only the specific approved question. It returns durable source-attributed findings, unresolved edges, tests/limits and the exact implementation now unblocked. It does not absorb all remaining project work. The regular AI integrates/validates the result; a specialist research checkpoint does not close a live feature.
- Preserve one owner per active work item and coordinate file/build access. Inspect HEAD/worktree before edits and delivery; do not overwrite another AI's changes, build the same output concurrently, switch their branch or stage their unfinished work. Update shared status/ledger at a coherent checkpoint, commit/push and verify the remote under section 4.
- While an escalation is pending, continue the highest-priority independent supported work. Do not claim all methods were exhausted unless the recorded attempt history supports that statement.

### Restriction outcomes must be explicit at every checkpoint

For each unresolved contract associated with a denied operation, report whether the **question was resolved by identified permitted evidence**, **remains under investigation with a concrete next method**, or **has an ESC request pending with specified missing fields**. A historical denial remains recorded even when the question is subsequently answered. "No Daybreak request created" is not a resolution status.

Keep unresolved production-blocking questions in the escalation register as NEEDS_INFORMATION when a request is not yet complete; this is a tracking record, not an automatic assignment or a claim of exhaustion. Include method/alternative/result evidence before requesting PM approval. Do not indefinitely replace investigation of the same missing public contract with unrelated test-only checkpoints without explaining their integration value. Research/test-only progress must be labelled separately from an enabled UI command and from live acceptance.

## 7. Current delivery priority — first real result

The user accepted the project-management correction: **first establish a supported real-game connection, acquire one real resource point and display it in the rebuilt Map Data page**. Follow [the bounded live-result task](docs/first-live-result.md). This is the active priority across R5, R6 and R7; those labels are work areas, not evidence that prerequisite functionality is complete.

Defer PM9-A/future automatic updates, broad export/migration completion and unrelated research unless a named part directly blocks this demonstration. Keep a minimal current-client fingerprint/integrity check before live work. Every further research checkpoint must name the exact missing fact, why it blocks this demonstration, its evidence-producing method and the code/test it would unlock. Reuse established findings; stop substituting unrelated research, markers or fixture tests for the real connection/data/display path.

The demonstration is not full acceptance of both pages. Preserve all 47 acceptance cases, unknowns and provenance. Report a demonstrated real result or the exact unresolved blocker with attempted approaches and next action; never claim success from a running process, mocked/manual row, command send or screenshot alone. Commit/push each coherent checkpoint under section 4.

### One function at a time — explicit user instruction, review 13 (2026-09-10)

Maintain one active user-visible function with a defined input, expected result and exit criteria. Reproduce each failed button/action, identify the broken link, recover missing contracts, implement the fix and verify the actual result before moving to the next function. Research IDs and successful builds do not constitute feature completion. Do not switch to unrelated work merely because a checkpoint has ended or a failure is difficult.

Current function and its ordered repairs are in `docs/implementation-handoff.md`: resource scan -> saved/normal search/display -> fresh refresh -> reopen; monster acquisition/search is next. Preserve all 47 acceptance cases. If a real external restriction prevents closure, keep the same function ACTIVE/BLOCKED, document the exact cause/attempts/permitted next method or required external condition, and continue only direct permitted dependencies. If none remain, report that fact instead of inventing filler checkpoints. A blocker is not success or automatic specialist assignment.

Verify available Computer Use capabilities from the installed skill and callable tools each environment actually exposes. PM review 13 successfully used `mcp__node_repl__js` with `@oai/sky` for native Windows observations and clicks; a browser-only tool's limitations must not become a blanket assertion that native access is absent. This does not override historical/current operation restrictions. Keep saved data separate from live state and require source/request/query/render correlation rather than accepting any nonempty row or screenshot as proof. Commit/push each coherent checkpoint without waiting for full function completion, while keeping its acceptance gate open.

## 8. Mandatory delivery hierarchy and owner-assisted testing — updated 2026-09-11

The owner evaluates delivered results in the live game. Research volume, commits, passing offline checks and tool setup do not substitute for a working feature.

- **Owner:** highest project authority; sets priorities and receives plain-language results. Higher-priority environment rules still apply.
- **Project manager:** combines findings, audits implementation/test evidence, maintains priorities and instructions, assigns bounded tasks, reviews escalations and reports to the owner. The PM edits planning/audit documents and delivers those documentation checkpoints, but does not implement application code, perform reverse-engineering experiments or operate the game for testing. Request missing verification from the appropriate worker instead of duplicating their execution.
- **ChatGPT Web:** single primary researcher, implementer and technical verification worker. Owns fixes, evidence collection scripts, offline checks, preparation of the exact runnable build, detailed owner instructions and interpretation of results. The current session reports Desktop Commander files/shell access but no native screenshot/click tool. Discover actual capabilities; a terminal is not automatic authorization to drive the desktop through custom code.
- **Owner as manual tester:** follows clear permitted UI steps, describes what is visible and supplies screenshots. Assume no technical knowledge. Never require the owner to enter commands, copy terminal output, calculate hashes, locate databases, inspect JSON or diagnose recovery. Web must automate any required technical collection and provide a simple way to start it, preferably operated directly by Web through permitted tools. A tested double-click shortcut is acceptable when local user initiation is necessary.
- **Former Sol-labelled worker:** no separate active role. Its completed attempts remain historical evidence. The diagnosed route was codex-chatgpt-web 5.0.6 / chatgpt-web/high, not evidence of a native Sol model session. Do not schedule another Web-to-Sol handoff.
- **Codex Daybreak:** bounded specialist reverse-engineering after a complete reviewed escalation. No standing broad assignment and no automatic transfer of rejected operations. Claims about a model's different restrictions do not override actual environment rules.

Follow [the team workflow and copyable prompts](docs/team-workflow.md). Current order: Web prepares a verified build, automatic evidence collection and a beginner-friendly test guide -> owner performs permitted manual steps and supplies screenshots/descriptions -> Web diagnoses/fixes and prepares a focused retest -> PM audits evidence. Routine repair/retest preparation needs no repeated PM approval. Keep one active function; PM selects the next after acceptance unless the owner changes priorities.

Use [the live test handoff](docs/live-test-handoff.md) for technical identity, capture paths, results and resume state, and [the owner guide](docs/user-test-checklist.md) for plain UI steps. Web fills both and records technical results; PM records acceptance. READY_FOR_OWNER_CHECKS requires an actual tested collection script/entry point and complete instructions for the explicitly permitted scope. It does not authorize a restricted action or imply live success. Unimplemented collection remains pending, not a promise that it already works.

During owner testing, Web must not rebuild/replace the tested app or mutate its profile/store. Save results after each step; after interruption inspect actual state and cleanup obligations before resuming. Do not replay a live action blindly. Preserve unrelated work and follow section 4 for each coherent delivery.

### Mandatory automatic evidence collection

Before asking for owner testing that requires technical output, Web must implement or reuse and verify scripts that automatically save the required evidence. Include an attempt ID, actual app/client/profile identity, scoped logs/request/result references, stored-query evidence where permitted, timestamps, errors and cleanup/restoration results. Collect actual normal Search request/result and rendered-state evidence when available through permitted instrumentation; never substitute direct SQLite data or screenshots for missing correlation. Missing evidence remains explicit. Do not invent capture fields or scripts that have not been implemented.

The owner-facing entry point must explain in plain language whether preparation succeeded, the next visible action, completion/failure and where results were saved. Resolve paths automatically, preserve previous attempts, save partial results on failure/interruption, and keep secrets/unrelated personal data out of shared evidence. Prefer Web collecting files directly rather than asking the owner to browse technical folders. User screenshots and descriptions complement the technical evidence; they are the only manual evidence expected of the owner.

### Tool limitations and restrictions

Distinguish unavailable capabilities, ordinary technical errors and explicit approval/safety rejections. Another supported API/CLI or read-only observation can address a capability gap only when environment/tool/skill rules permit it. An explicit rejection cannot be recreated through shell clicks, OS automation, a helper, another model or an owner-run script. Do not disable safeguards or route the rejected operation to the owner. Preserve exact call/time/error and diagnose saved logs without replay.

The recorded pre-Start get_window_state rejection occurred upstream of local Computer Use dispatch; no Start was sent. Its reviewer-specific cause remains unknown. Successful prior navigation does not clear that restriction, and the rejection does not prove an LWBridge code defect or that every manual check is prohibited. SB-97 remains recorded; owner-assisted testing is not a workaround for the denied automated operation. Keep independently permitted saved-data/UI checks separate from restricted fresh acquisition, and do not mark the latter ready until its execution and evidence conditions are satisfied.
