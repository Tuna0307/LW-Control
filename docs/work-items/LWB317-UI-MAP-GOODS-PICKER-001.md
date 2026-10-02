# LWB317-UI-MAP-GOODS-PICKER-001

Owner: project-lead takeover, while the manual-relay worker rests.
Starting revision: fbde5d6525fcd46b29307a6cf9e08abbb61e8d96.
Status: COMPLETE implementation for focused source/local UI scope;
independent peer review follow-up. Phase: UI parity only.

Goal: replace the Truck/Alliance Train native retained-goods select with the
actual recovered 0.3.17 details/menu, preserving accepted query/sort ownership.

Authority: exact MapDataPanel-B4GXEND2.js (ct renderer UTF-8 byte 28232,
parent caller 53802), plus GameAssetImage-Diy9VTIr.js's unavailable-image branch.
Verify reference/source hashes; record exact locators in the evidence packet.

Scope: strict selected-key lookup, summary/icon slots, source item names/order,
active classes, raw-key callback, change-before-close, parent page/filter/sort
behavior and independent Truck/Train ownership. Reuse recovered CSS.

Non-goals: native game image loading, scans/gameplay, provider integration,
original protected post-auth access/pixels, other Map presentation/campaigns.
Unknown/unavailable game icons retain the original placeholder presentation.

Acceptance: actual original/current renderer/callback comparisons including
empty/missing/numeric/string/zero keys; actual page callbacks and per-kind
queries/itemCount sort removal; immutable baseline mismatch; real offline
browser selection/close/keyboard and EN/light + JA/dark; maintained Map replay,
canonical check/build/package, evidence validator and protected-WIP guard.

Completion boundary: coherent documented/committed/pushed implementation and
exact remote verification; independent peer review remains a returning-worker
checkpoint. No fixed timebox and no full Map/pixel/native acceptance claim.

Delivery: 144 original/current comparisons across nine languages, six unloaded
asset renders, exact parent query/sort comparisons, fresh English/light and
Japanese/dark browser/keyboard/tab-retention/sort-clear QA and maintained Map
regressions pass. Canonical check/build/package and evidence/WIP validation pass.
See docs/reviews/2026-10-03-LWB317-UI-MAP-GOODS-PICKER-001.md and the matching
evidence packet. Original loaded icons/pixels/native behavior remain unproved.
