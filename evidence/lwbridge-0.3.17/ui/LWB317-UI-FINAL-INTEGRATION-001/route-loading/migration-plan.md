# Route loading source contract and proposed canonical split — 2026-10-04

Recovery is complete; product migration is **not yet assigned to this lane**.
No App/Pages product changes or native actions were made here. The exact source
contract and actual current declaration/dependency graph are in
`source-contract.json`; `check-original-loading.mjs` executes the original
navigation, preloading and route-selection callbacks in 15 distinguishing cases.

## Exact original behavior

Original asset: `index-BVfnK1wp.js`, SHA-256
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
All byte ranges/lengths/hashes are pinned in the contract JSON.

- `Qr` attaches preload to both mouse-enter and focus, then route selection to
  click. Home may receive those callbacks but has no loader.
- `Wi` uses a finite loader table; unavailable routes are a no-op and rejected
  preloading is swallowed. Hotkeys and Mini Games share `Pi`. Advanced has no
  loader. Do not invent a separate import retry policy.
- `Tt` at UTF-8 byte 364377 returns on the active route. For Map it initiates the
  summary request and attaches its error logger, **without awaiting it**. It then
  preloads, then starts the route transition, adds a previously unseen route to a
  copied Set and commits active route. Revisited routes retain Set identity.
- Home is eager. Named exports from six panel modules feed stable module-level
  React.lazy component types. The Hotkey type serves both Hotkeys and Mini Games.
- One Suspense wraps `profile-view-context`: shared save errors, profile-switch
  state and all visited Activity children. Its fallback is a `panel` containing a
  `muted` span translated with `common.processing`.
- Activity first visits, stable route identity and profile-key lifetime continue
  to own state/effect retention. Imports must not relocate those boundaries.

Current App already performs the accepted non-awaited Map summary initiation
before transition (lines 187–199 at recovery time). Preserve this behavior. The
remaining current differences are eager Pages dependencies, absence of route
preloading and the missing original route Suspense boundary.

Concrete UX consequences: hover/focus can warm the panel before a click; the
first actual visit may suspend while its module loads; transitions use React's
already-revealed Suspense behavior instead of inventing an unconditional spinner.
The renderer has a localized fallback for genuine suspension. Actual timing of
old-view retention/fallback must be verified by deferred mounted imports, not
asserted from a timeout or screenshots alone.

## Proposed module graph

`Pages.jsx` becomes the sole canonical route dispatcher and stable loader/lazy
registry, importing Home eagerly and other panels dynamically. It must not
re-export/import every lazy page eagerly for old tests. No duplicate or legacy UI.

| Module | Existing declarations moved without body changes | Static dependencies |
|---|---|---|
| `sharedPageUI.jsx` | Switch, ToggleRow, PanelTitle | React/i18n only |
| `HomePage.jsx` | RECOVERY_ACTIVE_STATES, previewHomeState, translatedError, HomePage | shared UI, i18n |
| `AutomationPage.jsx` | automation category/card constants through AutomationPage, including WeeklyQualityPreview, runtime/config/fields/cards, ResourceGatherCard, TradeStationCard | shared UI, React/i18n, current AutomationMeta/Assist/draft/fixture/Trade/image modules |
| `SquadsPage.jsx` | SquadsPage through EquipmentContent: all AFK/profile/drill/garrison/bus/card/dialog/equipment declarations | shared UI, React Activity/i18n, RallyJoinSettings, existing AFK/draft/equipment/image modules |
| `CityLayoutPage.jsx` | CITY_LAYOUT_PREVIEW_STATES, CityLayoutPage | React/i18n and exact existing City contract exports |
| `HotkeyPages.jsx` | HotkeyCard, MINI_GAMES_PREVIEW_STATES, RecoveredHotkeyPanel, HotkeysPage, MiniGamesPage | shared UI, React/i18n and existing hotkey/sheep contract exports |
| `SettingsPage.jsx` | SETTINGS_PREVIEW_STATES, initialFeedbackState, SettingsPage | shared UI, React/i18n and existing diagnostic/updater contract exports |
| `MapRoutePage.jsx` | existing PageForRoute Map branch as a wrapper | MapDataPage, getMapPreviewProvider; preserves preview-only provider overlay exactly |
| `Pages.jsx` | PageForRoute plus loader table/stable lazy types/preload | eager Home, React.lazy; no eager lazy-panel dependencies |

The current AST graph contains 49 top-level declarations and 79 imported names.
It pins dependencies for every declaration, allowing import lists to be derived
instead of guessed. All declarations must have exactly one owner, and all named
imports must resolve. Preserve the shared Hotkey module; splitting it into two
independent implementations would undermine common source behavior.

App integration belongs to the lead: preserve Map initiation, then add preload
before transition; nav hover/focus call the same loader; place the single
Suspense boundary at the source-equivalent profile-view-context level. Preserve
Activity keys/modes, props and the current profile-switch condition. Native
providers, state producers, handlers and action fences remain unchanged.

## Verification and historical harness migration

1. Pin immutable current Pages/App bytes and record each original declaration
   hash. Move declaration bodies byte-for-byte; compare post-move AST bodies,
   exported function signatures, literals, state initializers and JSX. Moving an
   import or adding an export is not permission to edit its handler.
2. Build the actual module graph and production package. Assert lazy route
   modules appear as split chunks and Home's startup graph does not import them
   statically. Hotkeys and Mini Games must share the Hotkey module loader.
3. Execute original/current preload and transition cases: active no-op; hover and
   focus; rejected preload; Map unresolved/rejected summary ordering; first and
   repeated visits. Use actual canonical loaders with controlled import promises
   in mounted tests. Do not simulate loading by adding an arbitrary timer.
4. Mounted React proof must cover first lazy suspension, resolve/reject, another
   navigation while pending, visited Set, Activity effect cleanup/reinstall,
   retained local drafts and selected-profile reset. Reuse accepted Equipment,
   City, Mini Games and Settings lifecycle checks with current module bindings.
5. Canonical checks need small maintained updates: `check-home-integration.mjs`
   currently reads Home predicates from Pages; `check-ui-complete.mjs` scans
   Pages for locale/fixture markers. Read the actual owned modules instead.
   `check-static.mjs` may still require the canonical Pages router file.
6. Historical checkers commonly extract named declarations from Pages. Preserve
   their old records/manifests. New current adapters should collect actual module
   bodies/import bindings (or bundle them through esbuild) rather than fabricating
   duplicate production implementations or re-exporting lazy modules eagerly.
   Relevant families: HOME-ERROR/BUSY/SWITCH, Automation/AFK, weekly and Trade,
   Equipment/R1, four remaining pages/R1, shell mounted retention, plus current
   image-caller and Trade-label checks. Update only maintained/current adapters;
   document historical hash pins as historical.
7. Browser proof: navigation via real controls, hovered/focused first visits,
   retained modified Equipment state, Mini Games/Settings timer cleanup, Map
   return, EN/light and JA/dark, measured narrow viewport, fresh console. Network
   capture can establish split-module requests; it does not establish native UI
   producers or original runtime pixels.

No native/read-only provider calls, live gameplay, original service access or
package cleanup are required for this module-loading campaign. Missing producers
remain disclosed. Quality gates and protected WIP hashes remain mandatory.
