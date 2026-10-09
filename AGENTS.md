# LW-Control application candidate

## Current owner direction — 2026-10-09

Home is the only new feature priority until the entire in-scope Home checklist
is complete. New City marking/Map work is deferred. Next owner-relayed scope:
LWB317-HOME-COMPLETE-DELIVERY-004; read its work item and current directions from
origin/research/offline-controller before creating the feature branch.

Main merges require the project lead's independent verification of both the
original LWBridge 0.3.17 A -> A observable contract and working packaged native
behavior for the named feature. Clone-only passing tests, fixture states and a
worker READY status cannot establish parity. Unknown required behavior or known
differences keep the feature incomplete. Accepted Launch/Connected/Close alone
does not mean the whole Home tab is done.

After merging/publishing, the lead checks unique commits, open PRs, dirty and
ignored local work and active processes, then deletes merged feature branches
locally/remotely and retires task worktrees after preserving needed artifacts.
Keep main, research/offline-controller and only the currently necessary feature
branch. Prefer the existing checkout; explain and record any required worktree.
Do not lose unfinished work, force-push, or import the research archive into main.


Target behavior is LWBridge 0.3.17 post-auth, excluding its login/licensing UI.
Preserve recovered observable behavior; distinguish original evidence, current-client adaptations and unknown behavior. Do not describe an unavailable feature as completed parity.

This branch contains the application/build dependencies and a small Home feature suite. The research archive and detailed recovery authority remain on `research/offline-controller`. Read `docs/FEATURE_STATUS.md` before changing a feature. This is an incremental candidate, not complete Home/Map acceptance.

Do not launch/control a game, mutate its installation or access protected services merely to build or run checks. Live work requires a specifically assigned scope and identity, isolation, backup and exact restoration checks. Use isolated/inert providers for automated tests. Preserve unrelated work; do not force-push.
