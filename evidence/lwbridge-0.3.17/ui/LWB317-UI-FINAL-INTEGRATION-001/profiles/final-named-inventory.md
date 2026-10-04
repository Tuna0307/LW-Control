# Named UI finish inventory — 2026-10-04 audit snapshot

This reconciles the current eight-page source/local evidence with concrete shared
surface omissions. It is a lead-review input, not product-wide acceptance. Root
integration proceeds concurrently; refer to the final campaign report for final
hashes, browser observations and gates. No percentage is derived from test counts.

Sources: exact recovered main asset `index-BVfnK1wp.js` SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`, exact page
assets/manifests in the already accepted work packets, and current canonical files.
Byte locators below are UTF-8 byte offsets. Source facts are `EXACT_BYTES` /
`EXACT_CONTRACT`; actual local implementation proof does not establish native
`LIVE_PROVEN` or original pixel equivalence.

## Eight known routes

| Surface | Current canonical locator | Current evidence basis | Actual separate dependency/proof gap |
|---|---|---|---|
| Home | `Pages.jsx:HomePage` / source main `qr` 336694 | Accepted HOME-ERROR-001/002-R1, HOME-BUSY-001, SWITCH-LOCALE plus retained shell/App tests | Native proxy/launch busy producers, actual folder picker/preference/lifecycle persistence; original images/geometry |
| Automation | `Pages.jsx:AutomationPage`, cards/Trade; exact AutomationPanel assets | Accepted weekly/Trade corrections and AUTOMATION-AFK-CLOSEOUT coverage | Live runtime task data/execution and config persistence; source image renderer/wiring covered separately below |
| Map Data | `MapDataPage.jsx`, exact MapDataPanel asset | Accepted rows/filters/interaction/lifecycle/header/feedback/refresh/Auto and retained App ownership scopes | New Map notice/Map-entry work must retain own current evidence; native Treasure/jobs producers and exact native images remain separate |
| Squads / AFK | `Pages.jsx:SquadsPage`, EquipmentPresetBoard; exact SquadPanel asset | Accepted AFK residuals/Equipment parent+R1; controlled modal and Activity lifetime proof | Physical Equipment HTML5 drag remains unproved; native names/assets/config/runtime and gameplay remain separate |
| City Layout | `Pages.jsx:CityLayoutPage`; exact CityLayoutPanel asset | Four-page inventory 16 branch groups, R1 source/local acceptance + physical local pointer movement proof | Native city/draft/apply/validation job producers and live confirmation; original layout pixels |
| Hotkeys | `Pages.jsx:HotkeysPage` / shared HotkeyPanel asset | Four-page inventory 9 branch groups, R1 event-time error and config controls | Global/OS key bindings/game actions; native config persistence |
| Mini Games | `Pages.jsx:MiniGamesPage` / shared HotkeyPanel category | Four-page inventory 13 branch groups, R1 timer/status/error/provider fencing | Land/Sheep native runtime/execution; no fabricated native success |
| Settings | `Pages.jsx:SettingsPage`; exact SettingsPanel and main updater `Rn` | Four-page inventory 16 branch groups, R1 source-shaped optimistic/error/cooldown/export/updater proof | Native diagnostic/updater/visual preferences; source profile-focus row parent wiring remains a named shared integration gate |

The existing four-page **54-group** inventory is finite and useful. It is not the
denominator for all eight pages or every native/live branch. Automation/AFK, Home
and Map work have named bounded inventories; adding their multiplied scenario
counts would produce a misleading completion percentage.

## Concrete shared surface gates and exact source/current anchors

| Named branch group | Exact original locator | Current source / disposition at this audit |
|---|---|---|
| First-visit route retention / suspended hidden effects | main `profile-view-context` 372793 and Activity callsites; accepted retention packet | `App.jsx:RetainedPages` accepted; preserve existing proof |
| Map navigation summary-dispatch before transition, active-route no-op | main `Tt` 364377 | `App.jsx:selectRoute`; separate MAP-ENTRY takeover/verification owned by root |
| Cross-server pointer-down/history/jump→summary→history acknowledgement | main `ti` 350656, parent `Mt` 365565 | Accepted Cross-server packet; preserve current evidence |
| Theme reduced-motion / view-transition / 240 ms class fallback | main `Et` 364550; CSS view-transition 4899 / theme-transitioning 5078 | Root added `shellTheme.js` and App binding; pending final root proof/acceptance |
| Header source-shaped version and available/downloading/opening/error retry | main `si` 354710 | Root `ShellPresentation.jsx:TopVersion` and App status inputs; native updater producer remains separate |
| Four flag save-error banners across all pages | main `Oe` 199047 + parent four-map 372793 | Root `ShellPresentation.jsx:ShellConfigSaveErrors`; production flag state producer must not be fabricated; explicit local fixture proof |
| Profile compact/expanded list, select/add/remove/reorder/note/errors | main `Yr` 339285 + helpers 194447..194896 | `ProfileSidebar.jsx`, 20 named groups in README; 98-case isolated original/current proof; root App/browser integration pending final lead review |
| Profile note native-modal busy/Escape/Tab/focus lifetime | main `In` 209017 + `Yr` note expression | `ProfileSidebar.jsx:ProfileNoteDialog`; exact controlled jsdom comparison, physical browser breadth separate |
| Profile-switch loading renderer | main conditional expression 373027 / class 373064 | `ProfileSwitchState.jsx`; 12 exact-expression render cases. No source switch-error panel exists, so none is invented |
| Native profile bootstrap/cache on selected-profile replacement | main layout effect 368735, cache check 368788, bootstrap/effects around 369239 | `App.jsx:selectedProfileId` remains native bootstrap authority; multi-profile explicit preview never changes native dispatch. **UNKNOWN native dependency / OPEN producer integration**, not source-sidebar failure |
| Multi-profile shared category/Map/AFK-tab parent state and Settings focus-game row | main parent route bindings 373839 onward; Settings `onFocusGameOnProfileSelectChange:dt` 375651; storage key `lwbridge.focusGameOnProfileSelect`358006 and following `ui`/`di` helpers | Current panels support their local tabs; broader real profile parent producer remains separate. Root now passes local source-shaped focus-game state; its mounted Settings row and persistence still require the final campaign integration proof |
| Hover/focus/nav-trigger preloading | main `Qr` 349778, `Wi` 361263 / `Tt` 364377 | Clone bundles pages eagerly and currently has no equivalent preloader callbacks. **OPEN source scheduling/performance distinction**; no native dependency. It is not a new visible navigation entry |
| App-exit modal null/count/busy/Cancel/Confirm/Tab/Escape | main `qi` 375835, `Ji` 376111, shared `In` 209017 | `AppExitDialog.jsx` / optional `AppExitPrompt`; 33 cases,32 against actual source. Exact PNG reused. Root explicit offline fixture/integration pending review; native exit provider unavailable/fenced |
| Source image placeholder→loaded `<img>`, visibility/cache/coalescing/request lifetime | `GameAssetImage-Diy9VTIr.js` `g`156 / `v`421 / `y`812 / `b`1165 / `x`1307 | `GameAssetImage.jsx`; isolated16 groups/13 source comparisons incl36 normalization pairs. **OPEN caller wiring** until root replaces unconditional placeholders. Native lookup/assets remain separate |

Source-specific cache and scheduling quirks are intentionally retained: original
image cache **does not promote cache hits** in insertion order, and a 60-second
negative-cache expiry alone does **not** schedule retry. Existing accepted Map
timer sentinel/hidden options/Dispatch reconnection behavior also stays unchanged.

## Image integration locators

Source image module SHA-256:
`2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0`.
Exact source component accepts one of `assetPath`
or `spriteName`, plus `alt`, `className`, `deferUntilVisible`; source reader returns
`{dataUrl}`. Current optional reader is separately supplied; no reader means placeholder.

| Image caller | Original locator / field | Canonical integration target |
|---|---|---|
| Normal Map reward rows | MapDataPanel byte16692, `item.iconPath` | `MapDataPage.jsx:MapTable` reward cell |
| Scheduled Dispatch/Truck reward rows | MapDataPanel bytes23747/27715 | `ScheduledPlunder.jsx` image adapter |
| Goods picker current/menu icons | MapDataPanel bytes28531/28911 | `MapRetainedGoodsFilter.jsx` adapter |
| Manual Assist stars/rewards | AutomationPanel bytes36327/36655 | `DispatchAssistManual.jsx` image adapter |
| Train preferred rewards | AutomationPanel byte56749 | `Pages.jsx` Train reward list |
| Trade goods/purchased frames and icons | AutomationPanel bytes1645/1781,4198/4334 | `Pages.jsx` Trade cards/history: `spriteName=cfm_tongyong_daojukuang_${quality}` only quality>0, item.iconPath; deferred visibility true |
| Trade currency icons | AutomationPanel bytes4729/6231 | currencyIconPath; purchase deferred true, selection normal immediate |
| Equipment hero/slot icons | SquadPanel bytes188842/184358 | `Pages.jsx` Equipment hero.iconPath / equip.iconPath |

Existing host read contract is verified in current C# only: Map317CommandService
case99 and `ReadAssetImageAsync`211..224, ManualMapScanCommandService
`GetAssetImageAsync`539..564 accept exactly one trimmed source and return dataUrl;
absent current-client reader rejects GAME_DISCONNECTED. This isolated audit did
not invoke that reader, validate live path mappings, create a new provider, or
prove native image compatibility. Native files/assets/texts remain **UNKNOWN** /
their separately recorded existing dispositions, not silently closed UI proof.

## Explicit non-closure gates

1. Root must finish owned integration/browser/evidence/package checks and decide
   acceptance for the new shared surfaces, image callers and MAP-ENTRY takeover.
2. Native multi-profile cache/switch, exact original status/config/update/exit/image
   producers and actual current-game asset/text mappings need separate assigned
   integration/recovery work. UI renderers can be tested without pretending those
   producers work.
3. Physical Equipment HTML5 drag and full native dialog/confirmation behavior are
   disclosed proof limits; actual rendered-handler comparisons are not physical
   pointer evidence.
4. Direct original post-auth geometry/pixels remain **BLOCKED**. Reusing exact
   CSS/source and inspecting clone images does not close this comparison gate.
5. Login/account/Upgrade/licensing/entitlement system remains **OUT_OF_SCOPE**.
   Ordinary local profiles and capacity are intentional clone-owned runtime state.

No master/global completion status was changed by this audit. The root report must
distinguish named renderer closure, unresolved producer/assets and pixel/native
proof rather than announce a fully verified one-for-one UI or program.
