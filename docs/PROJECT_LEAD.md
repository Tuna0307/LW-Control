# Project lead control sheet

Current lead decision, 2026-10-03: MAP-REFRESH-OWNERSHIP-001 and the prior four Map units remain accepted for focused source/local UI scope. MAP-AUTO-CONFIG-001 is PARTIAL after interruption: A/B/C implementation is pushed through f5a1eaf; focused helper/profile/control/current-ownership and check/package reruns pass. Resume milestone D verification/evidence only; historical extractors need current adapters. MAP-CLOSEOUT-002 remains PARTIAL. One worker, no subagents. Full UI/original pixels/native parity remains unaccepted. See the dated Auto interrupted-progress review.

Recovery update, 2026-10-03: four prior Map units and completed ownership child are accepted for focused scope. Remaining Auto UI/configuration is assigned separately to one worker; the parent campaign stays PARTIAL. See dated Auto dispatch review.

The main project lead owns integration and should keep this file small and current.

## Current target

LWBridge 0.3.17

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Current parity scope

Target one-for-one parity for the **in-scope post-auth product experience**.

The original login/account/licensing/entitlement system is intentionally
`OUT_OF_SCOPE` for reconstruction.

Owner clarification, 2026-10-01: necessary auth-related local dependency
research is allowed within a named in-scope feature, using supplied artifacts
and authorized access; the login UI remains excluded. See `AGENTS.md` section 6.
Use one canonical production path and fix defects there. No legacy product
fallback is wanted; the existing `--legacy-ui` switch has a separate pending
retirement task. Preserve its source as historical evidence.

## Current phase

**UI parity — Map refresh ownership accepted; complete Auto UI/configuration next**

Earlier static UI baseline acceptance does not establish complete UI/UX parity.
The current owner priority is UI reproduction before function integration.
Direct post-auth runtime visual comparison remains `BLOCKED` by the original
auth boundary. The historical Phase 2 Map campaign is preserved at
`docs/GOAL_CAMPAIGN_PHASE2_MAP.md`; this assignment does not reopen it.

## Current work items

The 2026-10-01 incoming handoff was stale at `13b25f6`; takeover inspected the
actual clean/pushed campaign baseline `389df37`. Preserve existing implementation.
Static UI baseline acceptance does not imply complete Home state coverage or
original runtime pixel parity. Complete all recoverable UI states/interactions,
starting with Home and Map, under the full UI task below while
the Map closeout awaits a separate independent acceptance review.

