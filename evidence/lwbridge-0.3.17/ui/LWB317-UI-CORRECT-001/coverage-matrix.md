# LWB317-UI-CORRECT-001 granular recoverable-state coverage

Status: **AWAITING_REVIEW**. This is worker evidence, not parity acceptance.

Lead disposition: **CHANGES_REQUIRED** after PM-007 review of `543b6eb`.
The matrix below preserves the worker audit; omitted Construction/Training/
Gather controls and AFK draft leakage show that its full-coverage claim is not
accepted. Map fixture filters/sort/page-size behavior was not implemented.
Home request scoping is accepted at the source/transport boundary.
See `lead-review/` evidence and UI-CORRECT-002 for continuation.

Locators below are zero-based UTF-8 byte offsets unless explicitly described as a
recovered expression or clone line. Exact source identities:

- MAIN = index-BVfnK1wp.js, SHA256 44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6.
- AUTO = AutomationPanel-BJ0gIqFh.js, SHA256 6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
- MAP = MapDataPanel-B4GXEND2.js, SHA256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
- SQUAD = SquadPanel-HC3-DJei.js, SHA256 ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7.
- CITY = CityLayoutPanel-DoNWkywK.js, SHA256 C1B83A0B6524EF9AF510A114DF9679DAE0260D34B633659EB1A5FFB2AC5CF49C.
- HOT = HotkeyPanel-XA8idRHB.js, SHA256 9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6.
- SET = SettingsPanel-DqxIWv_E.js, SHA256 129CCBACDD5F309070A3E2728912165006F0FA35FCF748D56F7AA9500D690C2B.

Each row names one of those exact hashes plus its locator. QA labels are defined
in interaction-qa.md. NATIVE_FENCED means browser preview cannot report native
success. BLOCKED_ORIGINAL means post-authenticated original pixels remain behind
the existing authorization boundary.

