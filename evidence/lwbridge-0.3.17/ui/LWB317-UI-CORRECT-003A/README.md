# LWB317-UI-CORRECT-003A evidence

This folder contains the focused weekly-quality evidence for Trucks and Secret
Task only. `source-locators.json` records exact recovered-source hashes and
weekly control locators. `browser-results.json` records the local browser
interactions and screenshot hashes. `check-weekly-state.mjs` independently
checks source locators, defaults, weekday order, preview validation, failed
save/retry/discard, and the deferred-save queue behavior. The lead-review
`check-weekly-onchange.mjs` now extracts and executes the actual production JSX
weekly callbacks plus the actual Retry/Discard button callbacks; its original
failing `onchange-baseline.json` remains preserved and `onchange-r1.json` records
the corrected R1 result.

The two screenshots are synthetic local preview data. No Last War/native
provider or gameplay action was launched.
