# LWB317-UI-007 visual-comparison evidence

Date: 2026-09-29

This directory contains clone-side before/after evidence for the UI-007
evidence-backed fix pass.

The `before` images are the fixed `1120 x 720` clone captures committed by
LWB317-UI-006. The `after` images were captured from the same standalone
`src/LWBridge.UI-0.3.17` preview after UI-007 corrected markup/hierarchy against
the exact recovered 0.3.17 frontend components and stylesheet.

These images are **not** direct screenshots of the original post-auth LWBridge
reference. That comparison remains blocked by the out-of-scope authorization
boundary. The evidence here therefore demonstrates clone-side change plus
static `EXACT_BYTES` contract alignment, not runtime-proven visual parity.

## Files and SHA-256

| Capture | SHA-256 |
|---|---|
| `automation-before-ui007-1120x720.png` | `2F2E04B64EEEBC536A24CF91423FAFEF128683BA704002AD67500D4987F1270C` |
| `automation-after-ui007-1120x720.png` | `6C8170769E82AA0D29B85111734C05204EA040D7FD03899671F9DF048D85B684` |
| `map-data-before-ui007-1120x720.png` | `482C066208D4C1C59DBD566BAEE9370BC59D4722EBB8340E24FF827117ADA54A` |
| `map-data-after-ui007-1120x720.png` | `A61623DDD9025F039C0F4D81EF041DF215F97F9BED2D78F93BC23BC2420CF94B` |
| `squads-before-ui007-1120x720.png` | `B8901E548954BE2AFA7DD8E037A255E45E04442B6D43FB94B4BF64F4F25E604A` |
| `squads-after-ui007-1120x720.png` | `3A6EE8165527583CE72877B3A3503E3B9A4C062D374A33E95B577C2857678A95` |

## Comparison basis

The fixes are grounded in exact recovered frontend bytes, principally:

- `frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js`;
- `frontend-package/web/assets/AutomationCard-LCx_jIi7.js`;
- `frontend-package/web/assets/MapDataPanel-B4GXEND2.js`;
- `frontend-package/web/assets/SquadPanel-HC3-DJei.js`;
- `frontend-package/web/assets/index-rIL9Fpht.css`;
- `frontend-package/web/assets/en-BisSXcTB.js`.

No Last War process, reference auth bypass, or gameplay/backend behavior was
used to create these captures.