## Shared shell and Home

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Eight-page shell/navigation | MAIN 44C4E4…24C6; expression near byte 338037 | Visible exact shell; dormant Advanced excluded | src/App.jsx, src/Pages.jsx | BROWSER_RENDER + static check | BLOCKED_ORIGINAL pixels |
| Language selector/nine locales | MAIN 44C4E4…24C6 byte 216275 | en, zh-CN, zh-TW, ja, ko, vi, id, ru, pt | App.jsx; locales/*.js | en/ja/zh-CN renders; 9×1383 check | BLOCKED_ORIGINAL pixels |
| Theme toggle | MAIN 44C4E4…24C6 byte 356643 | Light/dark local shell state | App.jsx | BROWSER_REAL_INPUT light→dark | BLOCKED_ORIGINAL pixels |
| Cross-server popover | MAIN 44C4E4…24C6 byte 352124 | Local popover; Jump requires provider | App.jsx | BROWSER_REAL_INPUT; Jump disabled/disconnected | Native provider |
| Home profile/root selection | MAIN 44C4E4…24C6 byte 338037 | Profile gates profile-scoped settings | App.jsx, Pages.jsx | CONTRACT_TEST + render | Native lifecycle separate |
| Home auto-reconnect scope (R1) | MAIN 44C4E4…24C6 byte 338725 | Missing profile fails before dispatch; selected profile injected | backendBridge.js:85; App.jsx:168-177 | CONTRACT_TEST PROFILE_REQUIRED/profileId | Original native write not replayed |
| Home reconnect busy/ack/error | MAIN 44C4E4…24C6 byte 338725 | Checked state changes only after acknowledgement; busy disables control | App.jsx:168-177 | CONTRACT_TEST | BLOCKED_ORIGINAL pixels |
| Home checking/missing/connected | MAIN 44C4E4…24C6; recovered Home predicate inventory | Presentation only; no fake launch | Pages.jsx Home markers | marker check + render | Original post-auth states |
| Home repair/recovery failure | MAIN 44C4E4…24C6; recovered recovery predicate inventory | Failure details only in preview | Pages.jsx home-repair/home-recovery-failed | responsive dark zh-CN screenshot | Native repair/recovery lifecycle |

## Automation

All rows use AUTO SHA256 6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725.
Browser preview mutates local draft/presentation state only. Real Run now/task
execution remains disabled when disconnected.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Category tabs/per-card shell | AUTO 6DB232…1725 byte 37051 | One active category; card-specific settings/action | Pages.jsx:162,442 | BROWSER_REAL_INPUT Daily→Chat | BLOCKED_ORIGINAL pixels |
| Construction target branch | AUTO 6DB232…1725 byte 46993 | Target-level/type fields conditional on enabled | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Construction rewards/max builders | AUTO 6DB232…1725 byte 49154 | Source fields replace generic interval form | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Treatment amount | AUTO 6DB232…1725 recovered treatment expression | Numeric amount form, no invented interval fields | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Official Application target position | AUTO 6DB232…1725 recovered official-application branch | Numeric recovered position IDs | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Chat claim delay | AUTO 6DB232…1725 byte 37051 | Min/max seconds default 0/0 | Pages.jsx:254+ | BROWSER_RENDER | Persisted native config |
| Auto Reply branch | AUTO 6DB232…1725 byte 38540 | Off by default; reveals reply min/max and phrase textarea | Pages.jsx:254+ | BROWSER_REAL_INPUT; revealed 2/5 sec + textarea | Persisted native config |
| Treasure auto-search | AUTO 6DB232…1725 byte 37082 | Independent local checkbox | Pages.jsx:254+ | BROWSER_RENDER | Native search execution |
| Treasure dispatch branch | AUTO 6DB232…1725 byte 39998 | Reveals dispatch min/max, retry and Squads 1-4 | Pages.jsx:254+ | BROWSER_REAL_INPUT | Native dispatch execution |
| Trucks weekly/reset settings | AUTO 6DB232…1725 recovered Trucks branch | Source-backed reset + quality schedule | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Trucks ticket-departure option | AUTO 6DB232…1725 byte 68393 | Exact source checkbox | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Secret Task core settings | AUTO 6DB232…1725 byte 41933 | Collect/reset/quality schedule | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Secret Task alliance assistance | AUTO 6DB232…1725 byte 35301 | Assist enables qualities/delay/interval/no-ally state | Pages.jsx:254+ | SOURCE_HANDLER | Alliance runtime data |
| Ghost own auto-start | AUTO 6DB232…1725 byte 73911 | Exact semantic switch | Pages.jsx:254+ | SOURCE_HANDLER | Runtime task provider |
| Ghost alliance join/filter | AUTO 6DB232…1725 byte 74110 | Join reveals SR/UR/special filter | Pages.jsx:254+ | SOURCE_HANDLER | Runtime task provider |
| Ghost rewards/counters | AUTO 6DB232…1725 byte 73911 | Presentation counters only | Pages.jsx:254+ | BROWSER_RENDER | Runtime counters |
| Alliance Train strategies | AUTO 6DB232…1725 byte 53309 | Normal fixed/reward; VIP acceptance/strategy/thanks/tickets | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Tech donation threshold | AUTO 6DB232…1725 byte 51952 | Recovered threshold | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Interval-backed alliance cards | AUTO 6DB232…1725 byte 45290 | Gifts/Excavation/Center/Building resource/Armed Truck intervals | Pages.jsx:254+ | SOURCE_HANDLER | Persisted native config |
| Resource Gather recall | AUTO 6DB232…1725 byte 12386 | Local recall-on-disable boolean | Pages.jsx:345 | SOURCE_HANDLER + render | Runtime task provider |
| Trade Station tabs/toggles | AUTO 6DB232…1725 byte 8264 | Goods/History; cross-server/exclusive local booleans | Pages.jsx:387 | SOURCE_HANDLER + render | Runtime Trade data |
| System attack/shield card | AUTO 6DB232…1725 byte 44073 | Source-specific form retained | Pages.jsx:254+ | SOURCE_HANDLER | Runtime action provider |
| Save/saved/error/validation states | AUTO 6DB232…1725 recovered save-state expressions | Deterministic presentation only | automation-saving/saved/save-error/validation-error | marker check + render | Native persistence |

## Map Data

All rows use MAP SHA256 CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089.
The deterministic browser provider is read-only, online:false and preview-only.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Manual scan | MAP CE7434…3089 byte 44143 | Default unless map-auto state | MapDataPage.jsx:306 | CONTRACT_TEST + render | Native scanner |
| Auto scan/config | MAP CE7434…3089 byte 44302 | Interval 20-1440; servers/types/mode/return | MapDataPage.jsx:345-357,710 | CONTRACT_TEST + screenshot | Native scanner |
| Auto scheduled state | MAP CE7434…3089 byte 44302 | 60 min, 321/322, fast, deterministic nextRunAt | MapDataPage.jsx:346-354 | marker + render | Native schedule execution |
| City data | MAP CE7434…3089 byte 6190 | Exact table/filter path | map-city | all-kind CONTRACT_TEST + prior browser render | Original positive-row pixels |
| Resource data/filter | MAP CE7434…3089 byte 5721 | Resource name/filter/search | map-resource | BROWSER_REAL_INPUT City→Resource + filter 100281 | Original pixels |
| Monster data | MAP CE7434…3089 byte 5774 | Monster filters/table | map-monster | all-kind CONTRACT_TEST | Original pixels |
| Truck data | MAP CE7434…3089 byte 5819 | Truck table + batch schedule UI | map-truck; MapDataPage.jsx:845 | all-kind test; schedule disabled | Native schedule provider |
| Railway data | MAP CE7434…3089 byte 5864 | Railway rows/table | map-railway | all-kind CONTRACT_TEST | Live positive row separately state-blocked |
| Dispatch data | MAP CE7434…3089 byte 5918 | Selection + schedule UI | map-dispatch; line 843 | selection test; schedule disabled | Native schedule provider |
| Ghost data | MAP CE7434…3089 byte 5966 | Ghost rows/quality/table | map-ghost | all-kind CONTRACT_TEST | Live positive row separately state-blocked |
| Treasure data | MAP CE7434…3089 byte 6017 | Treasure type/status/table | map-treasure | all-kind CONTRACT_TEST | Live positive row separately state-blocked |
| Scheduled Plunder | MAP CE7434…3089 byte 50746 | Presentation row set | map-scheduled | marker + render | Native schedule provider |
| Alliance/foreign-radar filters | MAP CE7434…3089 bytes 51465/52280 | Kind-specific conditional filters | MapDataPage.jsx | SOURCE_HANDLER | Original pixels |
| Quality/status/plunderable filters | MAP CE7434…3089 bytes 11278/14164/54297 | Kind-specific conditional filters | MapDataPage.jsx | SOURCE_HANDLER | Original pixels |
| Sort/selection/pagination | MAP CE7434…3089 table renderer | Sort cycle; Set selection; fixture total > page | MapDataPage.jsx:213,333,559,857 | CONTRACT_TEST | Original pixels |
| Loading | MAP CE7434…3089 query-loading predicate | map-loading keeps query pending | mapPreviewApi.js | marker check | Native latency |
| Query error | MAP CE7434…3089 query-error predicate | MAP_FIXTURE_QUERY_FAILED | mapPreviewApi.js | marker check | Native error variants |
| Native scan/jump/mark/clear/export | MAP CE7434…3089 byte 54518 | Never succeed in preview | mapPreviewApi.js | CONTRACT_TEST PREVIEW_NATIVE_ACTION_BLOCKED | Native provider |
| Preview/native fence | MAP CE7434…3089 + bridge contract | Fixture only when bridgeMode=preview and map-* | mapPreviewApi.js:114+ | CONTRACT_TEST native/native-unavailable=null | None inside UI scope |

## Squads / AFK / Equipment

All rows use SQUAD SHA256 ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| AFK/Equipment tabs | SQUAD ED466E…06C7 bytes 192380/192395 | AFK default; equipment fixture may open Equipment | Pages.jsx:481-497 | BROWSER_REAL_INPUT | BLOCKED_ORIGINAL pixels |
| AFK profiles | SQUAD ED466E…06C7 byte 39957 | Two deterministic initial profiles | Pages.jsx:633 | BROWSER_RENDER | Runtime persistence |
| Profile Add | SQUAD ED466E…06C7 byte 39957 | Add local draft + open editor | Pages.jsx profile model | BROWSER_REAL_INPUT added third profile | Runtime persistence |
| Profile select/enable/delete | SQUAD ED466E…06C7 byte 39957 | Local preview CRUD | Pages.jsx:633 | SOURCE_HANDLER + render | Runtime persistence |
| Profile reorder | SQUAD ED466E…06C7 byte 39957 | Drag or ArrowUp/ArrowDown | Pages.jsx:633 | BROWSER_REAL_INPUT ArrowDown Steel→below Gold | Runtime persistence |
| AFK basic/assignments | SQUAD ED466E…06C7 editor expression | Name/target/custom/attack/continuous/squads | Pages.jsx:507+ | SOURCE_HANDLER + render | Runtime persistence |
| AFK join timing | SQUAD ED466E…06C7 byte 20433 | Join enabled; slot/delay mode | Pages.jsx:507+ | SOURCE_HANDLER | Runtime task provider |
| AFK member picker | SQUAD ED466E…06C7 byte 22369 | whitelist/blacklist, search, check, confirm/remove | Pages.jsx:507+ | SOURCE_HANDLER + render | Runtime alliance members |
| Alliance Drill | SQUAD ED466E…06C7 byte 32233 | active/squad/order/required error | Pages.jsx:562 | render + exact-key check | Runtime drill provider |
| Garrison settings | SQUAD ED466E…06C7 byte 9052 | Center default, adjacent optional, target priority | Pages.jsx:569 | BROWSER_REAL_INPUT opened | Runtime buildings |
| Garrison ally picker | SQUAD ED466E…06C7 byte 11289 | Search/available checkbox/confirm/cancel | Pages.jsx:569 | BROWSER_REAL_INPUT Casey selected; target count 1→2 | Runtime members |
| Garrison squad order | SQUAD ED466E…06C7 byte 12512 | Squads 1/2 selected fixture default | Pages.jsx:569 | BROWSER_RENDER | Runtime squads |
| Garrison recall | SQUAD ED466E…06C7 byte 14205 | Local boolean | Pages.jsx:569 | BROWSER_RENDER | Runtime stop/recall |
| Garrison native run | SQUAD ED466E…06C7 byte 9052 | Provider-only action | Pages.jsx:569 | disabled/disconnected render | Runtime provider |
| Zombie Bus compact branch | SQUAD ED466E…06C7 byte 17627 | Source-visible compact branch | AFK toolbar | BROWSER_RENDER | Runtime provider |
| Equipment presets | SQUAD ED466E…06C7 byte 192395 | 4 presets×4 squads×5 positions in fixture | Pages.jsx:687+ | BROWSER_REAL_INPUT AFK→Equipment | Runtime provider |
| Rename dialog | SQUAD ED466E…06C7 byte 190337 | Blank disables Save; Enter saves; Escape/backdrop closes; busy blocks | Pages.jsx:692,771-789,865 | BROWSER_REAL_INPUT blank; Raid+Enter; reopen+Escape | Runtime persistence |
| Same-slot item drag | SQUAD ED466E…06C7 bytes 189283/183734 | Same slot only; wrong slot message | Pages.jsx:721-755,853 | SOURCE_HANDLER | Browser connector physical-drag limitation; runtime identities |
| Hero-loadout drag | SQUAD ED466E…06C7 byte 183734 | Whole-position swap | Pages.jsx:721-755,846 | SOURCE_HANDLER | Browser connector physical-drag limitation |
| Squad-loadout drag | SQUAD ED466E…06C7 byte 183734 | Swap corresponding five positions | Pages.jsx:757-769,841-842 | SOURCE_HANDLER | Browser connector physical-drag limitation |
| Partial apply result | SQUAD ED466E…06C7 byte 182958 | Deterministic partial presentation only | squads-equipment-result | marker check | Native apply provider |
| Apply progress | SQUAD ED466E…06C7 byte 183294 | Deterministic running presentation only | squads-equipment-progress | marker check | Native apply provider |
| Equipment provider actions | SQUAD ED466E…06C7 provider expressions | Refresh/read/apply/save+apply never fake success | Pages.jsx:490,832-834 | BROWSER_RENDER all disabled | Native equipment provider |

## City Layout

All rows use CITY SHA256 C1B83A0B6524EF9AF510A114DF9679DAE0260D34B633659EB1A5FFB2AC5CF49C.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Populated workbench/zoom | CITY C1B83A…CF49 bytes 13431-13679 | 64 cells; 3 buildings; 2 movable; zoom 24px | Pages.jsx:914+ | BROWSER_RENDER + screenshot | Runtime city data/geometry |
| Click/Ctrl selection/Escape | CITY C1B83A…CF49 byte 13559 | Single click, Ctrl/Meta toggle, Escape clear | Pages.jsx:1066+; 983-1002 | BROWSER_REAL_INPUT Barracks; Escape clear | Original pixels |
| Box selection/Shift additive | CITY C1B83A…CF49 bytes 13431/13495 | Grid pointer rectangle; Shift adds | Pages.jsx:969-980,1035-1044 | SOURCE_HANDLER | Physical pointer-box harness not replayed |
| Drag move/group | CITY C1B83A…CF49 byte 13620 | Movable only; bounds/overlap/invalid cells rejected | Pages.jsx:957-967,1045-1053 | BROWSER_DOM_DRAG Barracks (5,2)→(6,5) | Connector physical HTML5 drag limitation |
| Invalid overlap rejection | CITY C1B83A…CF49 byte 18993 | Conflict => no commit | Pages.jsx:893-930,966 | BROWSER_DOM_DRAG onto HQ left position unchanged | Runtime city geometry |
| Undo/Redo | CITY C1B83A…CF49 bytes 11499/11595 | Enabled only with history | Pages.jsx:939-955,1011-1012 | BROWSER_REAL_INPUT Undo; Ctrl+Y redo | Original pixels |
| Restore initial | CITY C1B83A…CF49 byte 11683 | Enabled only with changes | Pages.jsx:1013 | BROWSER_RENDER enabled after move | Original pixels |
| Validation/conflict state | CITY C1B83A…CF49 bytes 17816/18993 | Valid or conflict set | Pages.jsx:893-930 | valid DOM drag + conflict screenshot | Runtime data |
| Refresh/Save/Apply | CITY C1B83A…CF49 provider expressions | Provider-only | Pages.jsx:1010,1014-1015 | disabled/disconnected render | Native city provider |

## Hotkeys

All rows use HOT SHA256 9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Seven shortcut switches | HOT 9BD7C1…1BE6 byte 185 | Connected/save-error fixture enables local switches | Pages.jsx:1141-1157 | BROWSER_REAL_INPUT Attack off→on | Persisted native config |
| Attack item speedup | HOT 9BD7C1…1BE6 byte 5321 | Enabled with Attack switch | HotkeyCard | BROWSER_REAL_INPUT checked | Persisted native config |
| Attack diamond fallback | HOT 9BD7C1…1BE6 byte 5649 | Nested attack option | HotkeyCard | BROWSER_REAL_INPUT checked | Persisted native config |
| Load error | HOT 9BD7C1…1BE6 byte 2610 | Deterministic presentation | hotkeys-load-error | marker check | Native loader |
| Save error | HOT 9BD7C1…1BE6 byte 3040 | Error + local controls | hotkeys-save-error | Japanese browser render + screenshot | Native saver |

## Mini Games

Mini Games is recovered from the same HOT source.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Black Market chest toggle | HOT 9BD7C1…1BE6 byte 6074 | Browser-local presentation only | Pages.jsx:1162,1188-1190 | BROWSER_REAL_INPUT off→on | Native gameplay/provider |
| Land Cell result states | HOT 9BD7C1…1BE6 bytes 3236/3363 | Success/error presentation; action disabled | mini-games-land-success/error | marker check | Native gameplay |
| Food House solving/executing | HOT 9BD7C1…1BE6 bytes 1312/1360 | Deterministic status/progress | Pages.jsx:1163-1175 | marker check | Native gameplay |
| Food House completion/conflict states | HOT 9BD7C1…1BE6 recovered state expression | complete/all-complete/activity-ended/ui-open/conflict | Pages.jsx:1164-1175 | marker check | Native gameplay |
| Solve-failed/unsupported/start-failed | HOT 9BD7C1…1BE6 bytes 1732/1793 | Error/result presentation only | mini-games-solve-failed/unsupported/start-failed | browser solve-failed + marker checks | Native gameplay |
| Unlock/Start/Stop actions | HOT 9BD7C1…1BE6 byte 6461 + Food House branch | Game-required actions | Pages.jsx:1195,1204 | BROWSER_RENDER disabled | Native gameplay provider |

## Settings

All rows use SET SHA256 129CCBACDD5F309070A3E2728912165006F0FA35FCF748D56F7AA9500D690C2B.

| Recoverable surface / branch | Source hash + locator | Predicate/default/limits | Clone location | QA | Open dependency |
|---|---|---|---|---|---|
| Show FPS | SET 129CCB…0C2B byte 2878 | Default off; local preview state | Pages.jsx:1215,1231 | BROWSER_REAL_INPUT on | Persisted native config |
| Show Ping | SET 129CCB…0C2B byte 2993 | Default off; local preview state | Pages.jsx:1216,1231 | BROWSER_REAL_INPUT on | Persisted native config |
| Metrics load error | SET 129CCB…0C2B byte 2031 | Deterministic error | settings-visual-error | marker check | Native loader |
| Account focus-game | SET 129CCB…0C2B byte 3339 | Visible in multiprofile/complete; default on | Pages.jsx:1214,1217,1233 | BROWSER_REAL_INPUT on→off | Persisted native config |
| Feedback progress | SET 129CCB…0C2B byte 878 | Deterministic 45%; export disabled | Pages.jsx:1234-1240 | marker check | Native exporter |
| Feedback success/error | SET 129CCB…0C2B bytes 1128/1264 | Presentation only | settings-feedback-success/error | marker check | Native exporter |
| Updater checking/available/downloading/error | SET 129CCB…0C2B updater branch | Presentation only | Pages.jsx:1219-1248 | markers + dark available screenshot | Native updater |
| Diagnostic/update actions | SET 129CCB…0C2B byte 878 + updater branch | Provider-only | Pages.jsx:1240,1248 | BROWSER_RENDER Export/Check disabled; Download disabled fixture | Native providers |

## R1-R4 disposition

| Finding | Worker disposition | Evidence |
|---|---|---|
| R1 Home reconnect missing profile scope | **Corrected; awaiting lead review.** | invokeProfileScoped + PROFILE_REQUIRED + focused Home contract check. |
| R2 Automation generic/invented forms | **Corrected against recovered per-card source; awaiting lead review.** | AUTO byte anchors plus real Treasure conditional-browser interactions. |
| R3 Equipment nested interactions missing | **Corrected for source-recoverable browser-local interactions; awaiting lead review.** | Rename/keyboard/modal QA plus exact drag/result/progress source anchors; native actions remain fenced. |
| R4 broad coverage evidence insufficient | **Replaced with granular evidence; awaiting lead review.** | This matrix, source-manifest.json, interaction-qa.md and 11 pinned screenshots. |

No row converts unavailable native/gameplay functionality into success, and no
row claims full original visual parity.
