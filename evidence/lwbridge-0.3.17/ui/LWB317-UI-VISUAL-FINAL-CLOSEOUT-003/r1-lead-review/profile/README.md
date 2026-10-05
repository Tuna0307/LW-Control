# Independent R1 profile/configuration review

Date: 2026-10-06

Reviewed delivery: `0a6e0c35065f44ce5c8151a3a0e37ee9989e4638` against lead checkpoint
`8db68a6fd4bf46fa5ff6bcfd467c2eccb0d11591`.

Recommendation: **CHANGES_REQUIRED — one focused Equipment save-contract defect**.

The AFK profile ownership correction is accepted for recovered-source/local scope.
Actual original `T`, `re`, `me` byte execution proves distinct profile owners;
current App/Squads propagates the selected owner into all four source-backed AFK
store scopes. A fresh isolated served-App A-edit → B-clean → A-retained → B-clean
replay passes. Independent deferred draft-engine success/rejection/Retry/Discard
cases pass for four AFK scopes and Equipment. The fresh copied Equipment browser
regression passes all 17 assertions (save, Retry, Discard, deferred busy and
same-profile hide/return). The copies change only repository/output path binding,
not proof assertions, and write into this review directory. Worker historical
files were not recorded over.

Equipment ownership is independently source-backed: exact `fd` at UTF-8 byte
176937 calls `e('equipment', ..., l)` with selected profile `l=c()`. Selection,
rename UI, action-busy state and drag state remain component-local in that source.
All current Equipment fixtures initialize `confirmedPresets` as a clone of
`presets`; the new draft-store initializer therefore does not lose a distinct
initial confirmed value in any supported fixture.

## Blocking counterexample: unsolicited Equipment confirmation

Current `EquipmentContent.flushPreviewConfig` calls
`equipmentConfig.store.flush()` with the default `true`. Exact original `fd.P`
at UTF-8 byte **180274** explicitly calls `u.state.flush(!1)`.

The difference is meaningful when a profile's pending save survives a keyed view
remount. Exact current App uses a `Fragment key={selectedProfileId}`; the retained
config store survives while transient Equipment action-busy state is re-created.
The recovered original has the same separation of persistent config state and
component-local action-busy state.

`equipment-owner-adversarial.mjs` mounts actual served `SquadsPage` and
`usePreviewConfigAdapter` in an isolated browser context on lead-owned port 4441.
It installs a local controlled deferred write adapter on the actual store,
executes the actual rendered Rename/Save callbacks, remounts A → B → A with profile
keys, invokes the rendered React loadout drag/drop handlers to make a second
unsaved edit, then releases the first save acknowledgement. No second Save is
invoked.

| Settled behavior | Exact original T + fd.P | Actual current Equipment |
| --- | --- | --- |
| First rename acknowledged | yes | yes |
| Later move remains dirty | **true** | **false** |
| Confirmed config | first rename only | second, moved draft |
| B inherits A rename | no | no |

The original `flush(false)` stops after the first acknowledgement when no explicit
second flush queued a write. The current default `flush(true)` automatically
continues writing the changed draft, confirming an Equipment move without Save.
This is a source/local configuration acknowledgement mismatch, not a native
provider claim. Both outcomes execute the exact recovered/current store engine;
the current page path executes actual rendered component handlers.

Correction: retain the recovered one-write contract in Equipment's explicit save
path (pass `false` to `flush`), then add a distinguishing owner-return pending-save
case. Also preserve the intentional explicit second-save queued-write behavior.
Do not alter the global draft-engine default or other AFK/Automation save contracts.

## Evidence and limits

- `profile-ownership-results.json`: fresh current actual App ownership and exact
  original owner registry; zero browser issues.
- `equipment-regression-results.json`: fresh 17 mounted assertions; zero issues.
- `equipment-owner-adversarial-results.json`: actual page snapshots plus exact
  original function byte/source and settled comparison; zero issues.
- Reference EXE and original Squad/main asset hashes are rechecked by the copied
  ownership proof against the exact 0.3.17 identities.
- Controlled writes are inert local adapters. Browser drag proof executes rendered
  React HTML5 handlers; physical connector drag is not claimed.
- No Last War, native provider, gameplay, protected original service/runtime or OS
  updater action was invoked. No production code, master status, Git index/history
  or historical worker evidence was changed by this reviewer.

The single finding is blocking for R1 source/local acceptance. All other reviewed
R1 ownership claims are supported within their recorded proof boundaries.
