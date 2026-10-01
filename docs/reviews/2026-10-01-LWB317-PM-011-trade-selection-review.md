# PM-011 — Trade selection review and two-expression correction

Date: 2026-10-01. Reviewed worker commit:
`a24bf6c736398e76c2e01451e6761e4d96fd28ec`.
Disposition: **CHANGES_REQUIRED for CORRECT-003B**, limited to currency data
composition. Retain the successful saving-lock correction and all weekly work.

Local HEAD and direct origin branch lookup matched the worker commit. The only
product changes in that delivery remove the config.saving locks on currency and
goods checkboxes. Those changes match the original explicit saving:false callers
at UTF-8 bytes 8412 and 10054. The actual production-handler test passes immediate
write, last-currency protection, queued currency editing, goods selection and
final-good disable, plus actual Retry/Discard callbacks after failures.

Two existing composition mismatches remain within the assignment's currency/
goods selection scope:

1. Original AutomationPanel-BJ0gIqFh.js at byte 5605 adds an offer to the currency
   Map only when its currency ID is absent, retaining the first offer's metadata.
   Clone TradeStationCard constructs a Map from every offer pair, retaining the
   last offer for repeated IDs. Keep positive-ID filtering and ascending ID order.
2. Original goods renderer at byte 1197 deduplicates offers by currencyId and
   renders the first name for each ID, in encounter order. Clone possibleCurrencies
   deduplicates names. It can show two names for one ID or collapse two distinct
   IDs with the same name. Deduplicate by ID, not displayed text.

Exact source hash:
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.
The lead's saved check extracts and evaluates these actual production expressions
with synthetic metadata; all three baseline cases fail. These values are QA
inputs for an EXACT_CONTRACT discrepancy, not claims about current game data.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-PM-011/`.

Independent verification passed for the delivered saving fix:

- Worker focused Trade actual-handler/source-hash/byte-anchor check.
- Canonical npm.cmd run check, build and check:production-build. Production
  source fingerprint 4d9f35dc87ec38afa1ae1743bfbfdaf262966b0e19e1523f2e6723d7ec53b8a9;
  artifact fingerprint 4bad0446a04ec3f6295c3b3c6896ce5dd7d9ed0021b6bca420ee86ff40a3cfd8.
- All three worker screenshot hashes match browser-results.json. The lead viewed
  the exclusive screenshot; browser interactions remain worker evidence, with no
  new independent browser or game session in this review.
- git diff --check.

The package includes the preserved uncommitted AFK fixture, so it does not accept
that WIP. previewAfkFixtures.js, .scratch-lwb317/ and parent CORRECT-003 screenshots
remain unchanged and unstaged. Parent CORRECT-003 remains PARTIAL. UI matrix and
ledger retain IMPLEMENTED_NOT_VALIDATED / BLOCKED for original/native parity.

Next assignment: CORRECT-003B-R1, only the two currency composition expressions,
actual-expression regression proof and a small existing-preview recheck. Purchase
history, cross-server settings, runtime summaries, Assist, AFK and native/gameplay
work remain separate. No fixed work-block deadline or automatic worker dispatch.
