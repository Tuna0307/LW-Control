# LWB317-PM-006 — independent review of UI completion

Date: 2026-10-01. Project lead decision: **CHANGES_REQUIRED**.

Reviewed clean worker HEAD: `a253cceff5f06512eeedaaf33cf6617f1e6170b3`.
Implementation: `7c4daed4112747fcdd619e533184251da86cb389`.
Documentation: `6181748a032445f0baed1a340492dba2aefcd315`, `a253cce`.
The remote matched the reviewed HEAD before this lead checkpoint.

## Decision

Preserve the useful implementation and evidence. Do not accept
`LWB317-UI-COMPLETE-001` as complete source-backed UI/UX: concrete source
deviations and missing local interactions remain. A successful build and route
screenshots do not establish complete conditional coverage. The lack of original
post-auth screenshots is a separate validation blocker, not the cause of the
implementation defects below. No new function family opens from this report.

The correction assignment is `docs/work-items/LWB317-UI-CORRECT-001.md`.

## Findings

### R1 — Home reconnect preference omits required profile scope

`src/LWBridge.UI-0.3.17/src/App.jsx:168` creates the callback;
line 173 invokes `set_automation` with only `name` and `enabled`.
`src/LWBridge.UI-0.3.17/src/backendBridge.js:75` forwards that payload unchanged;
its selected profile is exposed separately at line 20.

`src/LWBridge.Desktop/LWBridgeBackend.cs:212` validates scope before dispatch.
`set_automation` is absent from `GlobalCommands` (line 17).
`ValidateCommandScope` (line 1052) therefore calls `RequireProfile` (line 1144),
which rejects an absent `profileId` with `PROFILE_REQUIRED` before the write.
The later `RequireOptionalProfile` in the dispatch case does not bypass the
earlier validation. This is an `EXACT_CONTRACT` defect established against the
current frontend/backend sources; no native preference write was performed.

Read-only reproduction:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-COMPLETE-001/lead-review/inspect-home-reconnect.mjs
selectedProfileId: offline-review-profile
command: set_automation
payload: { name: autoForceUpdateReload, enabled: true }
profileIdPresent: false
```

The harness executes the actual checked-in callback body and actual frontend
bridge against an in-memory WebView transport. Its successful mock reply is
synthetic and proves the outbound envelope only. Native rejection follows from
the exact host validation above; it was not live-tested. The worker's claim
that both preference writes are validated is unsupported.

Required correction: use the existing selected-profile contract, handle missing
profile/error state honestly, and test the callback-to-transport boundary without
changing or weakening host scoping.

### R2 — Automation replaces different source forms with generic forms

Clone: `src/LWBridge.UI-0.3.17/src/Pages.jsx:217` through line 244.
Line 235 gives Trucks, Secret Task, alliance Secret Task assistance, Ghost Ops
and train boarding the same quality/three-minute interval/collect-rewards form.
Line 241 gives unhandled cards a generic sixty-minute interval.
Every card gets a Settings expander and an enabled preview Save button with no
handler at lines 265–274.

Exact original:
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js`
SHA-256 `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.
Locators below are zero-based UTF-8 byte offsets at the first matching token:

- `title:\`automation.ghost.title\`` at 73781: own-start toggle, alliance-join
  toggle, conditional alliance-filter radio group and auto-claim toggle. These
  are absent from the clone's generic Ghost form.
- `title:\`automation.secretTask.title\`` at 68579: execution section,
  collect-rewards control and reset-delay input with min 0/max 1440, alongside
  task-specific actions/status rows. The clone does not reproduce that form.
- `automation.autoCollectRewards` at 49154 belongs to Construction's
  `autoClaimCompleted` control. Construction's clone form omits it.
- First `settingsCollapsible:!1` at 44797 belongs to a resource-card renderer;
  Treatment also explicitly uses `settingsCollapsible:!1`. The clone does not
  preserve these card-specific presentation rules.

