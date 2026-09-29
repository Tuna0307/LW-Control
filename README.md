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

The project has been reset back to reverse engineering.

The new execution order is:

1. **Project preparation and evidence hygiene**
2. **One-for-one UI reproduction of LWBridge 0.3.17**
3. **Function-by-function reverse engineering**
4. **Current Last War compatibility mapping**
5. **Live validation and parity closure**

Do not skip directly to function implementation while the UI inventory/reproduction phase is incomplete unless the project lead assigns a narrow dependency investigation.

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
10. `docs/PROJECT_STRUCTURE.md`
11. `docs/implementation-handoff.md`
12. `BACKLOG.md`

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

The main project lead owns scope, evidence standards, master status and work assignment. Worker AIs should take one bounded work item at a time and return a durable report/checkpoint rather than independently changing project direction.

See `docs/AI_WORK_PROTOCOL.md`.
