# Home — current functional status

Worker follow-up: 2026-10-11. **PARTIAL: F-04 and F-06 genuine live gates now pass; current-client double-launch attempts did not sustain a second game. F-07 genuinely simultaneous independent installations remain unverified. Not approved for a whole-Home merge.**

Current goal is a faithful, reliable functional replacement, with documented minor
implementation differences. Missing original licensing responses, private constants
or protected screenshots alone do not block release. Read
[owner direction](OWNER_DIRECTION_FUNCTIONAL_REPLACEMENT_2026-10-10.md) and
[acceptance catalogue](HOME_004_ACCEPTANCE_PROPOSAL.md).

## One current checkpoint

- Branch: `codex/home-complete-delivery-004`, same checkout. Final compiled worker product source `234480bcbffbd3834274505d26df743bb3eb03b7`. Earlier lead-assessed source `d09b8328ce1752cd6c305acae8f52acf827e6c87` remains historical.
- Final ZIP: `artifacts/release/home004-functional-234480bc/LW-Control-HOME004-FUNCTIONAL-RC-234480bcbffb.zip`; SHA-256 `474BCEF416640236D6DF1790084192115B49E649FB89BB04210A1D8CCD2D1D63`, 83 entries / 81 runtime files. Extracted `SOURCE-COMMIT.txt` and ProductVersion both equal compiled source; extracted EXE SHA-256 `0117BDE4B3E91130400CCB3F15FB58A20ED3B8983A865D06A1AE0C05E1A5B06A`.
- The earlier lead `d09b8328` package and first worker `1b075e7b` ZIP are preserved as historical/failed predecessor evidence. The `1b075e7b` extracted inert Home run failed from over-broad F-07 admission and **must not** be treated as final. See [live follow-up receipt](HOME_004_F04_F06_F07_LIVE_FOLLOWUP_2026-10-11.md).
- [PR #6](https://github.com/Tuna0307/LW-Control/pull/6): open, draft, unmerged.
- One worktree; main, research and the necessary active Home branch. No obsolete
  local branch/worktree identified for removal. Preserve unfinished Home work.

## Actual progress

| Feature | Evidence available | Remaining distinction |
| --- | --- | --- |
| Manual Launch / Connected / Close (F-02) | Newly repeated in actual Windows Home runs for F-04 and F-06 with authenticated game PIDs, fresh heartbeats, exact Stop and restored v24 originals. | Real game evidence is tied to the predecessor corrections; final extracted candidate has separate controlled lifecycle verification. |
| Automatic startup and host restart/adoption (F-03/F-05) | Earlier genuine single-game startup/adoption; current native preference/admission/identity regressions pass. | Two genuine simultaneous games remain F-07. |
| Automatic Reconnection (F-04) | **Genuine verified:** task-owned host dropped the *actual authenticated pipe* while the same game PID kept a fresh heartbeat. OFF left game running and Home Disconnected; ON ran disconnect recovery to a distinct authentically Connected PID/session, native status `succeeded` after stable verification. Separate Stop with ON prevented any replacement for over two minutes; exact restoration and zero residual game/host/journal checked. | No remaining F-04 live gate from this assignment. This is a current-client adaptation, not original private pipe parity. |
| Update-and-Launch / repair (F-06) | **Genuine verified:** an isolated same-client journal survived a host exit while task-owned DPAPI adoption was archived; actual Home repair initially returned `GAME_CLOSE_FAILED`, corrected missing-challenge stop-helper boundary and then returned `restarted=1`, fresh PID/session, authenticated Home Connected and exact Stop/restoration. | Long-lived external updater and foreign-account recovery are outside this gate. Negative error trace retained. |
| Folder, preferences and profile controls (F-01/F-07/F-08) | Native picker/storage, SQLite Add/Delete/enable/note/order, independent A/B inert owner routing and prior EN/JA WebView controls checked. Simultaneous Start native regression guards the shared Lua triplet. The new [genuine double-launch observation](HOME_004_F07_DOUBLE_LAUNCH_2026-10-11.md) sustained only one real game through duplicate EXE and official-launcher attempts. | **F-07 remains open:** a second independently supportable official installation with its own mutable Lua package and independently authenticated game has not been demonstrated. |
| Candidate and integration (F-09) | Native, frontend and real Windows named-pipe checks PASS after corrections. Final extracted `234480bc` EN/light and JA/dark mounted Home native/WebView proofs PASS: both inert A/B owners stopped, zero real game/Map launches, zero active requests/subscriptions, no cleanup failures, UI screenshots saved. ZIP/source/EXE/ProductionUi identity and v24 genuine cleanup all independently checked. | Whole Home is still PARTIAL solely on genuine simultaneous independent F-07 game capability. |

Earlier genuine receipts are usable within their recorded source and operation
boundaries. Current Windows UI / SQLite / named-pipe tests are real software tests,
but their game effects are inert. A green Connected screenshot in such a fixture
is not a newly connected Last War session.

## Remaining completion boundary

**F-07** is the one outstanding functionality requirement. The current helper
installs/backs up/restores one mutable `LocalLow/FunFly/.../lwScripts` package
for the Windows user, regardless of `gameRoot`. The official launcher config
points its `app_dir`, executable and uninstall paths to the *owner's one*
installation. A copied folder and two metadata rows would not establish two
supported launchers or independent Lua/restoration ownership. The current
implementation now rejects simultaneous starts before shared-package mutation;
independent owner/session functionality is still a genuine capability gap.
The owner-authorized [direct double-launch experiment](HOME_004_F07_DOUBLE_LAUNCH_2026-10-11.md)
observed a second `LastWar.exe` PID briefly, which exited by six seconds; retrying the
official launcher produced no second sustained game through 22 seconds. This
supports a current-client limitation, not universal multi-instance impossibility.
Do not silently change the intended Home profile-concurrency scope.

The owner's latest clarification **requires two simultaneous official Windows
PC games** and rejects an Android emulator or sequential profile switching as
a substitute. Read-only host and official-client feasibility follow-up is
recorded in the [F-07 process receipt](HOME_004_F07_DOUBLE_LAUNCH_2026-10-11.md):
this PC runs Windows 11 Home (no supported Windows Sandbox); no installed
alternate PC login executable or verified separate official Windows client was
found. VM client support is also unverified, with external reports of rejection.
The requirement and F-07 BLOCK remain unchanged.

An independently supported second game installation with separate mutable
scripts/launcher identity (or a verified supported multi-client adaptation) is
required to test simultaneous A/B games, saved settings, batches and cleanup.

Do not treat a missing test fixture as proof of impossibility. First investigate
available task-owned host/adapter seams and compatible isolated copies. Preserve
owner networking, installation, credentials and unrelated processes. If a genuine
client capability is absent, report the specific evidence; do not silently remove
expected concurrency or count controlled owners as live ones.

## Independent check and handoff

[Lead progress review](HOME_004_LEAD_PROGRESS_2026-10-11.md) remains an earlier
independent source assessment. The current worker's new genuine v24 F-04/F-06
evidence and F-07 implementation findings are in
[the live follow-up](HOME_004_F04_F06_F07_LIVE_FOLLOWUP_2026-10-11.md).

Follow [the compact campaign](HOME_004_LOOP_CAMPAIGN.md) and
[copy-ready worker prompt](HOME_004_REMAINING_LIVE_PROMPT.md).
Detailed worker evidence remains in [functional report](HOME_004_FUNCTIONAL_REVIEW_REPORT.md),
[correction receipt](HOME_004_FINAL_CORRECTION_RECEIPT.md) and
[differences D-01–D-15](HOME_004_BEHAVIOR_DIFFERENCES.md).
Older candidate/status details remain in Git history and those receipts; they are
not additional active tasks. The lead independently decides merge/publication.
