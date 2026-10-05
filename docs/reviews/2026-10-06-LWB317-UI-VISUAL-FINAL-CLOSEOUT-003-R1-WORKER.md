# Worker R1 delivery — LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R1

Date: 2026-10-06

Decision requested from project lead: **AWAITING_REVIEW** for the recoverable
source/local UIUX scope. This worker delivery corrects the one demonstrated defect
from the independent 2026-10-06 lead review and does not declare project-lead,
native/provider or protected-runtime acceptance.

## Result

The Squads profile-owner defect is corrected. The canonical App now propagates the
selected account profile into Squads. AFK config stores use the recovered registry
owner for `task:monsterSweep`, `task:staminaPotion`,
`task:allianceGarrison` and `task:zombieBus`, so each profile retains its own
draft, confirmation and error lifecycle.

The R1 source audit also resolved the work item's explicit Equipment uncertainty.
Recovered Squad component `fd`, UTF-8 byte 176937, obtains the selected profile
and calls the same config registry with scope `equipment`. R1 therefore gives the
Equipment config draft/confirmed/saving/error lifecycle the same profile ownership.
Selection, rename dialog/input state, drag/drop, toast, action progress/result and
timers remain component-local transient state.

No store is globally cleared on profile change. A's retained state returns only
with A; B starts and remains independent.

## Exact recovered owner authority

The reference executable re-hashes to
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Recovered asset identities remain:

- main `index-BVfnK1wp.js`:
  `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`;
- Squad `SquadPanel-HC3-DJei.js`:
  `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`.

The exact main registry anchors remain `T` byte 191297, `re` byte 193714 and
`me` byte 195132. `re` keys stores as `JSON.stringify([profile, scope])`.
The Squad call sites are `task:monsterSweep`, `task:staminaPotion`,
`task:allianceGarrison`, `task:zombieBus` and the separately proven
`equipment` store in `fd`.

## Fresh R1 evidence

`r1/profile-ownership.mjs` preserves and binds the independent lead failure
baseline, executes the exact original registry, and distinguishes the corrected
actual App:

| Step | Potion owner state |
| --- | --- |
| Local 1 initial | 50, clean |
| Local 1 inert consumer edit | 10000, dirty |
| switch to Local 2 | 50, clean |
| return Local 1 | 10000, retained only on Local 1 |
| return Local 2 | 50, still clean |

Controlled current draft-engine cases prove deferred success/rejection,
Retry/Discard and confirmation/error isolation across every recovered AFK scope
plus Equipment. They use local adapters only and invoke no native provider.

Affected verification was freshly replayed rather than repinning historical
records:

- 30 whole recovered/current AFK compositions;
- 14 recovered/current AFK browser pairs with zero console/page errors;
- 11 recovered/current Join-dialog contract checks;
- 25 mounted AFK/Join/hide-return interactions with zero browser issues;
- 17 Equipment save/error/Retry/Discard/deferred/hide-return assertions;
- unchanged Automation conditional/profile-owner authority is inherited by pinned
  current identity and exercised again in the complete-App profile journey.

Fresh R1 host reconciliation passes 257 assertions and freezes the current
67-file source closure at
`281D3C06ADB0ED89F6D0BA8C66A99AECD09E6152F45FF100B706C33811D1580E`.
The final complete-App record adds the missing AFK profile-owner journey to the
accepted routes, subtabs, Map, Automation, shell, dialog, timer, locale/theme and
race coverage. Its rewrite-free validator reports:

- 67 source files;
- 275 complete-App assertions;
- 16 decoded settled screenshots;
- 9 locale catalogs with 1,383 keys each;
- 11 detected semantic mutations, including removal of AFK or Equipment profile
  identity;
- 0 console/page issues.

## Canonical gates

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`: PASS.
  Build fingerprint:
  `3687f1b12facf940af3419c6a70cd7f9630eb831c754d543e4350755e0f3e12c`.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`: PASS.
  Package fingerprint:
  `0f74ce69349b51d9a8f5747f28fd209b92554916669518e382576413a8b73400`.
- Legacy WIP archive check: `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10`.
- Reference executable SHA-256: exact expected hash above.
- R1 final rewrite-free evidence validator: PASS, 275/16/9/11/0.
- `git diff --check`: PASS before delivery staging; rerun at final Git delivery.

## Remaining limits

The fresh browser evidence is source/local preview behavior and inert consumer
ownership where offline controls remain disabled. Controlled deferred/error cases
use local adapters and do not claim provider-positive native success.

No gameplay/native action, updater/OS action, original-service/auth access or
protected post-auth original-runtime complete-App pixel oracle was invoked.
Loaded native game assets and provider-positive behavior remain separate project
dependencies. Project-lead review is the next acceptance step.

Primary evidence:
`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/`.
