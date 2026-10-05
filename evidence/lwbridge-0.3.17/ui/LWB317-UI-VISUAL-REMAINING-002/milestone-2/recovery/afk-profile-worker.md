# Milestone 2 recovered AFK/profile composition differential

Worker scope: recovered `SquadPanel-HC3-DJei.js` function `I` versus the current
`SquadsPage.jsx` AFK/profile composition. This deliberately excludes the already
accepted `AfkProfileEditor` internal renderer/callback packet and excludes the
Garrison/Zombie implementation owned by the sibling recovery task. Garrison and
Zombie are treated as opaque toolbar children only where their placement determines
the full `I` ancestry.

Status: recovery/differential only. No production/shared/master file was edited and
no native/gameplay/provider action was invoked.

## Frozen identities and exact locators

Reference executable identity remains
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

| Item | Exact locator |
| --- | --- |
| Original Squad asset | `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js`; SHA-256 `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7` |
| Original complete AFK renderer `I` | UTF-8 byte `28070`, length `22554`, SHA-256 `0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E`; readable copy `unit-d/afk-visual/I.pretty.js` lines 1-210 |
| Original AFK new-profile factory `Ce` | same asset, UTF-8 byte `26646`, length `663`, SHA-256 `99AEF53AA709B29D04B8690347BCBCA8391964A023E52009CF699D8470CBF7F3`; factory ends with `squadIndexes:t.slice(0,1)` |
| Original Squads parent `pd` | same asset, UTF-8 byte `191313`, length `1517`, SHA-256 `F4644E68B880678194C13EAC0FF9F7D395061EF7EB63FC505A85DD3E2C1419E8`; source asset line 11 |
| Original shared config-error renderer `Oe` | `index-BVfnK1wp.js` SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`; UTF-8 byte `199047`, length `587`, SHA-256 `0AD5697E94B54AD9826ABD4AA044A744B9E1E23FF804A0E5D845F12D8883A5E1` |
| Current canonical Squads file | `src/LWBridge.UI-0.3.17/src/SquadsPage.jsx`; SHA-256 `9A6FEC76B71513F975F1669DE82B7DFCB1BE65E9C483C37E7D4ACA54F0E5919E` |
| Current `SquadsPage` slice | UTF-8 byte `1231`, length `2338`, SHA-256 `0BDA76C5824E354FF17E701F3DCC943C0F71B49C546F6063C548456CFAF157B0`; lines 12-45 |
| Current `AfkContent` slice | UTF-8 byte `32163`, length `14763`, SHA-256 `366D300B08CF3CBABEF5A2D978DAABD284A6E5767E75DD3F4C11988A5529C868`; lines 234-307 |
| Current Drill slice | `AllianceDrillPreviewSettings`, UTF-8 byte `13878`, length `3442`, SHA-256 `8D69096468BE5D718FD4F7585555EE2F1EFA464A58CF0C0F36F0D3607C162C3C`; lines 96-114 |
| Current new-profile factory | `previewAfkContracts.js` `makePreviewAfkProfile`, UTF-8 byte `3764`, length `1126`, SHA-256 `669F0B9F4E344716A3A2A025609C67AC3BFCE0B68263FCF4CA8E0C0ED42A77A9`; it hard-codes `squadIndexes: [1]` |
| Current preview config owner | `previewConfigHook.jsx` `usePreviewConfig`, UTF-8 byte `319`, length `1198`, SHA-256 `B39BA5AE52955585E147A9A60BC3E9D877026A470F3FD7D616FDA0F2E3B4F10D`; error renderer lines 38-45 |

The accepted editor packet remains authoritative for the `I` editor subtree:
`LWB317-UI-AFK-EDITOR-VISUAL-001/source-locators.json` freezes source editor bytes
`44257..50535` SHA-256
`0574D30268196555E512FFFBA78724009A62055E4AAC90546E8C6BD8AF9862BF`.
Nothing below reopens those internal editor corrections.

## Exact full-composition ancestry

Recovered `I` has this composition order (pretty copy lines 172-209):

1. root `.monster-afk-layout`;
2. `Oe(y.state, label=Master)` and `Oe(b.state, label=Auto Use Potion)`;
3. `.monster-afk-toolbar`, containing Master, Potion, Drill, Garrison and Zombie
   compact children and, when selected, the Potion or Drill settings section;
4. when the selected panel is not Garrison/Drill/Zombie, a fragment containing
   `.monster-afk-profiles` followed by the selected `.monster-afk-editor` as a
   **sibling** under `.monster-afk-layout`;
5. final composition/action error `.automation-error.monster-afk-error` when `Ie`
   is non-empty.

Current `AfkContent` differs structurally at lines 280-304:

- `.monster-afk-toolbar` closes after the five compact cards at line 289. Potion,
  Drill, Garrison and Zombie settings are rendered afterward as direct children of
  `.monster-afk-layout`. For this worker, Potion and Drill therefore have the wrong
  parent. Source CSS makes `.monster-afk-toolbar` a four-column grid and
  `.monster-afk-toolbar-settings` span `1/-1`, so this is layout ancestry, not a
  harmless component-boundary difference.
- Current `.monster-afk-editor` is rendered inside the
  `.monster-afk-profiles` section (lines 295-304). Source closes the profiles
  section before rendering the editor sibling. The accepted editor packet proved
  the editor subtree only; it did not prove this surrounding two-column placement.
- Current has no equivalent final `Ie` composition error surface. Target-discovery
  failure is represented inside the current editor, and the Drill zero-squad
  message is rendered inside Drill settings. Source routes both through the final
  `.automation-error.monster-afk-error` surface under the conditions below.

This ancestry difference alone requires full original/current page pairs before any
crop. An isolated editor or settings-section pair cannot close it.

## Toolbar and state ownership differential

Recovered `I` owns two independent config stores before Garrison/Zombie:

- `y`: Monster AFK Master + strategies/profiles + Alliance Drill;
- `b`: Stamina Potion only.

Source lines 173 and the shared `Oe` renderer show errors independently as Master
and Auto Use Potion. Current lines 237-238 instead own profiles in `config` and put
Master + Potion + Drill (plus separately scoped Garrison/Zombie preview fields) in
one `toolbar` store. Both current `PreviewConfigError` calls omit labels.

Consequences that must be executable baseline cases before correction:

- a failed profile/Master/Drill write is labelled `Master` in source; current
  profile error is unlabeled;
- a failed Potion write is labelled `Auto Use Potion` in source; current toolbar
  error is unlabeled;
- invalid/dirty Potion state cannot invalidate or serialize Master/Profile/Drill
  writes in source because `b` is independent from `y`; current combines Potion
  validity with Master/Drill in `validAfkToolbarConfig`;
- a pending profile save and a subsequent Master/Drill mutation share one source
  `y` write queue. Current profile and toolbar stores can write independently.

Recovered action busy `B` is distinct from config-store `saving`. In recovered `I`,
`B` is set around Master stop, profile delete and profile reorder. It disables the
Master/Potion/Drill compact toggles, Add, profile enable/delete/drag and profile drag
acceptance. `pt = !!(B && B !== "save")` also supplies the profile enabled-label
disabled class; no `Fe("save")` call exists in recovered `I`, so that string branch
must not be invented as a reachable action here. Current `AfkContent` has no shared
action-busy token, so these controls stay active through the equivalent inert
pending-action fixture.

Current `data-draft-dirty`, `data-draft-saving`, `data-preview-fixture` and parent
preview counters are evidence/preview instrumentation absent from source. A DOM
comparator may strip only these explicitly disclosed task attributes; it must not
mask ancestry, text, controls or classes.

## Potion differential

Source `I` lines 2 and 173 establish:

- `minStamina` edits call `b.state.edit` with the recovered default debounce; there
  is no source `blur` flush handler;
- `preferFifty` also calls debounced `b.state.edit`;
- both controls are disabled by shared action busy `!!B`;
- the selected Potion settings section lives inside `.monster-afk-toolbar` and
  profiles/editor remain visible because `yt` excludes `potion`.

Current line 290 preserves the visible field order and numeric bounds, which the
parent source replay already checked, but behavior/composition still differs:

- current adds an explicit `onBlur => toolbar.store.flush()` to min stamina;
- current Prefer Fifty uses the default immediate `updateToolbar`, forcing an
  immediate flush instead of the recovered debounce;
- current controls only use the preview availability fence and do not receive the
  recovered AFK action-busy state;
- current Potion shares the wrong store and has the wrong settings parent.

These are source/local callback and lifetime differences; no native Potion action is
needed to prove them.

## Drill differential

Recovered `I` lines 99-115 and 172-184 establish these finite branches:

- enabling Drill with zero selected squads sets `k="drill"` and then sets global
  `Ie = afkAllianceDrillSquadRequired`; the Drill settings subtree itself does not
  unconditionally render a zero-squad error;
- squad choices are `_t = selected squadIndexes + Ne remaining`, where `Ne` is the
  actual discovered squad-index list;
- selected squad drag uses recovered drag SVG `T(name="drag")`, sets
  `dataTransfer.effectAllowed="move"`, sets `dropEffect="move"` on accepted drag-over,
  and exposes `dragging`/`drag-over` classes;
- squad checkbox, active-rally and drag reorder flush immediately through `y`;
  nested Join-restriction edits call `y.state.edit(...)` with the recovered debounce;
- only runtime rows with `activity === "allianceDrill" && running` form `K` for the
  toolbar state. Waiting rows append the translated/fallback target and wait seconds.

Current `AllianceDrillPreviewSettings` differs as follows:

- it renders inline `.status-error` whenever `squadIndexes.length === 0`, before
  the user attempts to enable Drill; current toolbar enable merely opens the panel;
- it always appends hard-coded squads `[1,2,3,4]` instead of the actual available
  `Ne` list;
- the drag handle is literal `↕`, with no recovered SVG; current drag handlers do
  not set `effectAllowed` or `dropEffect`;
- every child change flows to parent `updateToolbar` with immediate flush, including
  Join restrictions that source debounces;
- its settings section is outside `.monster-afk-toolbar`.

The existing Unit-D source replay proved control values, the waiting-detail text and
basic drag predicates for a four-squad fixture. It does not prove the missing
available-squad branch, zero-squad error lifetime/ancestry, drag icon/data-transfer
semantics, nested Join save timing or full toolbar parent.

## Profile-list exact differential

Recovered profile card order is fixed at `I.pretty.js` lines 186-204:

`enabled toggle -> select button(name/badge, level+distance, optional range,
source+action+bound squads, runtime rows) -> Delete -> drag SVG`.

Current line 301 stops the select-button content after source/action/bound squads.
The following differences are confirmed from source/current code:

- source profile-card root is a `div`; current uses `article`;
- source card adds `drag-over` when `He === profile.id`; current has no profile
  drag-over state/class or drag-leave lifecycle;
- source enabled label adds `is-disabled` for shared action busy or zero squads;
  current only reflects zero squads (plus the separate preview fence on input);
- source enable checkbox, Delete and drag button are disabled while `B` is active;
  current Delete/drag have no action-busy disable path and drag stays draggable;
- source profile drag button renders the recovered drag SVG; current renders `↕`;
- source drag-start writes `effectAllowed="move"` and `text/plain` profile id;
  current only records local `draggedProfileId`;
- source reorder enters `B="reorder"` until the exact config write settles;
  current starts `config.store.flush()` without an action-busy presentation;
- source keyboard ArrowUp/ArrowDown is ignored during `B`; current keyboard reorder
  has no equivalent action-busy guard.

### Missing per-squad runtime rows

This is the strongest content omission. For each profile, source first selects
runtime workers whose `squadIndex` is bound to the profile. It renders a row only
when a runtime worker exists for that bound squad. Each row is appended **inside the
profile select button after the source/action/bound-squads span**.

For each supplied worker/profile pair:

- class is `status-ok` only when that profile is the worker's active `profileId`
  and the worker is `running`; otherwise class is `muted`;
- active profile with `step` renders `ge[step] || step` translated;
- inactive/no-step row renders `Completed` when `completedStrategyIds` contains the
  profile id, otherwise `Idle`;
- active `waiting_join_delay` appends target plus seconds, preferring translated
  `joinTargetNameKey` then `joinTargetName`;
- any profile with `executionLimit > 0` appends
  `strategyProcessed[profile.id] || 0` / limit;
- active profile with `lastError` appends the recovered translated error.

Current profile cards render none of these runtime rows. This omission is independent
of native execution: disclosed synthetic `ke` workers with the exact recovered
consumer fields are sufficient renderer inputs.

## Empty, populated, selected, new and Add-menu branches

Source and current both have a muted `afkNoProfiles` empty-list branch and both use
the first strategy/profile as the initial selection when populated. Those facts
should be preservation guards rather than invented defects.

The surrounding selected/new composition still fails because source editor ancestry
is sibling-to-profiles while current editor ancestry is nested-in-profiles. The
source `is-new` predicate is `selected && !confirmed.strategies.some(id)`; current
passes the equivalent `!config.confirmed.some(id)` into the accepted editor.

Recovered Add behavior has further differences:

- source installs a document `pointerdown` listener and closes the menu when the
  target is outside the ref-owned add control. Current only implements Escape and
  click toggle; it has no outside-pointer dismissal;
- source Add is disabled when recovered target list `M` is empty or `B` is active;
  menu items additionally exclude `name:` targets and divide farm by `!rally` and
  join by `rally`. Source has already removed `group === "drill"` before `M` is set;
- current top-level Add checks `previewAfkTargets.length`, and menu-item predicates
  do not exclude `name:`/Drill. `addProfile` later excludes Drill/name, so a
  drill-only/name-only fixture can expose an enabled no-op item/button;
- source new-profile `Ce(target, Ne, kind)` binds `squadIndexes` to the first actual
  available squad (`Ne.slice(0,1)`). Current `makePreviewAfkProfile` always binds
  `[1]`, even when the disclosed available squad set begins at another index;
- after source Add, the menu closes, `Ie` clears, and a zero-delay task calls
  `editor.scrollIntoView({behavior:"smooth", block:"nearest"})`. Current closes the
  menu/selects the new profile but has no scroll-to-editor action.

## Source discovery and composition error branch

Source target discovery (`I.pretty.js` lines 27-37) sets readiness false, loads
target options plus actual squad indexes when online, and on failure sets
`Ie = common.actionFailed`. `Ie` is rendered at the very end of the AFK layout as
`.automation-error.monster-afk-error`.

Current `squads-profile-target-failed` disables Add through `targetDiscoveryReady`
but does not create that final composition error. Any current editor-internal target
status remains outside this worker's accepted-editor scope and cannot substitute for
the missing source-level error ancestry.

## AFK ↔ Equipment Activity retention

Recovered parent `pd` and current `SquadsPage` agree on the core retention mechanism:

- keep a `visitedTabs`/visited-set;
- mount a tab after first visit;
- use React `Activity` with `visible`/`hidden`, rather than unmounting the visited
  AFK or Equipment subtree;
- therefore preserve AFK local selection/panel/menu/draft state while hidden and
  suspend hidden effects according to React Activity behavior.

Current lines 18-42 reproduce this source ownership. Existing Unit-D browser evidence
already proves editor/equipment draft retention through Activity. Milestone 2 should
extend that as a **preservation gate** to the newly full AFK composition: selected
profile/new state, toolbar panel selection and pending local draft survive
AFK -> Equipment -> AFK, while hidden AFK effects are suspended and resumed.

Current-only `role=tablist`, `role=tab`, `aria-selected`, button `type` and preview
data attributes are absent from recovered `pd`. They do not invalidate the matching
Activity lifetime, but a strict DOM comparator must either report them as explicit
semantic-attribute drift or disclose a narrowly bounded attribute normalization;
they must not be used to mask descendant composition differences.

## Intentional native/source-local fences

These are not correction targets for this worker:

- current `previewEnabled` availability fencing keeps native/unavailable actions
  inert; recovered source may have enabled controls when a real provider existed;
- AFK worker polling, target/squad discovery, Master stop, Potion persistence and
  runtime execution have no claimed current native provider in this milestone;
- synthetic targets, squad indexes and `ke` worker rows are allowed only as
  disclosed inert inputs to execute recovered renderer branches;
- DOM-dispatched drag handlers can prove class/order/callback lifetime, but cannot
  be reported as physical pointer HTML5 drag proof;
- Garrison and Zombie internal forms/errors/actions belong to the sibling worker;
  here they remain opaque children used only to pin full-toolbar order/ancestry;
- accepted `AfkProfileEditor` internal markup/callbacks and Equipment finite packet
  remain frozen and should be imported as lower-level proof, not regenerated.

## Proposed immutable failing baseline matrix

The coordinator can collapse locale/theme duplicates after proving renderer
equivalence, but the **branch cases** below are finite and should be frozen before
production correction. Each `FAIL` row has a concrete current/source difference;
`GUARD` rows are required preservation checks and must stay passing.

| ID | Source-valid fixture/action | Oracle assertion | Baseline |
| --- | --- | --- | --- |
| `AFK-FULL-01 selected-existing` | two valid profiles; first selected; `k=null`; no runtime rows | editor is `.monster-afk-layout > .monster-afk-editor`, sibling after `.monster-afk-profiles`; profile card root `div` | `FAIL` current nesting + `article` |
| `AFK-FULL-02 selected-new` | Add farm with actual `Ne=[3,4]` then render selected unconfirmed strategy | source new profile binds squad 3, editor is sibling with `is-new`, source schedules smooth nearest scroll | `FAIL` current binds 1, nests editor, no scroll |
| `AFK-FULL-03 empty-drill-only-discovery` | `S=[]`; recovered discovered options contain only `group=drill` | source filtered `M=[]`, Add disabled, muted empty text present | `FAIL` current raw-list predicate can enable Add/no-op join path; empty text itself is guard |
| `AFK-FULL-04 add-outside-dismiss` | populated list, Add menu open, document pointerdown outside add control | source menu closes | `FAIL` current lacks listener |
| `AFK-FULL-05 profile-disabled` | populated profile with `enabled=false` | preserve disabled card/toggle text while still enforcing source root element and drag SVG | `FAIL` current root/glyph |
| `AFK-FULL-06 action-busy-delete` | inert unresolved delete write, source `B="delete-<id>"` | Add + enable + Delete + drag disabled; enabled label `is-disabled`; Master/Potion/Drill toggles disabled | `FAIL` current has no shared action-busy gate |
| `AFK-FULL-07 action-busy-reorder` | two profiles, inert unresolved reorder write, `B="reorder"` | drop/keyboard reorder re-entry blocked; profile drag disabled; source `drag-over` clears on lifecycle | `FAIL` current re-entry remains available/no drag-over state |
| `AFK-FULL-08 profile-drag-over` | two profiles, drag first over second | source second card gets `drag-over`; drag payload `text/plain` id and effectAllowed move | `FAIL` current lacks class/payload semantics |
| `AFK-FULL-09 runtime-running` | bound squad worker `{profileId, running:true, step:"scanning_nearby"}` | appended profile-select row class `status-ok`, translated running step | `FAIL` row absent |
| `AFK-FULL-10 runtime-completed-idle` | two bound squad workers, one completedStrategyIds contains profile id, one neither active nor completed | two muted rows, `Completed` then `Idle` | `FAIL` rows absent |
| `AFK-FULL-11 runtime-wait-count-error` | active running worker `waiting_join_delay`, target key/name, seconds, lastError; profile executionLimit > 0 and processed count | one `status-ok` row contains waiting detail, processed/limit and translated error in source order | `FAIL` row absent |
| `AFK-FULL-12 profile-save-error` | profile edit followed by failed `y` write | source config error starts with strong Master label and Retry/Discard | `FAIL` current profile error is unlabeled |
| `AFK-FULL-13 target-discovery-failed` | online target/squad discovery rejection | source final `.automation-error.monster-afk-error` contains Action failed after profile/editor composition | `FAIL` current lacks final composition error |
| `AFK-FULL-14 potion-open` | `k="potion"`, valid potion draft, profiles populated | Potion settings child of `.monster-afk-toolbar`; profiles and editor remain visible as following layout children | `FAIL` current settings sibling outside toolbar + editor nested |
| `AFK-FULL-15 potion-edit-lifetime` | change min stamina then blur; separately toggle Prefer Fifty | source min blur has no forced flush; both edits use recovered debounce | `FAIL` current blur flush + Prefer Fifty immediate flush |
| `AFK-FULL-16 potion-error` | failed Potion write | source error label Auto Use Potion; independent of `y` | `FAIL` current aggregated toolbar error unlabeled |
| `AFK-FULL-17 potion-invalid-master` | Potion minStamina invalid/dirty, then toggle Master using inert local stores | source Master `y` write is independent from invalid `b`; Potion remains its own invalid/error state | `FAIL` current combined toolbar validity couples them |
| `AFK-FULL-18 profile-saving-master` | hold profile/`y` write unresolved, then mutate Master | source uses one serialized `y` queue; current profile and toolbar stores can progress independently | `FAIL` state ownership/order mismatch |
| `AFK-FULL-19 drill-open-valid` | `k="drill"`, `Ne=[2,4]`, selected `[2]`, no workers | source controls only 2 then 4, settings inside toolbar, drag SVG | `FAIL` current adds 1/3, settings outside, glyph text |
| `AFK-FULL-20 drill-empty-before-enable` | `k="drill"`, `E.enabled=false`, `squadIndexes=[]`, no enable attempt | source has no validation text yet | `FAIL` current renders inline required error immediately |
| `AFK-FULL-21 drill-empty-enable` | same state then attempt enable | source sets final layout `Ie` required error; panel stays open | `FAIL` current error remains inline/wrong ancestry and has no `Ie` state |
| `AFK-FULL-22 drill-drag` | selected `[2,4]`, drag 2 over 4 | source `dragging`/`drag-over`, SVG, effectAllowed/dropEffect move and reordered `[4,2]` | `FAIL` current text glyph/data-transfer semantics differ; handler reorder itself is guard |
| `AFK-FULL-23 drill-join-debounce` | edit a nested Drill Join restriction only | source `y.state.edit` debounces; current parent flushes immediately | `FAIL` callback lifetime |
| `AFK-GUARD-01 empty-normal` | no profiles, normal non-Drill target discovery | muted `afkNoProfiles`; no editor | `GUARD` |
| `AFK-GUARD-02 master-default` | `k=null`, populated profiles | Master selected; profiles visible | `GUARD` |
| `AFK-GUARD-03 activity-return` | select second profile/open Potion/edit local draft -> Equipment -> AFK | same selection, panel and draft retained; hidden AFK effects suspended/resumed | `GUARD` source/current Activity mechanism already aligned |
| `AFK-GUARD-04 accepted-editor` | import accepted 24 renderer / 12 pixel / 267 behavior / 28 mounted editor packet | internal editor subtree remains exact | `GUARD` do not rewrite editor internals |
| `AFK-GUARD-05 compact-cards` | import accepted compact AFK card primitive | card gear/toggle/selection primitive remains exact | `GUARD` full ancestry still tested separately |

For visual closure, pair the full failing compositions in the assignment-required
EN/light and JA/dark desktop modes plus source-valid narrow EN/dark and JA/light
where layout rules differ. Runtime-only text variants can be renderer/mounted cases
when they share identical geometry, but at least the selected existing, selected new,
Potion-open, Drill-valid/empty and runtime-row families need full unmasked page pairs
because they change layout height/columns. No baseline should mask profile cards,
editor ancestry, settings ancestry or runtime text.

## Implementation impact for the coordinator

The smallest source-backed correction unit is the full AFK owner around the accepted
editor: restore toolbar settings ancestry; restore editor sibling ancestry; add the
source runtime rows; model source action-busy/reorder/add lifetime; use actual
available squads for Add and Drill; restore source drag glyph/transfer semantics;
restore source error/store partition and Potion/Drill debounce behavior. Keep
Garrison/Zombie internals delegated and preserve their child insertion point.

Activity retention itself should be preserved. Corrections should remain inside the
visited AFK Activity subtree so selected/new/profile/panel state survives the
Equipment round trip exactly as recovered.
