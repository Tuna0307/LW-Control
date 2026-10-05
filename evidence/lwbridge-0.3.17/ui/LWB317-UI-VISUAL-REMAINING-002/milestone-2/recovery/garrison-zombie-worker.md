# Milestone 2 recovery note — Garrison + Zombie Bus

Bounded worker scope: recovered `SquadPanel-HC3-DJei.js` functions `pe`, `me`
and `he` only, compared with current `SquadsPage.jsx`
`GarrisonPreviewSettings`, `ZombieBusPreviewSettings` and their composition in
`AfkContent`. This note is recovery/differential evidence only. It makes no
production change and does not execute browser, native, game or provider actions.

Current checkout inspected: `research/offline-controller` at
`a83f4f49fed63eb16d810d0c7ca102e9865e9ad8`, clean before this note.

## Pinned identities and exact locators

Recovered asset:

| Item | UTF-8 byte offset | UTF-8 byte length | SHA-256 |
| --- | ---: | ---: | --- |
| `SquadPanel-HC3-DJei.js` | — | — | `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7` |
| Garrison `pe` | 4,586 | 11,256 | `4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85` |
| Zombie table `me` | 15,842 | 687 | `7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8` |
| Zombie card/settings `he` | 16,529 | 1,957 | `37F600C70C0F2F1A2F5722DC33C6E62075A7976C86ECA48001E97F1FA39BC32B` |

Source defaults immediately before `pe`: byte 3,838 begins
`recallOnDisable:!0`, byte 3,857 begins `squadPriority:[`, and byte 3,874
begins `targets:[]`. The recovered default is therefore
`{enabled:false, recallOnDisable:true, squadPriority:[], targets:[]}`.

Useful branch anchors inside the pinned `pe` slice:

| Branch | UTF-8 byte |
| --- | ---: |
| invalid-enable `garrison.targetRequired` / `garrison.squadRequired` paths | 6,128 / 6,507 |
| ally picker name-only search | 7,031 |
| localized building-name resolver call `fe(e, ae, ...)` | 7,086 |
| unresolved-source summary fallback `V=v?...:g.targets.length` | 8,356 |
| selected-building count | 10,437 |
| empty selected-allies copy | 11,651 |
| target-priority row | 11,966 |
| empty target-required copy inside priority list | 12,318 |
| selected squad drag handle | 13,117 |
| always-rendered squad hint | 13,255 |
| local inline error | 14,084 |
| source Run-now button, disabled only by action-busy `me` | 14,353 |
| ally modal availability class | 15,073 |
| ally modal separate availability `<em>` | 15,467 |

Useful branch anchors in `he`: running/failed/disabled summary selection begins
at byte 17,429; open settings table predicate begins at 18,201; the combined
config/status error sibling begins at 18,247; its status-error paragraph begins
at 18,383.

Current source identities at this checkpoint:

| Current item | UTF-8 byte offset | UTF-8 byte length | SHA-256 | Line locator |
| --- | ---: | ---: | --- | --- |
| `SquadsPage.jsx` | — | — | `9A6FEC76B71513F975F1669DE82B7DFCB1BE65E9C483C37E7D4ACA54F0E5919E` | — |
| `GarrisonPreviewSettings` | 17,320 | 11,875 | `D00344F113B04D5DE5D71B87FA0D74660F34282D86793D94AA50AD810DC6586A` | 116–202 |
| `ZombieBusPreviewSettings` | 29,197 | 1,123 | `858C6CABA9C9E2CACA0E43D71AFE837479F16A2E533B55EF6BE89C57550721E6` | 204–208 |
| `AfkContent` | 32,163 | 14,761 | `DD99E8BCF126C25556E2E3357CA7658C02B3FFA443E610A0CF9C1CFF10BE89A6` | 234–307 |
| `CompactAfkCard` | 47,239 | 1,584 | `EA05F60221761E50FE8959DB95C6B4E53E144CAC09431880C5BA2379709DF817` | 318+ |
| `previewAfkCloseoutFixtures.js` | — | — | `B8BADF0AC5A330712298EAA85F842906A98106413E4311CAB1A0C3C4E56D3491` | Garrison/Zombie fixture adapter |
| `previewAfkContracts.js` | — | — | `F75B4D22ECCD19F69D4AD139ABC2FFBC150B0007662BA47127CFB600CBF61EE3` | `refreshGarrisonTargets` 76–85 |

