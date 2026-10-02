# Map Treasure picker recovery

Lead-owned bounded UI unit, starting product ff4ed369. Source authority:
MapDataPanel-B4GXEND2.js, SHA-256
CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
UTF-8 locators: renderer lt begins 29030; name resolver Ke begins 7610;
parent caller begins 52026. Existing reference.css supplies map-item-filter,
summary/menu/button/active styling; no CSS or catalog changes were made.

Production replaces the native Treasure select with a source-like details/menu.
The summary resolves the strictly matched selected option's name, or All when
unmatched. Menu order follows items, names use treasureName (the recovered Ke
resolver), counts render raw source values, active classes preserve truthiness
and strict equality (including the original numeric-zero oddity), and clicks
call onChange with the raw key before removing the open attribute. Parent still
resets page 1 and preserves accepted query/preference/filter lifecycle behavior.

check-picker.mjs invokes the ACTUAL original lt function captured from the exact
original-component runner and compiles the ACTUAL canonical component with the
same inert JSX/ref/i18n inputs. It compares element/attribute/text structures,
button order and each callback's raw key / change-before-close trace. The 28
cases cover English/Japanese, empty/populated, missing selections, numeric/string
keys, zero/null/missing counts and name/supplies/trial/unknown resolution. Actual
parent callbacks check page reset and numeric/string query projection. The
immutable ff4ed369 page reproduces the select-versus-menu baseline mismatch.

Browser results are fresh project-lead UI actions against an owned offline Vite
preview: open/select/close/reopen/active label and real Enter activation for both
summary and All. EN/light and JA/dark screenshots were saved and inspected.
Counts change 45 -> 30 -> 45 and JA Radar -> 15件. Console errors/warnings are
empty. Fixture names/counts are disclosed synthetic data, not native data.

Run from repo root:

    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/check-picker.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/replay-parent.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/replay-r1.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/replay-historical.mjs
    node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/validate-evidence.mjs

Historical scripts/assertions/results remain unchanged. picker-harness supplies
only a virtual select test entry point that calls the actual new parent's menu
callback for the old lifecycle cases. Menu behavior is tested separately above.
jsx-binding supplies the new child identifier to the historical fixed-binding
filter harness, which never renders the child; existing assertions are unchanged.
Output redirects preserve historical parent/R1 records. Old hash-pinned evidence
validators describe old product versions and are not updated to claim current
acceptance. A temporary R1 checker output written during lead regression was
restored to its known initially clean committed blob; fresh results live here.

Limits: source/component and offline-browser UI proof only. No original post-auth
pixel comparison, native Treasure viewer/refresh/claim producer, scan/gameplay or
native persistence proof. Overall UI remains IMPLEMENTED_NOT_VALIDATED. Remaining
Map scan/header/feedback and other UI gaps retain separate work ownership.
