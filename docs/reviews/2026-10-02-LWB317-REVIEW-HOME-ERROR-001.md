# Independent Home error translation review — LWB317-REVIEW-HOME-ERROR-001

Date: 2026-10-02. Recommendation: **ACCEPT for focused source/local scope**.
Reviewed current checkpoint: `554a28cf829033fae121261fb1698492ea39f4b9`.
This review changes no product code and makes no broader Home/native acceptance.

## Finding

No source/local mismatch was found in the retained `translatedError` helper or
Home recovery-error composition. The recovered frontend asset is still SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`;
the reference executable remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Direct UTF-8 byte recovery locates `Ir` at 328453, `Lr` at 328684 and Home `qr`
at 336694. Exact expressions are preserved in `independent-cases.json`.

The production helper matches the recovered Home behavior: it takes a string
object `code` first, extracts uppercase code tokens from `Error.message` or
`String(value)`, deduplicates then reverses the resulting code list, and probes
`error`, `auth.error`, then `update.error` for each code. A missing translation
falls back through localized `common.actionFailed`. A plain object `message`
property has no special treatment in the original; a custom `toString` does.
Object `code` values bypass the uppercase-token regex, which the independent cases
also exercise with a lowercase translated code.

Thirteen independently selected cases execute the actual extracted original and
production functions. They include cross-namespace reverse priority, duplicate
priority, `Error` with both `.code` and message tokens, plain object code/message,
custom stringification, lower-case object code versus lower-case string, namespace
fallback, and generic fallback. Every production result equals the original.

The original `Lr` accepts an optional third fallback-key argument while the local
`translatedError` helper exposes only the default behavior. Current recovered Home
`qr` calls `Lr` without that third argument for root, action, and recovery errors,
so this API-shape difference creates no mismatch in the assigned Home scope.

For locale evidence, the original English asset places `common.actionFailed` at
UTF-8 byte 35900 (`The action could not be completed.`) and
`recovery.failedDetail` at byte 53541 (`Reason: {error}`). The Japanese asset
places the corresponding values at bytes 32534 and 44426. Actual original `qr`
and current Home rendering both compose an unknown Japanese recovery error as
`原因：操作を完了できませんでした。`.

## Existing checker assessment and verification

The existing HOME-ERROR-001 checker is substantive: it parses the exact source and
production functions, reconstructs the original shared-plus-locale catalogs, and
executes both sides. Its saved record was verified without regeneration: 60
synthetic cases, 4,230 catalog checks across nine locales, and 27 Home error-render
comparisons all pass. `validate-evidence.mjs` also passes. The independent cases
above add distinguishing combinations rather than repeating that report blindly.

Current neighboring regressions remain green: HOME-ERROR-002 passes 9 callback
scenarios, 4 source picker cases and 72 render comparisons; HOME-BUSY-001 passes
13,824 render comparisons, 384 predicate cases, 27 preference checks and 63
preview fixtures; UI-SWITCH-LOCALE-001 passes 360 render comparisons across nine
languages. Their evidence validators also pass.

Bounded browser QA used the pre-existing local preview server on port 4319/PID
62280. English `home-error-unknown` displayed `The action could not be completed.`;
Japanese `home-recovery-error-unknown` displayed
`原因：操作を完了できませんでした。`. Launch/close controls and both Home switches
remained disabled, and the attached browser console had no errors. One Japanese
recovery capture is saved with SHA-256
`E74CAAE8D6BDB8534EB650CFD14A8E98F8531269987BF2B1DDEA918530EDA357`.
The reviewer-owned tab was closed and the pre-existing server was left running.

Canonical `check`, production `build`, and `check:production-build` pass through
`npm.cmd`. The resulting build/package fingerprints are
`53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e` and
`96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782`.
The first attempt through bare `npm` was blocked by the host PowerShell execution
policy at `npm.ps1` before project scripts ran; rerunning the same scripts through
`npm.cmd` required no policy change. `git diff --check` passes; its only output is
Git's line-ending warnings for this assignment file and the explicitly preserved
pre-existing AFK WIP.

## Limits

This recommendation covers only recovered-source/local Home error translation and
the recovery-detail composition. App rejection paths can still reduce thrown
objects to message strings; native producer/error-object contracts are outside
this review. HOME-ERROR-002 channels, HOME-BUSY-001 presentation, original
post-auth runtime pixels, native persistence and gameplay/lifecycle behavior are
not accepted by this review. Direct original post-auth visual parity remains
unproved under the documented access boundary.

Reproducible review evidence is under
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-001/`.