The AFK editor acceptance deliberately changed other `SquadsPage.jsx` bytes
after the parent Unit-D replay. Its review states the surrounding Squads functions
were byte-preserved. The older Unit-D whole-file hash is therefore historical,
while the function hashes above are the current authority for this unit.

## What the inherited Unit-D proof actually closes

The parent `unit-d/check-afk-source-replay.mjs` is useful but narrow:

- Garrison: 10 checks prove current target/squad reorder handler results,
  availability disablement in three synthetic states, assignment-row count, and
  source `N` snapshot refresh semantics.
- Zombie: four cases prove the isolated `me` table for empty/populated rows,
  gold/normal/unknown bus labels, supplied translated `nameKey`, and EN/JA text.
- Fixture fencing proves positive AFK/Garrison/Zombie fixture rows do not leak into
  inactive/native/non-AFK states.
- The parent browser checks are current-only for Garrison/Zombie. They do not
  establish whole `pe`/`he` composition parity.

Therefore the table renderer `me` is reusable accepted lower-level evidence.
The remaining work is the actual `pe`/`he` card/settings/error composition
and interactions that change structure or visible placement.

## Recovered Garrison branch model

`pe({online, serverId, config, open, onOpenChange, onLog})` owns both the compact
Garrison card and, when `open`, the complete settings surface. Its source-valid
state axes are:

- config draft: `enabled`, `recallOnDisable`, ordered `squadPriority`, and
  ordered `targets` of `allianceBuilding` or `allyCity`;
- discovery: unresolved `v=null`, or supplied `buildings` + `allies`; open +
  online refreshes both discovery and squad indexes every five seconds;
- status: `E=null` or `allianceGarrison` status containing
  `guardingCount` and `assignments`; the status poll itself does not render a
  `lastError` field;
- local transient state: ally picker open/search/draft selection, dragged target,
  dragged squad, local error `ge`, and action-busy `me = ""|"stop"|"run"`;
- config store state: draft/config error through recovered `ne`;
- online/action boundary: disabling while online can call the source stop action
  with `recallOwned`; Run-now calls the native action. Those actions are fenced
  in current and are not part of this worker proof.

The source fetch `F` is one combined `Promise.all([te(), f()])`. Before it
resolves, the source has no explicit “members loading” row. On rejection it writes
the translated failure into the same local inline error `ge`.

## Garrison structural / branch differential

