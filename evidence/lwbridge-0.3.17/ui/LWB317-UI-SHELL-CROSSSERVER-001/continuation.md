# LWB317-UI-SHELL-CROSSSERVER-001 continuation

Status: AWAITING_REVIEW. Milestones A/B/C are complete. The worker's final response reports the closeout commit and directly verified remote SHA; project-lead review is the only continuation after delivery.

Current checkout started at dispatch commit `7c0c0b8bcc3f5c036824607004293bb29d4e2dc8` on `research/offline-controller`.
Reference EXE and original shell asset hashes were verified before edits. The seven protected WIP hashes passed before work. Pre-existing dirty/untracked paths were recorded and are not owned by this task.

Checkpoint A is `a74aba9b3a5abaf7a12979c7938e8bcc2ca465c7` and was pushed with the direct remote ref verified. Recovered authority and executable differential proof are under `recovery/`. The normalized `55c9ca1` App blob and dispatch App were equal before correction.

Canonical `src/LWBridge.UI-0.3.17/src/App.jsx` now restores the demonstrated 0.3.17 contracts: outside pointer-down close; profile-keyed history reset/import; event-time localized validation/action errors; offline/scan Enter fencing; summary-before-history/close acknowledgement; summary failure swallowing at the parent jump boundary; and removal of the action-path `refreshStatus`. It deliberately adds no Escape/autofocus/focus-trap behavior.

`milestone-b/run-focused.mjs` mounts the actual corrected App body with inert bridge/map responses. It passes 12 cases covering legacy normalization/lists, trigger/outside/Escape, locale validation, offline, active scan, profile reset/import, changed/unchanged deferred summary, summary failure, jump failure, history failure, and busy trigger behavior. `npm.cmd run check` also passed from the UI package.

Milestone C passes the three affected ownership regressions, canonical check/build/production-package checks, protected-WIP count 7 and diff checks. Browser QA is recorded separately from mounted action proof: true offline Preview EN/light and JA/dark, real keyboard Enter/Escape and route click-away/return, a directly measured 800x543 CSS narrow state, three inspected JPEGs and zero warning/error consoles. The owned port 4327 server and both owned QA windows were closed.

Exact continuation after the worker's final closeout push: project lead opens `docs/reviews/2026-10-04-LWB317-UI-SHELL-CROSSSERVER-001.md` and the evidence README, runs `milestone-c/validate-current.mjs`, `milestone-c/run-current-checks.mjs` and the protected-WIP guard at the reported final remote SHA, reviews the App diff against `recovery/branch-matrix.md`, and accepts or returns focused findings. Do not restart accepted SHELL-RETENTION-001 or begin the later broad shell/final inventory/native work from this unit.
