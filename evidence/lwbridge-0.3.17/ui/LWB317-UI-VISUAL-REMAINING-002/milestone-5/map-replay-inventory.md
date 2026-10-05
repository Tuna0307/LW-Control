# Map inherited proof and replay inventory

Research snapshot: 2026-10-05, `research/offline-controller`, repository HEAD at replay time `52dd0e353a9525abbf9e04f022cf5ffbf3371eed`. This note is evidence research only. It does not re-pin, rewrite, or relabel any accepted parent packet.

## Parent Unit B authority

The exact inherited Map composition packet is:

`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/`

Its recorded production checkpoint is `8bfd58efdc0502e6c732ba9f7c7ba35b8e521f4d` (`current-manifest.json`). The packet is the accepted finite Map/Scheduled oracle referenced by milestone 1; M5 must reuse it rather than rediscover the eight table renderers or Scheduled state machine.

Reference/source identities pinned by Unit B:

| Source | Identity / locator |
| --- | --- |
| Reference executable | `lwbridge-0.3.17.exe` SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` |
| Recovered Map asset | `frontend-package/web/assets/MapDataPanel-B4GXEND2.js` SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089` |
| Recovered main asset | `frontend-package/web/assets/index-BVfnK1wp.js` SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6` |
| Recovered CSS | `frontend-package/web/assets/index-rIL9Fpht.css` SHA-256 `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545` |
| Recovered `GameAssetImage` | `GameAssetImage-Diy9VTIr.js` SHA-256 `2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0` |
| Scheduled Dispatch/Ghost renderer | `ot`, Map asset UTF-8 byte `20712`, length `3679` |
| Scheduled Truck renderer | `st`, Map asset UTF-8 byte `24391`, length `3841` |
| Scheduled helper tables/functions | `b` `996+928`; `Te` `2671+517`; `x` `1925+174`; `Ce` `2099+93`; `we` `2192+475`; `S` `3189+174`; `C` `3363+234`; `F` `8753+246` |
| Main-asset Truck helpers | `Fe` `201275+124`; `Ie` `201399+307` |
| Integrated recovered page slices | `R` `29826+27748` SHA `8343C829...3905`; `ct` `28232+798` SHA `833BCF69...2152`; `lt` `29030+796` SHA `0DF7F6BD...BC9`; `it` `14469+364` SHA `F11B14C6...E8A` |

The recorded Unit B current source was `MapDataPage.jsx` SHA `46C6DEFD2A5D7AF7A50E9587486EAEDE68F9020573F2ED8EFE9C38287EEF66DF` (61,517 bytes), with `MapTreasureTypeFilter.jsx` `675E0BF8...2AB7` and `MapRetainedGoodsFilter.jsx` `D890612C...7C51`.

That `MapDataPage` pin is intentionally historical now. The current file is SHA-256 `0619C0589FED7A6DA37C7018D021D890A2A2396AC77D21A64E6D77CE348A5BD5` (61,566 bytes), which is exactly the later accepted Unit A lead-audit pin. The six-line change after the Unit B checkpoint removes the non-source query-error banner and the two Treasure text `span` wrappers. It is an accepted parity correction, not an M5 regression. `MapTreasureTypeFilter.jsx` and `MapRetainedGoodsFilter.jsx` still match the Unit B hashes.

The Scheduled canonical files currently still match the hashes recorded in `results.json`:

| File | Current / recorded SHA-256 |
| --- | --- |
| `ScheduledPlunder.jsx` | `9D6814A70A62C9198720CE15FAA84403F43E1DE172A88E285179151D017B5534` |
| `GameAssetImage.jsx` | `2BFCA11136F2495780EA6A2037BD5007F1D7653284849452E430D2A4FA05D379` |
| `mapPlunderPresentation.js` | `6E4BF5315990966823C5E162CF4875C9D1DB3A897CD7BACC40752C9A7C2A62FD` |
| `mapTablePresentation.js` | `B6C0D9B369D3E78CCC74AA7296F2B88EABF6B6560C3669A199546FC299839427` |
| `mapPlunderFixtures.js` | `73B6BF73DB557088BDC8BC45286095F61B0AF7C829A55E6C1B5BC9E1ABB72B60` |

## Result identities and accepted Map proof

The material Unit B result files are unchanged historical evidence:

| Result | SHA-256 | Accepted content |
| --- | --- | --- |
| `results.json` | `82B915C2448B8AC95A5A9EECDD8D190EF55E7866DEAEA2BF21EF5F0FD648D716` | `LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK`; 10,482 renders; 61/61 fixture branches; 51/51 mutations detected |
| `current-results.json` | `5091662AF2351D5ACA7B6560CDE72366920BC79144CDDF0EF5A9CB06FE585AFB` | `LWB317_VISUAL_FINAL_UNIT_B_INTEGRATED_MAP_EXECUTED`; 70 required + 5 supplemental = 75 cases |
| `integrated-browser-pairs.json` | `F235F2F532584B79C7C1DFAB89DFB9CF11DBEB1E1F714D4B1701FE41396162C9` | 16 recovered/current browser pair definitions |
| `browser/comparison-results.json` | `240DA8B3540BE6E7A15C25DC88A9294C4337C2E667AA979B78472E94DE3EEFB1` | 16 captured pair comparisons |
| `pixel-results.json` | `47D7915755C44444A4BDF882A5EFFA608C1A8AA225C8099E0B176BCD29005EBB` | historical 13 exact + 3 accepted-difference classification; zero pixels outside then-accepted regions |
| `mounted/browser-interactions.json` | `8FAE02311A7C132B5DDDB370D032DEB7E4A637686EDF865CD0BABDD49BE1CA7D` | 25 mounted local assertions, two screenshots, zero console/page errors |
| `current-manifest.json` | `0309E2AD518290F80B487D75498F50B27F0C60689E09543CBCE517612381240F` | original/current source and tool identity packet at Unit B checkpoint |

Accepted inherited coverage is finite:

- All eight data tabs (`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`) execute empty/loading/populated/query-rejection in EN and JA: 64 required integrated cases.
- Scheduled Plunder adds empty/populated/conditional in EN and JA, reaching all recovered Dispatch/Ghost/Truck row/status/timing/result/action branches. Together with the normal tabs this is 70 required cases.
- Five supplements cover EN Auto configured, JA Auto configured, missing server, backend unavailable, and Scheduled provider fenced, giving 75 integrated page cases.
- The Scheduled differential executes the exact recovered `ot`/`st` renderers against canonical groups for 10,482 renders, 61 explicit source branches, timing boundaries, error/result maps and action predicates. All 51 seeded source/plausible mutations are killed.
- Historical browser proof is 16 pairs / 32 PNGs across populated data tabs, empty/loading/error, Scheduled empty/populated/conditional and Auto configured, including desktop light/dark and narrow JA/light.
- Unit B originally classified 13 pairs exact and three bounded differences. The later independent Unit A lead audit rejected the broad City query-error mask, recovered the exact source catch at Map asset byte `39114+93`, corrected production and produced three fresh City error pairs that are byte-identical without masks. Therefore the inherited City mismatch is a preserved positive-control baseline, not an accepted current difference. The two Scheduled Start Scan differences remain the source-classified native-availability fence.
- Mounted Unit B proof exercises all nine result tabs, Scheduled tables, Manual/Auto switching, EN/light and JA/dark with 25 assertions and no native/gameplay click.

Accepted shell integration proof supplements Unit B and should be inherited rather than recreated from scratch:

- `LWB317-UI-SHELL-RETENTION-001`: accepted first-visit `Activity`, same-route identity, profile reset boundary and hidden/return effect ownership. It includes six actual mounted Map/App cases, three maintained Map replays and a browser Truck page-2 return. Map query/options/server/tab/page survive route hide/return; hidden obsolete searches are fenced; options acknowledgements retain source behavior; current server props update on return; no duplicate timer/listener owner is accepted.
- `LWB317-UI-SHELL-MAP-ENTRY-001`: accepted original `Tt` at main-asset byte `364377` starting `map_summary` before preload/transition without awaiting settlement. Recovery 5/5, focused App 10/10 and four affected checks passed at acceptance. Active-Map re-click stays inert and direct initial Map keeps its independent bootstrap.
- `LWB317-REVIEW-SHELL-CROSSSERVER-001`: accepted popover/parent ordering and rejection behavior. The independent original/current runner passed 11/11 cases; lead current checks passed focused 12/12 and affected retention/Map/Home 3/3. Successful ordering is jump -> summary -> changed-history/acknowledgement -> close; summary rejection is tolerated; independent App polling remains the owner.

These packets close page-local and earlier shell-local behavior. They do not substitute for the final M5 whole-App ancestry after later App/profile/shell edits.

## Read-only replay status on the current dirty M4 tree

The safe Unit B commands from repository root are:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/validate-unit-b.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/check-scheduled-current.mjs
```