| ID | Recovered `pe` | Current | Source-valid condition | Classification |
| --- | --- | --- | --- | --- |
| G01 | Compact enable validation sets `ge` and returns; it does **not** call `onOpenChange`. | `AfkContent` line 287 opens the Garrison panel when enable is attempted with zero targets or zero squads, without setting the recovered inline error. | `open=false`, `enabled=false`, target or squad list empty, local enable click. | **Candidate mismatch.** Distinguishing behavior is visible panel-open state, not a native action. |
| G02 | Ally search at byte 7,031 is `e.name.toLowerCase().includes(search)`: **name only**. | Line 129 searches `${member.name} ${member.uid}`. | Open populated picker; search a UID substring absent from the member name. | **Candidate mismatch.** Current returns a row that source filters out. |
| G03 | Ally modal row renders `<span><strong>name</strong><small>level/power</small></span><em>availability</em>`; separate `em` at byte 15,467. | Line 199 appends availability to the `small` text with a middle dot and has no `em`. | Picker open with available-online, available-offline and unavailable/cross-server members. | **Candidate mismatch, structural and likely pixel-visible.** |
| G04 | Empty selected allies is `<span className="muted">` inside `.garrison-selected-allies`. | Line 183 uses `<p className="muted">`. | `targets` contains no `allyCity`. | **Candidate mismatch.** Element margins/line box can distinguish it. |
| G05 | `garrison.targetRequired` is a `<p class=muted>` **inside** `.garrison-priority-list` after mapped rows. | Lines 185–186 close the list first, then render the paragraph as its sibling. | Disabled config with `targets=[]` is source-valid because source validation requires targets only when enabled. | **Candidate mismatch, exact ancestry.** |
| G06 | The squad section always renders `garrison.squadHint` after the choices; `garrison.squadRequired` appears only through the local error path after an invalid enable/run/removal attempt. | Line 191 immediately substitutes `garrison.squadRequired` whenever `squadPriority.length===0`. | `enabled=false`, `squadPriority=[]`, open settings, no invalid action yet. | **Candidate mismatch.** Current exposes an error/requirement earlier than source. |
| G07 | Target-priority and selected-squad drag affordances use recovered `T({name:"drag"})` SVGs. | Lines 185/190 use the text glyph `↕`. | Any open settings with >=1 target and >=1 selected squad. | **Candidate mismatch, visual.** This is directly source-proven; do not reuse the old text-glyph assumption. |
| G08 | Building display name calls `fe(e, ae, fallback)` at byte 7,086 after `F` loads translations for nonblank building `nameKey`. | Lines 138–140/180 use `building.name` directly; current routed fixture has no translated building-name map. | Supplied source-valid building with `nameKey` whose recovered text differs from fallback name. | **Coverage/model candidate.** A direct renderer case is needed before changing production; current fixture cannot exercise this source branch as written. |
| G09 | Online unresolved discovery has `v=null`: no explicit member-loading copy; Choose Allies is disabled only by `!online`. A rejected combined fetch sets `ge`. | Lines 181–183 disable Choose Allies on `!memberFixture.ready` and add separate offline/loading/failed member copy. | `online=true`, `open=true`, discovery pending; separately combined discovery rejection. | **Candidate mismatch / invented branch risk.** Current member-specific loading/failed presentation is not present in `pe`. |
| G10 | `pe` consumes status assignments/guardingCount but never renders `E.lastError`. Poll failures are swallowed; discovery/action failures use `ge`. | Line 197 renders `runtime.lastError` as `automation-error`. Current `squads-profile-garrison-error` fixture supplies this invented status field. | Status object with `lastError` only, no config/discovery/action error. | **High-confidence candidate mismatch.** Parent current-only “garrison-error” capture is not original evidence. |
| G11 | Recovered config error `ne({state:h.state,label:garrison.title})` is inside the open Garrison settings after the two-column grid; local `ge` follows it. | `AfkContent` lines 281–282 place toolbar `PreviewConfigError` globally above the entire toolbar; `GarrisonPreviewSettings` has no config-error child. | Open Garrison + inert failed config write. | **Candidate mismatch, placement/ancestry.** |
| G12 | Ordinary settings mutations call `Se`, which uses default/deferred `h.state.edit(...)`; explicit enable save `xe` is immediate + flush. | Line 292 routes every Garrison `onChange` through `updateToolbar` with default `immediate=true`, which flushes immediately. | Toggle recall, building/ally selection, target reorder or squad reorder while no action runs. | **Candidate behavior/lifetime mismatch.** Preserve the distinction between deferred settings edits and immediate enabled-state save. |
| G13 | Invalid removal of the final squad while enabled sets `ge`; a later successful `Pe` mutation does not itself clear `ge` (refresh/action paths clear it). | Line 190 clears `selectionError` immediately before every successful squad mutation. | Enabled config with one selected squad: remove it, then select another before the next source refresh. | **Candidate error-lifetime mismatch.** Must use a controlled clock/refresh in baseline. |
| G14 | While discovery is unresolved, capacity `V` falls back to `g.targets.length`; compact enabled summary can therefore use saved target count before source availability arrives. | Lines 269–273 calculate available targets only from current runtime/member maps; empty maps yield zero. | Enabled saved config, two targets/two squads, `v=null`, status guardingCount 0. | **Candidate transient-summary mismatch.** Needs actual direct `pe` render to distinguish from fixture policy. |
| G15 | Source target/squad rows are draggable whenever their source predicates allow it; offline/busy config controls are not globally replaced by presentation-only state. | Current uses `disabled={!previewEnabled}` at the compact/page boundary and propagates it into settings drag/inputs. | Non-preview/native-unavailable current mode. | **Intentional native/availability fence.** Do not “fix” by enabling source actions or native saves. |
| G16 | Source Run-now is enabled when not action-busy and calls native `we()`. | Line 198 always disables it and marks `data-preview-action="presentation-only"`. | Any open Garrison preview. | **Intentional native-action fence.** Pixel difference in an online source case must be reported/fenced, not corrected by invoking or enabling the native action. |

