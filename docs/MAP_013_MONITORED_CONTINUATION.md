# MAP-013 — monitored recovery and scanner continuation

**Current result (2026-10-12, continuation from `b2eacb821538`): PARTIAL.**
Monitoring was implemented and validated against the prior successful
`0x140191253` control (observed actual native child, 613-ms native
decompilation). Reusing the saved original analysis project, the first
prior timeout `0x14013a8c1` was admitted with an 1,800-second function
budget, but after ~659 seconds the native `decompile.exe` reached
8.20 GB peak working set with only 0.458 GiB free physical memory.
Only its path-/PID-/start-/parent-verified task child was stopped
for **RESOURCE_PRESSURE_STOP**; headless Java saved the project and
exited normally. **No 30-minute function, MCP, REA startup or outer
runner deadline expired.** Native CPU-time growth and pcode errors
do not establish useful decompilation progress:
**OBSERVABILITY_INSUFFICIENT**. Historical negatives were preserved.
Source-backed original Capstone call/jump inventories were added as
a lower-memory alternative, but full original Map semantics remain
unproven. See `MAP_013_REA_NATIVE_RECOVERY.md` and its compact proof.
The scanner's second-region request/reply/cache gap remains open;
Normal/Fast, Home, stored datasets, native names, profile ownership,
shared mixed acquisition and Auto Stop-before-return remain untouched.

Owner clarification, 2026-10-12: determine whether analysis is still advancing
before concluding that another time limit is sufficient. Continue the existing
assignment, branch `codex/map-action-providers-007`, PR #9. Starting lead checkpoint
`befe791046cfd7ae32ac0c99f9ed5fa633d5fab5`; this instruction commit is its descendant.
Repo: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Work solo. No new branch, worktree, campaign or other AI delegation.

## Read and preserve

Read AGENTS.md, the MAP-013 work item, MAP-013 R1 lead review, tools/rea-mcp/README.md,
the installed reverse-engineer-anything skill and MAP_012_SCANNER_QUESTIONS.md.
Retain the accepted complete 16,815-character original pseudocode and all prior
8-second/75-second negative reports. Preserve original input bytes, admitted
toolchain, source bounds, production scanner, saved data, Home, native names,
profile ownership, mixed acquisition and Auto Stop-before-return.
Normal/Fast presentation and behavior remain unchanged.

## A — add observation before another long analysis

Inspect the installed APIs and process lifecycle. Our Map013Selective.java imposes
the 10..300-second range; this is a helper policy, not a proven Ghidra ceiling.
Make longer budgets configurable for this owned investigation. Do not simply
change 75 to 300 and repeat without observations. Do not modify bundled engine
files in node_modules or weaken artifact admission.

For each function, retain identity, source bounds, options, elapsed wall time,
actual native decompiler PID/start identity, CPU-time deltas, memory observations,
available task/phase messages and start/end records. Collect approximately
30-60-second observations independently while the decompile call is blocking.
Include Java orchestration and its native decompiler child; Java activity alone
does not establish native decompilation activity. Flush receipts during execution.

Record Ghidra result flags for completion, timer expiry, explicit cancellation
and failure to start, plus error text. Distinguish per-function limits from REA
startup, MCP/client, headless and outer runner limits. Log which layer ended a
run; give enclosing deadlines adequate margin or use a monitored background
runner with periodic result polling so a short tool wait does not kill analysis.

Where supported, use source-verified diagnostic dumps and execution/stack samples
for the owned analysis processes. Determine what they actually reveal; do not
promise phase-level telemetry or a percentage if the engine does not provide it.
CPU activity, a heartbeat, growing memory and an unchanged stack are each only
signals. CPU activity may be a loop, silence may be ordinary computation, and
the same stack may cover legitimate lengthy work. Do not classify a hang from
one such observation. A heartbeat shows the observer is alive, not progress.
If detailed progress is unavailable, state OBSERVABILITY_INSUFFICIENT.

