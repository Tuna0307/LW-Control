# Home — current functional status

Lead check: 2026-10-11. **PARTIAL: not fully live verified; not approved for a whole-Home merge.**

Current goal is a faithful, reliable functional replacement, with documented minor
implementation differences. Missing original licensing responses, private constants
or protected screenshots alone do not block release. Read
[owner direction](OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md) and
[acceptance catalogue](HOME_004_ACCEPTANCE_PROPOSAL.md).

## One current checkpoint

- Branch: `codex/home-complete-delivery-004`; assessed HEAD
  `7d23e79268938277c8a8e427417994be740b9e53` (documentation after product source below).
- Compiled product: `d09b8328ce1752cd6c305acae8f52acf827e6c87`.
- Candidate: `artifacts/release/home004-functional-d09b8328/LW-Control-HOME004-FUNCTIONAL-RC-d09b8328ce17.zip`.
- ZIP SHA-256: `0F871CDCADACBCF0C3685726608F7AA36D1B96744E42EFC8859801C16EA4FA43`.
- Extracted EXE SHA-256: `66AEE4C45FCA50156982827801712D341B89ACA3ED5EFEA6D318B48214CEDC39`.
- [PR #6](https://github.com/Tuna0307/LW-Control/pull/6): open, draft, unmerged.
- One worktree; main, research and the necessary active Home branch. No obsolete
  local branch/worktree identified for removal. Preserve unfinished Home work.

## Actual progress

| Feature | Evidence available | Remaining distinction |
| --- | --- | --- |
| Manual Launch / Connected / Close (F-02) | Earlier genuine packaged game sessions; exact authenticated ownership, Close and restoration. Current controlled regressions pass. | Latest entire candidate has not had a fresh genuine end-to-end campaign. |
| Automatic startup and host restart/adoption (F-03/F-05) | Earlier genuine single-game startup/adoption; current native preference/admission/identity regressions pass. | Two genuine simultaneous games remain F-07. |
| Automatic Reconnection (F-04) | Earlier genuine exit/hang recovery and pending-recovery user Stop; current route-loss, thresholds and cancellation controlled checks pass. | Responsive-game transport-only loss has not been genuinely verified with current monitor. |
| Update-and-Launch / repair (F-06) | Actual production callback and native helper boundaries pass with controlled success/failure/no-op outcomes. | Genuine repair of the replacement bridge and authenticated reconnection remain unverified. |
| Folder, preferences and profile controls (F-01/F-07/F-08) | Native picker/storage, SQLite Add/Delete/enable/note/order, A/B ownership and extracted EN/JA WebView controls tested. D-14/D-15 registry routing/acknowledgement fixes pass. | Two inert owners and four metadata slots do not prove two real games. |
| Candidate and integration (F-09) | Source marker, EXE/ZIP hashes, extracted UI identity and saved controlled EN/JA cleanup receipts checked independently. | Final functional acceptance follows the three real-operation checks below. |

Earlier genuine receipts are usable within their recorded source and operation
boundaries. Current Windows UI / SQLite / named-pipe tests are real software tests,
but their game effects are inert. A green Connected screenshot in such a fixture
is not a newly connected Last War session.

## Only remaining completion queue

1. **F-04:** responsive real game with fresh heartbeat but lost authenticated route;
   verify Reconnection OFF and ON, stable authenticated successor and user Stop.
2. **F-06:** safely isolated supported replacement-bridge repair through packaged
   Update-and-Launch; prove fresh authenticated connection and exact restoration.
3. **F-07:** establish supported distinct installations and genuinely simultaneous
   game sessions; independent selection, Start/Stop and batch/cleanup behavior.

Do not treat a missing test fixture as proof of impossibility. First investigate
available task-owned host/adapter seams and compatible isolated copies. Preserve
owner networking, installation, credentials and unrelated processes. If a genuine
client capability is absent, report the specific evidence; do not silently remove
expected concurrency or count controlled owners as live ones.

## Independent check and handoff

[Lead progress review](HOME_004_LEAD_PROGRESS_2026-10-11.md) records fresh frontend,
native, pipe, build and extracted-package validation. The local generated UI dist
was stale; rebuilding restored its correct identity, matching the retained ZIP.
No product source was changed, no new ZIP made and no live game experiment run.

Follow [the compact campaign](HOME_004_LOOP_CAMPAIGN.md) and
[copy-ready worker prompt](HOME_004_REMAINING_LIVE_PROMPT.md).
Detailed worker evidence remains in [functional report](HOME_004_FUNCTIONAL_REVIEW_REPORT.md),
[correction receipt](HOME_004_FINAL_CORRECTION_RECEIPT.md) and
[differences D-01–D-15](HOME_004_BEHAVIOR_DIFFERENCES.md).
Older candidate/status details remain in Git history and those receipts; they are
not additional active tasks. The lead independently decides merge/publication.
