# Worker prompt: Home game-connection methods

You are continuing an evidence-led comparison of LW-Control and the user-supplied LastWarPlugin. The owner means **how each tool launches, attaches to, communicates with, monitors and reconnects the game**, and whether one is measurably more reliable or efficient. Profile-card appearance and Last Used are secondary. Whole-product comparison is later.

## Repository and branch

Repository: `Tuna0307/LW-Control`
Local checkout when available: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
Research branch: `codex/lastwarplugin-connection-research`
Product baseline: `main@45e151c8`, released v1.0.

Read current AGENTS.md and `docs/research/lastwarplugin/2026-10-23/README.md`, `CONNECTION_COMPARISON.md`, `LEAD_REVIEW.md`, and `WORKER_REPORT.md`, in that order. The independent lead corrections take precedence over the archived report's recommendations. This is a new owner-authorized research task; old Map work items are historical.

## Target and tools

Target supplied by the owner: `C:\Users\chimw\Downloads\Compressed\LastWarPlugin\LastWarPlugin.exe`.
SHA-256: `3d90f6c2d12f8aecd2035406ecaaaef7c109b6fff8a8a48e12c1c0be002ac311`.
Use an owner-supplied matching copy if working elsewhere; the EXE is not in Git.

REA 6.7.0 and matching skill 42 were installed and verified with 207 tools and Ghidra 12.1.4/JDK 21. Read `.agents/skills/reverse-engineer-anything/SKILL.md`; verify the actual connected server rather than repeating installation. `tools/rea-mcp/rea.cmd` is the local CLI.

## Existing evidence and limits

Byte-exact recovered login/Launch Center HTML/JS, bootstrap text, provenance/hashes and portable checks are in this research folder. Local raw evidence, when available, is indexed in `evidence-index.json`.

The Launch Center is not the decrypted post-launch Home. The production bootstrap reads a 32-byte named-mapping key, decrypts `core.py.enc` with AES-GCM, optionally zlib-decompresses and executes it. This establishes core loading, not game attachment. Neither encrypted core nor UI was found in the inspected installation. The owner has never logged in; no valid license/authenticated payload was supplied. Ciphertext alone cannot establish decrypted Home logic.

A previous Ghidra search expanded into whole-program analysis with substantial decompiler memory use and was cancelled. Do not repeat that unchanged route. Use retained evidence, direct-file decoding and verified source ranges; explain a changed failure reason before any replacement native trial. Analysis-tool resource usage is not product-performance evidence.

## Scope

1. Trace the active LW-Control chain from Home Start through official launcher, game-side attachment, current transport, readiness, Stop and AutoReconnect. Identify actual production composition, not just available classes. Preserve exact profile/session ownership and stale-response protections.
2. Recover the corresponding competitor game-connection chain from accessible evidence. Explicitly label what is observed, recovered, inferred or still unknown. A UI comment, filename or decryptor is not proof of the core's behavior.
3. Compare launch/attach method, readiness validation, transport/polling, failure cleanup, recovery, ownership and shared-installation isolation.
4. Measure startup/ready latency, connected-idle CPU/RAM, transport latency and reconnect behavior only when equivalent authenticated observations exist. Separate auth/download/update costs and game versus helper costs. Record trials, failures and source-bound receipts.
5. Correct earlier overclaims: LastLaunchedAt has no discovered writer; double-click can bypass the renderer button's connecting gate; missing competitor backend evidence does not prove ours faster or safer; an unchanged fallback poll schedule does not automatically reduce reads.

## Authority and preservation

The owner authorized opening, running, unpacking, disassembling and analyzing the target and associated files. Work in the background without mouse/keyboard input or interference with owner windows. Use exact task-owned sessions and cleanup. No gameplay/account-changing actions, fabricated credentials, production changes, merge or release. Do not expose license/token/key/account values in Git or reports. Preserve released source and unrelated local work.

If legitimate authenticated payload access is unavailable, complete the supported connection-chain analysis and name the exact missing evidence. Do not loop through login attempts, repeated cache searches or whole-program decompilation, and do not substitute launcher UI for the unavailable game-connection implementation.

## Deliverable and return boundary

Write `CONNECTION_METHOD_REPORT.md` beside these files with stage-by-stage source/evidence links, measurements or explicit UNKNOWN cells, limitations, reproduction commands, and concrete recommendations supported by the findings. Keep raw/private captures under ignored artifacts and retain sanitized reproducible summaries in the research branch. Run the evidence verifier and checks appropriate to any touched research harness.

Return the branch/commit, report path, concise verdict, verified method differences, measured advantages, remaining blockers and cleanup outcome. The owner will relay the handback to the lead for independent acceptance. Stop at research handback; do not implement product changes.
