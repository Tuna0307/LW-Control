# Final review: committed canonical page (HEAD c57be79) against the original-source contract

Scope: `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`, `mapInteractions.js`, `mapPreviewApi.js` (read-only), compared with `contract.md` / the actual original component. Every candidate below was EXECUTED on both implementations by `original/final-review.mjs` (original via `original-runtime.mjs`, canonical via `../flavors.mjs` `bootCanonicalLike` + `../driver.mjs`; the canonical `MapTable` child is rendered by compiling the same source with one extra in-memory statement). Raw per-candidate data: `original/final-review-results.json` (written with `--record`). Canonical source hash is stored in that file. Synthetic deferred replies and a fake clock; not browser or native evidence.

Classification: DEFECT = should match the original and does not; DOCUMENTED = runtime fence or out of scope (as listed by the coordinator); UNKNOWN = not executable here.

## Commands

```
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/final-review.mjs --record   # differential, writes final-review-results.json
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/final-review.mjs            # same without writing (output in section "Raw output")
```

## DEFECTS (canonical differs from the original)

| id | original (executed) | canonical (executed) | repro (candidate id in final-review.mjs) |
|---|---|---|---|
| D1 C01 | Server change while on page 3: first request is page 1 for the new server. | First request is **page 3** for the new server, then page 1. The data server is derived in the same render as the server-change effect, so the search effect runs with the stale page before `setPage(1)` lands. Observed requests: original `[456 p1, 456 p1]`, canonical `[456 p3, 456 p1]`. | city tab, resolve with total 230, `setPage(3)`, `emitServer(456)` |
| D2 C02 | An options refresh that no longer offers the selected alliance resets the alliance filter to `all` and refetches without `alliance` (original options handler `kt(...)`, also `qt` dispatch level and `Vt` treasure type validation). | Alliance stays selected (`alliance: "Foo"` still queried); no refetch. Only resource/monster names are validated (`loadOptions`). | select alliance Foo, make options offer only Other, refresh |
| D3 C03 | Clear-data resets the dispatch level (`qt("")`), alliance (`kt("all")`), item filter (`zt({})`), treasure type (`Vt("")`) as well as name selection; the refetch after clear carries no min/max level. Keyword and quality are kept. | Level `5` survives clear-data and the refetch still sends `minLevel/maxLevel 5`; alliance, item filter and treasure type are likewise not reset (`clearData` only clears rows/selections/options). Name selection is equivalent only because a cleared server offers no names (C04b same). | dispatch tab, level 5, keyword kw, click Clear server |
| D4 C05, C06, C27 | Failure messages for share/clear/export/scan start are translated through `Lr` (error.* catalogs, fallback `common.actionFailed`). Export success sets message `map.exportExcelSuccess {count,path}` (unless the dialog was cancelled). Export failure shows the translated text in the message span (`role=status` span `map-claim-result`). | Share/clear failures show raw `CODE: message` (`DISPATCH_PLUNDER_GAME_DISCONNECTED: game disconnected`); scan start failure banner is raw `GAME_DISCONNECTED: GAME_DISCONNECTED`; export success shows NO message and export failure is a `role=alert` banner with raw text (`X: boom`). The page already has an equivalent `translatedError` in `Pages.jsx` (not exported) and the locale files carry the `error.*` keys. | share/export/scan-start rejected with a coded Error |
| D5 C07 | Rows refetch at most once per second while a scan reads and once more when it completes (and options refresh at completion): 1 search request during 1.5 s of scan, 2 at completion (rows + options-triggered). | 0 search requests during the scan, 0 at completion (rows only update on user action). Counts are refreshed by the 5 s summary poll, which is a different path. Reclassify as DOCUMENTED only if the "scan polling" fence is meant to cover row refresh. | emit scan state reading, advance 1500 ms, emit completed |
| D6 C08, C28 | Treasure tab: "prioritize lucky" is checked by default (`localStorage lwbridge.mapLuckyTreasurePriority !== "false"`); both treasure checkboxes persist to `lwbridge.mapLuckyTreasurePriority` / `lwbridge.mapIncludeForeignRadarTreasures` and initialise from them. Treasure queries always carry `includeForeignRadarTreasures` and `luckyFirst` booleans (`false` is sent as `false`). | Lucky default unchecked, no persistence (the page only uses `lwbridge.mapScanMode` and the auto-scan key); false values are omitted from the query. (Viewer fields `viewerUid/viewerAllianceId` and the viewer-identity wait are native-dependent: DOCUMENTED.) | open the treasure tab offline |
| D7 C09 | Alliance options are values `name:<encoded name>`; an alliance literally named `none` is queried as `alliance: "none"`. | Option values are the raw names; choosing an alliance named `none` (or `all`) collides with the sentinel values and sends `withoutAlliance: true` / no filter. Low severity. | options with alliance named `none`, choose it |
| D8 C15 | Player-mark button: `disabled = !row.ownerUid` only (offline, scanning, busy do not disable it). Rows [with uid, without uid]: offline `[false,true]`, online `[false,true]`, scan reading `[false,true]`. | `disabled = actionDisabled || !ownerUid || actionBusy`: offline `[true,true]`, online `[false,true]`, scan reading `[true,true]`. | city rows with and without ownerUid, `online:false` |

