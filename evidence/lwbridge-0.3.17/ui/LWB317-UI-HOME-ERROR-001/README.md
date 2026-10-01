# Home error translation checkpoint

LWB317-UI-HOME-ERROR-001, 2026-10-02, AWAITING_REVIEW.
Production change is limited to translatedError and three explicit browser-only
Home error fixtures in Pages.jsx. No callback, error channel, locale, CSS or native
provider changed. Synthetic fixture tokens are not actual-game failure findings.

`check-home-errors.mjs` evaluates actual original Ir/Lr and clone translatedError
functions, with 60 edge/namespace cases and 4,230 checks across nine locales. It
reconstructs original shared-plus-language locale objects (preserving overrides)
and verifies each complete clone catalog. Actual original and clone Home render
functions match 27 error states; no-error and missing-root guards are also checked.
Native mode cannot select fixtures. `--record` writes helper-render-results.json;
`--verify-record` checks the saved report against freshly evaluated source.

Run both commands from repository root:
`node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-001/check-home-errors.mjs --verify-record`
`node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-ERROR-001/validate-evidence.mjs`

Source hashes/byte expressions include Ir/Lr/Home and shared locale tables, plus
language-specific generic-error/recovery labels. Browser-results records five
observations: generic English, recognized embedded English, Automation/Home return,
Japanese recovery detail and no-error guard. Two images are hashed. No captured
browser console errors. Owned IAB tab 5 closed; pre-existing port-4319 server kept.

PM-015 evidence remains a historical failing baseline, not a current acceptance
checker. This task closes only its translation difference in implementation/local
checks. Error channels, busy labels and switch localization remain separate tasks;
native lifecycle/persistence and original runtime pixels remain unproved.
