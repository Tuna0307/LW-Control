# Project structure

This repository now contains two different generations of work. Keep them explicit.

## Current 0.3.17 program

Current authority:

- `README.md`
- `AGENTS.md`
- `task.md`
- `BACKLOG.md`
- `docs/README.md`
- `docs/strict-parity-recovery.md`
- `docs/lwbridge-project-status.md`
- `docs/lwbridge-parity-matrix.md`
- `docs/lwbridge-feature-ledger.md`
- `docs/lwbridge-ui.md`
- `docs/AI_WORK_PROTOCOL.md`
- `docs/implementation-handoff.md`

Current evidence root:

- `evidence/lwbridge-0.3.17/`

Current 0.3.17 UI source:

- `src/LWBridge.UI-0.3.17/` — separate React/Vite Phase 1 static UI reconstruction

Future 0.3.17 findings should use `LWB317-*` IDs.

## Legacy 0.3.1 implementation/research

Existing code:

- `src/LWBridge.Desktop/`
- `src/LWBridge.GamePipeAdapter/`
- `tests/LWBridge.Desktop.Checks/`

Existing evidence/reviews/tools were primarily built around 0.3.1/current-client work.

The root `Start *.cmd` launch/check helpers are also legacy 0.3.1 reconstruction tooling. They are intentionally left unchanged because historical evidence records their exact hashes/paths.

They are preserved because they contain valuable recovery knowledge. They are not automatically current 0.3.17 product code.

Do not silently modify the old reconstruction and call it the 0.3.17 implementation.

## Current source separation

The 0.3.17 UI implementation was created at `src/LWBridge.UI-0.3.17/` rather
than overwriting the old reconstruction in place. Keep that separation unless a
later project-lead decision explicitly changes the architecture.

## Why this separation exists

The old project accumulated years of phase-specific assumptions and compatibility work. Keeping it intact gives researchers an oracle while preventing accidental status inheritance into the clean 0.3.17 program.
