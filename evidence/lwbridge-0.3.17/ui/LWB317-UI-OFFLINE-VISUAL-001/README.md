# Offline source-rendered Home / Map table checkpoint

Date2026-10-04; production revision `a01d5d548c634ae00f9d33ad364895922e024733`.
This is **EXACT_BYTES / EXACT_CONTRACT plus bounded local browser comparison**.
It is not protected-original-runtime pixel acceptance. No product code changed
in this checkpoint. The larger visual campaign remains PARTIAL.

## Actual execution and result

Home builds from the actual original Ir/Lr/Kr/zn/Bn/qr declarations in the pinned
main asset and the actual current HomePage/shared Switch/ToggleRow bodies. Both
execute through React19.3.0 static rendering with the same current exact-recovered
EN/JA catalogs, inputs and CSS. All six original slices/locators/hashes are in
home/render-results.json. There is no guessed replacement Home markup.

42 cases:30 have matching structure/attributes/text after ignoring only the
clone's explicit button type. The remaining12 are disabled-control differences:
six lifecycle-provider cases, two unavailable-provider cases, and four preference
save-busy cases (two switches × two languages). Every difference is retained in
the report rather than normalized into a match.

Four Home pairs under identical browser conditions match every measured element's
rectangle and selected computed style, and produce identical JPEG bytes. Cases:
EN/light proxy-busy-running1280×720; EN/dark missing/error/busy740×600;
JA/dark proxy-busy/repair740×600; JA/light launch/proxy overlap1280×720.

Map executes the actual original memoized table,21 pure helpers, three module
constants, original truck helpers, reward formatter, UiIcon and GameAssetImage
declarations. Current executes the actual MapTable/coordinateText bodies, current
helper modules and bundled actual GameAssetImage. The clock is fixed; original
image cache is empty; React SSR effects do not run or request assets. All original
slices/hashes are in map/render-results.json. Current scan-label keys are extracted
from the actual declaration, not a guessed tab-label mapping.

48 Map comparisons match:all eight normal kinds × empty/loading/populated × EN/JA.
Four browser pairs have matching rectangles/styles and identical JPEG bytes:
EN/light populated City1280×720; EN/dark loading Truck740×600;
JA/dark populated Treasure740×600; JA/light populated Secret Task1280×720.
Narrow tables preserve internal horizontal scrolling with no document overflow.

Sixteen screenshots (eight pairs) were saved; current images were visually
inspected and their originals have the same exact byte hashes. The isolated
reference tab's captured warning/error console is empty. These comparisons use
identical empty navigation/isolated panel containers. They do not establish the
full original shell or Map header's pixel geometry. Fixed-clock table dates are
controlled evidence, not live observations.

## Home finding requiring a follow-up correction

The original Home passes label/checked/onChange to Bn with no disabled property
(qr byte336694; Bn byte213332). Current Home additionally disables a preference
when busy is autoLaunchGame or autoReconnect. This is a **valid recovered UI
discrepancy**, beyond intentional unavailable-provider fencing.

The original Auto Launch setter writes local storage and updates local state
immediately (main byte248381; Sr246842). The original reconnect callback edits
and flushes a profile flag draft (It366485), while current App waits for native
acknowledgement before updating localConfig. These producer differences mean
removing the disabled clauses alone is not a complete interaction correction.
Next bounded task must recover optimistic/draft/concurrent-edit semantics and
verify actual callbacks, acknowledgement/failure ordering and profile ownership.
Keep existing unavailable-provider fences and profile injection; do not invent
native success or enter gameplay. Historical accepted tests remain evidence of
their earlier scopes, not proof against this new exact-source counter-evidence.

## Reproduction and tooling

Run home/build-reference.mjs and map/build-reference.mjs; start serve-reference.mjs
on loopback4337. Generated HTML is ignored/reproducible; source/reference hashes,
render reports and browser measurements are durable. Node24.18.0, React19.3.0,
esbuild0.28.2, Babel AST parser and jsdom27 from the existing local harness runtime.
No installation was needed. Server CSP disables scripts/network resources.

The first Map harness bound City/Resource table aria labels to tab-label keys,
which yielded12 harness-only differences. It was corrected to execute the actual
SCAN_TYPE_LABEL_KEYS declaration; no production change was made. The first image
bundle used classic JSX without a React binding; automatic JSX compilation fixed
that harness error. No behavioral assertion or real mismatch was suppressed.

## Limits / continuation

This checkpoint accepts the named offline Home equal-state layouts and Map table
renderers, not complete Home busy preference interaction or every visual surface.
Native state/config/assets, game actions, physical HTML5 drag and protected-original
runtime comparison remain separate. Map header/full panel, other-page visual
differentials and the Home preference correction remain in the visual finish queue.
The current named inventory distinguishes those remaining gates.
