# PM-011 focused Trade review evidence

Reviewed implementation: a24bf6c736398e76c2e01451e6761e4d96fd28ec.
See docs/reviews/2026-10-01-LWB317-PM-011-trade-selection-review.md.

check-trade-composition.mjs verifies exact source hash/anchors and extracts the
actual TradeStationCard currency and goods-currency expressions using Babel.
composition-baseline.json records three demonstrated failures at the reviewed
commit. Its synthetic labels are QA data, not recovered game currency names.

Run from repository root:

    node evidence/lwbridge-0.3.17/ui/LWB317-PM-011/check-trade-composition.mjs

Exit 1 is the known pre-correction result. A worker must fix the production
expressions, preserve the baseline and record a passing result separately.
