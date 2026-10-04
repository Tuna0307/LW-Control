# Independent Home / Map table checkpoint review

Date: 2026-10-04. Production HEAD reviewed: `a01d5d548c634ae00f9d33ad364895922e024733`.

Recommendation: **ACCEPT this bounded evidence checkpoint**, retaining the Home preference discrepancy as an open correction. This is not acceptance of complete Home interaction parity, the complete Map panel, global UI parity, native behavior or protected-original-runtime pixels. No production change is recommended as part of this evidence-only checkpoint.

## Independent execution and integrity

Read both builders, their durable render reports, browser measurements/comparison reports, the preference counter-evidence, README and current inventory. Reran only the two authorized builders; they regenerated their current reports and ignored static HTML.

- Home: **42 cases / 30 matches**, reproducing the same 12 differing cases. Every remaining difference is the `disabled` attribute: six lifecycle-control cases, two unavailable-provider cases and four preference-save cases, across English/Japanese. There are 14 differing elements because each unavailable-provider case affects both switches.
- Map table: **48 cases / 48 matches**: eight normal data kinds, three empty/loading/populated states, two languages.
- Independently checked **40 exact UTF-8 slices**, their recorded text and SHA-256, against the pinned original assets: six Home slices, 31 Map/dependency slices and three preference counter-evidence slices. Full original asset hashes match. Both builders execute actual extracted original and current renderers rather than reconstructed markup.
- Independently compared all **eight** saved browser pairs: **325** measured element records have identical rectangles, selected computed styles, tags/classes/text; paired viewports/document widths match and show no document-level overflow. All **16 JPEG files** match their recorded hashes, with identical bytes within each pair.
- Visually inspected the current English/dark missing-root/error/busy Home image and Japanese/dark populated Treasure table image. Their paired original JPEGs are byte-identical. The Treasure table keeps internal horizontal scrolling at narrow width.
- `map/browser/console.json` is empty. This is the saved capture, not a newly executed independent browser session. The browser evidence was validated from durable records; the independent rerun was the source/static-render builders.
- Protected-WIP guard passes **7/7**. `git diff --check` passes, with only the pre-existing LF/CRLF advisories. This reviewer edited no product, historical evidence or unrelated files.

## Renderer/dependency assessment

Home binds original `Ir`, `Lr`, `Kr`, `zn`, `Bn`, `qr` directly, with the actual current Home/shared UI declarations and the same canonical catalogs. Original `qr` starts at main byte **336694** and `Bn` at **213332**. The original button predicates remain observable: lifecycle and preference differences were not erased by a success-only fixture or broad normalization.

Map binds the actual memoized table `at` at Map asset byte **14837**, its 21 pure functions/three constants, original reward/icon/truck/image declarations, and actual current helpers/image component. Checked the original main export aliases independently: **Ct → Ie**, **St → Fe**, **Tt → De**, **o → Vr**. They correspond to the harness's truck helpers, i18n hook and icon renderer. Current scan-label keys are extracted from the actual declaration. Empty original image cache and absent current reader make these SSR comparisons placeholder comparisons; effects do not run and the original image reader is bound to throw if unexpectedly called. No native asset producer is inferred from a placeholder match.

The only structural normalization filters button `type`. This is valid for the isolated containers, which contain no surrounding form; it does not establish form-submission parity in another context. No disabled predicate, text, class, style or image output is normalized. Home's automated descendant description omits the section root itself; its matching root class was also checked directly in the original/current source. This is a small coverage boundary, not a current defect.

## Findings: valid / invalid / why / action

**Valid: preference save-editability mismatch.** Original `qr` supplies `Bn` only label/checked/onChange, and `Bn` defaults `disabled` to false. Current Home additionally disables a preference during `busy === "autoLaunchGame"` or `"autoReconnect"`. The distinguishing inputs keep `production:true` and defined booleans, so unavailable-provider fencing does not explain these four cases. No production change was made by this reviewer; retain the planned focused correction.

**Valid: removing only those disabled clauses would leave a producer/interaction mismatch.** Original `Sr` at **246842** and the `setAutoLaunchGame` property at **248381** write `lwbridge.autoLaunchGame` and update local state synchronously. Current `App.jsx` `updateAutoLaunch` waits for `local_config_set` before updating local config. The original reconnect binding uses `de = ue.draft` (main byte **362521**) and routes Home changes through `It` at **366485**, which immediately edits the selected profile's flag draft and then flushes. Current `updateAutoReconnect` changes local config only after acknowledgement.

Beyond reading those slices, independently executed the exact original setter with inert storage/state bindings: true then false produces immediate storage/state pairs in that order. Executed exact original draft factory `T` at **191297**, using its actual `w` JSON serializer at **191274**, with controlled inert writes: edit true/flush, edit false/flush while the first write is pending, then resolve the first acknowledgement. The writes are **[true, false]**, the newer draft survives the older acknowledgement, and final draft/confirmed are false. This confirms an observable optimistic/concurrent state contract without invoking a provider. `me` at **195132** subscribes to that store's snapshot; `re` at **193714** keys it by profile and flag. Exact transport compatibility, failure recovery and profile replacement still need their own focused proof.

**Invalid: interpreting the six lifecycle differences as renderer defects requiring controls to be enabled immediately.** Current lifecycle provider is unavailable and deliberately fenced. The differences are accurately recorded as unimplemented native reachability, not hidden by the comparator. This checkpoint does not authorize a native provider or fake a successful action.

**Invalid: interpreting eight identical browser pairs as complete original pixel parity.** They prove the named source-rendered isolated fixtures under identical browser conditions. Both use the same recovered CSS, empty navigation and fixture shell wrappers. They omit the complete original shell, Map header/filter panel, loaded assets, mounted async behavior and protected original runtime. README/inventory retain those limits and the larger campaign remains PARTIAL.

## Continuation

Accept the named equal-state Home layouts and Map table evidence. Keep Home preference optimistic/editable/concurrent state semantics open for a separate bounded correction with actual callbacks, failure/acknowledgement ordering and profile ownership. Preserve unavailable-provider fences and existing profile injection. Continue full-panel and other-page visual differentials; do not derive a completion percentage from these case counts.
