# Home — independent single-game acceptance, 2026-10-11

**LEAD_ACCEPTED for one active official Windows game. Simultaneous games remain
unfinished F-07; full multi-game Home is not accepted.**

Owner-approved scope: OWNER_DIRECTION_SINGLE_GAME_HOME_2026-10-11.md.
Assessed branch HEAD: `b71186c5a45ff50d382d86d48b4ba78f4619cfc9`.
Compiled candidate: `234480bcbffbd3834274505d26df743bb3eb03b7`.

## Basis of acceptance

- Independently read actual genuine F-04 fault receipts and recovery events:
  the exact authenticated route closed, OFF preserved the responsive game,
  ON reported disconnect and moved from waiting through verifying to succeeded
  with a fresh authenticated route. Stable verification lasted over 18 seconds.
- Independently read genuine F-06 dispatch history: initial GAME_CLOSE_FAILED
  preserved, corrected journal-owned Stop followed by restarted=1/error=none,
  fresh authenticated successor and actual Home Stop. Source review confirms
  omitted challenge is allowed only on Stop, while exact profile/session/PID/
  path/creation/journal/backup checks still protect restoration.
- All three genuine task finish receipts independently report zero remaining
  processes, originalHashesRestored=true and recoveryJournalExists=false.
  Prior genuine manual/startup/adoption/hang/pending-Stop evidence remains.
- Fresh default Release native suite PASS, including ownership, CRUD, repair,
  monitor, reconcile and stored-data regressions; zero game launches in this rerun.
- Fresh five-group frontend suite PASS; nine locale catalogues contain 1,383 keys.
- Fresh Desktop Release build PASS: zero compiler warnings/errors, canonical
  production UI build and identity checks PASS.
- Extracted candidate ProductionUi independently validated against current
  source/artifact hashes. ZIP SHA-256 matches
  `474BCEF416640236D6DF1790084192115B49E649FB89BB04210A1D8CCD2D1D63`.
  Source/EXE identity and saved extracted EN/light/JA/dark controlled cleanup
  receipts match `234480bc`. Those receipts explicitly use inert game effects;
  they are not extra genuine live game sessions.
- Actual isolated Windows pipe probe rerun records authentication rejection,
  malformed/idle retirement, reconnect, RPC and concurrent inert routing.

No new live fault was induced by the lead during this review. Acceptance uses
verified saved genuine observations, current source review, fresh regression
execution and current package identity; not a claim every exceptional case was
performed live. Rare/unsafe failure cases remain meaningfully controlled-tested.

## Supported and unfinished

Accepted: one real game's folder/settings, manual launch/authenticated connection/
Close, automatic startup, saved reconnect preference, exit/hang/transport-loss
recovery, user Stop/cancellation, same-build adoption, journal-backed bridge
repair, local profile persistence and packaged interface. Journal-free repair
adversity is controlled-tested; no official-game updater capability is claimed.

Not accepted: simultaneous official games, original commercial-service parity,
100% original private-runtime equality, other tabs' unverified game actions or
future game versions. The existing guard prevents two local profiles overwriting
one mutable per-user Lua package. F-07 remains a distinct capability backlog.

PR #6 may be made ready and merged for this scope. Publish the source-identified
candidate with this supported-use statement. Preserve failed attempts and needed
local artifacts; retire the merged feature branch only after merge verification.
