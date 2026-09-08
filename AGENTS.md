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
