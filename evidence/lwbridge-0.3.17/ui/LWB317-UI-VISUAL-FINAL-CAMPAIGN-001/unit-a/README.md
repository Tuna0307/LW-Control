# Unit A — Treasure label correction and evidence hardening

Status: worker-complete for Unit A; campaign continues to Unit B.

Production correction:

- `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx` removes only the two Treasure checkbox text `span` wrappers identified by the independent review. The inputs, callbacks, checked state and `setPage(1)` behavior are unchanged.

Evidence:

- `compare-toolbars-r1.mjs`: fresh actual recovered/current renderer replay, 34 required + 12 supplemental states.
- `browser/`: EN/JA, light/dark Treasure pairs. Descendant text nodes use recovered `LABEL.map-filter-field` ancestry, identical effective color and identical text rectangles. The independent lead pixel checker reports zero changed pixels outside the two provider-fenced claim buttons for all four pairs.
- `pixel-mutation-control.json`: the same independent checker rerun on the preserved submitted-defect packet detects the exact historical 1,734 outside-fence pixels.
- `action-messages/` and `action-message-browser/`: Dispatch/Ghost empty and populated message states in EN/JA. Message state is injected only through the test harness; no schedule/share action executes. All eight browser pairs are pixel-identical and have zero geometry/style/pseudo/text-run differences.
- `mounted/`: 34 mounted local interaction assertions across EN/light and JA/dark, including both Treasure checkboxes, dropdowns, filters, pagination and page-reset behavior. Zero console/page errors.
- `dependencies.json`: exact hashes for externally referenced replay inputs and toolchain identity.
- `validation-results.json`: self-contained Unit A acceptance assertions.

Validation completed on 2026-10-04:

```text
node unit-a/validate-unit-a.mjs
LWB317_VISUAL_FINAL_UNIT_A_VALIDATED
rendererCases=46
treasureBrowserPairs=4
outsideFencePixels=0
mutationOutsideFencePixels=1734
actionMessagePairs=8
exactActionMessagePixelPairs=8
mountedCases=34

npm --prefix src/LWBridge.UI-0.3.17 run check
LWB317_UI_DRAFT_CHECKS_OK

npm --prefix src/LWBridge.UI-0.3.17 run build
LWB317_PRODUCTION_UI_BUILD_OK

npm --prefix src/LWBridge.UI-0.3.17 run check:production-build
LWB317_PRODUCTION_UI_PACKAGE_OK

node evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs
LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10
```

The owner listener on `127.0.0.1:4335` was observed and left untouched. Unit A mounted QA used a separate temporary Vite listener on port 4336.
