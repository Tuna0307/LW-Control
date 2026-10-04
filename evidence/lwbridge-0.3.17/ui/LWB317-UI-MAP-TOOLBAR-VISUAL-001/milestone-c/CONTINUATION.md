# Exact continuation — LWB317-UI-MAP-TOOLBAR-VISUAL-001

Status: **AWAITING_REVIEW**. Branch: `research/offline-controller`.

The assignment began at lead checkpoint
`bea2a1870e9bf34117fbed2f7e434504e9d8361a`. Milestone A is
`4574cc8aaf5751b1089c22f997e580d0ad16dff8`; Milestone B is
`9668133737f2589c27224613d8d343c5e732e474`. Milestone C contains the six-pair
browser packet, inspection, affected regression replay, validators and current-doc
handoff. The final Milestone C and remote SHA are reported by the worker after push.

Production scope is one file: `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`.
The correction restores recovered `.map-search` ownership of table/error/pagination,
direct integer result-count interpolation, and the recovered
`map-plunderable-filter` class. `MapTreasureTypeFilter.jsx` and
`MapRetainedGoodsFilter.jsx` were compared but did not require production changes.

There are zero unresolved proven in-scope presentation defects. The remaining
Truck/Secret Task/Treasure visual deltas are disabled native/provider actions that
the assignment explicitly requires us to preserve. Accessibility/button-type raw
DOM differences are also preserved. Scheduled job bodies, table rows, native
functions/gameplay, other pages and global UI parity remain outside this task.

Starting unrelated/protected WIP remains outside task commits. The ten-file guard
passes byte-for-byte, including the pre-existing modified Auto-config regression
result, R1 independent result, `previewAfkFixtures.js`, scratch files, the
CORRECT-003 zero-byte screenshot and equipment motion vendor license/notice files.
The parent filter-lifecycle protected result also matches its pinned bytes. The
lead should review the three milestone commits and saved evidence, then decide
acceptance; no further implementation is requested by this packet.
