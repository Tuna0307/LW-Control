# LastWarPlugin research: Home and game connection

Research branch: `codex/lastwarplugin-connection-research`
LW-Control product baseline: `main@45e151c8` (released v1.0).
Research status: **PARTIAL**; no production behavior change.

## Owner's clarified question

Home means the method used to start, connect to, monitor and reconnect the game. Compare **implementation, reliability and measured efficiency**, not just the appearance of profile cards. Whole-product comparison can follow later.

The current evidence does **not** establish whether LastWarPlugin connects to the game faster or more efficiently than LW-Control. Its readable Launch Center is an account launcher, while the implementation after authentication remains unavailable. Keep LW-Control's current connection architecture until comparable evidence supports a change.

## Reading order

1. [CONNECTION_METHOD_REPORT.md](CONNECTION_METHOD_REPORT.md): completed connection-method investigation and owner-facing verdict.
2. [connection-method-evidence.json](connection-method-evidence.json): sanitized machine-readable source anchors, native offsets, measurements and evidence limits for that report.
3. [CONNECTION_COMPARISON.md](CONNECTION_COMPARISON.md): earlier authoritative scope, connection findings and measurement plan.
4. [LEAD_REVIEW.md](LEAD_REVIEW.md): independently checked earlier findings and corrections.
5. [WORKER_REPORT.md](WORKER_REPORT.md): archived worker findings; read together with the lead corrections.
6. [NEXT_WORKER_PROMPT.md](NEXT_WORKER_PROMPT.md): self-contained continuation instructions for another AI worker.
7. [evidence-index.json](evidence-index.json): source identity, retained file hashes and local-only evidence index.

## What is retained in Git

- Full worker report and independent review, with local user-path prefixes generalized.
- Exact recovered login/Launch Center HTML and JavaScript; provenance is in `manifest.json` and `scripts-manifest.json`.
- The recovered 456-byte production bootstrap as inert text in `bootstrap.txt`.
- A sanitized unauthenticated-runtime summary and the controlled renderer-gating receipt.
- Portable verification scripts. No target is executed by `verify_evidence.py`. The renderer check executes recovered JavaScript only inside JSDOM with mock IPC and sample profiles.

The target executable, runtime libraries, live credentials, keys and account data are not included. The original EXE SHA-256 is `3d90f6c2d12f8aecd2035406ecaaaef7c109b6fff8a8a48e12c1c0be002ac311`.

## Local-only evidence

Original artifacts remain under `artifacts/lastwarplugin-home-20261023/` and `artifacts/lastwarplugin-static-20261023/`, which Git ignores. The approximately 2.9 MB REA evidence export and detailed runtime receipt remain local because the latter contains machine-specific process paths and cache inventories. Their digests are recorded in `evidence-index.json`. A different machine can reproduce the retained byte extractions from the same user-supplied EXE.

References in the archived reports to those `artifacts/` directories describe the original locations. Corresponding retained files have the same basename in this folder; the original binary bootstrap is represented by `bootstrap.txt`. Historical commands with `%USERPROFILE%` placeholders should be adapted to the worker's machine; use the verifier below for portable reproduction.

## Verification

From the repository root:

```powershell
python docs/research/lastwarplugin/2026-10-23/verify_evidence.py
python docs/research/lastwarplugin/2026-10-23/verify_evidence.py --exe 'C:\path\to\LastWarPlugin.exe'
node docs/research/lastwarplugin/2026-10-23/renderer_gate_check.cjs
```

The optional EXE argument verifies identity and exact source ranges without running it. The renderer check uses the existing UI project's JSDOM dependency. Installation/tooling updates do not prove recovery of the encrypted Home or qualify either connection method's performance.