These are `EXACT_BYTES` source differences, independent of unavailable native
Automation execution. A translated string alone does not prove that a control
belongs on a particular card or that its defaults/limits are correct.

Required correction: recover and reproduce each card's actual form, predicates,
validation, status/action presentation and local transitions. Record missing
contracts instead of inserting a generic substitute.

### R3 — Equipment nested interactions are missing

Clone: `src/LWBridge.UI-0.3.17/src/Pages.jsx:526` (`EquipmentContent`).
Create, Rename, Read Current and Save appear enabled in preview but have no
handlers at lines 534 and 540–543. No rename dialog or equipment drag/drop
handlers exist in this component. Its screenshot proves only a static render.

Exact original:
`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js`
SHA-256 `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`.
The `equipment-preset-dialog-backdrop` token starts at byte 189887. Its branch
contains the Rename title, editable preset-name prompt, Enter-to-submit,
Cancel, Save, empty-name disable rule and busy-state disable rule. The
`squad.presetNamePrompt` token starts at byte 190337. The equipment-position
rendering at byte 189283 (`equipment-position-items`) calls the recovered item
renderer, rather than merely printing empty text. The item's `draggable:!!e&&!_`
predicate at byte 183734 has slot-specific drag/drop handlers. Nearby branches include
equipment result/toast/apply-progress presentation.

Required correction: reproduce recoverable editor/dialog/local interaction
contracts in an isolated preview provider; keep actual game application outside
this task. Do not present an enabled inert control as completed UX.

### R4 — coverage evidence cannot substantiate the broad completion claim

The worker coverage matrix has one broad row per page, with asset filenames
rather than an exhaustive branch inventory. Eleven clone screenshots and the
reported interactions are useful samples, but omit the source-proven branches
above. `scripts/check-ui-complete.mjs` checks literal English translation keys,
nonempty catalogs, a fixture fence string and preview marker strings; it does
not verify native preference scope, card-specific structures, modal transitions
or complete state coverage.

Locale extraction also removes imported spreads. Japanese has 49 fewer direct
keys than English, including updater error messages. No literal `t("...")`
reference examined in App/Pages/Map was missing from Japanese, so this review
does not claim raw translation keys currently appear on those default screens.
The follow-up must audit inherited in-scope messages and updater error branches
against the actual original composition; original auth-error reconstruction
remains excluded.

Required correction: per recovered nested surface/state/interaction, list the
source hash and byte/expression locator, predicate, implementation, QA result
and precise open dependency. Audit all eight pages and shared surfaces;
unreviewed pages are not automatically accepted because three examples failed.

## Verification performed by the lead

- Canonical `npm.cmd run check --prefix src/LWBridge.UI-0.3.17`: passed.
- Canonical `build` and `check:production-build`: passed. Package index hash
  `7489c997b5253b5eef84a9bd1da9d1f2de98dfd1d7629e277f40f88110d361da`;
  asset hash `a34bcd9545789e191c6ce9002b3c9f2a9522f14d09a54c99bdcbeda96b43cfe5`.
- Map deterministic checks: `LWB317_MAP_CHECKS_OK`.
- Recomputed all 30 manifest identities: executable, ten frontend sources,
  nine locale assets, eleven clone screenshots; zero hash mismatches.
- Inspected the equipment screenshot and exact component source snippets.
- Read-only callback/transport harness confirms the missing `profileId`.
- Pre-documentation working tree and `git diff --check`: clean.

No reference/game process was launched or controlled. No live/native preference,
scan, gameplay, lifecycle, update, feedback-export or service operation was run.
This review does not change independently recorded Map live-proof scopes.

## Continuation

One manually dispatched fresh worker should perform `LWB317-UI-CORRECT-001`.
Keep the current implementation, repair the demonstrated defects, audit remaining
source branches and return granular evidence. Native Home lifecycle remains a
later assignment. Original visual parity remains `BLOCKED` until legitimate
matched post-auth reference states exist. The Map Goal still awaits its separate
lead closeout.
