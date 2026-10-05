# Remaining source/local UI branch-to-proof matrix

This is the finite coordinator queue for `LWB317-UI-VISUAL-REMAINING-002`.
It reconciles parent Units B/C/D/I/J and rechecks E–H rather than treating worker
status wording as acceptance. Detailed AFK/Squads, shell/Home and Map/global rows
are in sibling inventory files; Automation exact rows are merged from the bounded
Automation recovery once that worker finishes.

## Source identities

| Surface | Original asset | SHA-256 / root locator | Current root |
| --- | --- | --- | --- |
| Shell / Home | `index-BVfnK1wp.js` | `44C4E404...524C6`; `Gi` byte 361306/14461; Home `qr` 336694/2225 | `App.jsx`, `HomePage.jsx`, split shell components |
| Automation | `AutomationPanel-BJ0gIqFh.js` | `6DB232819...1725`; complete `Ae` byte 21817/53453, SHA `61F181D0...4B68` | `AutomationPage.jsx`, `AutomationMeta.jsx`, shared forms |
| Map | `MapDataPanel-B4GXEND2.js` + main asset | `CE743455...3089`; Unit B `results.json` pins every helper/render slice | `MapPage.jsx` and map presentation modules |
| Squads / AFK | `SquadPanel-HC3-DJei.js` | `ED466E35...06C7`; full `I` byte 28070/22554, SHA `0A4895C2...EE2E` | `SquadsPage.jsx` and AFK/Equipment helpers |

## Finite queue

| Gate | Remaining source/local branch set | Existing proof reused | Required next proof | Status |
| --- | --- | --- | --- | --- |
| B — Map composition | Eight tabs, Scheduled, Manual/Auto, availability/fence overlaps only need final shell/retention revalidation | Unit B validator currently passes: 10,482 Scheduled renders, 51/51 mutations, 75 integrated cases, 16 browser pairs, 25 mounted cases | Final App journey and read-only Unit B rerun | `OPEN-INTEGRATION` |
| C — Automation default card inventory | Seven categories, 56 collapsed/expanded default pairs | Unit C accepted lead visual packet | Reuse, do not reopen | `ACCEPTED` |
| C — Automation helper/runtime overlap | meta/future-time 52, named runtime 48, Assist 32, Gather 54; Weekly/Trade behavior and Construction dynamic builders | Unit C accepted-source replay and focused packets | Reuse as lower-level oracle; it cannot substitute for full card composition | `ACCEPTED-LOWER-LEVEL` |
| C — Automation full conditional compositions | Source-valid expanded nested card bodies, data-dependent rows, positive/running/saving/error/invalid overlaps, Train fixed/reward/VIP, Chat forms, populated Gather, Trade Goods/Purchased/history and other conditionals reached by `Ae` | current mounted preview coverage exists for many branches | Execute exact recovered `Ae` subtrees/complete page against canonical page, preserve any failing baselines, four-mode browser pairs and local edits/Activity retention | `OPEN` |
| D — Equipment finite presentation | Equipment presets/rename/progress/result/offline/empty and local edits | parent Unit D + accepted rename/dialog packet | Reuse; final Squads route return only | `ACCEPTED` |
| D — compact AFK cards | compact renderer primitive | parent Unit D | Reuse | `ACCEPTED` |
| D — AFK editor | 24 exact renderer / 12 exact pixels / 267 behavior / 28 mounted | current `LWB317-UI-AFK-EDITOR-VISUAL-001` read-only validator passes | Reuse unchanged | `ACCEPTED` |
| D — AFK full profile list | empty/populated, selected/new, enable/busy/save, add/reorder, runtime squad rows, errors | editor proof only | Exact recovered full `I` versus canonical full composition. Source shows current missing per-squad runtime rows and busy/drag card details; baseline before correction | `OPEN` |
| D — Potion / Drill | selected panel, valid/invalid squads, reorder, runtime leader/join/wait detail, saving/error | compact primitive only | Full `I` paired renderer + mounted local edits/reorder | `OPEN` |
| D — Garrison | closed/open summary, buildings/allies, source empty/error/loading implications, modal search/select/confirm, priorities/assignments/busy/errors | current preview only | Exact recovered `pe` + `I` full composition. Candidate differences include zero-squad hint/validation placement and selected-allies empty element; native Run Now remains fenced | `OPEN` |
| D — Zombie Bus | summary, settings table, runtime/config errors open and closed | current preview only | Exact recovered `he/me` + `I`; source places open+error in separate sibling error surface | `OPEN` |
| E — City Layout | finite source/local renderer matrix | 30 paired states, five pair modes in shared E–H set, mounted/mutation proof | final route integration only | `ACCEPTED-PAGE` |
| F — Hotkeys | finite source/local renderer matrix | 18 paired states, exact E–H pixels/mounted/mutation proof | final route integration only | `ACCEPTED-PAGE` |
| G — Mini Games | finite source/local renderer matrix | 78 paired states, exact E–H pixels/mounted/mutation proof | final route integration only | `ACCEPTED-PAGE` |
| H — Settings | finite source/local renderer matrix | 63 paired states, exact E–H pixels/mounted/mutation proof | final route integration only | `ACCEPTED-PAGE` |
| I — Home isolated renderer | checking/root/busy/recovery/stopped/connected/repair/errors/preferences | rerun: 42 EN/JA cases, 34 raw exact; eight differences only disabled attributes from absent native providers | Full Home inside exact shell; availability difference stays fenced | `OPEN-COMPOSITION` |
| I — header/top actions | status, version/update phases, theme, language, server jump, refresh | Cross-server and Map-entry accepted in isolation; page-local theme/locale captures | Exact recovered `Gi/si` whole-header pairs including narrow mode | `OPEN` |
| I — Profile sidebar | compact/expanded, all connection states, capacity, row/batch busy/error/restart, note dialog, drag/reorder | helpers/retention only | Exact recovered `Yr` full shell pairs and mounted local actions with inert callbacks | `OPEN` |
| I — dialogs/errors | shared `In`, exit `Ji`, Profile note, shell config-save errors, profile-switch loading | exact source slices and lifecycle tasks | Whole-shell paired modal/error/loading combinations and real focus/Tab/Escape rules from recovered source | `OPEN` |
| J — complete app | eight routes, category/tab/subtab state, profile boundary, locale/theme, dialogs/popover, route returns/hidden effects | parent current-only 8 routes/64 forward-return transitions | final original/source-local integrated App run + real browser journey; rerun all affected accepted validators | `OPEN` |

## Demonstrated E–H holes

None at inventory time. `unit-e/recover-pages.mjs --verify` was rerun and reports
City 30, Hotkeys 18, Mini Games 78 and Settings 63 actual paired states. Their page
packets explicitly defer only global shell/profile/native integration, now covered
by Gate J above.

## Evidence classification rule

An `ACCEPTED` or `ACCEPTED-PAGE` row is reused only through its read-only validator
and preserved hashes. `ACCEPTED-LOWER-LEVEL` means the proof is valid but cannot
close a larger composition. `OPEN` rows require actual recovered renderer execution,
current canonical rendering and local browser interactions before closure. A source
provider that does not exist in the clone remains an explicit dependency/fence and
is not “fixed” by enabling or fabricating the action.
