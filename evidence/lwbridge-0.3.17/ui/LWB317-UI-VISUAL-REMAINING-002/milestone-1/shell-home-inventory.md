# Shared shell / sidebar / dialogs / Home finite inventory

Authority: executable SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` and
`index-BVfnK1wp.js` SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

Exact original renderer roots used here:

| Renderer | UTF-8 byte | Bytes | SHA-256 | Current owner |
| --- | ---: | ---: | --- | --- |
| `In` shared dialog | 209,017 | 1,209 | `E42C17AAC67AFE7A73D525902C9E757644E40838422730CEB32EC47830C4F8A2` | `AppExitDialog.jsx`, `ProfileSidebar.jsx` dialog wrappers |
| `qr` Home | 336,694 | 2,225 | `6547A9E79620B4F02E14397FC59359D2FF8616C0F45AB394A1756D042F5FF385` | `HomePage.jsx` |
| `Yr` Profile sidebar | 339,285 | 7,154 | `FE5D0408492E71E9DD74615C269699B8C75B55D346F823BC22B05992C330A490` | `ProfileSidebar.jsx` |
| `si` top version/update | 354,710 | 3,232 | `32E9CFF50FC34FCD3B91B2A3256AEC4DC9C605E98713B4F8F0ED536A4B94B405` | `ShellPresentation.jsx` |
| `Gi` complete post-auth shell | 361,306 | 14,461 | `AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5` | `App.jsx` plus split shell components |
| `Ji` exit dialog | 376,111 | 881 | `72F95F7202BC43FD3F9E449DC77133A08C57DE1C8D6F48AB929DCC1D34F48C21` | `AppExitDialog.jsx` |

Home helper slices are independently pinned by the existing offline renderer packet:
`Ir` 328453/231, `Lr` 328684/166, `Kr` 336469/225, `zn` 213222/110,
`Bn` 213332/564 and `qr` above. Current file hashes at inventory time are recorded
in `starting-state.md`/the milestone transcript.

## Accepted/reusable proof

| Branch family | Existing accepted/local proof | What it does not close |
| --- | --- | --- |
| Eight-route retained `Activity` shell and profile reset boundary | `LWB317-UI-SHELL-RETENTION-001`; exact source contract plus mounted page returns | Whole shell geometry/pixels and all sidebar/header/dialog combinations |
| Map navigation dispatch order | `LWB317-UI-SHELL-MAP-ENTRY-001`; exact `Gi`/parent selection recovery | Other header composition and cross-page integration |
| Cross-server popover | accepted lead checkpoint from `LWB317-UI-SHELL-CROSSSERVER-001`; full branch matrix including pointer-away, history, busy, locale and acknowledgement order | Whole header plus open popover together with page/profile/dialog states |
| Home errors, busy predicates and preference lifetimes | HOME-ERROR-001/002/R1, HOME-BUSY-001, HOME-PREFERENCE-LIFETIME-001A/B | Full Home-in-shell pixels and combinations with sidebar/header/dialog |
| Home isolated recovered renderer | current rerun of `LWB317-UI-OFFLINE-VISUAL-001/home/build-reference.mjs`: 42 EN/JA cases, 34 raw-structure matches; eight differences are only disabled action/preference attributes caused by explicit unavailable native providers | The harness intentionally uses an empty synthetic shell, so it is not shell/Home composition proof |

## Remaining branch matrix

| Branch | Exact source condition / action | Current component | Existing proof | Missing proof / gate |
| --- | --- | --- | --- | --- |
| Base complete shell | recovered `Gi`: brand/version, connection+pending status, top actions, profile rail when entitlement permits, eight nav entries, active retained page | `App.jsx` 581–735 | current-only Unit J route smoke | Execute recovered complete shell renderer under source-valid offline inputs against actual current App; compare full DOM/geometry/pixels |
| Connection/status strip | disconnected/connected/stopped/checking plus finite pending count | `App.jsx` status-strip | route smoke only | Full original/current shell pairs across representative status states and locale/theme modes |
| Update indicator | `si`: hidden/available/downloading/opening/error-with-different-version; busy progress/action availability | `TopVersion` | exact source comment, no full pair | Original/current render branch matrix; button availability may differ only where native updater is deliberately fenced |
| Theme control | source SVG/control, persisted theme, light↔dark title/icon and live CSS inheritance | `App.jsx` theme-toggle | many page-specific browser captures | Whole-shell original/current light/dark pairs and real local toggle retaining active route/drafts |
| Language selector | all recovered locales, current selection and live shell/page translations | `App.jsx` language-select | page-specific multilingual tests | Full shell EN/JA desktop and narrow cross-mode transition, including stored event-time errors staying in their original locale where source requires |
| Cross-server open/closed | header-owned recovered popover under route/profile/theme/locale changes | `App.jsx` server-jump | popover task accepted in isolation | Whole-shell pair with popover open/closed; route pointer-away, profile reset and narrow geometry in integrated App |
| Refresh action | recovered header refresh action and source availability | `App.jsx` Refresh status | current route smoke | Render pair and local click only with inert existing bridge fixture; keep unavailable/native fence |
| Profile sidebar compact | `Yr`: collapsed compact rows, selected profile, role/display/server/note, all recovered connection states | `ProfileSidebar` lines 182–190 | source helper checks/retention, no final full pair | Original/current sidebar pair in full shell for connected/offline/locked/error/etc.; desktop/narrow geometry |
| Profile sidebar expanded | heading/quota, batch start/stop, rows, edit/delete/run actions, add/capacity, errors, restart-required | `ProfileSidebar` lines 191–237 | no complete visual gate | Recovered `Yr` against canonical sidebar under source-valid states; action availability differences classified as native fences |
| Sidebar collapse/error forcing | source collapsed preference plus errors/restart forcing expanded surface | `ProfileSidebar` `isCollapsed` | no complete pair | Mounted collapse/expand, injected source-valid errors, return navigation and profile identity switch |
| Profile note dialog | recovered `In` modal wrapping note editor, cancel/save, busy/focus/Tab behavior | `ProfileSidebar` lines 238–242 | shared dialog source known | Full shell pair + real local open/edit/cancel/save with inert callback; focus return/Tab/Escape from exact `In` only |
| Profile drag/reorder | recovered row drag states and reorder semantics | `ProfileSidebar` rows 202–212 | helper function only | Mounted DOM drag handlers / reorder and pixels; physical pointer HTML5 drag remains a stated limit if connector cannot exercise it |
| Profile start/stop/batch errors | source row and batch busy/error/restart branches | `ProfileSidebar` 143–175, 192–235 | no full visual gate | Renderer pairs with inert callbacks and source-valid instance records; no native instance launch |
| Profile switch loading | recovered `Gi` loading branch at the shell content boundary | `ProfileSwitchState.jsx` + `App.jsx` 724 | source comment only | Full shell original/current loading pair; ensure retained page hidden/replaced exactly as source |
| Shell config save errors | recovered `Oe`/`Gi` ordered error banners for flag stores, Retry/Discard | `ShellConfigSaveErrors` | Home reconnect/Automation store behavior proves lifetimes | Full shell composition with one/multiple errors and real local Retry/Discard through inert stores |
| Exit prompt absent/open/busy | `Ji` inside exact `In`; count text, Cancel, confirm, closing label; native close request provider optional | `AppExitDialog.jsx`, `App.jsx` | source recovered, preview fixtures exist | Original/current modal pair in complete shell + focus trap/cancel/focus return interactions; confirm provider stays inert/fenced |
| Shared modal behavior | `In` showModal, prior-focus restore, Cancel/Escape through `onCancel`, Tab loop, optional backdrop dismissal using pointerdown/click pair | split wrappers | exact source now recovered | Verify current wrappers preserve only the relevant `In` branches; no invented modal shortcuts |
| Home checking/missing/root error | exact `qr` rootResolved/rootValid branches | `HomePage.jsx` | isolated source renderer + Home error tasks | Full Home inside actual shell with header/sidebar/navigation geometry |
| Home stopped/running/connected/repair | exact `qr` state/status/action composition | `HomePage.jsx` | isolated source renderer exact except disabled lifecycle actions | Full pair with narrow disclosed native-action availability fence; do not enable missing lifecycle provider |
| Home recovery states | waiting/updating/repairing/launching/verifying/maintenance/failed detail, with source busy precedence | `HomePage.jsx` | busy/error source exhaustive local checks | Full shell pair and real route away/return while recovery state props change |
| Home independent error channels | root error and action error may coexist; recovery error independent | `HomePage.jsx` | ERROR-002/R1 accepted local callback/render proof | Whole shell pixels, locale/theme transitions and retained return |
| Home preferences | Auto Launch + Automatic Reconnection confirmed/draft/saving/error/Retry/Discard and incoming acknowledgement | `HomePage`, `App`, profile draft store | lifetimes accepted | Full shell pair with preference error banners + route/profile transitions; retain current provider fences |
| Narrow shell | recovered CSS with header/top actions/sidebar/nav/main interactions at narrow widths | whole App/reference CSS | current-only shell smoke plus page-local exact narrow pairs | Original/current full-shell 375-ish source browser reference and 800-class integrated smoke; check overflow/overlap and dialogs/popover |

## Scope limits

Original protected post-auth runtime pixels are unavailable. The exact recovered
`Gi`/child functions plus recovered CSS are the source-side renderer authority. The
native updater, game lifecycle, instance start/stop, real profile service, native
folder picker and close-process confirmation remain fenced. Those controls may be
disabled in current when the source renderer had a provider; evidence must isolate
that availability attribute rather than remove the fence. No shell branch may invent
Escape, focus or persistence behavior beyond recovered `In`, `Yr`, `Gi` and the
already accepted cross-server source.
