# Home 004 independent lead review — 2026-10-09

Disposition: **PARTIAL / CHANGES_REQUIRED. No merge/publication.**
Reviewed worker source 5296ad58430e5aadd4164a19f9f44bdf7391c521 and PR #6.
PR is converted to draft while required corrections and Home proof remain open.
Main remains 0f0a435c5804db84107af3980f68686248e78775; released product unchanged.

## LEADHOME004-01 — repair decoder change introduces an actual failure (P1)

OverviewLifecycleAdoption.cs:102 serializes the real CreateUpdateRestartResult
object using JsonSerializer.Serialize(repairResult) without JsonOptions.Default.
The nested OverviewStartupError record has PascalCase ProfileId/Error/Message.
The new DecodeOutdatedRepairFailure at line 199 reads lowercase error/message.
An actual internal failed repair therefore throws KeyNotFoundException instead
of returning its error. The former PascalCase reader matched that internal
serialization; the worker's claim that it was necessarily broken is rejected.
The external native response is camelCase, but it is a different boundary.

The new worker check in GameRootSelectChecks.cs:60 uses JsonOptions.Default,
which manufactures camelCase and bypasses the actual caller. It passes while
the actual production combination fails. This is not adequate integration proof.

An isolated lead probe executes the actual private CreateUpdateRestartResult
producer (via reflection) and actual DecodeOutdatedRepairFailure consumer with
the exact caller serialization. GAME_CLOSE_TIMEOUT produces KeyNotFoundException;
the same object passed through the worker's Web options succeeds. This reproduces
the defect without any game, protected service or installed-script action.
Compact receipt: docs/proof/HOME-004-LEAD-REPAIR-INVERSE.json. Replay source/log
remain ignored in artifacts/lead-home004/probe and repair-serialization-inverse.log.
Fix the real producer/caller/consumer contract, then add an actual full outdated-
build AdoptOrRepair -> UpdateAndRestart -> failed native helper test, not merely
a direct decoder test with independently chosen serialization. Preserve the
before receipt and distinguish external wire casing from internal records.

## LEADHOME004-02 — matrix is inherited inventory, not current reconciliation (P2)

tools/home_004_matrix.py imports the old 010 home-obligations.json and copies
most status/readiness/dependencies verbatim. The resulting matrix leaves H-33,
H-37, H-39, H-42, H-43 and H-46 as needing static/locale recovery already partly
or substantially present in later c-handlers recovery. In particular the current
GameRecoveryStatusCommandService itself cites the decoded 0x154905 behavior,
while H-43 still labels PROFILE_ID_REQUIRED/PROFILE_RUNTIME_UNAVAILABLE clone-
defined and says the original errors are undecoded. This is an obsolete rationale.
Later source is not automatically accepted: compare original facts and actual
main behavior, correct genuine remaining differences, and record precise gaps.

Use existing research c-handlers/handlers-recovery.facts.json and
handlers-locale.json/handlers_locale.py, plus current native sources. Distinguish
ready local implementation, missing actual proof and genuinely protected input.
Do not overwrite historical rows. Make a derivative with all 47 IDs and actual
current caller/code/test references; stale generator output cannot close task A.
A lack of clone multi-owner capability is implementation work, not by itself an
unavailable original-service dependency. Licensing/account UI remains excluded.

## LEADHOME004-03 — required actual Home scenarios not performed (P2)

The new native receipt proves preference switches, language/theme, exit/reopen
with a disabled isolated profile and zero launches. It does not prove the changed
Start UI admission path against a real launch, cancellation, automatic startup,
reconnect/recovery enabled/disabled, repair or host restart/adoption. The earlier
002 accepted Launch/Close receipt is preserved, but predates these changes.
No exact denial, missing live input or failed attempt is recorded preventing all
these assigned bounded local/live checks. They remain work to perform, not a
collective external blocker. Follow existing authorized Home verification and
identity/isolation/backup/restoration rules; preserve any actual failed attempt.

## Independent executed checks and preserved work

- Canonical frontend five groups PASS; nine catalogs at 1,383 keys.
- Native delivery suite PASS: root, optional Close, ordered profile reconciliation,
  stored Map regression. No live launches. It does not detect LEADHOME004-01.
- Dedicated actual-producer repair inverse reproduces the defect (exit 1).
- Default ignored dist was stale (051b... versus current cc7c...). Fresh frontend
  build and integrity verification PASS. Worker final-publish/ProductionUi already
  matches current source cc7ce094a249a68e32fd4e1ba1efaf69bd39a8a9c4e261db2b0e4aa2d2a88af6
  and artifact 7d854d2b8031a73ef625c70bc9b052bb0548ddb39a1000fd46c8eafe8ad08c0f.
  Stale local cache alone is not evidence that the submitted package is wrong.
- Worker RC hash independently matches 62246D08879180FFA68A0CEAF0C61671476BDBFCF6B9FDA96F7CF440F6C2C811.
- Reference executable hash matches the required 0.3.17 identity; diff check PASS.
- No game/launcher/clone process was present at passive inspection. No desktop
  capture/input, game launch or installed-script change was made by this review.
- Two unrelated untracked evidence directories present at start remain untouched:
  UI-MAP-MANUAL-VISUAL-001 and UI-OFFLINE-VISUAL-001. No new worktree/branch created.

Continue only Home on this same feature branch after owner relay. Keep draft PR,
unfinished branch and needed local artifacts; cleanup applies after accepted
integration, not to incomplete work. Do not promote whole Home or merge a known
required defect. See HOME_004_R1_CONTINUATION.md.
