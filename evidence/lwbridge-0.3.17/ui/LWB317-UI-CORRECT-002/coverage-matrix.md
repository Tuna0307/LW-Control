# CORRECT-002 affected branch coverage

This matrix covers the interrupted assignment and the continuation. It is not
an exhaustive eight-page or full-runtime parity matrix. `source-contracts.json`
contains exact source hashes, zero-based UTF-8 anchors/excerpts and the field
producer/consumer trace. `browser-qa.json` records actual visible outcomes.
All completed local branches retain `IMPLEMENTED_NOT_VALIDATED` for original
and native parity; the local assertions below are narrower evidence.

| Branch | Source contract ID | Local evidence | Disposition / limit |
|---|---|---|---|
| Construction category order/filter and tabpanel | construction-category | Browser populated fixture | Implemented; synthetic building inventory |
| Construction ArrowRight, End, Home and focus handler | construction-category | Real browser keyboard input | Implemented; ArrowLeft handler source-reviewed, not separately asserted |
| Building checkbox/selected summary/empty hint | construction-category | Browser clear both defaults then select Farm | Implemented; empty list is a hint, not an invented save prohibition |
| Builder limit and correction | construction-values | Browser21 rejected,2 retained through collapse/tab/navigation | Implemented; fixture total20; current native builder total unavailable |
| Target level/default/auto-claim fields | construction-values | Controlled drafts and task validator | Implemented local; no construction action proof |
| Training positive/invalid quantity and enable guard | training-quantity | Browser1000001 error, cannot enable;500 valid/locked | Implemented local |
| Training order reuse/new activation | training-quantity | Actual activateTraining helper tests with reusable/unconfirmed orders | Implemented local contract; no native order/progress proof |
| Available-level union, selected unavailable level | training-level | Browser Level6 selectable; saved9 disabled | Implemented; names still need actual runtime assets |
| Order progress/reason, trained/promoted/collected | training-state | Browser recorded text in populated/no-order fixtures | Implemented synthetic values |
| Training no camps/data unavailable/error/open failed | training-state | Browser exact branch observations and camp rejection | Implemented; camp click never invokes gameplay |
| Camp production/time/idle detail | training-state | Browser populated two-camp fixture | Implemented; native atHome/status binding remains separate |
| Gather radius/resume/recall | gather-options | Browser500/30 choices and tab roundtrip | Implemented; no native config persistence |
| Gather squad/resource/level and no-squad guard | gather-squads | Browser gold max8, enabled-squad guard and independent drafts | Implemented local; inventory/caps synthetic |
| Gather recalling/recall-failed/unconfirmed/runtime-wait/manual/shield branches | gather-squads | Source/handler review and explicit fixture selectors | Implemented branches; not all separately browser-asserted |
| Normal Train fixed one-or-none/clear/Driver | train-fixed | Browser select/clear/reselect and disabled Driver | Implemented |
| VIP Train selection cap2 and clearing | train-fixed | Browser blocks third unselected choice; clearing source handler reviewed | Implemented local |
| Reward selected-first ordering and up/down | train-rewards | Browser Medal/Parts select and Move up; tab roundtrip | Implemented; synthetic reward options/icons |
| Reward HTML5 drag ordering | train-rewards | Actual drag handlers reviewed | Implemented; physical drag NOT proven |
| Train modes/VIP/quantity/thanks/ticket conditional drafts | train-rewards | Source contract and controlled model review | Implemented; not every combination browser-asserted |
| Treatment/Donate/Official/interval forms | task-numeric-validation | Task-specific validator and named drafts; Official0 enable fenced | Implemented local controls; detailed runtime status rows still missing |
| Chat claim/reply/dispatch ranges, phrase requirement, drafts | chat-validation | Browser phrase requirement, reversed delays, tab roundtrip; targeted tests | Implemented local; payload normalization/native saves not proved |
| Dispatch Assist ranges/qualities | assist-validation | Targeted invalid-delay/no-quality checks | Implemented draft validation; allied positive task scheduling picker remains missing |
| Alliance Gathering selection/priority | task-numeric-validation plus source automation-squad-priority-item | Controlled selection, last-squad guard, source drag handlers | Implemented local; physical ordering not proven |
| Weekly Trucks/Secret Task quality choices | Exact Automation source defaults | Existing source-backed options retained through hidden categories | Present; weekly values still use uncontrolled select state and are not covered by error/discard adapter |
| Remaining simple/background Automation cards | Exact AutomationCard/Automation source | Prior structure retained; task action buttons fenced | Detailed runtime summaries/results/errors and shield/task timing presentation require another UI inventory pass |
| Trade Station currencies/goods/history | Exact Automation source le/ue/fe/de | Existing empty-state structure retained | Positive currency/goods selection, item details and purchase history rows remain implementation gaps, not auth/native-only blockers |
| AFK whole per-ID model/selection | afk-profile-drafts | Two farm profiles with distinct target/squad/count/distance edits and roundtrips | Implemented; no editor reset workaround |
| New AFK profiles | afk-profile-drafts, afk-join-defaults | Two newly added join profiles with independent defaults/members | Implemented; confirmed state distinguishes new/editor |
| AFK list/custom target and metadata | afk-targets-filters | Custom switch restore to Food; helper identity metadata tests | Implemented fixture contract; actual option inventory/undiscovered/attackable-range presentation still needs fuller UI pass |
| AFK squad/count/level/distance/progressive | afk-targets-filters | Browser drafts and helper invalid numeric/filter checks | Implemented local; actual searchable attack-range warning remains missing |
| Join enable/mode/slot/delay/precision/list | afk-join-defaults | Browser new defaults; validator boundaries/duplicate UID tests | Implemented local; all conditional values retained per ID |
| Join member search/stage/confirm | afk-members | Browser Avery select/confirm and two-profile roundtrip | Implemented fixture; member-loading/failed/left/self/missing-target variants not all implemented/validated |
| Draft debounce/ack/concurrent edits/error/retry/discard | draft-save-state | Actual store deferred-write tests; browser AFK discard/Automation retry | Implemented local memory; no native persistence claim |
| AFK Equipment/main navigation and Automation collapse/category/main navigation | draft-save-state | Browser roundtrips | Draft data survives; not every view's transient UI state is persisted |
| Read-only Map keyword/alliance/marks/names/quality/items/status/levels/radar/lucky filters | Current MapContracts/MapStore.Query.cs; map query contract already recovered by Map campaign | Actual provider deterministic tests, browser alliance/zero match | Applied to disclosed complete fixed dataset; production provider unchanged |
| Map supported sort keys/null-last/tie-break/distance ordering | MapStore.Query.cs NormalizeSorts/SortExpression | Provider tests and browser descending Level | Implemented on fixture values; unsupported sorts reject even empty/single-row results |
| Map page/pageSize/totals/final/out-of-range/zero | MapStore.Query.cs Search | Provider10-size pages, final page, out of range; browser50+2 rows | Implemented consistent totals |
| Map fixture fencing/native action rejection | Existing getMapPreviewProvider/backendBridge mode gate | Both native modes return no fixture; all mutation functions reject | Preserved; online remains false |
| Home reconnect correction | PM-007 accepted source/transport fix | Actual Home callback deferred ack/reject/no-profile harness rerun | Preserved; native lifecycle remains separate |

All other pages and live Map campaign states keep their prior dispositions.
The focused corrections close the demonstrated C1 controls, C2 leakage and C3
fixture-query defects. They do not close the additional source-recoverable gaps
listed above or the original authenticated pixel comparison.
