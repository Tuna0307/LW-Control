# Milestone 2 independent review — Garrison + Zombie Bus

Scope: current coordinator edits in `SquadsPage.jsx` and
`previewAfkCloseoutFixtures.js`, reviewed only against
`recovery/garrison-zombie-worker.md` and exact recovered
`SquadPanel-HC3-DJei.js` `pe/me/he`.

Current working identities:

- `SquadsPage.jsx` SHA-256
  `26A896E92667EE12719F3AD1CB907AADF99D957819771465023D9396333AF544`
- `previewAfkCloseoutFixtures.js` SHA-256
  `B1FB835809F847FAC51C1A79A8DDB8584AFACA15B9143DC01CCC7C5393856E68`
- current `GarrisonPreviewSettings`: byte 17,677, len 11,800,
  SHA `EC53C9224024C4616DA2C3C38B7C9C4A255A7B9F6D5BD8622F164660F443EAF4`
- current `ZombieBusPreviewSettings`: byte 29,479, len 1,332,
  SHA `DBFA2A86E9311832A72619634724CD9A194CBE317DCFD83796D6D648344532E8`
- current `AfkContent`: byte 33,495, len 19,832,
  SHA `D27730A1EE83E2F086E7608314A75CA6AE35CD7A75F339EC71BE537CC2E1C619`

Recovered source remains unchanged:

- `pe`: byte 4,586, len 11,256,
  SHA `4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85`
- `me`: byte 15,842, len 687,
  SHA `7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8`
- `he`: byte 16,529, len 1,957,
  SHA `37F600C70C0F2F1A2F5722DC33C6E62075A7976C86ECA48001E97F1FA39BC32B`

## Closed by the coordinator edits

The changed code correctly closes the main recovery-note mismatches:

- **G01** invalid Garrison enable now records the correct local error and does not
  auto-open settings.
- **G02** ally search is now source name-only.
- **G03** ally availability is again a separate `<em>`.
- **G04/G05** empty selected-allies and target-required ancestry match source.
- **G06** the squad section always shows `garrison.squadHint`; Required is now an
  action error rather than an eager empty-state replacement.
- **G07** target/squad drag affordances now use the recovered drag SVG.
- **G10** the invented Garrison status-`lastError` paragraph was removed.
- **G11** Garrison config Retry/Discard is now owned inside the open Garrison
  settings after the settings grid.
- **G12** ordinary Garrison settings edits now use the deferred store path; enable
  remains immediate.
- **G13** a successful squad mutation no longer clears the prior invalid-final-squad
  error immediately.
- **Z02/Z04/Z05** Zombie runtime/config errors now live in the recovered separate
  Zombie sibling block, including the config+runtime overlap.
- The isolated Zombie `me` table remains unchanged and consistent with the
  inherited exact source replay.

No new regression was found in those individual corrected sub-branches.

## Incorrect / incomplete coordinator composition change

**COMMIT BLOCKER — `pe/he` fragment ordering is still wrong in `AfkContent`.**

Recovered `I` has one `.monster-afk-toolbar` whose children are, in order:

1. Master compact card
2. Potion compact card
3. Drill compact card
4. whole `pe` Garrison fragment
   - Garrison compact card
   - Garrison settings when open
   - Garrison picker when open
5. whole `he` Zombie fragment
   - Zombie compact card
   - Zombie settings when open
   - Zombie error sibling when present
6. Potion settings when selected
7. Drill settings when selected

Exact source anchors: toolbar byte **35,833**, `pe` call **36,795**, `he` call
**36,922**, Potion settings **37,041**, Drill settings **37,831**.

Current lines 335–344 instead render **all five compact cards first**, then append
the selected Garrison/Zombie settings/error components. Moving the settings into
the toolbar was directionally correct, but splitting `pe/he` still changes DOM/grid
order. In particular, when Garrison is open, source places Garrison settings before
the Zombie compact card; current places them after it. This must be corrected before
the milestone commit because full composition, not isolated widget equality, is the
Milestone-2 gate.