## DOCUMENTED (executed, matches the stated fences)

- C22 search failure: original shows nothing (log only), canonical shows a `role=alert` banner (`queryError`).
- Search button with no data server, the extra post-options request at mount, 5 s summary and options polling: unchanged from the earlier differential (`canonical-differential.json`).
- C14 tab counts after clear-data: both end at 0 once the provider answers zeros after a clear. The mount-time difference (original 0, canonical 60) is only the harness summary stub (not a deviation).
- C04 name selection after clear-data differs only while the harness keeps offering the name; with the empty names a cleared server returns (C04b) both clear it and request without a name key.
- Provider fences (by reading): scheduling/share/cancel/clear buttons need provider methods; treasure claim buttons (`disabled` literal) and truck/railway "follow" buttons (`liveTargetDisabled` default true) are permanently fenced. C16 coordinate-jump predicate is identical offline/online.
- Viewer identity wait and `viewerUid/viewerAllianceId` on the treasure query (native claim status).

## SAME (executed, no deviation found)

C10 job-list load counts (mount, Scheduled entry, change event, after schedule success), C11 online toggle refetch, C12 name select at page 3, C13 sort at page 3, C17 checkbox disabled/checked after expiry (table clock), C19 schedule busy/failure, C20 cancel/clear busy keys and call arguments (ghost cancel `ghost:uuid`), C21 truck selection after clear-data, C23 per-tab quality and page reset, C24 item filter clearing drops the itemCount sort, C25 out-of-range page values, C26 stale selection snapshot is what is scheduled. The earlier-fixed action-message clearing on tab change shows PARITY in `check-interactions.mjs`.

## UNKNOWN / not executed

- C18 timers: the canonical harness does not expose its timer table, so interval conditions could not be compared; by reading, MapTable ticks only truck/dispatch/ghost and the page clock only the Scheduled tab (the original also ticks while a scan reads, with no visible effect).
- `ScheduledPlunder.jsx` row rendering (separately owned; see `../scheduled/`), Activity hide/show effect re-run for the canonical page (its harness cannot model it), and `mapPreviewApi.js` (fixture provider; read only, no contract statement depends on it).

## Regression commands (verbatim, no --record flags; run from evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001)

Exit codes: check-interactions 0, check-integration 0, check-request-lifetime 0, replay-historical 0, original/check-original-runtime 0.


### $ node check-interactions.mjs  (exit 0)

```
PARITY (baseline same) keyword.typing-does-not-search
PARITY (baseline same) keyword.search-click-on-page-1-searches-once-with-typed-text
PARITY (baseline same) keyword.search-click-on-page-2-goes-to-page-1-and-searches-once
PARITY (baseline differs) keyword.typed-but-unsubmitted-text-is-used-by-a-filter-change
PARITY (baseline differs) keyword.tab-change-keeps-input-and-queries-with-it
PARITY (baseline differs) keyword.page-change-queries-with-typed-text
PARITY (baseline same) keyword.no-timer-or-debounce-after-time-passes
PARITY (baseline differs) search.button-is-not-disabled-by-loading
PARITY (baseline differs) search.click-while-loading-issues-a-newer-request-and-retires-the-older
PARITY (baseline differs) search.connection-change-refreshes
PARITY (baseline same) search.failure-clears-rows-and-total
PARITY (baseline differs) name.select-clears-keyword-resets-page-and-searches-once
PARITY (baseline differs) name.typing-after-selecting-drops-the-name-and-searches-with-the-text
PARITY (baseline same) name.typing-without-a-selected-name-does-not-search
PARITY (baseline same) name.resource-and-monster-selections-are-independent
PARITY (baseline differs) name.option-text-uses-game-text-then-key
PARITY (baseline differs) selection.dispatch-toggle-membership-and-count
PARITY (baseline differs) selection.dispatch-survives-page-filter-sort-search-and-server-change
PARITY (baseline differs) selection.dispatch-resets-on-any-tab-change-including-dispatch-ghost
PARITY (baseline differs) selection.truck-is-independent-and-persists-across-tabs-pages-and-servers
PARITY (baseline differs) selection.payload-keys-and-identifier-edge-cases
PARITY (baseline differs) delay.input-default-and-controlled-value
PARITY (baseline differs) delay.schedule-button-predicate-per-delay-text
PARITY (baseline differs) actions.schedule-and-share-predicates-with-and-without-selection-online-busy
PARITY (baseline differs) actions.truck-schedule-predicate
PARITY (baseline differs) flow.schedule-dispatch-success-busy-then-clear-reload-and-navigate
PARITY (baseline differs) flow.schedule-dispatch-failure-keeps-selection-and-tab
PARITY (baseline differs) flow.schedule-with-invalid-delay-never-calls-the-provider
PARITY (baseline differs) flow.schedule-truck-success-clears-only-truck-selection
PARITY (baseline differs) flow.share-label-selection-pruning-and-partial-message
PARITY (baseline differs) flow.share-full-success-message-and-selection
PARITY (baseline differs) flow.scheduled-tab-lists-counts-and-reloads-on-entry-and-change-events
PARITY (baseline differs) flow.scheduled-cancel-identities-busy-keys-and-reload
PARITY (baseline differs) flow.scheduled-plunder-again-and-clear-history
PARITY (baseline differs) flow.action-message-is-cleared-by-a-tab-change
LWB317_INTERACTIONS PASS scenarios=35 currentMismatches=0 baselineMismatches=28 originalErrors=0
```

