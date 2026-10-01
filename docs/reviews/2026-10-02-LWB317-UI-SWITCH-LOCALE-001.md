# Shared switch state descriptions — LWB317-UI-SWITCH-LOCALE-001

Date: 2026-10-02. State: **AWAITING_REVIEW**. Returning-worker delivery for
independent project-lead review.

Assignment checkpoint:
`1cfce34523b19c7fe0e40cc00466a8f3ba7ffe75`.

## Result

The shared production `ToggleRow` now gets `t` from the existing i18n context
and uses `common.enabled` / `common.disabled` for only the state suffix of its
aria-label. The label passed by clone callers remains unchanged and already
translated. Visible content, `role="switch"`, `aria-checked`, button type,
checked/disabled rules, `Switch`/CSS, all callers and the clone's optional
`onChange?.(!checked)` behavior remain unchanged.

No caller was refactored.

## Exact source and coverage

Target executable SHA-256:
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered asset SHA-256:
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
The checker independently locates recovered `Bn` at zero-based UTF-8 byte
213332. Its switch aria-label is the translated label plus translated
`common.enabled` / `common.disabled`.

`check-switch-locale.mjs` executes recovered `Bn` and actual production
`ToggleRow`. Because recovered `Bn` accepts a translation key while clone
callers already supply `t(labelKey)`, the clone side receives that translated
label value before the helper comparison. Across all nine catalogs, ten unique
caller labels, both checked values and both disabled values, all 360 render
comparisons agree. Enabled callbacks receive `!checked`; the clone's omitted
optional callback still does not throw.

The AST inventory finds 11 callers across the assigned families: Home (2),
Automation/Trade (1), Squads/AFK (4), Mini Games (1), and Settings (3). Every
caller still supplies its label through `t(...)`.

## Browser and regression checks

On the pre-existing local preview server, Settings Show FPS was observed
`off -> on -> off`; its aria-label changed
`Show FPS: Disabled -> Show FPS: Enabled -> Show FPS: Disabled` while visible
text stayed `Show FPS`. Japanese Home `home-connected` showed both disabled
checked switches with the `有効` suffix. `home-running-disconnected` showed
both disabled unchecked switches with `無効`. Attempting browser input on a
disabled Home switch was rejected with `BROWSER_ELEMENT_DISABLED`. Captured
browser console errors were empty.

The durable screenshot is
`evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001/japanese-home-disabled-unchecked.png`,
SHA-256
`C52854CEA6CA7C663E0A841DC89B448DB1BD599DC3D58E5547F98F211A5F8E24`.

Focused checker, HOME-ERROR-001, HOME-ERROR-002 and HOME-BUSY-001 saved-report
regressions pass. `npm.cmd run check`, `npm.cmd run build` and
`npm.cmd run check:production-build` pass. Build/package fingerprints are:
`53201aabb3833ec593a92f23ca6ced0f12bb3c645ad9dbfca159a32f1692f13e` /
`96b9dd1a99d67a6bf4dee3e39035915401de25395f99cdf2c0af5bbe4ccd9782`.
Evidence validation and `git diff --check` pass.

## Boundary and handoff

This is local recovered-source and browser-preview evidence. It does not prove
original post-auth pixels, native preference persistence, native lifecycle
transitions, gameplay, or authentication behavior. Global Home/UI remains
`IMPLEMENTED_NOT_VALIDATED`. The previous lead Home units remain
`AWAITING_REVIEW`; this delivery does not accept them.

The owned browser tab was closed after restoring English. Pre-existing port 4319
PID 62280 was retained. The assignment's unrelated
`previewAfkFixtures.js`, `.scratch-lwb317/`, and parent CORRECT-003 screenshot
WIP remain untouched and unstaged. No next task is assigned pending project-lead
review.
