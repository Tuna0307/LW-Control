# LWB317-UI-MAP-AUTO-VISUAL-001 — evidence review

AWAITING_REVIEW for the bounded Auto Scan configuration-card comparison. No
production code changed. Exactly two original/current browser pairs were made,
and no Run now/native action was invoked.

## Findings

Reference EXE SHA-256 is
`4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783`;
recovered MapDataPanel SHA-256 is
`ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089`.
The original Auto card is pinned at bytes 46001-48950. The existing original
runtime executes that recovered component body; the packet additionally executes
the recovered index `Vr` icon function because INTERACTIONS deliberately leaves
Icon as an inert typed dependency.

| Comparison | Result | Evidence |
|---|---|---|
| Default EN/light, 1280x720 | Exact structural/markup and PNG match | Both PNGs `34e3f61e8bcba907cab550c64febbdc83f542b651e1616451a5088db454c47d7`; no geometry/style differences. |
| Configured JA/dark, 375x1000 | All card/control layout matches; server-chip remove glyph differs | Original uses recovered SVG `Vr(remove)`; current uses text `×`. 12 child-rectangle and 9 child-style differences; parent anchors equal. |

Original remove use is pinned inside `MapDataPanel-B4GXEND2.js` and its recovered
`Vr` implementation starts at index-asset UTF-8 byte 330489. Current Auto card
starts at `MapDataPage.jsx` UTF-8 byte 46645 / line 1011; current remove child is
UTF-8 byte 48425 / line 1041, length 34, SHA-256
`9c93c57e5ede6357ea3c89cb4829f4af3f59822d9ab5840d91e8b277d1ab89d5`.

Configured original remove children are SVG `.ui-icon` nodes with path
`m4 4 8 8M12 4l-8 8`, each 14x14. Current children are text spans containing
`×`, each 7.59375x13. Their x/y differ correspondingly. No containing chip,
button, card, grid, type group, option or text anchor changes geometry/style.
The configured card remains 341x650 at (17,17); the default card is 1246x314 at
(17,17). All four screenshots were manually inspected and have zero fresh
console issues.

Labels and controls otherwise match for both samples: master state/status,
server label/input/Add/hint, interval, speed/options, all eight scan types and
checked states, return option, Run now state, navigation notice and next-run
text. The configured servers are 8/15/120 and selected types are
city/truck/treasure. The sample is valid under recovered kinds/server parsing and
normalizes unchanged under the current config helper.

## Validation and limits

`compare-renderers.mjs` executes the actual recovered/current sources and records
raw differences; `capture-browser.mjs` measures the two required browser pairs;
`validate-evidence.mjs` rechecks source/dependency/config pins, raw markup,
screenshot hashes, inspection records and the exact expected mismatch set. The
required canonical frontend, production-package, ten-path WIP guard and Git
checks all pass. Production-package integrity reported
`865208f65150f67aa0f3a29cd8e6c0b97352181e68e7047cad47c6df87866ea2` and
`7f3bbbf98bd110e0b15b413a4e52d4d3c281ed076f856830cd64395fabb64a90`;
the protected-WIP guard reports `checked: 10`. `git diff --check` passes with
only the repository's line-ending warnings.

Evidence is isolated source rendering with inert inputs, current recovered EN/JA
catalogs and a fixed Asia/Singapore clock. It does not prove full-shell,
protected-runtime or native pixel behavior. This task assigns no correction for
the remove-glyph mismatch and makes no statement about another Map surface.