### $ node check-integration.mjs  (exit 0)

```
PASS cache restoration keeps the typed keyword and refreshes the restored page with it
PASS name selection survives tab cache restoration and refreshes with the name and restored page
PASS Dispatch selection resets on tab change while the cached page and rows are restored
PASS selection is retained across row refresh (connection change) and search while rows are replaced
PASS rapid navigation with deferred replies settles on the final tab only
PASS Normal/Scheduled transitions: counts, list loading on entry, change events and normal state untouched
PASS the page clock drives Scheduled Plunder once per second from the controlled clock
PASS fence: without a provider every job/schedule/share control stays disabled and calls nothing
PASS schedule success: busy, selection cleared, list reloaded, navigation to Scheduled Plunder; failure keeps selection
PASS truck schedule success clears only the Truck selection
PASS share: label flips while sharing, only shared uuids leave the selection, partial/success messages
PASS Scheduled actions: busy keys, ghost cancel identity, reload, error message and fences
PASS clear-data empties both the Dispatch/Ghost and the Truck selection (original tr: on({}), cn({}))
PASS a selected Resource name that the refreshed options no longer offer is cleared
LWB317_INTEGRATION_OK 14 scenarios
```

### $ node check-request-lifetime.mjs  (exit 0)

```
LWB317_REQUEST_LIFETIME_OK baselineFailures=0 deliveryFailures=4 currentFailures=0 scenarios=38
```

### $ node replay-historical.mjs  (exit 0)

```
PASS LWB317-UI-MAP-FILTERS-001/check-filters.mjs (filters) :: LWB317_MAP_FILTERS_OK 472 original/current queries; baseline 330 mismatches; fences PASS
PASS LWB317-UI-MAP-FILTERS-001/check-table-regression.mjs (unchanged) :: LWB317_MAP_TABLE_REGRESSION_OK historical evidence unchanged; only test setter dependency adapted
PASS LWB317-UI-MAP-FILTERS-001/check-treasure-checking.mjs (unchanged) :: LWB317_TREASURE_CHECKING_OK 216 display comparisons; 384 input fences; 8 original producer cases
PASS LWB317-UI-MAP-ROWS-001/check-row-actions.mjs (rows) :: LWB317_MAP_ROW_ACTIONS_OK 160 comparisons; preview/native fences PASS
PASS LWB317-REVIEW-MAP-STATES-001/check-review-map-states.mjs (unchanged) :: }
```

### $ node original/check-original-runtime.mjs  (exit 0)

