# Mechanical lazy module migration checkpoint — 2026-10-04

Assigned module migration is implemented; project-lead integration/review remains
pending. No native operations, commits or staging were performed by this lane.

Canonical `Pages.jsx` now imports Home eagerly and declares stable React.lazy
wrappers for six real dynamic module loaders. Hotkeys/Mini Games share the Hotkey
module and lazy panel type. `preloadRoute` has the recovered finite optional-loader
and rejection-swallow behavior. The prior Map provider overlay lives in
`MapRoutePage`, preserving preview-only fencing. There are no eager compatibility
re-exports or duplicate UI implementations.

Eight module files own the moved helpers/pages. The 48 pre-existing non-router
declarations have **identical declaration bytes/hashes**, each with one canonical
owner; imports compile successfully. `baseline-Pages.jsx`,
`module-preparation.json` and `module-migration-results.json` preserve this finite
proof before any separately assigned presentation changes. The original router
rewrite and Map wrapper are explicitly recorded rather than claimed byte-identical.

Canonical check changes only point existing checks at their actual new owners:
Home predicate reads `HomePage.jsx`; locale/fixture inventory scans the actual
page modules; scaffold presence covers the new canonical modules. Checks retain
their semantic predicates and all nine locale catalogs remain at 1,383 entries.

Verified at this checkpoint:

- `check-module-migration.mjs --require-router`: 48 exact declaration matches,
  unique owners, actual canonical router identity and import compilation PASS.
- `npm.cmd run check`: PASS, 470 referenced locale keys.
- `npm.cmd run build`: PASS; six real route chunks emitted.
- `npm.cmd run check:production-build`: PASS, source/package fingerprints
  `75227808a326caa9bf2994a8bf685b283b4f6b86b4d2a824ac906af041022184` /
  `e63acfa6ff7a902887508980996677297e365f184b515bf4e1bc21439d417932`.
- `check-production-route-graph.mjs`: PASS; all six routes are dynamic entries
  and excluded from the actual startup static chunk graph.
- Owned tracked diff whitespace check: PASS.

Exact continuation: the lead owns App hover/focus/pre-transition preload and
single Suspense integration; the lifecycle review lane owns current mounted
deferred imports and regression adapters. Browser loading/retention and final
integration acceptance are **not** established by the mechanical or build proof.
Historical Pages-shaped extractors/manifests remain preserved; maintained current
adapters must read actual module bodies/import bindings. The separately assigned
Equipment motion lane may now own `SquadsPage.jsx` without changing its handlers.

If later intentional presentation changes modify moved declarations, preserve
this checkpoint proof as historical and create a new focused delta proof instead
of refreshing these hashes to manufacture an unchanged-body pass.
