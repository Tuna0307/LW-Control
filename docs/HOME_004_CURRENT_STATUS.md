# Home — current status

2026-10-11: **LEAD_ACCEPTED for one active official Windows Last War game.**
The owner approved single-game Home first and retained simultaneous two-game
support as unfinished F-07. This is a functional replacement acceptance, not
100% equality with protected original internals.

Read [owner scope](OWNER_DIRECTION_SINGLE_GAME_HOME_2026-10-11.md),
[lead acceptance](HOME_004_SINGLE_GAME_LEAD_ACCEPTANCE_2026-10-11.md) and
[documented differences](HOME_004_BEHAVIOR_DIFFERENCES.md).

## Release candidate

- Product source: `234480bcbffbd3834274505d26df743bb3eb03b7`.
- ZIP: `artifacts/release/home004-functional-234480bc/LW-Control-HOME004-FUNCTIONAL-RC-234480bcbffb.zip`.
- ZIP SHA-256: `474BCEF416640236D6DF1790084192115B49E649FB89BB04210A1D8CCD2D1D63`.
- Extracted EXE SHA-256: `0117BDE4B3E91130400CCB3F15FB58A20ED3B8983A865D06A1AE0C05E1A5B06A`.
- PR #6 is merged; release `home-single-game-v0.4.0` is published. Current checkout
  is main and the merged feature branch is retired. See
  [release receipt](HOME_004_RELEASE_RECEIPT_2026-10-11.md) for exact identity.

## Verification

| Function | Acceptance basis |
| --- | --- |
| Manual Launch / Connected / Close | Genuine native sessions, authenticated readiness, exact Close/restoration; current regression suite passes. |
| Automatic startup / host restart-adoption | Earlier genuine single-game evidence plus current saved-preference, duplicate, identity and admission checks. |
| Reconnection | Genuine crash/hang and responsive transport-only loss; OFF preserves game, ON authenticates stable successor, user Stop prevents late replacement. |
| Update-and-Launch repair | Genuine journal-backed replacement-bridge repair, initial failure preserved then corrected, fresh authenticated successor and restoration. No-journal/failed/cancel paths have controlled evidence; official game updater is not certified. |
| Folder/settings/profile controls | Actual native storage and packaged WebView/SQLite tests, durable Add/Delete/enable/note/order, owner separation and concurrent-reply checks. Multiple saved profiles are not simultaneous games. |
| Package | Current source/EXE/ZIP identity and extracted EN/JA controlled UI checked; fresh Release/native/frontend/Windows pipe suites pass. |

The lead inspected saved genuine receipts and reran current controlled/native
checks; no new live game experiment was run during this acceptance. Not every
exceptional case was tested live. See the precise boundaries in the acceptance.

## Unfinished F-07

Two simultaneous official PC games are **not supported by the verified setup**.
One mutable per-Windows-user Lua package is shared; production rejects concurrent
starts before unsafe package mutation. Direct EXE/launcher attempts did not keep
a second game alive. This is an observed limitation, not universal impossibility.

F-07 stays in the backlog: find and verify legitimate simultaneous client support
with independent package/transport ownership and real A/B lifecycle. Do not claim
inert owners, Android emulators or sequential switching satisfy it. No new F-07
or Map campaign is assigned by this release.

Detailed genuine evidence: HOME_004_F04_F06_F07_LIVE_FOLLOWUP_2026-10-11.md and
HOME_004_F07_DOUBLE_LAUNCH_2026-10-11.md. Previous failed receipts and packages
remain historical; older PARTIAL/draft status is superseded only for this scoped
release. Revalidate compatibility when the game changes.
