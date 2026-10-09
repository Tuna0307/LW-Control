# Completed release branch/worktree cleanup

Owner requested Home-first delivery and removal of completed branch/worktree clutter.
No product code or feature acceptance changed. Current product policy is committed
on main at 0f0a435c5804db84107af3980f68686248e78775; v0.3.0 release remains pinned
to its previously verified 62d2bfd3fde3027e3d500d759244f2c558d1f06d package.

## Verified retention and removal

Merged PRs #3, #4 and #5 retain these exact feature heads in main:

| Removed local/remote branch | Retained head |
| --- | --- |
| codex/home-feature-release-001 | a344873af691b52518934f1b81aedcfea0f88932 |
| codex/home-launch-delivery-002 | b42ac69c002aad1b02fc69b9505b51daa4723dc4 |
| codex/map-data-delivery-003 | a0192e236d1900637f96ed56cb9465ab7a2a272f |

Old remote claude/amazing-ptolemy-ouxnu3 at
da7fae11fe07d44c310e8a4c81f0f067c7ab5f44 has zero commits absent from main/research
and is an ancestor of research. Its remote ref was removed; no unique work lost.
All ancestry tests passed before deletion. Normal local branch deletion checked
the current research HEAD after remote refs were gone and refused; main ancestry
was rechecked, then compare-and-delete used each exact retained head. No reset,
force push, unfinished branch deletion or product history rewrite occurred.

## Linked worktree

The extra checkout C:\Users\chimw\.codex\worktrees\home-feature-release\LW-Control
was created to isolate application delivery from the large research tree. It was
clean, on main, without an active task process before retirement. Needed ignored
artifacts were moved into the research-local ignored archive:
artifacts/retired-worktrees/2026-10-09-home-feature-release.
All 1,673 files (1,235,157,530 bytes) were SHA-256 checked before/after; zero mismatches.
A local manifest is retained beside that directory. Private snapshots/captures
remain outside tracked Git and downloadable packages. Regenerable build caches
did not need preservation.

The managed archive_worktree tool returned a completed archived attachment;
git worktree list now contains only the original research checkout, and the
retired checkout path is absent. The archived Git snapshot remains recoverable
via Codex if needed. Existing merged PR attachments remain historical references.

## Final state

Local branches: main and research/offline-controller.
Remote branches: main and research/offline-controller.
One active checkout. No open PR or unfinished feature branch was deleted.
No new game/desktop action, installed-script change or owner-data mutation.
Next only after relay: Home complete delivery on one temporary main-based branch
in the existing checkout. City marking is deferred. Future lead reviews repeat
this cleanup after accepted integration/publication.