Use one known successful original function as a monitoring control. Add meaningful
checks for timeout/cancellation classification, missing observations and owned
process cleanup, without replaying the entire accepted installation campaign.
Store large dumps and databases in ignored artifacts, compact conclusions in docs.

## B — monitored long attempt and recovery

Analyze functions individually, avoiding competing heavy analysis processes while
the owner uses the PC. Begin with a 30-minute per-function diagnostic allowance
for one relevant previously timed-out function, with enough outer allowance.
This is an initial review point, not proof that longer work is useless. If the
engine exposes genuine advancement, extend the next allowance with the evidence
recorded. Do not use an arbitrary 300-second termination as a stall diagnosis.
Respect owner Stop and resource pressure; do not run blindly without observations.

Previously timed-out source ranges (end exclusive):

- 0x14013A8C1..0x14013B390
- 0x140143D67..0x140144C41
- 0x140159925..0x14015A27B
- 0x140427120..0x140428A92

Prioritize the function most relevant to the scanner causal question. Keep a
reusable owned analysis database where supported rather than deleting all import
and disassembly work after each attempt. Validate source identity and database
reuse. Completed phases may be reusable; do not claim interrupted decompilation
can resume internally unless demonstrated. Preserve full output and provenance.

If progress remains ambiguous, inspect diagnostics and instructions, control flow,
types, callees and source bounds; consider a supported alternate analysis style
or engine where justified. Record changed inputs and compare results. Recover
the feature predicates by validated value flow even if full pseudocode fails.
Neither a timeout nor a successful text dump establishes feature semantics.
This operation is decompilation, not decryption of the original protected Lua.
No key/ticket bypass or protected-service access.

## C — resolve the actual scanner question

Do not spend the whole continuation only on timeout tooling. Use accessible
current-game IL/Lua and relevant original findings to trace a known-working
region versus the failed R15/R16 second region through actual outgoing request
construction/dispatch, matching inbound reply segments and native cache insertion
and readout. Establish request/server/world/LOD/subscription attribution from
source-supported fields/context. Generic send counters and correct AOI geometry
are insufficient. Identify the first divergent guard, value or route.

Qualify two or three distinct fresh independently populated regions, fields,
response freshness, segment completeness and exact restoration before enabling
a faster route. Preserve stale/wrong-server/retirement/Stop rejection. Shared
mixed scans must reuse discovery; type-specific detail costs remain measured.
Only then compare baseline and candidate with alternating repeated individual,
mixed and bounded two-server workloads under MAP-012 accuracy gates. Do not
claim global optimality, universal recall or impossibility from finite trials.
No qualified candidate means no useful new full-sweep speed claim.

## Authority, loop and return

Background task-owned app/game startup and passive native inspection/scans and
passive server-view switching/return remain permitted with current compatibility,
exact ownership, isolated roots, backups and verified restoration. No mouse/global
keyboard, minimize, focus forcing, rearrangement, owner Chrome control or capture.
Preserve Chinese game language. No claims, plunder, sharing, marches, combat,
spending, relocation, updater or protected-service actions. Event/season deferrals
and actual approval denials remain. Stop only exact authorized owned sessions;
restore only after confirmed exit and no surviving installation process.

Loop diagnosis -> justified change -> distinguishing test -> self-review -> next
ready step without reporting each ordinary failure as completion. Keep the Q1-Q8
matrix and one continuation current. Check actual affected helpers and boundaries;
run product builds/regressions for product changes. No repeated ZIP for diagnostics.
Clean owned analysis helpers and live roots/sessions; retain original hashes,
Auto-off, journal, subscription and action-job cleanup receipts where applicable.
Commit coherent changes, push normally, verify direct remote SHA and update PR #9.
Do not merge or publish.

Return the causal scanner finding, monitored-analysis classification and evidence,
enabled improvement and accuracy-qualified measurements, or the exact remaining
edge and attempted alternatives. Use PARTIAL if necessary. Tool monitoring alone
does not justify ANSWERS_READY_FOR_LEAD_REVIEW. Independent lead acceptance stays
separate. On actual interruption leave a reusable checkpoint, not a completion claim.
