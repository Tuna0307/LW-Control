# Team workflow — strict parity phase

**Current through:** `LWB-R8-007`, 2026-09-24.

## Roles

- **Owner:** sets priority and judges the final working product.
- **ChatGPT Web:** primary reverse-engineering, implementation, validation, documentation and Git-delivery worker.
- **Project-manager/auditor role:** maintains parity classification and prevents unsupported “done” claims.
- **Daybreak/specialist:** bounded escalation only when the repository's escalation rules are satisfied.

## Mandatory workflow

1. Read `AGENTS.md`, `docs/strict-parity-recovery.md`, `docs/lwbridge-parity-matrix.md` and `BACKLOG.md`.
2. Verify the reference EXE identity before new original-artifact recovery.
3. Inspect HEAD/worktree and preserve unrelated changes.
4. Choose an original LWBridge feature/function, not a new design problem.
5. Recover the original bytes/contract with durable source locators.
6. Update the parity matrix before or with implementation.
7. Map the recovered behavior to the current Last War client without altering product semantics.
8. Compare against reference behavior/assets and run live proof when required.
9. Document the finding, tests, limits and remaining gaps.
10. Stage only the coherent checkpoint, run `git diff --check`, commit, push and verify the remote SHA.

## What not to do

- Do not optimize first and reverse-engineer later.
- Do not use LW Atlas or other tools as product specification.
- Do not retire difficult original features.
- Do not add useful-looking features absent from the reference.
- Do not treat a working current-client workaround as parity without original evidence.
- Do not reroute a prohibited operation through a different executor.

## Owner interaction

Automate technical collection first. Ask the owner only for minimal visible checks that genuinely require human observation.
