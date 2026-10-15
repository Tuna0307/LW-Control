# Owner Launch failure — stale recovery record, 2026-10-15

Owner reproduced Launch failure in the extracted e6cafd52 Map RC. The normal
profile's actual helper and host evidence show that session cea17d7c launched
and authenticated a real game, then stopped it during startup rollback roughly
two seconds after host-start. The normal runtime still contained a valid adoption
record for a different October 13 session whose captured game had ended.

Root cause: CommitAdoptionRecord's exclusive write accepted only the new session
and challenge. It refused the dead previous session, so the successful new game
could not commit its recovery record and was stopped. Earlier clean isolated
proofs lacked this persisted owner state and missed the defect.

## Correction

- Preserve the exclusive OS file transaction and current-owner nonce check.
- Permit a different old session only if its DPAPI identity is valid, profile
  and normalized executable match, and a query/synchronize-only process handle
  proves exit, PID absence or a different parsed UTC creation instant.
- Live, inaccessible, unknown, malformed, foreign-profile/install and same-session
  wrong-nonce records remain untouched. No process is killed by this classifier.
- Add specific localized recovery-record-save failure feedback in all nine app
  languages, retaining the recovered catalogs and generic unknown-error policy.

Actual production lifecycle tests cover stale-record Start/commit/Stop, exact
rollback preserving protected records, PID reuse versus equivalent timestamp
formatting, query errors and a racing file replacement. Full Release/inert and
frontend suites pass. The initial test lock exception was corrected for Windows
UnauthorizedAccessException without changing production ownership protection.
One initial frontend run hit a transient missing dependency; the unchanged
dependency tree passed the complete rerun. Raw logs remain under artifacts.

**Source checkpoint: native final-package verification pending.** Next verify
real Home Launch -> stable authenticated Connected -> Home Close using a copy
of the owner's persisted profile and old record, then the final extracted ZIP.
Preserve the normal profile's data and original game scripts. No Map optimization
or account action is part of this correction; previous RC acceptance does not
certify this new source before that proof.