Two details are already aligned and should not be reopened casually: target/squad reorder
payload semantics and ally snapshot refresh are covered by the inherited source replay;
the selected-target/building availability counts use the same target-key model when
discovery is present.

## Recovered Zombie Bus branch model

`he({online, config, open, onOpenChange, onLog})` owns the Zombie compact card,
open settings and error presentation:

- local config is only `{enabled:boolean}`;
- status `p` is initially null, then may contain `busAssignments` and
  `lastError`; polling is one second while online;
- assignments in states `sending|marching|guarding|recalling|returning` make the
  compact summary Running;
- summary precedence is disconnected > status lastError Failed > active assignment
  Running > config-enabled Waiting > Disabled;
- assignment `nameKey` values are collected and translated asynchronously into
  `g`; `me` uses supplied translation when it is nonblank/different from the key,
  otherwise gold/normal/unknown;
- open settings contains title, description, and `me` only when rows exist;
- `f.error || p?.lastError` creates a **separate sibling**
  `.monster-afk-toolbar-settings` error block. That sibling contains config Retry/
  Discard UI `ne` and/or a status `automation-error`.

The isolated `me` table is already accepted by parent Unit D. The remaining
Zombie work is the `he` ancestry/error placement and whole-card state combinations.

## Zombie structural / branch differential

