# Retained goods menu recovery

Lead-owned UI unit, baseline fbde5d6525fcd46b29307a6cf9e08abbb61e8d96.
Status: COMPLETE implementation for focused source/local UI scope;
independent returning-worker review remains a follow-up.

## Exact authority

Reference EXE SHA-256:
4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783.

MapDataPanel-B4GXEND2.js SHA-256:
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
UTF-8 byte 28232: actual ct details/menu renderer.
UTF-8 byte 53802: Truck/Train parent caller, raw-key edit/page reset and
itemCount sort removal when clearing. These are EXACT_BYTES/EXACT_CONTRACT.

GameAssetImage-Diy9VTIr.js SHA-256:
2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0.
UTF-8 byte 2250: unloaded-image span, class and aria-label branch.
Native image loading remains outside this unit; these slots are placeholders.
Recovered reference.css's map-item-filter classes are reused unchanged.

## Production behavior and proof

MapRetainedGoodsFilter replaces the Truck/Train select. The original preserves
strict key equality, source option order/names, icon slots, truthiness of All's
active class, selected-name OR All fallback, raw keys and callback-before-close.
It does not add option label/value coercions, outside-click/Escape handlers or
invented fallbacks. Existing changeItemFilter handles page/sort/per-kind state.

check-picker.mjs captures and invokes the ACTUAL original ct from the exact
component runner; canonical code is compiled by existing esbuild. 144 comparisons
use all nine locale catalogs with identical translation inputs, empty/populated
options and eight selected-key states. Asset child props are compared as slots;
six separate actual original GameAssetImage unloaded renders are compared to
canonical placeholders with effects inert. No loaded/native image claim.
Callback traces include every raw key, change-before-close, thrown callback
retaining open and absent DOM ref. Original and production parent callbacks
produce identical query/sort traces across Truck/Train selection/reset/clear,
string-versus-numeric keys and cross-tab retention. Immutable baseline exposes
the old native select; no old source/evidence was edited to hide that defect.

Fresh owned localhost:4328 IAB QA confirms click/Enter opening, selection/close,
per-tab restoration, active-on-reopen and All reset. English/light and
Japanese/dark menus were captured and visually inspected. Japanese real-table
itemCount sort is removed on All while Updated At sorting remains. Browser
console errors/warnings are empty. Data/labels are explicit offline fixtures.

## Regression commands

Run from repository root:

    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/check-picker.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/run-regressions.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/validate-evidence.mjs

goods-harness supplies only test-entry select adapters for actual parent menu
callbacks; actual render/raw-key/closing tests above do not use them.
legacy-bindings binds JSX children that old fixed-binding tests never render,
and adapts only goods-handler lookup/invocation. All historical assertions stay
unchanged. Parent/R1 result writes are redirected into this packet. The first
regression invocation reused the previous picker's R1 wrapper, which updated
its results hash; that solely lead-created output change was restored to its
known clean committed bytes. The owned R1 wrapper now avoids that side effect.
No historical result content is included in this commit.

regression-results.json records request lifetime (38/0), navigation (17/0),
interaction differential (38/38), integration (14/14), parent/R1 and maintained
historical filter/table/Checking/row/state tests. The previous Treasure picker
also remains 28/28. verification.json records canonical check/build/package.
validate-evidence verifies source/product/image hashes and six protected WIP
files. Screenshots are actual JPEG bytes despite screenshot API's default.

Limits: source/runtime-component and offline preview only. Original post-auth
pixels, native image loading, native persistence and gameplay are not accepted.
Full UI remains IMPLEMENTED_NOT_VALIDATED. Next UI work: manual scan header/
summary/timing and export/start feedback, with native operations still separate.
