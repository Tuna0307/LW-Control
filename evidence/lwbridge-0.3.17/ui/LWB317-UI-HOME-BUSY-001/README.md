# Home busy presentation evidence

LWB317-UI-HOME-BUSY-001, 2026-10-02. State: AWAITING_REVIEW.

check-home-busy.mjs extracts actual original Kr/qr and actual clone HomePage JSX.
It compares 13,824 render combinations in all nine catalogs, including all eight
independent root/proxy/launch flag combinations, null/invalid/valid root, running,
online, repair and eight recovery states. It verifies 384 actual clone predicate
expressions against original Kr using an isolated available-provider test input;
actual product lifecycleProviderAvailable remains false throughout render tests.
It also checks 27 preference/root-busy isolation cases and 63 preview fixtures.
Run from repo root with --verify-record to compare busy-results.json.

busy-results.json pins exact original source hash/byte locators and source state
producer/consumer anchors. Current App is unchanged and its normalized LF hash
is pinned. Production busy writes are gameRoot, autoLaunchGame, autoReconnect;
proxyBusy/gameLaunchBusy have no production lifecycle producers in this clone.
Recovered display inputs and explicit preview fixtures do not prove live states.

browser-results.json records nine local browser observations, including navigation
retention and Japanese launching text; two screenshots are hash-pinned. Native
controls remained disabled. verification.json includes canonical build/package
fingerprints. validate-evidence.mjs verifies source/producer hashes, counts,
browser output against actual render results, screenshots and verification data.

Original pixels, native/gameplay lifecycle and state reachability remain unproved.
No lifecycle event handlers, native providers, account UI or fallback introduced.