| ID | Recovered `he/me` | Current | Source-valid condition | Classification |
| --- | --- | --- | --- | --- |
| Z01 | `me` table columns/rows and name resolution. | `ZombieBusPreviewSettings` table at line 207. | Empty and populated rows; gold true/false/null; translated `nameKey`. | **Accepted lower-level.** Parent replay has four exact cases. Reuse it in whole-card baselines. |
| Z02 | With `open=true` + `p.lastError`, settings remain one section and the status error is rendered in a **second sibling** `.monster-afk-toolbar-settings` block (bytes 18,247+). | Line 207 places `runtime.lastError` inside the open settings `section`. | Open Zombie settings, no assignments, status `lastError="common.actionFailed"`. | **High-confidence candidate mismatch, exact ancestry.** |
| Z03 | With `open=false` + status error, the separate error sibling still renders after the compact card. | `AfkContent` line 294 adds a separate error block only when the Zombie panel is closed. | Close settings while status error persists. | **Likely structurally aligned at high level**, but exact host/order must be frozen in the whole `he` baseline rather than inferred from the isolated settings component. |
| Z04 | Config save error `f.error` uses recovered `ne` inside the same Zombie-specific sibling error block, regardless of settings open state. | Toolbar `PreviewConfigError` is global at `AfkContent` lines 281–282, before all compact cards/settings. | Failed inert Zombie enabled-toggle save, status has no runtime error. | **Candidate mismatch, placement/ownership.** |
| Z05 | Config error + status error overlap share one Zombie error sibling: `ne` then status paragraph. | Current separates the toolbar store error at page top; if open, runtime error sits inside Zombie settings; if closed, runtime error is a later standalone block. | Failed inert config save and `p.lastError` simultaneously. | **Candidate overlap mismatch.** This is a required immutable case because isolated single-error screenshots cannot prove it. |
| Z06 | `he` has no explicit loading/empty copy while status is null/has zero assignments; open settings shows title + description only. | Current empty/waiting settings likewise has no table and no extra empty copy. | `online=true`, status unresolved/null; separately status with empty assignments. | **No mismatch found.** Include one baseline to lock this absence. |
| Z07 | Compact summary precedence at byte 17,429 is status error > running assignment > config-enabled waiting > disabled, with disconnected overriding all. | `AfkContent` line 288 implements the same precedence for its preview model. | enabled/error/running/waiting/disabled and inactive modes. | **Semantically aligned.** Whole-card composition still needs a direct pair. |
| Z08 | `nameKey` translations are an actual `he` async branch feeding `me`. | Parent direct replay stubs it successfully, but routed `previewZombieBusRuntime` positive rows have blank `nameKey`; normal page preview never exercises the translated-name branch. | Populated row with nonblank `nameKey` and supplied recovered text map; fallback map missing/equal-to-key. | **Coverage hole, not yet a production mismatch.** Whole `he` baseline should include it without changing runtime/native providers. |
| Z09 | Source compact toggle is disabled when `!online`; status/config polling and config write are provider-backed. | Current disables toolbar controls outside explicit `squads-profile*` preview and uses inert local store. | Native/native-unavailable/inactive current modes. | **Intentional native/provider fence.** Preserve it. |

## Current routed-fixture limitation

The current preview-state router cannot express several source-valid overlaps without
a task-local direct renderer:

- Garrison panel open **and** member discovery loading/failed/offline, because
  `toolbarPanel` opens Garrison only for preview states containing `garrison`,
  while `previewMemberFixture` loading/failure states use `members-*`;
- Garrison source discovery unresolved versus failure as the original combined
  `F` operation;
- Garrison config error in its recovered in-panel location;
- Zombie translated `nameKey` rows through the normal routed page;
- Zombie config error + runtime-error overlap in the recovered single sibling block.

Do not add production preview states merely to make these cases reachable. A
Milestone-2 evidence harness can call the canonical components with disclosed inert
fixture functions/stores, just as the accepted AFK editor packet does.

## Proposed immutable failing baseline cases

The coordinator should freeze these cases **before** any correction. Each case should
pin the recovered/current function hashes above, independent locale catalogs, equal
host CSS, exact fixture JSON, raw/normalized DOM and decoded PNG results. No masks.
Where a case is behavior-only, record the exact callback/store trace beside the DOM.

