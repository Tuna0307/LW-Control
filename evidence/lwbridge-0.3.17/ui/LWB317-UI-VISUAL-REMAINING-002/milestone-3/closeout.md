# Milestone 3 — Automation conditional compositions

Status: **COMPLETE / READY**. Coordinator gate is green and the independent post-fix
review reports no remaining Milestone-3 source/local blocker.

## Exact recovered source

- `AutomationPanel-BJ0gIqFh.js` SHA-256:
  `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`.
- Recovered `Ae` is UTF-8 byte offset `21817`, length `53453`, SHA-256
  `61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68`.
- `AutomationCard-LCx_jIi7.js` SHA-256:
  `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61`;
  exact card function SHA-256:
  `A3FA760F0D42AA4A927C8B6E810407723F2BA314360CD35AC05D3DF08F639657`.

## Preserved failing state

`baseline/failing-baseline.json` records the original ten whole-`Ae` mismatches, and
`baseline/pre-fix-*` pins the three production files before Milestone-3 correction.
`baseline/regression-default-squad/` additionally preserves the intermediate state
that exposed the inherited Unit-C offline squad seed as source-invalid. The old Unit-C
renderer explicitly forced `Ut=[1,2,3,4]` while `online:false`; exact `Ae` only fetches
squad indexes online, so this milestone does not repin or rewrite that accepted packet.

## Corrected source/local composition

- Replaced hard-coded squad availability with the one recovered discovery provider,
  retaining the last non-empty result and feeding Alliance Gather, Resource Gather and
  Treasure from the shared set.
- Restored selected-first Alliance/Treasure priority rows and Treasure drag ordering;
  Resource Gather merges the shared provider with configured/runtime indexes.
- Split Secret Task and Dispatch Assist into the two recovered draft/save/error stores,
  including independent Retry/Discard ownership and Assist validation.
- Restored recovered immediate-save behavior for the `W(...)` control families while
  retaining deferred/blur validation for the fields that use it in source.
- Restored Railway running disables, exact card/page error ancestry, Trade fetch-error
  DOM role, Daily/Trade/Resource conditional compositions, and Activity retention.
- Native run/action callbacks remain fenced; all exercised controls are local preview
  state/config behavior only.

## Current proof

- `validate-milestone-3.mjs`: `LWB317_REMAINING_M3_VALIDATION_OK` with 14 exact-source
  contracts and 21 current contracts.
- `run-browser-current.mjs`: 154 mounted-browser assertions, 23 screenshots, zero
  console/page errors. Coverage includes English/Japanese, light/dark, desktop/narrow,
  nested forms, Weekly, Trade/history, Assist manual/auto/error states, training,
  running Railway, save Retry/Discard, hidden-Activity save completion, non-contiguous
  squads and non-empty-to-empty discovery retention.
- `source-render/`: 56 actual recovered-original/current whole-category screenshot
  pairs, zero changed pixels, zero browser console issues, with the source-valid offline
  `Ut=[]` state and independent original/current CSS/locales.
- Inherited exact lower-helper replays `003A` through `003E` remain green.
- Canonical `npm run check`, `npm run build`, `npm run check:production-build`, legacy
  archive preservation and `git diff --check` are green.
- `review/automation-independent-review.md` follow-up verdict is **READY** after
  re-checking separate Assist ownership, immediate-save families, Railway running,
  Activity retention, shared squad retention/Resource Gather and Trade error DOM.

## Deliberate limits

The renderer packet is source-valid offline presentation with inert stores/effects and
no protected original runtime. Asset-complete original runtime remains unavailable.
Mounted current-browser interaction evidence covers the recovered conditional states
that cannot be driven by that offline exact renderer. No native gameplay action was
invoked or added.
