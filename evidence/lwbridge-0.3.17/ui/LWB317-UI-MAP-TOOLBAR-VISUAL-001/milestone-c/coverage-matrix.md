# Toolbar/pagination coverage matrix

| Area | Executed coverage | Result |
| --- | --- | --- |
| Finite renderer matrix | 34 required EN/JA states: all nine default tabs plus configured states for all eight normal kinds | Complete; every difference retained/classified |
| Supplemental renderer states | Loading EN/JA, missing server, backend unavailable, one-page/first/middle/last pagination EN/JA, query errors EN/JA | Complete |
| City | Alliance including encoded/literal identities, marked-only, keyword, Search, counts, pagination | Corrected/current proof passes |
| Resource / Monster | Name options, keyword/name mutual clearing, Search | Current interaction proof passes |
| Truck / Train | Quality, retained-goods dropdown, plunderable filter | Current interaction/render proof passes; unavailable Truck schedule remains fenced |
| Secret Task | Status, level, quality, plunderable, delay, selected count | Current interaction/render proof passes; schedule/share remain fenced |
| Ghost Ops | Status, quality, delay | Renderer matrix passes |
| Treasure | Type dropdown, foreign/lucky flags, claim controls | Current interaction/render proof passes; claim actions remain disabled |
| Scheduled Plunder | Tab/count/result toolbar only | Renderer/browser proof passes; job-table body excluded by assignment |
| Pagination | zero/one page plus first/middle/last multi-page states and real mounted next/previous handlers | Pass; recovered `.map-search` ancestry restored |
| Local interactions | 34 mounted Vite assertions in EN/light and JA/dark | Pass; zero console/page errors |
| Translations | 45 assigned keys × 9 catalogs | 405 direct entries; zero fallback |
| Required browser proof | 6 pairs / 12 inspected PNGs | 0 geometry deltas; 3 pixel-identical pairs; 3 intentional action-fence pairs |
| Navigation/request lifecycle | adapted navigation + 38 request-lifetime scenarios | Pass |
| Interaction differential | 38 original/current scenarios | 0 current mismatches |
| Filter lifecycle | accepted R1 5-case original/current server/options lifecycle | Pass |
| Auto / Manual preservation | Auto control replay + superseding 7-state Manual empty-selection callback proof | Pass; no native mutators |
| Table/state regressions | 48/48 actual table renderers, 216 Treasure display comparisons, 384 fences, 160 row-action comparisons | Pass |

Remaining browser differences are intentional availability contracts, not open
presentation defects. Current accessibility metadata and explicit button types are
kept in raw DOM evidence rather than normalized away.
