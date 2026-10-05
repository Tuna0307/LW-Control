# Independent lead review: shell/Home and whole AFK

Date: 2026-10-06. Worker delivery: `5c0a5cb3c9e76fd573ee7facdebfd3bdd53a6758`.
Reviewed comparison baseline: `da15258d`.

**Verdict: CHANGES_REQUIRED for one demonstrated AFK profile-store ownership
defect. LR-S1/S2/S3 and the assigned whole-AFK composition gap are otherwise
accepted for their recovered-source/local proof scope.**

This reviewer changed no production, historical evidence, Git index/history or
owner processes. All fresh outputs are under this report's `shell-afk/` directory.
Browser work used separate isolated contexts and read-only HTTP against the
lead-owned Vite listener 4440; contexts/helpers were closed. No native command,
Last War control, updater or protected original service was invoked.

## Accepted shell corrections

Current `App.jsx` has the three required corrections:

- Recovery effect: `unwrapProfileEvent(event, profileId)` plus closed and dynamic
  selected-profile guards; initial recovery acknowledgement has the same owner
  boundary. Exact current effect starts line 384, UTF-8 byte 18522.
- Periodic status effect: one `inFlight` slot with `finally` release, closure and
  dynamic profile checks before all status/proxy/error acknowledgements. It uses
  `readStatusSnapshot` with a guarded acknowledgement predicate. Explicit
  `refreshStatus` intentionally calls the snapshot without the periodic lock.
  Snapshot: line 349/byte 16737; periodic effect: line 426/byte 20200.
- Preview profile loading increments a selection generation; B's abandoned
  completion cannot clear C's loading, while cached return remains synchronous.
  `App.jsx` lines 107–123 contain this lifetime.

`poll-recovery-independent.mjs` executes AST-extracted CURRENT functions and
the exact recovered `Pe` at byte 201013, length 262. All 13 independent assertions
passed: same-profile/raw/mismatched envelopes, old initial acknowledgement,
overlap suppression, current rejection visibility, `finally` unlock followed by
successful retry, error clear, stale-profile rejection, unmounted success, and
timer/listener cleanup. Results: `poll-recovery-results.json`.

The worker A shell checker was rerun read-only. Its behavioral assertions passed,
including original recurring-overlap semantics and deferred B→C loading; its
terminal recorded-object comparison fails ONLY on the historical App whole-file
SHA after the later profile-prop integration change. This disclosed historical
pin was preserved. Current hash independently recorded here:
`81DB8E04E3FA57D7B064DC02B1E64EC828552F167BE435A107497C6D82D0F26D`.
This historical-hash failure is not a new UI defect and is not the review blocker.

## Accepted whole-AFK composition proof

The old pre-fix whole-I adapter is no longer being presented as current proof.
The B producer executes exact recovered `I` (SquadPanel byte 28070, length 22554)
against CURRENT `AfkContent`, with actual source children `ye`, `pe`, `he/me`,
compact card, error component, and modal. Current editor/Drill/Garrison/Zombie
children are compiled from current source. I inspected hook-state inputs and
profile/config/runtime mappings; the sides use matched source-valid data.

Actual read-only executions:

| Check | Result |
| --- | --- |
| `whole-afk-composition.mjs --verify` | PASS, 30 whole compositions |
| `join-modal-contract.mjs --verify` | PASS, 11 contract checks |
| `capture-whole-afk-pairs.mjs --verify` | PASS, 14 fresh original/current browser pairs; zero console errors |
| Fresh `mounted-afk-independent.mjs` on 4440 | PASS, 25 real local assertions, three fresh captures, zero console errors |

The 14 pair verifier freshly renders full compositions in desktop/narrow,
light/dark. Twelve pairs are byte-identical. Two Garrison pairs differ only in the
explicit unavailable Run Now disabled state: changing only that disabled
attribute produces identical complete screenshots. It does not crop away an
unexplained difference. This is a bounded, auditable provider fence, not a
provider-positive/native execution claim.

The fresh mounted adapter is a relocated read-through of the worker interaction
suite, using current modules, a new output directory and the lead-owned HTTP
endpoint. It exercised Join native-dialog/search/select/confirm/Tab/Escape/focus
cleanup, Garrison select/confirm, actual inert config failure/Retry/Discard, and
AFK↔Equipment dirty-state retention. It did not rewrite saved B records.

