# Checkpoint A — LR-GHOST-EXPIRY-005 correction

State: **COMPLETE**.

Starting assignment HEAD / origin: `e18d8f3fdbad0a23804c783e5497ad450d74d49c`, clean.

## Production correction

`CurrentClientMap317ActionProvider.PrepareGhostPlunderRows` now matches the
already-recovered host expiry predicate without changing any reader:

- zero, negative and missing `taskExpireTime` are allowed;
- a positive expiry is rejected only when `taskExpireTime <= plunderAt`;
- the existing protection floor remains
  `plunderAt >= completionTime + protectTime * 1000`;
- UUID, ownerServer, count aliases, frontend-owned delayed `plunderAt`, and raw
  row values remain unchanged.

The exact code change is limited to removing `taskExpireTime <= 0` from the
source-projection rejection set and making the expiry timing comparison
conditional on `taskExpireTime > 0`.

`PrepareGhostPlunderTasksAsync` remains `GAME_PROVIDER_UNAVAILABLE`. Its
message now separates two distinct facts: protected preparer row
transformation/rejection semantics remain incomplete, while downstream Ghost
execution terminal-result correlation is separately unresolved.

## Fresh packaged production proof

Historical failing evidence remains untouched at
`../LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004/lead-review-2026-10-07/expiry-results.json`.

Fresh corrected evidence is `a-expiry-results-corrected.json`, produced by
`a-check-expiry-corrected.ps1` against the Release packaged
`MapActionControlPlane.ValidateDispatchScheduleRows` and
`CurrentClientMap317ActionProvider.PrepareGhostPlunderRows`.

| Case | Packaged host | Corrected helper |
|---|---|---|
| positive after plunder | ACCEPTED | ACCEPTED, raw row preserved |
| zero | ACCEPTED | ACCEPTED, raw row preserved |
| negative | ACCEPTED | ACCEPTED, raw row preserved |
| missing | ACCEPTED | ACCEPTED, raw row preserved |
| positive equal plunder | REJECTED | REJECTED |
| positive before plunder | REJECTED | REJECTED |

This directly closes the lead's LR-GHOST-EXPIRY-005 mismatch.

## Existing reader contract was not broadened

The Desktop helper still uses the pre-existing `ReadInteger -> ReadNullableInteger`
path. On this runtime, `JsonElement.TryGetInt64` throws
`InvalidOperationException` for a string-valued JSON element before the later
string-parse expression can be reached. The fresh replay therefore records:

- numeric-string positive expiry: host ACCEPTED, helper
  `InvalidOperationException`;
- malformed-string expiry: host ACCEPTED, helper
  `InvalidOperationException`.

The assignment explicitly forbids widening numeric parsing, so this pre-existing
reader behavior is preserved and asserted in
`MapProviderSemanticsChecks.GhostPreparationPreservesExpiryReaderContract`.

## Retained guards

The production module-initializer checks cover:

- raw row preservation;
- frontend random-delay preservation;
- zero/negative/missing/positive expiry boundary;
- numeric-string/malformed reader behavior;
- stale `stolenCount` and `maxStealCount` rejection;
- pre-protection `plunderAt` rejection;
- positive expiry at/before plunder rejection;
- non-decimal UUID and invalid ownerServer rejection.

Release Desktop.Checks build passed with 0 warnings / 0 errors and re-ran
`LWB317_PRODUCTION_UI_BUILD_OK` and `LWB317_PRODUCTION_UI_PACKAGE_OK`.

## Tooling negative

The first new test fixture used a normal interpolated C# string containing a
literal newline and failed compilation before test execution. It was corrected
to a one-line JSON fragment; no product/runtime state was changed.
