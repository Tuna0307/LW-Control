# LWB317-PM-007 — independent correction review

Date: 2026-10-01. Reviewed worker HEAD:
`543b6ebffb62125cd5e1c5d290260c5ad8445aff`, clean and matched to the remote.
Worker milestones: `239254a`, `591409d`, `543b6eb`.

Decision: **CHANGES_REQUIRED** for `LWB317-UI-CORRECT-001` as a complete
recoverable-UI campaign. Preserve all useful corrections. Accept the specific
Home profile-scope correction; the original broad UI completion claim remains
open. Continue with `docs/work-items/LWB317-UI-CORRECT-002.md`.

## What this review accepts

- PM-006 R1 is corrected at the frontend/host-contract boundary: the actual Home
  callback invokes the profile-scoped helper, missing profile rejects before
  dispatch, and selected profile ID reaches the actual transport envelope.
  A fresh deferred-response harness executed the actual callback and bridge,
  checking unchanged checked state before acknowledgement, rejection handling
  and busy-state cleanup. Native write/live preference proof is not claimed.
- Equipment now has a real local Rename dialog, blank-name/busy guards,
  Enter/Escape/Cancel/backdrop handlers and local save state. The old inert Rename
  defect is addressed. Drag handlers and result/progress branches were added;
  the worker's explicitly limited physical-drag evidence remains limited.
- Locale generation now recovers the shared error-message composition; all
  nine generated catalogs contain 1,383 messages. No original auth UI was added.
- The Map fixture provider is gated to browser preview and rejects native
  operations. Existing production Map paths and recorded live-proof scopes are
  not replaced by the fixture.

These local corrections are useful accepted progress. They do not establish
original pixels, native gameplay/lifecycle operation or complete UI parity.

## C1 — source-recoverable Automation controls remain omitted

Exact source: `AutomationPanel-BJ0gIqFh.js`, SHA-256
`6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`, under
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`.
Offsets below are zero-based UTF-8 bytes in that exact file.

1. Construction's `construction-category-tabs` branch at byte 47933 includes
   category tabs, ArrowLeft/ArrowRight/Home/End navigation, a target-types
   tabpanel and per-building checkboxes updating `buildingTypeIds`. Clone
   `Pages.jsx:269` renders only a summary and repeated empty-selection message.
   Fresh browser QA opened it: zero category tabs and zero picker checkboxes.
   This is missing implementation, independently of native construction work.
2. The Soldier Training component starts at byte 14619. Its target selector at
   byte 16373 derives options from camp `availableLevels`; subsequent branches
   include invalid quantity, order progress/reason, summary, no-camps,
   data-unavailable, open-failed and camp cards. Clone `Pages.jsx:265` contains
   only two inputs and a single Highest option. The matrix does not inventory
   these Training branches.
3. Resource Gather at byte 11460 offers radii
   `[50,100,150,200,250,300,400,500]` and resume-delay choices 1–30 minutes,
   then data-dependent squad cards. Clone `Pages.jsx:365` onward pins one option
   each (200 and 2) and always prints squads-loading, even in populated preview.
4. Alliance Train fixed-normal branch at byte 54850 has a disabled Driver
   checkbox and carriage checkboxes that can clear the selection. The clone's
   radio choices at line 291 cannot clear a selected carriage. The source's
   reward-preference list/selection/reorder branch is also replaced by an empty
   message. The source normal fixed selection is one-or-none (`[e]` or `[]`),
   not unrestricted multi-select; preserve that exact distinction.

These `EXACT_BYTES` render/handler contracts are available without executing
native tasks. Unknown runtime values can be supplied by disclosed fixtures;
they do not make the control structure itself unknowable.

## C2 — AFK edits are shared across distinct profiles

Exact source: `SquadPanel-HC3-DJei.js`, SHA-256
`ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`.
Profile selection `st(e)` at byte 33322 loads the selected profile object;
target change `at(e)` at byte 32711 updates that profile's target contract.
The rendered name/target fields at bytes 44847/45104 are controlled by it.

Clone `Pages.jsx:602` stores only `{id,name,enabled}` for each profile. At line
635 the same unkeyed editor receives only its name. Target, assignments,
execution/filter inputs and join draft are uncontrolled or component-local,
without a per-profile model.

Fresh real-input reproduction in isolated preview:

1. Change Steel Hunt target to Food.
2. Select Gold Hunt: name becomes Gold Hunt, but target is still Food.
3. Change Gold Hunt target to Boss.
4. Select Steel Hunt: name becomes Steel Hunt, but target is now Boss.

This demonstrates local draft leakage, not a missing native persistence provider.
Merely remounting the editor would reset edits instead of recovering independent
profile drafts; reproduce the actual data-binding contract.

## C3 — Map fixture QA and matrix overstate the exercised contracts

`src/mapPreviewApi.js:124` receives a query but only uses `query.page`; filters,
sort and page size do not affect its returned rows/count. Pages always return
two synthetic rows with an independent total of 52–59. This provider can prove
rendering and request submission, but cannot prove applied filtering, sorting,
page-size behavior or final-page/count consistency. No production Map-query
defect is asserted here; this is an offline QA limitation.

The new matrix is more useful than the previous broad table, but several rows
remain collections of branches verified only by source-handler/marker labels.
Construction/Training/Gather omissions and AFK draft isolation demonstrate that
the matrix is not exhaustive completion evidence. Add exact render/handler
locators and actual assertions per affected branch; first label occurrences and
marker presence are not equivalent to behavior proof.

## Fresh verification and durable evidence

- Canonical check, build and production-package check passed. Index hash:
  `71705b869d4a80d0774b69760bd5678d996e45dc516cef8d315b2542a9eb10af`;
  asset hash: `331eef9754888657e8dfb4be0178c4c46c9c1ef620d1081e7b3ecb5cdea0f68b`.
- Map .NET deterministic checks passed: `LWB317_MAP_CHECKS_OK`.
- All 30 worker-manifest executable/source/locale/screenshot identities match.
- `lead-review/check-home-callback.mjs` passed acknowledgement, rejection and
  missing-profile cases using deferred in-memory transport replies.
- `lead-review/browser-findings.json` records real Construction and AFK QA.
  The two screenshots were visually inspected; they are clone evidence only.
- Both owned browser tabs were closed; the exact owned Vite listener/process on
  4319 was identified and stopped. Final listener check: `NO_4319_LISTENER`.

Evidence root:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/lead-review/`.
No original/game process was launched or controlled, and no native preference,
gameplay, scan, lifecycle, update or external service action was executed.

## Continuation

One manually dispatched worker performs UI-CORRECT-002: complete the remaining
source-backed Automation forms, isolate AFK drafts, make read-only Map QA
meaningful, and reconcile affected coverage. Keep the Home fix and other useful
UI work. The overall goal still covers all eight pages. Original pixel validation
and physical-gesture/provider validation limits remain separate from missing
implementation. Native Home lifecycle and new backend families remain unassigned;
the existing Map campaign still awaits separate lead closeout.
