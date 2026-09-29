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

## Future source location

When the project lead starts the 0.3.17 UI implementation, create a clearly separate source project/path rather than overwriting the old reconstruction in place.

The exact project name will be assigned at that checkpoint.

## Why this separation exists

The old project accumulated years of phase-specific assumptions and compatibility work. Keeping it intact gives researchers an oracle while preventing accidental status inheritance into the clean 0.3.17 program.
