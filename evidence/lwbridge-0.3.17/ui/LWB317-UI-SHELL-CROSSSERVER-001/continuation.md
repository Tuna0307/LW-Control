# LWB317-UI-SHELL-CROSSSERVER-001 continuation

Status: Milestone A recovery complete; checkpoint validation/commit pending.

Current checkout started at dispatch commit `7c0c0b8bcc3f5c036824607004293bb29d4e2dc8` on `research/offline-controller`.
Reference EXE and original shell asset hashes were verified before edits. The seven protected WIP hashes passed before work. Pre-existing dirty/untracked paths were recorded and are not owned by this task.

Recovered authority and executable differential proof are under `recovery/`. The normalized `55c9ca1` App blob and dispatch App are equal. Demonstrated mismatches are: missing outside pointer-down close; history import/reset not profile-scoped; hardcoded stored validation/action errors and offline/scan Enter duplication; changed/unchanged action acknowledgement closes before summary; changed history writes before summary; an extra `refreshStatus` runs; summary rejection becomes a stored popover failure instead of being swallowed by the parent refresh boundary.

Exact next step after checkpoint A: correct only those demonstrated mismatches in canonical `src/LWBridge.UI-0.3.17/src/App.jsx`, retaining no Escape/autofocus behavior and no invented UI. Then run the same production-mount cases against the corrected App, including jump/history failure branches and profile switching.
