# LWB317-UI-CORRECT-003D evidence

This evidence is limited to Trade Station's **Scan cross-server trade stations**
configuration switch. It pins the exact recovered 0.3.17 default, label,
offline-only disabled predicate and immediate-save helper, then executes the
production `TradeStationCard` callback against the recovered config-draft store.

`check-trade-cross-server.mjs` extracts the actual `save`, switch `onChange` and
disabled expressions from `Pages.jsx`, plus the actual shared Retry and Discard
callbacks from `previewConfigHook.jsx`. Its deferred adapter proves that an online
second toggle remains allowed while the first write is pending, that the newer
draft survives the older acknowledgement and is subsequently written, and that
`enabled`, selected goods and selected currencies remain unchanged. Separate
failed-write cases exercise the actual Retry and Discard callbacks.

`browser-results.json` records real browser interaction against the existing local
preview: online on/off toggles with retained Trade selections, fail-first-save
Retry and Discard, and the default offline-disabled state. The assignment asked
for focused browser QA without a new screenshot campaign, so this unit captures
no screenshots and changes no fixture inventory.

No cross-server gameplay, purchase execution, native provider, Last War control,
generic `ToggleRow`, shared draft-store behavior, accepted weekly settings, Trade
selection/composition or Purchased-items presentation is changed by this unit.
