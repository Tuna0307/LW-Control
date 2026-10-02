# LWB317-REVIEW-TRADE-STATUS-001 — independent Trade status review

Date: 2026-10-02. State: **AWAITING_REVIEW**, focused source/code/browser checks complete.
The source/local status presentation matches, but the independent review found
and corrected one browser-fixture fencing defect. This child must not accept its
own correction; project-lead review is the continuation point.

## Confirmed defect and narrow correction

`App.jsx` passes an empty preview state outside browser-preview mode. The old
`TradeStationCard` nevertheless called `previewTradeFixture("")`, whose default
branch returned synthetic goods, two purchases and `Success` counters. Disabled
controls did not prevent this positive QA data from appearing in native/offline
presentation. `native-fence-baseline.json` preserves the independently executed
original/current discrepancy: a fresh original offline component has empty goods,
absent status and `0 / 0 / 0 / -`; the old clone displayed `3 / 2 / 2 / Success`,
two visible synthetic goods and a Purchased tab with two synthetic records.

The correction is only in the owned Trade component and Trade fixture function:
the component passes an empty state when preview is disabled; the fixture returns
empty goods/history/texts, false loading, empty error and absent status for an
inactive preview. Existing disclosed Automation preview states retain their
previous data. Two additional full-component checks verify default inactive mode
and a supplied Trade preview string with `previewEnabled=false`. Neither uses a
native service or claims successful native behavior.

## Actual source and production evidence

The supplied target EXE independently hashes to
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered `AutomationPanel-BJ0gIqFh.js` hashes to
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.
The immutable original identity, UTF-8 byte locators, exact expressions, current
`TradeStationCard` hash and original locale property locators are in
`evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-TRADE-STATUS-001/independent-results.json`.

The independent checker evaluates the complete recovered Trade component and
the complete actual production JSX function, with isolated state hooks. It
expands the actual original goods and purchase-history renderers rather than
inferring retained row counts from fixtures. It invokes actual tab callbacks
and repeats renders after a status update. The full component is not a native
runtime simulation: configuration and asset providers are intentionally stubbed.

68 English/Japanese comparisons distinguish missing/null/sparse status, zero
and distinct counts, all four recovered result keys, Goods/Purchased/Goods
transitions, loading with and without retained goods, fetch errors with and
without retained goods, empty goods/history, and selected-tab preservation after
status updates. Counter defaults and result labels match; fetch error remains
outside either tab panel. Original English/Japanese locale text is independently
checked at exact source property offsets.

The actual original first fetch effect additionally executes six cases: success,
`Error` rejection, string rejection, cancellation followed by success/failure,
and offline. It clears the old fetch error at effect entry, sets loading without
clearing retained goods, stores `String(error)`, preserves independent purchase
status/history, and suppresses late writes after cleanup. The clone has no native
fetch producer within this unit; its disclosed browser fixtures render the same
states. These source contracts must not be mistaken for implemented fetching.

Useful exact source anchors: stats props object byte 8515; last-result selection
byte 7714; loading byte 9840; empty-goods byte 9928; error byte 10261. The complete
Trade component starts at byte 6352 and its fetch-effect call at byte 6721.

## Regression checks and coordinator browser evidence

Independent checker: `LWB317_REVIEW_TRADE_STATUS_OK` (68 comparisons, six original
effect cases and two inactive/native fixture checks). Existing actual-handler Trade selection, PM-011 first-offer/name
composition, purchase-history, cross-server switch and author 003E presentation
checks all pass on the current source. No accepted-control or history edits.

Coordinator recorded eight real browser observations: English
`automation-trade-loading-retained` through Goods → Purchased → Goods; Japanese
`automation-trade-error-retained` through the same transitions; English
`automation-trade-status-absent`; and default Automation → Trade without preview
state, showing zero counts, no QA goods, zero Purchased tab count and disabled
configuration controls. The inactive Purchased tab's inherited fieldset disabling
was confirmed and a click was rejected; the browser did not render inactive
purchase history. Its emptiness is proven by the actual component assertions.

The child independently inspected `loading-retained.png` (loading text, retained
goods and Success counters visible) and `error-retained-ja.png` (Japanese counters,
retained goods and supplied fetch error visible), in addition to coordinator
inspection. Screenshot hashes are in `browser-results.json`. No captured page
console errors. Full `validate-evidence.mjs` passes and checks return-tab rows,
statistics/error preservation, inactive fencing, exact expressions and image
hashes. Integrated check/build/package commands remain coordinator-owned and
must be recorded in the combined delivery.

## Conclusion and limits

No mismatch was found in the assigned preview counters, last result,
loading/empty/fetch-error, retained-data or tab-state render conditions. The actual
inactive-fixture leakage was corrected and awaits independent review. Recovered expressions
are `EXACT_BYTES`; matching tested source/local rendering is `EXACT_CONTRACT`.
Native goods fetching, purchases, persistence, game assets, protected original
runtime and original pixel comparison remain unverified or outside this unit.
Overall Automation/UI parity is not established by this review.

Files changed by this child are its isolated checker/evidence/review, one Trade
fixture call in `Pages.jsx` and only `previewTradeFixture` in
`previewAutomationFixtures.js`. No other card, master document, build output,
Git mutation or protected WIP was changed.
