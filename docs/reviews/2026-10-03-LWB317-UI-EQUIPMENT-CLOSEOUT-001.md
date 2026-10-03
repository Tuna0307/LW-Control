# Equipment Schemes closeout worker delivery — 2026-10-03

Status: **AWAITING_REVIEW**. This is the returning worker's bounded Equipment-only
delivery for `LWB317-UI-EQUIPMENT-CLOSEOUT-001`; the project lead has not accepted
it yet. No City Layout, Hotkeys, Mini Games, Settings, native provider, gameplay,
or login/account work was started.

## Changes and conclusions

The dispatch implementation had nine source-demonstrable mismatches. The delivered
Equipment path now derives selected/current/dirty state from source-shaped inputs,
renders live supplied squad/hero rows, restores the original Alt+1..4 apply behavior,
and uses first-visit tab mounting with retained hidden state. Rename now follows the
shared recovered dialog contract: trim/blank handling, unchanged clean close, no
backdrop dismiss, Escape/busy gating, focus trap/restoration and full-draft flush.

Item moves require the same equipment slot, hero-loadout moves swap the full position
equipment set, and squad-loadout moves use shared leading positions from live hero
counts. Self/no-op and wrong-slot paths are rejected, position success clears at the
source `450 ms`, and toast cleanup remains `1800 ms`. Result/error/progress rendering
now consumes supplied preview state; online/offline and busy state control action
presentation while all native-looking preview callbacks remain inert.

## Source and automated proof

`check-closeout.mjs --record` executes recovered `0.3.17` helper/nested functions
and current production callbacks for **105 assertions** across seven proof groups;
all pass. The immutable dispatch baseline records nine failures at
`d51d6b1fdd224c1e6dd479d1f05c82eb8a1e9f17`, and the same baseline predicates now
all pass without rewriting `baseline-results.json`.

The directly affected Automation/AFK behavioral closeout still passes **729 cases**,
and recovered Join/Assist renderer checks pass. Canonical `npm.cmd run check`, build,
and production-build checks pass from `src/LWBridge.UI-0.3.17`; build/package hashes
are recorded in `verification-results.json`. The historical AFK `validate.mjs` is
intentionally stale only at its pinned pre-Equipment `Pages.jsx` hash: current
`A07D08E1546824B5D7342B3F473C0569ED4775C36DF07784CD59529A309D130C`
versus historical `2B68DFBC6A8444B5C6561DF774A60945473BE6F5E5590EE6AB0228F96043414F`.
Its manifest was not rewritten to manufacture a pass.

## Browser evidence

English/light browser checks cover preset selection versus current identity,
Alt+3 inert apply, enabled inert Refresh/Read/Save-and-apply callbacks, and real
rename modal behavior including backdrop, blank value, Escape/focus restoration
and trimmed Enter save. A browser-only synchronous-preview Enter reactivation issue
was found and corrected with `event.preventDefault()` before save.

Japanese/dark supplied partial result rendering was verified at `1920x855`. A real
`800x543` Japanese/dark popup showed wrapped actions and no document-level horizontal
clipping. Three settled screenshots are saved and visually inspected. Fresh console
capture on the owned rename tab has zero warning/error entries.

The Desktop connector's physical pointer drag did not emit the page's HTML5 drag
lifecycle. This is **not** claimed as a physical pass. A separate DOM-dispatch path
exercised the actual rendered React handlers and produced the expected same-slot
swap, dirty state and mixed-current label; exact recovered original/current move
transformations are independently executable in the 105-case validator.

## Preservation and limits

The protected-WIP guard passes for all seven pre-existing paths, including historical
Map result files, `previewAfkFixtures.js`, `.scratch-lwb317`, and the parent screenshot.
They remain outside this delivery. `git diff --check` passes.

Remaining limits are explicit: no live Last War/native Equipment provider, native
persistence or gameplay was exercised; physical HTML5 drag is not proven through
connector pointer input; original post-auth pixel-for-pixel assets/geometry remain
outside this source/local closeout. The project lead should review this packet before
advancing Equipment or opening the next page.
