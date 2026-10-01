# LWBridge 0.3.17 parity recovery

This repository is the active reverse-engineering and reconstruction workspace for:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Reference identity:

- File version: `0.3.17`
- Product version: `0.3.17`
- Size: `15,866,880` bytes
- SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
- Active branch: `research/offline-controller`

## Current strategy

The project has been reset back to reverse engineering, with one explicit scope exception:

**we are not recreating LWBridge's original login/account/licensing system.**

The parity target begins at the in-scope post-auth application experience.

The execution order is:

1. **Project preparation and evidence hygiene**
2. **One-for-one reproduction of the in-scope post-auth LWBridge 0.3.17 UI**
3. **Function-by-function reverse engineering of in-scope features**
4. **Current Last War compatibility mapping**
5. **Live validation and parity closure**

Necessary auth-related local code/state-contract research is allowed for an
assigned in-scope feature; the owner clarified this on 2026-10-01. The clone still
has no login/account/licensing UI. See `AGENTS.md` section 6 for access boundaries.
Use one canonical production implementation, fixing defects there rather than
adding legacy fallback behavior. Old source/evidence remains preserved; the
existing selectable `--legacy-ui` switch is pending a separate retirement task.

## Current project-lead checkpoint — 2026-10-01

A project-lead-authored UI-only campaign is recorded at:

`docs/LOOP_CAMPAIGN_8H.md`

The static UI baseline was accepted. `src/LWBridge.UI-0.3.17/` is now the
canonical frontend, packaged by `src/LWBridge.Desktop/` as the normal default.
The Map implementation and stored live proofs are substantial; the Map Goal
remains `AWAITING_REVIEW`, with explicit validation/provider gaps.

Complete one-for-one UI/UX is **not** established. Home currently renders only
`Checking game setup…` and two disabled switches; its folder/launch/close/repair
states and native integration remain open. Original post-auth visual comparison
was blocked by legitimate access. The next bounded UI-only assignment is
`docs/work-items/LWB317-UI-HOME-STATES-001.md`, prioritizing Home and preserving Map.

Read `docs/reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md` for the
audited baseline, verification limits and checkout-byte repair; use
`docs/PROJECT_STRUCTURE.md` for active versus historical paths. The pasted
handoff ending at `13b25f6` is superseded by the actual `389df37` campaign baseline.

## Start here

Read these in order:

1. `AGENTS.md`
2. `task.md`
3. `docs/README.md`
4. `docs/strict-parity-recovery.md`
5. `docs/lwbridge-project-status.md`
6. `docs/lwbridge-parity-matrix.md`
7. `docs/lwbridge-ui.md`
8. `docs/PROJECT_LEAD.md`
9. `docs/AI_WORK_PROTOCOL.md`
10. `docs/LOOP_WORKER_PROTOCOL.md`
11. `docs/LOOP_QUEUE.md`
12. `docs/LOOP_CAMPAIGN_8H.md`
13. `docs/PROJECT_STRUCTURE.md`
14. `docs/implementation-handoff.md`
15. `BACKLOG.md`

## Historical 0.3.1 research

The repository contains substantial earlier LWBridge 0.3.1 research. It is **not discarded**.

That material remains useful as:

- architecture clues,
- known command names,
- prior static/dynamic findings,
- current-client compatibility history,
- tooling and scripts,
- examples of evidence quality.

It is **not authority for 0.3.17 parity** unless a finding is revalidated against the 0.3.17 reference.

The former high-level 0.3.1 management documents were preserved under:

`docs/archive/lwbridge-0.3.1-management/`

Chronological reviews and evidence remain in their original locations so historical links stay valid.

## Operating model

The main project lead owns scope, evidence standards, master status and work assignment.

Worker AIs execute bounded work items or explicitly pre-authorized Loop campaigns and return durable evidence/checkpoints rather than independently changing project direction.

See `docs/AI_WORK_PROTOCOL.md`.
