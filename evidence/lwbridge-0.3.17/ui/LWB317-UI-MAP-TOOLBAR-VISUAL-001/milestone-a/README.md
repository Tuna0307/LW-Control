# Milestone A — immutable toolbar baseline

Checkpoint: `bea2a1870e9bf34117fbed2f7e434504e9d8361a`

`baseline/` is the immutable assignment-start copy of the three owned production
files. `baseline-manifest.json` records their SHA-256 hashes and exact source
locators alongside the pinned recovered renderer/assets. `compare-toolbars.mjs`
executes the recovered Map page and the assignment-start current page against the
same data/options and locale catalogs. It expands the actual recovered/current
Treasure and retained-goods filters and their actual empty-cache image components;
table rows and Scheduled Plunder job-table bodies are deliberately excluded.

The run in `baseline-results.json` covers the required 34 states (default and
configured variants for all eight normal tabs plus default Scheduled Plunder in
English and Japanese) and 12 supplemental states for loading, missing server,
provider unavailability, first/middle/last/single-page pagination, and rejected
queries. Rich states exercise City alliance/no-alliance and marked controls,
Resource/Monster names, Truck/Train item and quality controls, Dispatch/Ghost
status/quality/delay/selection counts, Treasure type/foreign/lucky controls, and
all visible result counts.

The baseline proves these presentation differences:

- Recovered `MapDataPanel` keeps the normal table and `it` pagination inside
  `.map-search`; current closes `.map-search` after the searchbar and renders the
  table/pagination as siblings. The recovered `.map-search{display:grid;gap:8px}`
  therefore owns their spacing/layout.
- Recovered result counts interpolate the integer directly (`1202 items`), while
  current formats totals with `toLocaleString()` (`1,202 items`).
- Recovered Truck/Train/Dispatch plunderable labels carry
  `map-filter-field map-plunderable-filter`; current omits the second recovered
  class. The pinned stylesheet currently has no dedicated rule for that class,
  so this is a DOM-contract drift with no independent geometry delta.
- Recovered/current Treasure and retained-goods dropdown contents, active states,
  labels and unloaded icon slots match when their actual components are expanded.

The baseline also preserves documented accepted/current differences instead of
turning them into fixes: current tab/button accessibility attributes and explicit
`type="button"`; the previously accepted visible query-error banner; Search being
disabled when no data server is available; and protected/native provider action
fences (including disabled Treasure claims and schedule/share/truck actions).

Raw expanded toolbar HTML for every side/state is retained in `raw/`. The
assignment-start production sources remain untouched at this checkpoint.