## Remaining source-local gaps that must be closed

### 1. Garrison ally picker is not the recovered dialog

Recovered `pe` renders the picker as sibling component `y`; `y` resolves to
`index-BVfnK1wp.js` function `In`:

- byte **209,017**, length **1,209**
- SHA-256
  `E42C17AAC67AFE7A73D525902C9E757644E40838422730CEB32EC47830C4F8A2`

`In` renders native
`<dialog class="app-dialog garrison-modal-backdrop">`, calls `showModal()`,
restores prior focus on unmount, traps Tab within the dialog, handles Escape through
`onCancel`, and defaults `dismissOnBackdrop=false`.

Current line 201 instead nests a plain backdrop `div` inside
`.garrison-settings`, puts dialog semantics on the child `section`, has no native
top-layer/focus trap/focus restoration, and **closes on backdrop mouse-down**.
This is both structural and local-interaction drift and must be fixed/proven.

### 2. Garrison available squad source is still hard-coded

Recovered `pe` loads actual squad indexes into `D` and renders
`R=[...g.squadPriority, ...D.filter(...)]`.

Current `GarrisonPreviewSettings` line 140 still uses hard-coded
`[1,2,3,4]`. `AfkContent` now has an `availableSquads` input and passes it to
Drill, but does **not** pass it to Garrison. Missing/fewer/non-default supplied squad
sets therefore still cannot reproduce the recovered branch. This is a clear
coordinator omission and should be closed with source-valid missing/populated squad
cases.

### 3. Recovered localized building-name branch remains absent

Recovered `pe` byte **7,086** uses `fe(building, translatedNames, fallback)`;
`F` fetches translations for nonblank building `nameKey`, and `fe` also applies
`[allianceAbbr]` when present.

Current lines 142–144/184 use `building.name` directly. No current Garrison input
can prove the `nameKey`/translation/alliance-abbreviation branch. Either reproduce
that source-local presentation or provide an exact equivalent current input contract
and immutable proof before commit.

### 4. Discovery failure / unresolved summary branch is still unproved

Recovered `pe` combined discovery `F`:

- keeps `v=null` while unresolved;
- falls back to saved target count for compact enabled summary
  (`V = v ? available-count : g.targets.length`, byte **8,356**);
- writes a translated local inline error when the combined discovery call rejects;
- clears that local error on the next successful `F`.

Current summary lines 304–308 always derive availability from current fixture maps,
and there is no equivalent Garrison discovery-failure transition/error-clear path.
The earlier invented member-loading/error row was correctly removed, but this does
not yet reproduce the actual recovered pending/failure/success lifetime. This needs
one controlled inert source/current baseline before milestone commit.

## Evidence-only gap

Zombie `nameKey` translation logic is still correct in the current table, but the
normal routed positive fixture does not exercise it. Keep the inherited direct
`me` source replay or add a task-local whole-`he` translated-row case; this does
not require a production provider.

## Intentional fences — preserve

- Current Garrison Run-now remains disabled/presentation-only. Recovered source
  invokes a native action; do **not** enable or execute it for visual parity.
- Native Garrison stop/recall, Zombie config provider/status polling, and protected
  original runtime/gameplay remain outside this source/local milestone.
- Positive fixture data must stay restricted to explicit `squads-profile*` modes;
  do not leak synthetic buildings/members/assignments into native/inactive states.
- DOM-dispatched drag can prove handler/state behavior but is not physical
  pointer-driven HTML5 drag.

## Review decision

**NOT READY TO COMMIT for the Garrison/Zombie Milestone-2 subunit.**

The coordinator closed most previously inventoried leaf mismatches, but the whole
`pe/he` toolbar ordering, recovered Garrison native-dialog behavior, dynamic squad
source, building localization branch, and discovery pending/failure lifetime remain
source-local gaps. Close and freeze those cases before the milestone commit.
