# LWB317-UI-CORRECT-003B evidence

This evidence is limited to Trade Station selection controls. It compares the
current implementation with the exact recovered 0.3.17 frontend assets, invokes
the production Trade selection callbacks from `Pages.jsx`, invokes the shared
production Retry/Discard callbacks from `previewConfigHook.jsx`, and records
real browser interaction against the existing local preview.

`check-trade-selection.mjs` verifies the pinned source hashes and byte anchors,
the recovered defaults/restrictions/save timing, currency last-selection
protection, goods select/unselect including final-good disable, a newer edit
while an older currency save is pending, and selection failure recovery through
the actual Retry and Discard callbacks.

`browser-results.json` records the positive, exclusive-only-good, injected
save-error, and empty-goods browser observations. The screenshots are direct
captures of those focused Trade Station states.

Purchase history, other Automation panels, weekly settings, native persistence,
original-gameplay behavior, and live Last War interaction are outside this
evidence unit.
