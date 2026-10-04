# Unit C — Automation visual closeout

Unit C closes the Automation portion of `LWB317-UI-VISUAL-FINAL-CAMPAIGN-001` using fresh campaign-owned recovered/current source execution plus real mounted browser interaction. Historical packets were not edited.

## Result

One source-proven visual defect was found and corrected. The recovered shared `AutomationCard` adds `is-enabled` when an Automation is enabled, and recovered CSS gives that class a distinct border and shadow. Current generic Automation cards, Resource Gathering, and Trade Station had all omitted the class. `AutomationPage.jsx` now applies the recovered class in those three scopes without changing handlers, provider fences, or native/gameplay behavior.

The immutable pre-fix packet is `enabled-class-baseline.json` plus `enabled-class-baseline/`. It proves all three enabled current cards lacked `is-enabled` and records a source-class mutation control. The corrected packet is `enabled-class-current.json` plus `enabled-class-current/`; decoded mutation testing in `enabled-class-pixels.json` detects 1,484, 2,044, and 3,336 changed pixels when the recovered class is removed from generic Auto Training, Resource Gathering, and Trade Station respectively.

## Fresh proof

- `accepted-source-replay.json`: 186 recovered/current executable comparisons (52 meta/future-time, 48 named runtime/card, 32 manual Assist, 54 Gather callback/runtime cases).
- `replay-003A.mjs` through `replay-003E.mjs`: campaign copies of accepted Weekly and Trade checks updated only for current module locations/shared-image presentation; all pass.
- `browser-current-results.json`: 121 mounted browser assertions, 20 screenshots, four required EN/JA + light/dark + desktop/narrow modes, zero console/page errors.
- Browser interactions cover all seven categories, exact card counts, collapse/Activity retention, non-collapsible/no-settings distinctions, Construction keyboard tabs, Auto Training conditionals, Resource Gather validation/runtime branches, Secret Assist states, Trade loading/error/history/cross-server states, generic runtime error, and real preview-store Retry/Discard.
- `locale-inventory.json`: 363 Automation-relevant keys inventoried across all nine current locale modules, with no partial locale holes. `common.loading` is source-proven to use the shared `t(key)` fallback because it is absent from all nine catalogs; this is recorded rather than hidden.
- Production CSS remains byte-identical to recovered CSS at SHA-256 `3d87e9f65b39eace6a1a254bfc90a38acb72613d1fff7236c7cee7ab9bfaf545`.

## Limits

The untouched recovered desktop frontend cannot boot as a complete authenticated page in a plain browser because its protected desktop bridge is unavailable. Unit C therefore uses the actual recovered renderer/helper functions as the original-side oracle and real current mounted browser pixels for the current side. It does not manufacture a reference DOM or claim protected-original post-auth runtime pixels. EN and JA are actually rendered; the remaining seven locales are source/catalog inventoried as required.

No native/gameplay action was invoked. Disabled native action buttons remain fenced. All data used for current mounted interaction is disclosed browser preview fixture data.

Run `node validate-unit-c.mjs` from the repository root path to recompute the source/artifact assertions and rerun the campaign-owned Weekly/Trade source replays.
