# LWB317-UI-SHELL-MAP-ENTRY-001 continuation

Current milestone: COMPLETE / ACCEPTED for assigned source/local scope by lead
takeover, 2026-10-04. Product 147e5cf is retained; C browser/check/validator files
were preserved, rerun and completed with README and dated acceptance review.
No unfinished assigned work remains. Entries below retain the worker's B-time
history; its former next step is superseded by this closeout.

The worktree began with pre-existing protected WIP in three old Map result JSON files, `src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/`. They remain unrelated and must stay unstaged. The protected-WIP guard passed 7/7 before work.

Recovery files pin the exact reference identities, original `Tt` at byte 364377, its preload/summary context and the immutable dispatch App blob. `run-baseline.mjs` executes the exact original selector and the actual dispatch App callback/effects with controlled bindings. All five recovery cases pass as designed: three exact-original contracts and two distinguishing dispatch-App baseline failures.

The production correction moves `selectRoute` below the existing `refreshMapSummary` callback, starts that refresh only for a different `map-data` route before `startRouteTransition`, and removes the old post-activation route effect. It leaves the refresh implementation, profile/bootstrap effect, connected poll, scan-completion owner and Cross-server refresh path unchanged.

Milestone B verification is task-local. `run-focused.mjs` passes 10 current-App cases covering entry order/no-await, active Map no-op, non-Map navigation, deferred success/rejection, leave-return fencing, obsolete profile reply, unavailable bridge, direct initial Map and separate connected polling. `run-affected.mjs` replays accepted Cross-server focused behavior, current App/Map ownership and Home acknowledgement checks with all historical writes redirected here, proves the accepted historical result hashes stayed byte-identical, and pins `RetainedPages` exactly to the dispatch App. Canonical `npm.cmd run check` and the protected-WIP guard also pass.

Next exact step after checkpoint B is pushed: run the bounded true-offline browser proof for Home → Map → Home → Map, active Map re-click and Cross-server open/click-away in EN/light, JA/dark and a measured narrow CSS viewport. Then run the final focused/canonical package checks, write the integrity validator/README/delivery review/current tracking updates, and deliver `AWAITING_REVIEW` without opening native/gameplay work.
