# LWB317-UI-CORRECT-003C evidence

This evidence is limited to Trade Station Purchased-items presentation. It pins
the exact recovered 0.3.17 history renderer/name resolver/caller, exercises the
production history helper used by `TradeStationCard`, and records real browser
interaction against the existing local preview.

`check-trade-history.mjs` validates the exact source hash and UTF-8 byte anchors,
reversed input order, adjacent-only day grouping, explicit and fallback day
boundaries, daily/repeated-item quantities and order, name cleanup, row-key
fallbacks and missing optional-field behavior. It also asserts that the actual
`Pages.jsx` renderer consumes those production helpers and recovered predicates.

`browser-results.json` records the English populated and empty Purchased-items
flows plus a Portuguese locale check. `populated-history.jpg` and
`empty-history.jpg` are the only two screenshots captured for this unit.

Game item, quality-frame and currency images continue to use the clone's existing
asset placeholders because native asset delivery is not implemented. The QA
purchases are synthetic and no purchase execution, Last War interaction, native
provider work, original post-auth pixel comparison or full UI parity is claimed.
