# AFK / Squads finite remaining branch inventory

Authority: target EXE SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` and
`frontend-package/web/assets/SquadPanel-HC3-DJei.js` SHA-256
`ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`.
Current `SquadsPage.jsx` SHA-256 is
`9A6FEC76B71513F975F1669DE82B7DFCB1BE65E9C483C37E7D4ACA54F0E5919E`.

Exact recovered renderer locators used by this inventory:

| Renderer | UTF-8 byte | Bytes | SHA-256 | Meaning |
| --- | ---: | ---: | --- | --- |
| `pe` | 4,586 | 11,256 | `4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85` | Garrison compact card, settings, runtime and ally modal |
| `me` | 15,842 | 687 | `7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8` | Zombie Bus assignment table |
| `he` | 16,529 | 1,957 | source-pinned in the same asset | Zombie Bus compact/settings/error composition |
| `I` | 28,070 | 22,554 | `0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E` | complete AFK content composition |

The accepted editor packet separately freezes the exact `I` editor subtree and its
dependencies. This inventory does not reopen those accepted corrections.

## Accepted/reusable proof, not remaining work

| Branch | Current locator | Existing proof | Remaining limit |
| --- | --- | --- | --- |
| Equipment tab finite presentation | `SquadsPage.jsx` `EquipmentContent` | parent Unit D: recovered `fd` versus current, 16 SSR states / 20 paired browser captures; source replay 105 + R1 61 + mounted checks | Native equipment read/apply and physical pointer HTML5 drag remain outside UI/source-local scope |
| Compact AFK card primitive | `CompactAfkCard`, lines 318–335 | parent Unit D: 8 SSR states / 16 paired captures exact after gear SVG, enabled class and label propagation correction | Does not prove five-card toolbar composition or panels beneath it |
| AFK profile editor subtree | `AfkProfileEditor`, lines 47–94 | `LWB317-UI-AFK-EDITOR-VISUAL-001`: 24 exact renderer states, 12 exact unmasked pairs, 267 behavior checks, 28 mounted assertions | Does not prove surrounding profile card/runtime list or Garrison/Drill/Zombie compositions |

## Remaining source/local branches

| Branch family | Recovered source condition / action | Current locator | Existing proof | Missing executable proof / correction gate |
| --- | --- | --- | --- | --- |
| Full toolbar composition | `I`: Master, Potion, Alliance Drill, `pe` Garrison and `he` Zombie Bus shown together; selected panel `k` determines card selection and body | `AfkContent` lines 280–294 | compact cards isolated only | Execute the complete recovered `I` toolbar with source-valid config/runtime inputs against canonical `AfkContent`; pair browser pixels in four required locale/theme/viewport modes |
| Master selected/default panel | `k === null`; Master card is selected while profile list remains visible | `AfkContent` Master `CompactAfkCard`, lines 284, 295–304 | no full pair | Full composition pair including profiles and config-save banners |
| Potion panel | `k === "potion"`; min stamina + prefer-fifty; busy state disables edits | lines 285, 290 | compact card only | Exact original/current full pair; exercise number edit/blur and prefer-fifty local edit/flush; saving/error overlap |
| Drill panel empty/valid order | `k === "drill"`; selected squad order, active-rally toggle, zero-squad validation | `AllianceDrillPreviewSettings`, lines 96–114 and `AfkContent` 286/291 | no full pair | Execute original `I` branch for selected/unselected squads, validation and reordered squads; mounted local checkbox/keyboard/drag-handler interactions |
| Drill runtime summary/details | `ke` rows with `activity === allianceDrill`; leader / joiner / waiting summary; `waiting_join_delay` details | `previewDrillRuntime` consumed lines 100, 275–277, 286 and 113 | no original/full pair | Original/current runtime-row differential and browser pair; disclosed synthetic producer rows only |
| Profile list empty / populated | `!S.length` versus mapped profile cards | current lines 295–304 | editor only | Pair empty, one/multiple, selected/unselected, farm/join cards in full `I` ancestry |
| Profile enable states | original label classes include `is-enabled`, `is-disabled` for busy or no squads; checkbox disabled for `B` or zero squads | current profile card line 301 | source replay 103 does not pair full markup | Full original/current pair for enabled/disabled/no-squad/saving; correct only demonstrated markup/style differences |
| Profile busy/save state | original `B` disables enable/delete/drag and contributes disabled styling; profile config save error is rendered by recovered config-state error surface | current `usePreviewConfig` and profile buttons, lines 237–263, 301 | current save-error fixture exists; no full original pair | Preserve immutable failing baseline, pair saving/error/Retry/Discard composition and local edit retention |
| Profile drag states | original card adds `drag-over`; drag button uses recovered drag glyph and is disabled while busy; ArrowUp/ArrowDown reorder | current line 301 uses no `drag-over` card class and text `↕` for profile drag | no accepted full pair | Demonstrate original/current DOM/pixels before correction; mounted keyboard reorder and DOM drag lifecycle after correction; do not claim physical pointer HTML5 drag |
| Per-profile runtime rows | original `I` lines represented in parent `I.pretty.js` 195–196: one row per bound squad with running/completed/idle step, join-wait target/seconds, execution count and last error | current profile cards have no runtime rows | none | Concrete missing-composition candidate: execute original runtime cases, preserve failing baseline, add only source-proven row presentation, then exact browser pairs |
| Add-profile menu | source menu closed/open; farm and join availability predicates; Escape closes menu; add selects new editor | current lines 243, 248, 298 | editor new-state only | Full source/current menu pair + actual local open/Escape/add action and new-profile composition |
| Target discovery around card/editor | source target discovery unavailable/missing changes add availability, source badge and editor selection | current lines 267, 298–303 | accepted editor target states cover editor subtree | Pair surrounding profile list/add-menu composition; reuse editor proof rather than reopen editor |
| Garrison compact closed/open summary | recovered `pe`: enabled summary uses guarding count / available-target bound; offline/card busy predicates | current generic card line 287 + preview runtime | compact primitive only | Execute actual `pe` plus current full Garrison branch for online/offline, disabled/enabled, closed/open, busy/error |
| Garrison buildings | `pe`: no buildings online/offline message, selected count, unavailable/full/season-ended roles, localized names | `GarrisonPreviewSettings` lines 175–200 | current-only fixture coverage | Original/current branch pairs with source-valid building rows; no native discovery claim |
| Garrison allies closed/modal | `pe`: choose disabled offline; selected-allies empty is recovered muted span; modal search, availability, checkbox, confirm | current lines 181–199 | current local modal exists, no original full pair | Source pair open/search/selected/unavailable/cross-server; mounted search/check/confirm/Escape/backdrop only where recovered shared dialog proves it |
| Garrison target/squad priority | `pe`: target and squad drag order; squad hint remains the recovered hint surface; enabling with missing target/squad creates validation | current lines 184–198 | no full pair | Pair empty/valid priority, validation and reorder states. Current zero-squad required-message placement is a candidate difference to baseline before edit |
| Garrison runtime/error | `pe`: assignment empty/populated, guarding ratio, config-save error, inline action error, busy toggle/Run Now | current lines 188–198 | no full pair | Pair assignment states plus config/action error and busy. Native `Run Now` remains fenced; any button availability difference must be classified narrowly, never used to enable native action |
| Zombie Bus closed/open | `he`: disconnected/failed/running/waiting/disabled summary, open description, optional `me` assignment table | current card line 288 + `ZombieBusPreviewSettings` | no full pair | Pair summaries and table states from actual `he`/`me` versus current composition |
| Zombie Bus errors | `he`: config error and runtime last-error render in a separate sibling `monster-afk-toolbar-settings` error surface, including while settings are open | current lines 293–294 place runtime error inside the open settings section and outside only when closed | none | Concrete composition candidate: immutable failing baseline for open+error and config-save error, then source-proven correction and browser pixels |
| AFK tab ↔ Equipment retention | original page tab ownership plus current React Activity retention | `SquadsPage` lines 12–44 | parent retention/equipment checks partially cover route returns | Real mounted AFK edits/panel selection → Equipment → AFK and profile-context reset; full-page visual captures, not isolated component proof |

## True dependency / scope limits

The original protected post-auth page cannot be booted without the desktop bridge.
The recovered functions above are therefore the original-side renderer oracle under
declared inert/source-valid inputs. Native Garrison member/building producers, Zombie
Bus task producers, AFK gameplay execution, real equipment application and persistence
remain outside this UI/source-local assignment. Current native action fences stay
disabled even when the recovered renderer had an available provider; that difference
must be isolated in evidence rather than “fixed” by inventing a provider.