Both were run on this snapshot and pass. `validate-unit-b.mjs` reports 10,482 Scheduled renders, 51/51 mutations, 75 integrated renderer cases, 16 browser pairs, 13 historical exact pairs, three historical accepted-difference pairs, zero outside-accepted pixels and 25 mounted cases. It is a saved-packet consistency validator: it verifies the executable and stored result markers/counts but does **not** hash the current `MapDataPage.jsx`, so its PASS must not be described as current whole-Map replay.

`check-scheduled-current.mjs` is the live read-only current-source replay when invoked without `--record`. It completed in 321.8 s with `LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK`: 10,482 renders, all 61 fixture branches, all 51 mutations detected, zero survivors and zero React warnings. A stronger record-equality form is also read-only but repeats the same expensive suite:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/check-scheduled-current.mjs --verify-record
```

It was not redundantly rerun in this research pass because the live suite passed and every Scheduled canonical file hash still equals the recorded identity.

The following accepted read-only integrity validators were also tested and **currently fail at expected source-pin boundaries**, not at a demonstrated Map behavior assertion:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/validate-read-only.mjs
# fails first at App.jsx length: current 36,583 vs frozen 33,340

node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-RETENTION-001/milestone-c/validate-current.mjs
# recorded source identity differs from current App.jsx / MapDataPage.jsx / Pages.jsx after later accepted/in-progress integration work

node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-c/validate-current.mjs
# current App SHA E4039BEE...A93C vs packet pin 210842F7...6131

node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/validate-current.mjs
# current App SHA E4039BEE...A93C vs packet pin 7FD95D74...7F5C
```

