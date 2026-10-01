# Trade status/loading/error presentation

Project lead completed this assigned unit after the owner requested takeover on
2026-10-02. Delivery state: **AWAITING_REVIEW**. This is an implementation and
local verification checkpoint, not an independent acceptance of the lead's work.

`source-locators.json` pins exact 0.3.17 panel and English locale hashes and UTF-8
byte expressions. `check-trade-presentation.mjs` evaluates recovered render AST
expressions against the actual production JSX on eleven fixtures and four sparse
status inputs. Run from the repository root with Node; `--record` regenerates
render/source reports. It also checks the exact fetch-error effect and original
result labels. Fixtures are synthetic browser QA; counts are not game findings.

`browser-results.json` records twelve local observations, including retained
goods, retained purchase history during goods-fetch failure, absent status, four
result labels and a real local configuration save failure. Each scenario was
navigated through its explicit preview URL, then the visible Trade Station tab
was selected. The history case clicked Purchased items; the final case clicked
the cross-server configuration switch. No native/gameplay control was used.
The final `panelErrorBlocks` field samples the shared save error; it is not a
goods-fetch error. The two images show retained goods during loading/fetch error.

`verification.json` records checks and build fingerprints. Run
`node evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003E/validate-evidence.mjs`
to verify JSON, source locators/hashes, observed state assertions and image hashes.

Existing owned test tab was closed. The pre-existing preview server on port 4319
and unrelated AFK/scratch/parent screenshot work were preserved. Original runtime
pixel comparison, native templates/status/images/persistence and purchase execution
are not established by this checkpoint. Parent CORRECT-003 remains PARTIAL.
