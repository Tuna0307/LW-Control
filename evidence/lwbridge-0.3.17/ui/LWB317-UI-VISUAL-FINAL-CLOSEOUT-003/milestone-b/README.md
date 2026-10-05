# Milestone B — whole AFK / Squads original-current composition

Date: 2026-10-05

## Result

The stale pre-fix whole-AFK harness from `LWB317-UI-VISUAL-REMAINING-002` is no longer used as current proof. `whole-afk-composition.mjs` executes recovered `SquadPanel-HC3-DJei.js` function `I` at UTF-8 byte 28,070, length 22,554, SHA-256 `0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2`, and executes the live canonical `AfkContent` from `src/LWBridge.UI-0.3.17/src/SquadsPage.jsx` under matched source-valid inputs.

The original side uses the recovered compact card, config-error component, native dialog `In`, join settings `ye`, Garrison `pe`, Zombie `he/me`, and whole `I` inline profile/editor/Drill/runtime composition. The current side uses the live editor, Drill, Garrison, Zombie, Join and config-error components. Native/gameplay calls remain inert and the current Run Now fence remains intentionally unavailable.

The producer covers 30 matched whole-composition cases: selected existing/new/disabled profiles; discovered squads 1–4, `[3,4]`, and empty; translated and error runtime rows; Master stop busy; join-member loading/failure/positive; Potion open/invalid/save error; Drill waiting/empty/validation error; AFK save error; target discovery loading/failure; empty profiles; Garrison pending/failure/localized/no-target/no-squad/running; and Zombie waiting/error/running. It asserts material branch content, editor sibling ancestry, source/current control counts, runtime/error ownership, and the newly recovered non-default squad assignment behavior.

`capture-whole-afk-pairs.mjs` renders 14 representative original/current pairs in Chrome 154 at desktop and narrow widths across light/dark themes. It checks direct layout ownership, editor sibling placement, toolbar-setting ancestry, discovered squad controls, runtime rows, Garrison/Zombie composition, composition errors, target-loading/failed Add ownership, horizontal overflow, console/page errors, and records paired screenshots. Every non-fenced pair is byte-identical. The two Garrison pairs differ only because current keeps unavailable-provider `Run Now` disabled: normalizing only that disabled state makes each full screenshot byte-identical. Producer and `--verify` recomputation both pass with zero browser errors.

`join-modal-contract.mjs` pins the recovered `In` and current `JoinModal` function slices and verifies the native-dialog, showModal/close, focus restoration, cancel prevention, Tab trap, stop-propagation, pointer-origin and default busy/backdrop contracts. The current Join picker was corrected from a backdrop `div` to this recovered native-dialog lifetime.

`mounted-afk-interactions.mjs` adds 25 live browser assertions against the current Vite app with zero console/page errors. It mounts Join search/select/confirm plus backdrop/Tab/Escape/focus cleanup, Garrison ally search/select/confirm, real first-write failure followed by Retry and Discard acknowledgements, and AFK→Equipment→AFK draft retention. Three interaction screenshots are recorded and the producer has a read-only recomputation mode.

The whole-composition producer also executes the recovered AFK helper chain and current equivalents for join normalization/validation, profile validation, target resolution/range and ordering. It compares current Monster/Potion/Garrison validators to recovered store contracts for all 30 cases and explicit negative inputs. In particular, enabled Drill with no selected squad remains store-valid on both sides because recovered `I` owns that rejection in the toggle interaction instead of the config-store validator.

## Additional source-proven corrections

The AFK audit found and fixed five additional recoverable differences beyond the six lead defects:

- `AfkProfileEditor` now receives the discovered `availableSquads` list from `AfkContent`; recovered `I` uses `Ne`, so `[3,4]` renders two assignment controls and empty discovery renders none instead of always showing 1–4.
- The editor no longer invents inline target-discovery checking/failure copy. Recovered `I` keeps the target field quiet while loading and owns discovery rejection through the final composition error.
- `RallyJoinSettings.JoinModal` now follows recovered `In` native-dialog focus, cancel, Tab-trap and cleanup semantics. Its source default does not dismiss on backdrop click.
- Target discovery failure now retains the last successful target list while readiness is false, matching recovered `I`; first-load loading starts with an empty target list and disables Add, while a failed refresh keeps Add available when retained targets exist.
- `validMonsterAfkConfig` no longer adds a store-level Drill-empty rejection absent from recovered `I`; the visible Drill toggle interaction remains the owner of the squad-required message and refuses the invalid enable action.

## Verification

From repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-b/whole-afk-composition.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-b/capture-whole-afk-pairs.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-b/join-modal-contract.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-b/mounted-afk-interactions.mjs --verify
npm.cmd run check --prefix src/LWBridge.UI-0.3.17
git diff --check
```

Locale-specific whole-App branches remain inherited only where their material source/current dependencies are unchanged; final integrated verification rechecks live all-route locale/theme/narrow behavior after the closeout changes.

## Limits

Positive native/gameplay action execution remains outside this UIUX assignment. The recovered original protected runtime and loaded native assets are not invoked. Current unavailable native actions remain visibly fenced. These limits do not leave a recoverable source/local AFK composition gap in this milestone.