The Unit A lead audit intentionally pins a conservative dependency superset, so current M4 App work invalidates it even though current `MapDataPage.jsx` still exactly matches Unit A's accepted `0619C058...A5BD5` pin. Likewise the old shell validators are valuable immutable acceptance packets; their source-identity failures are the reason M5 needs a new final-App checkpoint rather than refreshed historical hashes.

Do not run the following Unit B evidence producers in place during M5 read-only replay:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/compare-integrated-map.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/prepare-integrated-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/capture-integrated-browser.mjs
python evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/check-browser-pixels.py
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/mounted/browser-interactions.mjs
```

`compare-integrated-map.mjs` hardcodes `outputRoot = here` and rewrites `raw/*` plus `current-results.json`; the browser preparation/capture scripts rewrite generated HTML/screenshots/comparison JSON; `check-browser-pixels.py` rewrites `pixel-results.json` and diff masks; mounted interactions rewrite screenshots and `browser-interactions.json`. Historical outputs must remain immutable. M5 should use a new task-local adapter/output directory (or a copied scratch repository preserving the same relative dependency tree) and run the same oracle logic there. Any new current pin belongs under M5; do not add a flag or refreshed hash to the accepted parent scripts/results.

## Minimal final-App Map journey matrix still required

Milestone 1 already closes table/Scheduled finite-state discovery. Four final-App browser journeys are the minimum useful set because the global inventory independently requires EN/light desktop, JA/dark desktop, EN/dark narrow and JA/light narrow. The cases below cover every remaining Map integration obligation without multiplying page-local states already proved by Unit B.

| Journey | Final-App actions and assertions | Requirement closed |
| --- | --- | --- |
| `MAP-J1` EN/light desktop | Start outside Map with the inert Cross-server fixture. Exercise successful popover action and assert the accepted jump -> summary -> changed-history/acknowledgement -> close ordering, then enter Map and preserve summary-before-transition/non-blocking rejection semantics. On a populated normal tab (Truck is useful because inherited browser proof already has page 2), apply a real local keyword/Search plus a source-owned filter, confirm result count and pagination, go to page 2, switch away to another top-level route, then return. The same Map tab, query/filter/page/count must be retained and exactly one page visible. | Cross-server -> Map ordering; search/filter/count/pagination; Map tab ownership; route-return retention. |
| `MAP-J2` JA/dark desktop | Establish a source-valid Scheduled populated/conditional state, switch Scheduled <-> normal data tab and Manual <-> Auto through the real controls, edit only local Auto configuration, and assert native Start Scan / Scheduled action fences remain disabled/inert where the provider is unavailable. Hide Map by navigating to another retained route, change language/theme while Map is hidden, return in JA/dark, and assert retained tab/config plus current translated/theme presentation with no duplicate Map request/timer/listener owner. | Manual/Auto local controls and fences; Scheduled composition inheritance; hidden `Activity` effect ownership; live locale/theme on a retained page. |
| `MAP-J3` EN/dark narrow | Build non-default Map state (normal result tab + keyword/filter + non-first page, and any source-owned local selection needed by the runner). Change selected profile through the real shell boundary while Map is active or retained. Assert the top-level route/visited ownership remains App-owned, while the keyed profile page subtree resets Map tab/query/page/local drafts to recovered defaults, retires old-profile in-flight ownership and mounts one new owner. Stay within the accepted narrow shell width and assert no horizontal shell overflow. | Profile identity reset; App-owned route visitation vs page-owned Map state; stale request/effect fencing; EN/dark narrow. |
| `MAP-J4` JA/light narrow | Render a whole-shell availability case using the disclosed offline/inert provider: missing-server or backend-unavailable is sufficient if it reaches the real final App ancestry. Confirm the page owns the correct Map tab/local mode, native-backed controls stay fenced, no synthetic success is shown, then route away/return once and verify the availability state does not create a second owner. Use the second required narrow locale/theme pair and assert zero console/page errors. | Whole-page missing-server/backend fence; local ownership under unavailable provider; second narrow locale/theme; return/owner sanity. |

Across the four journeys, record one fresh final-App source manifest after M4 is stable, current browser console/page errors, actual route/profile/theme/locale state, and owner counts around hide/return/reset. The final M5 packet can reference Unit B for the 8-tab 64-state matrix and exhaustive Scheduled proof; it only needs representative whole-App states that prove composition/ownership still holds under the final shell.

## Current blocker / continuation

No current Scheduled renderer failure was found. The concrete blocker to declaring inherited Map proof fully replayed is **source identity drift in the final App ancestry**: old Unit A/Shell retention/Map-entry/Cross-server integrity validators correctly reject the current dirty M4 tree because `App.jsx` and related shell/source files have changed since those accepted checkpoints. Unit B's saved validator still passes but does not hash current `MapDataPage.jsx` and therefore cannot close that gap by itself.

M4 subsequently stabilized and was committed/pushed at `1a7d863b536f8da67c2abc1b7dfbec483ec36d81`. M5 now consumes that exact App ancestry and keeps every historical parent hash unchanged.

## Final M5 replay resolution

The final M5 packet resolves the blocker above with task-local read-only adapters. `replay-map-pixels-read-only.py` consumes Unit B measurements/comparison JSON and PNGs without writing them; it replays the 15 unaffected historical pairs as 13 pixel-exact plus the two Scheduled Start Scan native-availability fences, with zero pixels outside the measured button boxes. The historical `city-error-en-dark` mask is explicitly excluded from final acceptance because Unit A later corrected the source mismatch. `capture-corrected-error.mjs --read-only` is replayed separately and returns `CORRECTED_SEARCH_REJECTION_EXACT` for three EN/JA desktop/narrow City rejection pairs with no masks or issues.

`replay-map-entry-current.mjs` executes the current `App.jsx` through an M5-local inert harness at SHA `E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`. It proves the final App starts Map summary before preload/route transition, a rejected summary does not block or roll back navigation, active-Map re-click does not create another entry request, unavailable transport still allows local navigation, and a pending old-profile Map response is ignored after replacement while the replacement profile bootstrap becomes authoritative.

The current real-App browser packet supplements the inherited finite matrix with Map integration state under the final shell. A populated fixture proves 52-result count, page-2 navigation and route-return retention. A JA/dark Auto journey proves Manual/Auto composition and local Auto retention with provider actions fenced. The real profile boundary keeps the App-owned Map tab while resetting child keyword, scan mode, quality/plunderable filters and profile-scoped Auto interval; request retirement is independently executed by the current-App harness above. Together with Unit B, the corrected City replay and the final 67-file dependency manifest, there is no remaining source-local Map composition gap in the recoverable UIUX scope.

Native Last War actions, Scheduled job production, protected runtime, loaded game assets and positive provider-backed gameplay remain fenced exactly as in the parent evidence.
