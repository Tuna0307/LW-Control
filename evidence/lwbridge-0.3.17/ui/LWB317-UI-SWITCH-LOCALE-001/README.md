# LWB317-UI-SWITCH-LOCALE-001 evidence

Date: 2026-10-02. Delivery state: AWAITING_REVIEW.

The exact recovered frontend asset is
`frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
`check-switch-locale.mjs` locates recovered `Bn` at zero-based UTF-8 byte
213332 and executes its switch branch against the actual production
`ToggleRow` from `Pages.jsx`.

The checker covers all nine catalogs, ten unique translated caller labels,
both checked values and both disabled values for 360 render comparisons. It
also inventories all 11 production callers, executes enabled callbacks with
`!checked`, and verifies the clone's existing optional-callback behavior.
The clone caller adapter is intentional: recovered `Bn` accepts a translation
key and translates it internally, while production callers already pass
`t(labelKey)`; therefore the differential passes that translated label into
`ToggleRow` and compares equivalent rendered output.

`browser-results.json` records the local preview checks. The enabled Settings
Show FPS switch completed off -> on -> off with its accessible state suffix
changing Disabled -> Enabled -> Disabled. Japanese Home fixtures show checked
disabled controls with `有効` and unchecked disabled controls with `無効`;
browser input rejects interaction with the disabled control. The durable
`japanese-home-disabled-unchecked.png` capture is local preview evidence, not
original-runtime pixel proof.

Reproduce the focused evidence with:

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001/check-switch-locale.mjs --verify-record`

`node evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001/validate-evidence.mjs`

No recovered checkbox-variant reconstruction, native lifecycle work, gameplay
action, auth action, or original post-auth runtime claim is part of this unit.
