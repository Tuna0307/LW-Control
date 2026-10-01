# Project structure

Current path map, reconciled by the project lead on 2026-10-01. The repository
contains active production code, exact reference assets, historical research,
and generated output. A directory's age does not decide whether it is useful.

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

## Active implementation and checks

| Path | Current role |
|---|---|
| `src/LWBridge.UI-0.3.17/` | Canonical React/Vite production frontend; Home and most non-Map pages still have partial static/disabled behavior |
| `src/LWBridge.Desktop/` | Active Windows/WebView2 host, native command services, packaging and current-client integration; includes historical paths that must not be confused with current parity |
| `src/LWBridge.Map-0.3.17/` | Active versioned Map control/data plane, persistence, queries, scans and action contracts |
| `src/LWBridge.GamePipeAdapter/` | Shared adapter dependency used by the Desktop project; historical origin does not make it disposable |
| `tests/LWBridge.Map-0.3.17.Checks/` | Deterministic Map317 checks |
| `tests/LWBridge.Desktop.Checks/` | Shared Desktop checks and explicitly selected proof modes; inspect scope before running |
| `src/LWBridge.Desktop/WebUi/` | Preserved older frontend; explicit `--legacy-ui` recovery/reference path, not normal default |
| `Start LWBridge.cmd` | Existing ordinary Desktop launch/build helper; launches the current host default. Other root verification helpers are historical/special-purpose until inspected |

Normal Desktop build produces packaged `ProductionUi` from the canonical source
through the existing native bridge. Build/package identity checks reject stale
output. No manual frontend copy or proof UI override is needed for ordinary use.

Future 0.3.17 findings should use `LWB317-*` IDs.

## Evidence and history

| Path | Role / authority |
|---|---|
| `evidence/lwbridge-0.3.17/ui/frontend-package/` | Exact recovered 0.3.17 assets and byte/hash provenance; preserve unchanged |
| `evidence/lwbridge-0.3.17/ui/` | UI inventory/clone QA/reference boundary evidence; clone captures do not prove original pixels |
| `evidence/lwbridge-0.3.17/map/` | Versioned Map contracts and stored live/check evidence; read each artifact's scope |
| `docs/reviews/` | Dated findings/reviews across generations; use `LWB317-*` and source identity to locate current findings |
| `docs/work-items/` | Bounded assignments; each fresh worker needs its complete dispatch prompt |
| `docs/archive/lwbridge-0.3.1-management/` and `docs/LEGACY_0.3.1_INDEX.md` | Preserved historical management and research index |
| `evidence/lwbridge-0.3.1/`, `evidence/lwbridge-implementation/`, older top-level research docs | Historical inputs; revalidate before treating as 0.3.17 facts |
| `tools/` | Shared recovery/check/proof tooling; filenames alone do not authorize live execution |
| ignored `bin/`, `obj/`, `node_modules/`, frontend build output | Generated prerequisites/output; neither proof of source parity nor release status |

Do not delete/move historical artifacts to make the tree appear smaller. Preserve
existing links and evidence. Promote an older fact only with exact 0.3.17
revalidation; record its source/locator and limits.

Current source/status review:
`reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md`.
Next UI-only assignment: `work-items/LWB317-UI-HOME-STATES-001.md`.
