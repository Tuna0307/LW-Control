# Team workflow — PM review 16 Overview corrections

PM audited `0758006` and returned two source-confirmed defects. Successful live lifecycle paths and prior owner acceptance remain credited. [Detailed audit and acceptance criteria](reviews/2026-09-12-review-16-overview-zero-open.md). [AGENTS.md](../AGENTS.md) is mandatory.

## Ownership and stop gate

Web is the single implementer/researcher/technical verification owner. PM audits and maintains instructions; the owner chooses features and supplies only simple UI observations/screenshots. Web captures all technical evidence automatically. No separate Sol assignment. No Daybreak assignment: these are implementation defects, not exhausted binary-analysis questions or a transfer of restricted operations.

**Do not begin Player City or any Map Data work.** PM16-02 is corrected/offline-tested as `LWB-PM16-001`; complete PM16-01 next, reconcile evidence under PM16-03 and return to PM/owner. No automatic transition to cross-server, other shared controls or the historical queue. Whole Overview remains incomplete while S02/S03/S06 are unfinished; S05 runtime-name scope depends on actual visible consumers.

## Main prompt for ChatGPT Web

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control on the existing research/offline-controller branch. Read AGENTS.md and docs/reviews/2026-09-12-review-16-overview-zero-open.md, then task.md, BACKLOG.md and docs/lwbridge-feature-ledger.md. Inspect actual HEAD/worktree and preserve all work; 0758006 is the audited code baseline, not a reset target.

The PM did not approve zero-open Overview. Complete PM16-02 (same-process incarnation checks across journal/repair/close/recovery), then PM16-01 (selected-root propagation and accurate selected-process status). Follow the exact negative/positive regression and ownership criteria in the audit. Reproduce adverse cases using isolated fake process/helper seams; do not move the real installation or force real PID reuse. Preserve successful live paths, original backups and all operation-specific restrictions. Recover facts first and label rebuild policies explicitly.

Then complete PM16-03: reconcile O01/O06 findings, reproduction details, status/ledger/backlog/handoffs and exact corrected-build evidence. Run appropriate checks, commit/push each coherent checkpoint, verify remote and CI for the actual delivered revision. Continue across these assigned steps without stopping after each commit. Prepare automatic capture and a simple owner guide only if the changed normal path needs permitted live regression; the owner supplies no commands/logs/hashes.

Return the defect dispositions, proof limits, shared S02/S03/S06 gaps and exact commit to PM/owner. Do not start Player City, any Map Data category, cross-server implementation, broad original-parity work or another feature. Do not claim the whole Overview page is 100% complete.
```

## Repeatable continuation prompt

```text
Resume the saved PM review 16 checkpoint. Inspect HEAD/worktree and the latest evidence first. Continue the next unfinished PM16-02, PM16-01 or PM16-03 acceptance criterion from docs/reviews/2026-09-12-review-16-overview-zero-open.md. Preserve prior work and restrictions; no repeated owner test or unrelated research. Commit/push/verify coherent progress. When all assigned corrections and applicable verification are delivered, return to PM/owner and stop: do not begin Player City, Map Data or a new feature.
```
