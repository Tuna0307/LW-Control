# LwTool comparison and LW-Control improvement programme

Research date: 2026-10-23

Research branch: `codex/lastwarplugin-connection-research`

Implementation branch: `codex/lwtool-full-improvement`

LW-Control released baseline: main@45e151c8

This folder supersedes the earlier competitor-connection conclusion that was
necessarily based on the unauthenticated LastWarPlugin.exe wrapper. The
owner's saved LwTool login later allowed the lead to recover the actual
production backend and UI and to retrieve the native Route-C DLL through the
normal authenticated endpoint.

Read in this order:

1. CONNECTION_METHOD_REPORT.md — owner-facing comparison and revised verdict.
2. evidence.json — sanitized hashes, source anchors, native RVAs/VAs, observed behavior and remaining gaps.
3. LEAD_REVIEW.md — independent acceptance, precision corrections and next measurement boundary.
4. FULL_PRODUCT_METHOD_MATRIX.md — whole-product Home/transport/Map/Automation/AFK/City/Hotkeys/Mini/Settings comparison with implement/retain/defer/reject decisions.
5. IMPROVEMENT_BENCHMARK_REPORT.md — pre-change baseline, matched candidate measurements, inert authenticated-pipe RTT/soak and live-performance limits.
6. FULL_IMPROVEMENT_LEDGER.md — durable programme state, implementation commits and completion boundary.
7. IMPROVEMENT_EVIDENCE.json — sanitized machine-readable implementation/measurement/check index.
8. FINAL_QUALIFICATION.md — final full regression, transport recovery and source-pinned packaging boundary receipt.

## Local evidence that is intentionally not in Git

The following remain under the ignored
artifacts/lwtool-connection-20261023/ research workspace:

- WORKER_HANDOFF.md;
- recovered-core.py;
- recovered-ui.html;
- the saved runtime key and authenticated session material;
- encrypted and decrypted lwtool.dll copies;
- targeted Ghidra/REA decompilation output;
- raw capture/runtime receipts.

No credentials, tokens, decrypted production payloads, DLL binaries, private
keys, memory dumps, account identifiers or user-specific paths are retained in
this Git folder.

## Scope

The initial connection comparison was specifically about:

- launch and attachment;
- command/result transport;
- readiness;
- process/session identity;
- disconnect/reconnect;
- Stop and cleanup;
- startup, latency, CPU and memory implications.

Only the active LwTool production Route-C chain is credited for normal Home.
Older fallback implementations that remain in recovered-core.py are not
treated as active merely because their code still exists.

The later full-product programme extends the comparison across every current
LW-Control tab and shared infrastructure. No game was launched and no
gameplay/account-changing operation was performed during either the connection
research or the implementation/qualification programme. Cross-product live
performance therefore remains explicitly unresolved rather than inferred.
