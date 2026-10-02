# Map Treasure picker — lead implementation

Status: **COMPLETE for focused source/local UI scope**; independent returning
worker review can extend confidence. This is not full Map/original pixel parity.

The previous native select mismatched original lt at UTF-8 byte 29030 in exact
MapDataPanel-B4GXEND2.js (SHA-256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089).
Original uses details/summary and ordered menu buttons, resolved names/counts,
strict key matching and close-after-change. Canonical MapTreasureTypeFilter now
reproduces those expressions, using the existing Ke-equivalent treasureName and
recovered map-item-filter CSS. Parent caller matches byte 52026: selected key
updates Treasure state and resets page to 1. No provider/schema/CSS redesign.

28 executed original/current renderer/interaction comparisons pass across en/ja,
empty/populated and key/name/count edge cases. Numeric 2 and string '2' remain
distinct; zero-key active truthiness matches the original. Parent callback/query
checks and immutable ff4ed369 baseline mismatch pass. Fresh browser EN/light
and JA/dark checks verify names/counts, menu opening, closing, active item on
reopening, keyboard Enter and All reset. Inspected screenshots and empty console
capture are in the evidence packet.

Parent filter lifecycle assertions pass through a documented menu-entry adapter;
PM-027 6/6, R1 5/5 and navigation 17/0 remain green; strict integration 14/14
passes. New historical shim retains all 472 filter comparisons (baseline 330
mismatches), table regression, Checking 216/384/8, row 160 and Map-state checks.
Canonical check/build/package pass; fingerprints
336e4ec56bd8ee29f2f079caae060fe705132567d2da95d2c8970f0502709ec3 /
13116927dac2ca8dd08bb4104a3ded9a261e9e0aa098710fb64d6b70f722d17d.
Protected WIP remains untouched. Historical evidence is not relabeled as current.

The old unadapted historical replay fails on the new JSX child identifier; the
new documented shim passes without changing its assertions. The old lifecycle
harness looks for a native select; its adapter calls actual menu parent callbacks,
while renderer/closing/strict keys are independently exercised by check-picker.
One initial checker failure was child-array shape normalization between JSX
runtimes, corrected in the test serializer; no product mismatch remained.

No native operation or original protected runtime was exercised. Native assets/
producers and scan/export/start UI timing/feedback remain separate gaps. Current
implementation handoff is the next-chat entry point; no new chat is required to
preserve this checkpoint. Owned preview resources are cleaned before delivery.
