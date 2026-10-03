# LWB317-UI-SHELL-CROSSSERVER-001 continuation

Status: Milestone B implementation and focused proof complete; checkpoint validation/commit pending.

Current checkout started at dispatch commit `7c0c0b8bcc3f5c036824607004293bb29d4e2dc8` on `research/offline-controller`.
Reference EXE and original shell asset hashes were verified before edits. The seven protected WIP hashes passed before work. Pre-existing dirty/untracked paths were recorded and are not owned by this task.

Checkpoint A is `a74aba9b3a5abaf7a12979c7938e8bcc2ca465c7` and was pushed with the direct remote ref verified. Recovered authority and executable differential proof are under `recovery/`. The normalized `55c9ca1` App blob and dispatch App were equal before correction.

Canonical `src/LWBridge.UI-0.3.17/src/App.jsx` now restores the demonstrated 0.3.17 contracts: outside pointer-down close; profile-keyed history reset/import; event-time localized validation/action errors; offline/scan Enter fencing; summary-before-history/close acknowledgement; summary failure swallowing at the parent jump boundary; and removal of the action-path `refreshStatus`. It deliberately adds no Escape/autofocus/focus-trap behavior.

`milestone-b/run-focused.mjs` mounts the actual corrected App body with inert bridge/map responses. It passes 12 cases covering legacy normalization/lists, trigger/outside/Escape, locale validation, offline, active scan, profile reset/import, changed/unchanged deferred summary, summary failure, jump failure, history failure, and busy trigger behavior. `npm.cmd run check` also passed from the UI package.

Exact next step after checkpoint B: run the affected accepted shell-retention and current Map/Home regressions required by the work item, canonical build/production-package checks, protected-WIP/diff guards, then perform bounded browser QA in offline preview plus controlled inert positive/deferred/error bindings. Record browser observations separately from synthetic proof, capture/inspect screenshots and console, write the review/evidence README, update tracking docs, commit/push, and verify the direct remote SHA.
