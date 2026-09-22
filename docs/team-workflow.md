# Team workflow — current

**Current through:** `LWB-R7-145`, 2026-09-22.

## Roles

- **Owner:** sets priorities and supplies minimal UI observations/screenshots when genuinely needed.
- **ChatGPT Web:** primary research, implementation, testing, evidence capture, documentation, cleanup, commit/push worker.
- **Project-manager/auditor role:** reviews evidence/status and keeps the repository coherent; it must not promote unproven live outcomes.
- **Daybreak:** bounded specialist escalation only after the requirements in `AGENTS.md` and `docs/daybreak-escalations.md` are met.

## Current workflow

1. Read `AGENTS.md` and current canonical docs before work.
2. Inspect HEAD/worktree and preserve unrelated changes.
3. Work only on a current `BACKLOG.md` item or a newly reproduced regression.
4. Recover/verify behavior before changing production semantics.
5. Run the checks appropriate to the changed behavior.
6. Update current docs plus durable evidence without rewriting historical provenance.
7. Stage only the coherent checkpoint, `git diff --check`, commit, push, and verify remote SHA.

## Current owner interaction

No routine Home/Map retest is required now. Owner-dependent work is limited to the external conditions listed in `docs/live-test-handoff.md`: Ghost/Supplies population, simultaneous multi-account availability, and explicitly authorized state-changing actions.

Do not revive old “next category” or Resource/Player City owner-test instructions from historical files.