```
ok   harness-executes-original-bytes (2 claims)
ok   kw-typing-no-request (4 claims)
ok   kw-shared-across-tabs, kw-tab-change-uses-typed-keyword (3 claims)
ok   kw-filter-change-uses-typed-keyword (2 claims)
ok   kw-clears-name-selection, kw-first-keystroke-with-name-requests (3 claims)
ok   kw-options-refresh-refires-search (3 claims)
ok   rr-stale-reply-dropped, search-while-loading (3 claims)
ok   rr-page-clamp (4 claims)
ok   search-button-page1-vs-page-gt-1 (2 claims)
ok   search-button-no-disabled (2 claims)
ok   export-city-query (3 claims)
ok   nr-per-tab-fields (6 claims)
ok   name-select-render, name-option-text-derivation (6 claims)
ok   name-select-request, name-select-resets-page-and-keyword (3 claims)
ok   name-selection-persists-across-tabs (2 claims)
ok   name-invalid-after-refresh-cleared (2 claims)
ok   tab-change-effects, tab-cache-restore, tab-change-same-tab-noop (8 claims)
ok   selection-dispatch-toggle (5 claims)
ok   selection-dispatch-cleared-on-tab-change, selection-truck-toggle, selection-truck-survives-tab-change (4 claims)
ok   selection-survives-server-change, server-change-effects (5 claims)
ok   server-loss-no-request (3 claims)
ok   clear-data-resets (4 claims)
ok   selection-key-trim-mismatch (5 claims)
ok   selection-duplicate-ids (2 claims)
ok   selection-checkbox-predicates (4 claims)
ok   selection-disabled-stays-selected (2 claims)
ok   selection-across-page (3 claims)
ok   delay-parse-matrix, schedule-button-disabled-predicate (7 claims)
ok   delay-shared-and-persistent (4 claims)
ok   schedule-dispatch-success-clears, schedule-dispatch-failure-keeps-selection (4 claims)
ok   schedule-truck-success-clears, schedule-truck-failure-keeps-selection, truck-schedule-button-predicate (3 claims)
ok   share-button-disabled-predicate (3 claims)
ok   share-success-prunes-and-message, share-failure-keeps-selection (4 claims)
ok   treasure-claim-button-predicate, treasure-row-claim-predicate, treasure-claim-flow (4 claims)
ok   scheduled-loads-on-mount-and-events (4 claims)
ok   scheduled-loads-on-entry, scheduled-stale-job-reply-applies-in-arrival-order (3 claims)
ok   scheduled-count-and-render-order (3 claims)
ok   scheduled-ticker-conditions (3 claims)
ok   scheduled-clear-and-cancel (5 claims)
ok   app-controlled-vs-uncontrolled-tab, activity-hide-show-modelled (3 claims)

59 scenario ids, 145 claims, 0 failed
```

## Raw output of original/final-review.mjs (no --record)

```
DIFFERS [DEFECT?] C01-server-change-at-page-3
DIFFERS [DEFECT?] C02-options-refresh-validates-alliance-level-treasure
DIFFERS [DEFECT?] C03-clear-data-resets-filters
DIFFERS [DEFECT?] C04-clear-data-resets-name-selection
DIFFERS [DEFECT?] C05-share-failure-message
DIFFERS [DEFECT?] C06-export-messages
DIFFERS [DEFECT?] C07-scan-completion-and-progress-refetch
DIFFERS [DEFECT?] C08-treasure-defaults-and-query
DIFFERS [DEFECT?] C09-alliance-name-none-collision
same    [DEFECT?] C10-job-list-load-counts
same    [DEFECT?] C11-online-toggle-refetch
same    [DEFECT?] C12-name-select-at-page-3
same    [DEFECT?] C13-sort-at-page-3
DIFFERS [DEFECT?] C14-tab-counts-after-clear-data
DIFFERS [DEFECT?] C15-mark-button-predicate
same    [DOCUMENTED] C16-coordinate-and-jump-buttons
same    [DEFECT?] C17-checkbox-expiry-clock
DIFFERS [DEFECT?] C18-ticker-intervals
same    [DEFECT?] C19-schedule-handler-busy-and-failure
same    [DEFECT?] C20-cancel-clear-busy
same    [DEFECT?] C21-selection-after-clear-data
DIFFERS [DOCUMENTED] C22-search-error-message
same    [DEFECT?] C23-quality-filter-per-tab-and-reset
same    [DEFECT?] C24-item-filter-sort-cleanup
same    [DEFECT?] C25-pagination-prop-range
same    [DEFECT?] C26-stale-selection-snapshot
same    [DEFECT?] C04b-clear-data-name-with-real-backend-names-empty
DIFFERS [DEFECT?] C27-scan-start-error-banner

differing candidates: C01-server-change-at-page-3, C02-options-refresh-validates-alliance-level-treasure, C03-clear-data-resets-filters, C04-clear-data-resets-name-selection, C05-share-failure-message, C06-export-messages, C07-scan-completion-and-progress-refetch, C08-treasure-defaults-and-query, C09-alliance-name-none-collision, C14-tab-counts-after-clear-data, C15-mark-button-predicate, C18-ticker-intervals, C22-search-error-message, C27-scan-start-error-banner
DIFFERS [DEFECT?] C28-treasure-checkbox-persistence {"localStorageKeysInPage":["\"lwbridge.mapScanMode\"","autoStorageKey"]}
```
