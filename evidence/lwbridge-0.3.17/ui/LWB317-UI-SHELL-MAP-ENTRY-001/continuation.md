# LWB317-UI-SHELL-MAP-ENTRY-001 continuation

Current milestone: A recovery complete from dispatch `74ceaf78dc24c08068320dfc9766cf349faa1b7e`; production correction has not started at this checkpoint.

The worktree began with pre-existing protected WIP in three old Map result JSON files, `src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`. They remain unrelated and must stay unstaged. The protected-WIP guard passed 7/7 before work.

Recovery files pin the exact reference identities, original `Tt` at byte 364377, its preload/summary context and the immutable dispatch App blob. `run-baseline.mjs` executes the exact original selector and the actual dispatch App callback/effects with controlled bindings. All five recovery cases pass as designed: three exact-original contracts and two distinguishing dispatch-App baseline failures.

Next exact step after the recovery checkpoint is pushed: correct only Map-entry ownership in `App.jsx` so a different Map selection calls the existing `refreshMapSummary()` before `startRouteTransition`, then remove the duplicate active-route entry effect. Retain profile/bootstrap, connected polling, scan-completion, Cross-server and existing generation/profile fencing owners.
