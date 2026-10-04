# Independent image caller and Trade label review — 2026-10-04

The migrated callers pass the bounded exact-source/local review. This lane did
not edit product files. Three source-backed Trade name-resolution differences
were reported to the lead and corrected in the lead-owned integration:

1. Goods icon alt/visible name used raw `item.name`; original `ue` derives its
   shared label with `C(gameTexts, nameKey, name, itemId)`.
2. Currency choices used raw `currencyName`; original `fe` resolves the retained
   first offer through `C`.
3. Goods possible-currency labels deduplicated by currency ID correctly but used
   raw names; original `ue` resolves each first retained offer through `C`.

`C` at UTF-8 byte 658 in `AutomationPanel-BJ0gIqFh.js` applies text overrides,
empty-name `#id` fallback and markup cleanup. Existing canonical
`resolveTradeName` matches that function. Currency-ID deduplication/ordering and
the first-offer rule remain intact.

## Executable proof

- `check-callers.mjs` / `caller-contract.json`: **360** current/original
  prop-expression and direct-guard comparisons, zero mismatches. Pins exact
  source bytes/hashes for **16 original callsites** and **15 canonical callsites**;
  canonical Scheduled Plunder shares one reward renderer for the two original
  groups. Includes icon paths, alt strings, class names, quality sprites,
  `deferUntilVisible` and direct optional-icon guards. Tests missing paths, missing
  names, markup names, text overrides and qualities 0/3 with controlled inputs.
- `check-trade-labels.mjs` / `trade-label-results.json`: **16** exact original
  Goods/currency renderer versus extracted actual canonical JSX comparisons.
  Captures rendered names, possible-currency labels and actual image props with
  inert bindings. Immutable Git baseline `147e5cf` differs on Goods names in 7
  cases, possible currencies in 7, currency choices in 8. Current matches all.
- `check-reader-boundary.mjs` / `reader-boundary-results.json`: actual App reader
  initializer returns null and invokes nothing for Preview/native-unavailable;
  available mode passes `game_asset_image` plus the source request to an inert
  bridge. Canonical Provider receives that reader.
- Independent read-only replay of `check-images.mjs`: **16** groups pass,
  including 13 original comparisons, 36 normalization comparisons, missing-reader
  fencing, reader-identity/cache isolation and inherited-reader/explicit-null
  behavior. This checks the actual current component, not a proposed abstraction.

The caller checker does not execute whole-page effects or prove the underlying
text/asset producers. The label renderer proof supplies actual controlled text
inputs. The reader proof never dispatches a real native operation. No available
game image files, current-game asset resolution, original protected-runtime
pixels or live gameplay/native functions are claimed. Empty Equipment slots
retain their original plain placeholder, rather than making an image request.

Rerun scripts without `--record` for read-only checks. Recording writes only the
named current packet outputs; source locators/hashes are refreshed against the
actual current files, without rewriting historical evidence.