| Case | Source-valid input / transition | Distinguishing assertion |
| --- | --- | --- |
| G-B01 invalid-enable-closed-target | `open=false, online=true, enabled=false, targets=[], squadPriority=[1]`; click compact enable. | Source does not open settings; current opens Garrison. |
| G-B02 invalid-enable-closed-squad | Same, but one target and `squadPriority=[]`. | Same open-state distinction; correct source error key is `garrison.squadRequired`. |
| G-B03 empty-settings | `open=true, enabled=false, targets=[], squadPriority=[]`, discovery loaded. | Source target-required is inside priority list and squad copy is Hint; current target-required is sibling and squad copy is Required. Also freezes empty-selected-allies `span` vs `p`. |
| G-B04 positive-drag-icons | Open with one building + one ally target and squads [1,2]. | Source both drag affordances contain recovered `ui-icon` SVG; current contains `↕` text. |
| G-B05 ally-modal-mixed | Picker open with Avery online/available, Blair offline/available, Casey cross-server/unavailable. | Source availability is sibling `em`; current folds it into `small`. |
| G-B06 ally-search-uid | Same picker; search exact UID substring not present in name. | Source list count 0; current count 1. |
| G-B07 translated-building | Building has `nameKey="fixture.garrison.center"`, fallback name differs, supplied translation exists. | Source `z/fe` displays translation. Current direct-name branch demonstrates whether it diverges. |
| G-B08 discovery-pending | `open=true, online=true`, saved two targets/two squads, discovery `v=null`, no action error. | Freeze source absence of members-loading row and source compact capacity fallback from saved targets. |
| G-B09 discovery-failed | Same but source combined `F` rejects with fixed inert error. | Source local inline error vs current member-specific failed/loading model. |
| G-B10 status-last-error-only | Loaded discovery; status has zero assignments/guarding plus `lastError`; no store/local error. | Source `pe` renders no status-lastError paragraph; current line 197 does. |
| G-B11 config-save-error-open | Loaded positive config, open panel; fail an inert ordinary config write. | Source config error appears after settings grid inside Garrison section; current toolbar error is global above toolbar. |
| G-B12 deferred-settings-write | Toggle recall or building selection using deferred inert store with observable pending queue. | Source `Se` schedules default/deferred edit; current Garrison `onChange` goes immediate flush. No native action. |
| G-B13 squad-error-lifetime | Enabled one-squad config; attempt removing final squad, then select another before source refresh tick. | Source local error remains until a source clear path; current clears immediately on successful selection. |
| Z-B01 whole-running-translated | `online=true, open=true, enabled=true`; assignments include gold, normal and unknown with one translated `nameKey`. | Reuse exact `me` table but lock compact Running summary + settings ancestry around it. |
| Z-B02 open-runtime-error | `online=true, open=true`, empty assignments, `lastError=common.actionFailed`, no config error. | Source has settings section + separate error sibling; current puts error inside settings. |
| Z-B03 closed-runtime-error | Same status, `open=false`. | Freeze exact separate error-block order/host while compact summary is Failed. |
| Z-B04 config-save-error-open | `open=true`, status clean; fail inert Zombie enable/config write. | Source Zombie-specific sibling `ne`; current global toolbar error above compact cards. |
| Z-B05 dual-error-overlap | `open=true`, both config error and status lastError. | Source one sibling block with Retry/Discard then status error; current uses separated placements. |
| Z-B06 unresolved-empty | `online=true, open=true`, status null; then resolved status with zero assignments. | Both sides must retain title/description with no table and no invented loading/empty copy. Positive control. |

For visual freeze, use at least EN/light desktop and JA/dark desktop for every
structural failure; add one 375 px narrow case for G-B03, G-B05, G-B11, Z-B02 and
Z-B05 because changed ancestry/text wrapping can alter height. The translated-name
cases should use independently recovered original/current catalogs, not a shared
translation helper.

The immutable baseline must intentionally retain each pre-fix difference. A later
current packet may pass only after the coordinator proves a correction against these
exact source inputs; do not rewrite or re-pin the failing baseline.

## Intentional fences and limits

- Garrison Run-now/stop and Zombie/native configuration providers are unavailable in
  current source/local preview. The always-disabled current Garrison Run-now button
  is an explicit presentation fence; this worker does not propose enabling it.
- Positive fixtures are restricted to `squads-profile*`; inactive/native modes
  deliberately expose no synthetic members/buildings/assignments. Preserve that fence.
- Recovered Garrison 5 s discovery refresh, 2 s status poll and Zombie 1 s status
  poll are provider/effect-lifetime facts. A direct evidence harness may inject their
  resolved/rejected states; it must not implement or claim native producers.
- Source drag handlers may be exercised as local callback/lifecycle proof. DOM
  dispatch is not physical pointer-driven HTML5 drag.
- The protected original post-auth runtime and real gameplay/provider reachability are
  not established. The original oracle for this unit is execution/inspection of the
  pinned recovered functions with disclosed source-valid state.
- The parent Zombie `me` table and Garrison reorder/snapshot facts remain reusable.
  They do not excuse the open `pe/he` ancestry, error-placement and transition cases
  listed above.