## Blocking finding: AFK child stores cross profile ownership

The complete App currently keys retained child React pages by selected profile,
but Squads child stores live in the module registry and exclude that owner.
Automation received the analogous profile-scoping fix; Squads did not.

### Exact source contract

Recovered Squad `I` starts byte 28070. Its initial declarations obtain
`g=c()` (selected profile) and call:

```text
e(`task:monsterSweep`, ..., adapter, g)
e(`task:staminaPotion`, ..., adapter, g)
```

The original main export `Dt` maps to `me` (byte 195132). `me` calls
`re(profile, scope, initial, adapter)`. Original `re` (byte 193714) indexes its
registry using `JSON.stringify([profile, scope])`. `T` (byte 191297) implements
the draft store. `afk-profile-browser.mjs` executes these exact functions with
same starting Potion config for A and B:

- A starts 50; invalid local A edit is 10000.
- B independently starts 50 with a distinct store.
- Returning A restores A's 10000 draft.

This is executed original ownership evidence, not a guessed expected UI reset.

### Actual current App counterexample

`afk-profile-browser.mjs` opens the served current App with fixed
`previewState=shell-profiles`, EN/light, Squads route. No fixture state changes.
It opens the Potion settings and uses the same native input setter/InputEvent
method as the submitted E Automation profile test to invoke an inert disabled
consumer callback. It never enables the control or claims physical offline
editing is available.

| Observed state | Current App | Executed original store |
| --- | --- | --- |
| Profile A initial Potion minimum | 50 | 50 |
| A after local invalid edit | 10000, dirty | 10000, dirty |
| Switch to actual selected Local 2 / profile B | **10000, dirty** | **50, clean independent owner** |

Current B also displays A's save-error/Retry/Discard UI after the old component's
cleanup flush. Fresh captures `profile-a-invalid-potion.png` and
`profile-b-inherited-potion.png` were saved; the latter was visually inspected.
It visibly shows selected Local 2, value 10000 and inherited save-error controls.
Browser issues: zero. Full output: `afk-profile-browser-results.json`.

The disabled input qualifier limits this to local consumer ownership proof,
exactly as the final E Automation profile test. It does not invalidate the
demonstrated recovered-profile contract or turn this into a native-provider gap.
The actual App passes through a profile replacement and keyed remount; the
leaking state is not produced by a fabricated standalone reset.

### Current locators and cause

- `App.jsx` `RetainedPages` fragment is keyed by selected profile; the march
  entry in `pagePropsByRoute` passes its active subtab but no `profileId`.
- `SquadsPage.jsx:12` accepts no profile owner and its AFK child receives none.
- `SquadsPage.jsx:336–339` keys its stores as `afk:${previewState}`,
  `afk-potion:${previewState}`, `afk-garrison:${previewState}` and
  `afk-zombie:${previewState}`.
- `previewConfigHook.jsx:6–16` uses one module-level `previewStores` map and
  returns the cached store when those keys repeat. A React remount therefore
  reuses the old owner's draft/error/confirmed state.

### Focused correction

Pass the selected profile identity through the Squads route/child composition
and include it in all four AFK config store identities, preserving same-profile
hide/return and cached profile restoration. The Potion counterexample directly
proves the defect; the other three keys share the same ownership omission and
should be audited with matched source owners instead of assumed clean.

Verify A-edit → B-clean → A-retained → B-still-clean, config errors/acknowledgement
isolation, concurrent/deferred saves and hidden/returned lifetime. Preserve the
30 composition cases, 14 paired captures, accepted Equipment lifecycle and the
corrected shell status/loading contracts. No native provider implementation is
needed to correct this UI state ownership.

## Boundaries

No additional Home formatter or whole-AFK presentation mismatch was found in
this scope. Source/local renderer and inert acknowledgement proof do not
establish loaded native assets, native persistence/gameplay execution or
protected original-runtime pixels. Final project acceptance belongs to the
project lead; this report recommends returning one focused Squads ownership
correction rather than reopening accepted visual campaigns.