| Work item / campaign | Owner | State | Scope |
|---|---|---|---|
| LWB317-UI-MAP-AUTO-CONFIG-001 | Single worker, interrupted | PARTIAL / A-B-C pushed | f5a1eaf saves helpers, profile ownership and controls; focused checks pass. Resume D: current regression adapters, browser evidence, validator and delivery. No subagents/native executor |
| LWB317-UI-MAP-REFRESH-OWNERSHIP-001 | Returning worker, lead reviewed | COMPLETE / ACCEPTED for focused source/local scope | cf75b4e actual App/panel ownership, overlap/disposal/count reset, redirect/Clear and interaction/package replays pass |
| LWB317-UI-MAP-CLOSEOUT-002 | Interrupted worker, split into children | PARTIAL / SPLIT | A and B accepted for focused scope; C source recovered, implementation assigned under AUTO-CONFIG-001 |
| LWB317-UI-MAP-REFRESH-FEEDBACK-001 | Project lead takeover | COMPLETE / ACCEPTED for focused source/local scope | Row revision timing, scan error rendering/priority and export localized labels/result/busy; 45/19/11 checks pass. Full options/poll/native/pixels excluded |
| LWB317-UI-MAP-SCAN-HEADER-001 | Project lead takeover | COMPLETE / ACCEPTED for focused source/local scope | Original timing/summary, stored-run matching, fractional progress and optional state; 270 comparisons/clock/fences/browser pass. Native timing/actions/pixels excluded |
| LWB317-UI-MAP-GOODS-PICKER-001 | Project lead takeover | COMPLETE / ACCEPTED for focused source/local scope | Original Truck/Train details menu, strict raw keys/icon placeholders and close-after-change; 144 comparisons, actual parent query/sort and browser controls pass; native icons/pixels excluded |
| LWB317-PM-028 | Project lead | COMPLETE | Accepts ff4ed369 R1 and parent for focused source/local UI scope after actual diff/original/regression/package checks. PM-027 failed baseline retained |
| LWB317-UI-MAP-TREASURE-PICKER-001 | Project lead takeover | COMPLETE / ACCEPTED for focused source/local scope | Source details/menu, resolved labels/counts, strict key selection and close-after-change; 28 original/current cases, actual parent callback, adapters and browser en/ja light/dark pass. Full Map/pixels/native excluded |
| LWB317-PM-027 | Project lead | COMPLETE review / CHANGES_REQUIRED | Delivery/source/regression/package replays pass; one added exact original/current options-only redirect case settles on different servers. Five extra availability/obsolete-rejection cases pass; scoped R1 assigned |
| LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1 | Returning worker, lead-reviewed | COMPLETE / ACCEPTED for focused source/local UI scope | PM-028 accepts ff4ed369: lead 6/6, R1 5/5, delayed-Clear and maintained regression/package checks pass. UI only |
| LWB317-UI-MAP-FILTER-LIFECYCLE-001 | Returning worker, lead-reviewed | COMPLETE / ACCEPTED for focused source/local UI scope | PM-028 closes PM-027 through ff4ed369 R1. Four delivered milestones and delayed-Clear behavior retained; native/original-pixel/global status unchanged |
| LWB317-PM-026 | Project lead continuation | COMPLETE | Independently replays all four milestones and full scheduled suite, closes final en/ja BR6 evidence, corrects stale docs and accepts scoped UI work. Remaining original/UI/native gaps stay explicit |
| LWB317-UI-MAP-INTERACTIONS-001 | Worker + two subagents; lead verified | COMPLETE / ACCEPTED for focused source/local UI scope | 5b76ae8 A-D accepted by PM-026: request disposal, search/names/selection, Scheduled presentation and integrated QA. Full scheduled replay plus fresh browser checks pass; native/functions/pixels excluded |
| LWB317-PM-025 | Project lead | COMPLETE review / CHANGES_REQUIRED | Delivery checks pass, but two independent availability cases show obsolete search success/rejection after backend loss; baseline ignored both. Correction is milestone A of INTERACTIONS-001 |
| LWB317-UI-MAP-NAVIGATION-001 | Worker delivery plus INTERACTIONS correction; lead-reviewed | COMPLETE / ACCEPTED for focused source/local UI scope | PM-026 verifies disposal correction and source-like first-page server requests. PM-025 failed evidence retained; no full Map/native/pixel acceptance |
| LWB317-PM-024 | Project lead | COMPLETE | Lead accepts focused FILTERS-001 after inspecting independent review af73d18 and replaying actual-code/source/evidence/package checks. Independent Checking browser limitation recorded; full UI/native/pixels remain open |
| LWB317-PM-023 | Project lead takeover | COMPLETE implementation checkpoint | Per-kind Map filter ownership and exact Secret Task level corrected; isolated EN/JA Checking preview added. Focused acceptance subsequently recorded by PM-024 |
| LWB317-UI-MAP-FILTERS-001 | Project lead, independently reviewed | COMPLETE / ACCEPTED for focused source/local scope | Four per-kind filters, query/table projection, goods-sort clear, exact level and isolated Checking inputs accepted by PM-024. Native/full UI/pixel reachability remains open |
| LWB317-REVIEW-MAP-FILTERS-001 | Returning worker, lead-integrated | REVIEW_COMPLETE / ACCEPTED for focused source/local scope | af73d18 recommendation independently checked and adopted by lead PM-024. No product edits. Empty independent Checking browser session explicitly limits visual claims |
| LWB317-POLICY-COS-001 | Project lead | RETIRED / SUPERSEDED | Owner returns to manual prompt/reply relay. Mandatory Chat On Steroids workflow removed; historical policy/evidence preserved. Connector repair is no longer a project blocker; existing lead review and bounded worker assignments continue |
| LWB317-PM-021 | Project lead | COMPLETE | Accepts independent Home busy review ff787e3 for focused source/local display/predicates; missing production lifecycle inputs remain explicit. Assigns only Truck/Train table review next |
| LWB317-PM-022 | Project lead takeover | COMPLETE | Integrates three owner-authorized UI lanes: independently reviewed Map row correction and basic tables, Trade 003E status review plus inactive fixture correction. Check/build/package pass; remaining UI gaps preserved |
| LWB317-UI-PARALLEL-001 | Project lead takeover + two authorized subagents | COMPLETE for assigned source/local scope | All three lanes delivered and lead-integrated by PM-022. No active worker implementation remains in this campaign; original pixels/native/full UI stay open |
| LWB317-UI-MAP-ROWS-001 | Project lead, independently peer-reviewed | COMPLETE / ACCEPTED for focused source/local scope | Follow/Following, raw Truck selection labels, Jump/invalid coords, marks/tooltips/classes/keys/reward cells corrected; 160 main cases and 41 independent final peer cases pass; no native provider introduced |
| LWB317-REVIEW-TRADE-STATUS-001 | Subagent A, lead-reviewed | COMPLETE / ACCEPTED for focused source/local scope | 68 component comparisons + six original effects + two inactive/native cases + eight browser flows. Synthetic goods/history/success leaked into inactive Trade; narrow correction accepted by PM-022. Native provider absent |
| LWB317-REVIEW-MAP-BASIC-001 | Subagent B, lead-integrated | REVIEW_COMPLETE / ACCEPTED for corrected current source/local scope | Preserves 104 baseline failures, verifies corrected City/Resource/Monster 372/0, browser DOM/image evidence and final Map peer review. Broad interactions/original pixels remain open |
| LWB317-REVIEW-MAP-TRANSPORT-001 | Returning worker; lead reproduced review result | REVIEW_COMPLETE / CHANGES_REQUIRED | 266ce2a review: two defects, 14 mismatches (Live Target content and Truck selection labels). Lead reran checker and evidence validator; corrections assigned under UI-PARALLEL-001. Historical evidence preserved |
| LWB317-PM-020 | Project lead | COMPLETE | Integrates independent Map-states review 6f52096 for three-table source/local formatting/selection scope; records missing Treasure refreshing producer as an open UI gap and reassigns Home busy review |
| LWB317-REVIEW-MAP-STATES-001 | Returning worker, lead-integrated | REVIEW_COMPLETE / ACCEPTED for focused source/local scope | PM-020 verifies 56 task comparisons, Treasure state/name cases, rendered labels/selection, source/image hashes and package checks. Missing refreshing producer and original pixels/native behavior remain outside acceptance |
| LWB317-UI-MAP-TREASURE-REFRESH-001 | Unassigned | QUEUED — frontend/host connection follow-up | PM-023 recovers exact context/refresh producer and adds isolated Checking preview. Existing desktop handlers are verified by source; canonical mapApi does not expose them. Live UI producer/viewer context/operation lifecycle remains unconnected; no native behavior is accepted |
| LWB317-UI-LEAD-TABLES-001 | Project lead + independent reviews | COMPLETE / ACCEPTED for reviewed normal-table source/local scope | PM-020 three-table review plus PM-022 corrected transport/basic row reviews complete the eight-kind formatting/metadata/selection scope. Broader actions/filters/scheduling, Treasure refreshing producer, assets/native/original pixels remain open; no complete Map acceptance |
| LWB317-PM-001 | Project lead | COMPLETE | Repository/documentation reset for 0.3.17 |
| LWB317-PM-003 | Project lead | COMPLETE | Takeover source/evidence audit, current path map and exact CSS checkout repair; no new live test/full Map acceptance |
| LWB317-PM-015 | Project lead, owner requested own tasks while worker rests | COMPLETE for source/state audit | 960 Home comparisons locate busy-label/header, error translation/channels and localized switch-description gaps. No product edit/native test; review 2026-10-02-LWB317-PM-015-home-audit.md |
| LWB317-UI-HOME-PRESENTATION-QUEUE | Project lead / returning worker | COMPLETE for named source/local corrections | Switch accepted by PM-016, translation by PM-017, corrected channels by PM-019 and busy by PM-021. Full Home pixels/native lifecycle and production proxy/launch busy producers remain open |
| LWB317-UI-SWITCH-LOCALE-001 | Returning worker, lead-reviewed | COMPLETE / ACCEPTED for focused source/local scope | PM-016 accepts 47c243a after independent helper-only scope/hash/locator, 360 comparisons, Home regression and rebuilt package checks |
| LWB317-PM-016 | Project lead | COMPLETE | Focused source/local switch-description acceptance; no original/native/full UI acceptance |
| LWB317-REVIEW-HOME-ERROR-001 | Returning worker as independent reviewer | REVIEW_COMPLETE / integrated by PM-017 | baf5473 recommends ACCEPT after 13 independent cases and unchanged nine-locale/regression/browser/package evidence |
| LWB317-UI-HOME-ERROR-001 | Project lead implementation, independent worker-reviewed | COMPLETE / ACCEPTED for focused source/local scope | PM-017 integrates independent review baf5473 after actual case/locale/render and package checks; no native contracts or original-pixel acceptance |
| LWB317-PM-017 | Project lead integrates independent review | COMPLETE | Source/local Home translation and recovery composition acceptance only; channels/busy/native proof separate |
| LWB317-REVIEW-HOME-ERROR-002 | Returning worker as independent reviewer | REVIEW_COMPLETE / integrated by PM-018 | db3aae3 recommends CHANGES_REQUIRED for root-status acknowledgement/polling; eight actual-callback/bridge cases reproduced by lead |
| LWB317-UI-HOME-ERROR-002 | Project lead, worker correction reviewed | COMPLETE / ACCEPTED for focused source/local scope | PM-019 resolves PM-018 acknowledgement/polling defect through accepted R1; independent root/action placement and preference ordering retained; native/original proof open |
| LWB317-PM-018 | Project lead | COMPLETE | Integrates independent channel review; root acknowledgement/polling defect reproduced; correction assigned, not implemented |
| LWB317-UI-HOME-ERROR-002-R1 | Returning worker, lead-reviewed | COMPLETE / ACCEPTED for focused source/local scope | PM-019 accepts 8415316: profile-effect initial root request, shared acknowledgement, no repeated root polling; 14 scenarios and four independent comparisons pass |
| LWB317-PM-019 | Project lead | COMPLETE | Accepts source/local root correction; no native/pixel/global UI acceptance |
| LWB317-REVIEW-HOME-BUSY-001 | Returning worker, lead-integrated | REVIEW_COMPLETE / ACCEPTED for focused source/local scope | PM-021 accepts ff787e3: 17 independent cases, exact source/current expressions, producer audit, saved-image and regression/package checks |
| LWB317-UI-HOME-BUSY-001 | Project lead, independent worker-reviewed | COMPLETE / ACCEPTED for focused source/local scope | PM-021 accepts header/button/picker precedence and predicates with distinct busy inputs. App produces folder/preference busy only; proxy/launch producers absent and native lifecycle remains fenced unavailable. No full Home/pixel/native acceptance |
| LWB317-UI-HOME-STATES-001 | None | SUPERSEDED | Historical Home-only proposal; replaced by full UI coverage |
| LWB317-UI-COMPLETE-001 | Previous worker | CHANGES_REQUIRED | Useful partial UI checkpoint at `a253cce`; PM-006 found missing Home preference scope, incorrect Automation forms and incomplete Equipment interactions; full source-backed coverage not accepted |
| LWB317-UI-CORRECT-001 | Previous worker | CHANGES_REQUIRED | PM-007 accepts Home profile-scope fix and retains useful nested UI work; Automation controls remain omitted, AFK drafts leak and Map fixture QA ignores query semantics |
| LWB317-UI-CORRECT-002 | Interrupted worker; project lead continuation requested by owner | AWAITING_REVIEW | Focused Automation controls, isolated AFK drafts/save states and applied Map fixture queries completed with source/state/browser evidence; coverage matrix records remaining UI gaps; no new native/game integration |
| LWB317-UI-CORRECT-003 | Previous worker; split by lead at owner's request | PARTIAL | Pushed 5204f67 adds substantial Automation/AFK code and baseline review; checks pass but Stage B browser evidence/master closeout incomplete; broad execution replaced by smaller continuations |
| LWB317-UI-CORRECT-003A | Worker delivery 2f439ce plus R1 d065d08 | COMPLETE / ACCEPTED for focused local scope | PM-010 accepts source-backed defaults, selection/save editability and corrected immediate dispatch/local recovery proof; original pixels/native persistence unproved |
| LWB317-UI-CORRECT-003A-R1 | Worker delivery d065d08, lead-reviewed | COMPLETE / ACCEPTED | Both actual callbacks dispatch immediately, retain newer edits during deferred writes and pass actual Retry/Discard; source anchors corrected |
| LWB317-UI-CORRECT-003B | Worker delivery a24bf6c plus R1 bfc652f, lead-reviewed | COMPLETE / ACCEPTED for focused local scope | PM-012 accepts source/local Trade selection, first-offer metadata, ID-based labels and saving/recovery; original/native/full UI parity unproved |
| LWB317-UI-CORRECT-003B-R1 | Worker delivery bfc652f, lead-reviewed | COMPLETE / ACCEPTED | Both production composition expressions match exact source; all three lead cases and unchanged selection/package/evidence checks pass |
| LWB317-UI-CORRECT-003C | Worker delivery 1a25a9c, lead-reviewed | COMPLETE / ACCEPTED for focused local scope | PM-013 accepts source/local Purchased-items grouping, totals, names, row fields and empty state; native images remain placeholders, purchasing/original/native parity unproved |
| LWB317-UI-CORRECT-003D | Worker delivery bd808994, lead-reviewed | COMPLETE / ACCEPTED for focused local scope | PM-014 accepts offline-only disabling and actual immediate/concurrent/recovery saves; no cross-server gameplay/native persistence acceptance |
| LWB317-UI-CORRECT-003E | Project lead, independent A review integrated | COMPLETE / ACCEPTED for focused source/local scope | PM-022 accepts counter/result/loading/error/retained-data behavior plus verified inactive/native fixture correction. Independent component/effect/native-fence and real browser checks pass. No native fetch/status provider or purchasing acceptance |
| LWB317-UI-001A | Worker AI | COMPLETE / ACCEPTED | Static frontend package inventory/extraction |
| LWB317-UI-CAMPAIGN-8H | Goal worker | ACCEPTED | Static UI recovery/reconstruction accepted; direct post-auth visual validation remains blocked |
| LWB317-COMPAT-MAP-V22-001 | Goal worker | COMPLETE | Installed Last War v22 Map compatibility revalidated statically/source-first; no production Map change required |
| LWB317-LIVE-MAP-V22-001 | Goal worker | AWAITING_REVIEW | Fresh v22 owned-session readiness/world metadata, coordinate navigation, a 2500/2500 Resource scan, query/filter/sort/pagination and clear/reset live-proven; its 678-row population was later shown not to be a trustworthy completeness census |
| LWB317-LIVE-MAP-V22-002 | Goal worker | AWAITING_REVIEW | Fresh same-session v22 proof covers genuinely active Resource scan stop/cancel, readiness after stop, clear/reset continuity, second scan/start-stop-clear in the same profile/instance/PID/server, false connection-loss root cause/fix, and structured orphan-free cleanup |
| LWB317-MAP-RESOURCE-COMPLETENESS-PREP-001 | Goal worker | AWAITING_REVIEW | Offline-only Resource completeness observability prepared: raw point/candidate/rejection/dedupe accounting, accepted-state/spatial distributions, and lossless source/published-row evidence for a separately authorized future live run; no live game/session launched |
| LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001 | Goal worker | AWAITING_REVIEW | Baseline completeness diagnostics proved same-tick camera restoration lost remote Resource association; Resource-scoped deferred restoration is LIVE_PROVEN by two corrected full-world scans (8008/8007 unique, zero rejected, full 0..999 bounds). `CURRENT_PATH_COMPLETE`; `GAME_UNIVERSE_COMPLETE` and original private traversal equivalence remain `UNKNOWN` |
| LWB317-MAP-UI-INTEGRATION-001 | Goal worker | AWAITING_REVIEW | Clean reconstructed `LWBridge.UI-0.3.17` Map page now uses the production native bridge. Resource-only Start/progress/completion, 7,960-row live query, page-2 pagination, Resource-name filtering, Clear/zero render and same-session connected readiness are `LIVE_PROVEN`; old Desktop WebUI is preserved |
| LWB317-MAP-UI-PRODUCTIONIZE-001 | Goal worker | ACCEPTED | Project-lead accepted at `086757e36562b76d7e45b857a282c51267e16171`; `src/LWBridge.UI-0.3.17` is the canonical/default packaged Desktop frontend; zero-argument normal launch, native bridge, all eight primary pages, and Resource Start/progress/completion/page-2/filter/Clear/post-clear health are LIVE_PROVEN. Legacy `WebUi` remains an explicit recovery/reference path |
| LWB317-RE-MAP-001 | Goal worker | AWAITING_REVIEW | Ordered Map continuation completed through restart/reopen recovery, live server jump/return, combined v22 category acquisition, safe action wiring and current-server Auto Scan. Direct canonical-WebView positive-row rendering for City/Monster/Truck/Dispatch and live UI marks/export remain IMPLEMENTED_NOT_VALIDATED; Railway/Ghost/Treasure positive rows were BLOCKED_BY_LIVE_STATE; Treasure claim/status and Ghost preparation remain BLOCKED; Resource game-universe/private-traversal completeness remains UNKNOWN |
| Other LWB317-RE-* | None | BLOCKED | Do not start another subsystem until Map Goal review |
| Login/auth/licensing reconstruction | None | OUT_OF_SCOPE | Boundary/dependency only |

Loop-mode workers must follow `docs/LOOP_WORKER_PROTOCOL.md`,
`docs/LOOP_QUEUE.md`, and the active campaign file.

## Project-lead responsibilities

Before assigning a worker/campaign:

1. define explicit goals/non-goals;
2. name the reference artifact/state;
3. name required evidence/output paths;
4. define acceptance/stop checks;
5. prevent two workers from editing the same master workstream unsafely.

After a worker/campaign returns:

1. review evidence/source identity;
2. reject unsupported inferences;
3. inspect each campaign checkpoint/commit;
4. update parity matrix/feature ledger conservatively;
5. run applicable checks;
6. accept/fix/revert only coherent changes;
7. decide whether the next function-family Goal may open.

## Status discipline

The project lead should distinguish:

- what the reference demonstrably contains;
- what has been observed at runtime;
- what has been copied visually;
- what has been reverse engineered;
- what has been implemented;
- what has been live-proven.

Do not compress those into a single vague “done” percentage.

## Desktop constraint

Desktop-control tooling remains available. The current Map Goal is
static/headless first and then explicitly authorizes **bounded live Last War Map
validation** using an assistant-owned session where possible.

It does not authorize auth bypass or unrelated gameplay actions.
