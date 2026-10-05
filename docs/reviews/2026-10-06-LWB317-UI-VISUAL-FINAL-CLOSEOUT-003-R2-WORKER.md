# Worker R2 delivery — LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2

Date: 2026-10-06

Decision requested from project lead: **AWAITING_REVIEW** for the recoverable
source/local UIUX scope. This R2 delivery addresses only the Equipment save-mode
blocker demonstrated by the R1 lead review. The accepted R1 profile-ownership
correction and all earlier bounded visual findings remain unchanged.

## Result

The recovered Equipment save contract is restored in the canonical source.
`EquipmentContent.flushPreviewConfig` now calls
`equipmentConfig.store.flush(false)`. The global `createConfigDraft.flush()` default
remains `true`, and no other Automation/AFK/global save behavior is changed.

Recovered `SquadPanel-HC3-DJei.js` function `fd` contains Equipment helper `P` at
UTF-8 byte 180274:

`async function P(e){u.state.edit(e,!1);try{return await u.state.flush(!1)}catch{return null}}`

That false mode matters when a first Save is still pending. It confirms the draft
that was actually requested, then stops unless another explicit Save queued a newer
draft. A later local edit is therefore left dirty rather than silently confirmed.

The R1 profile ownership stays intact: Equipment's retained config-store scope is
still `JSON.stringify([profileId, equipment scope])`; selection, dialog/input,
drag/drop, toast and other transient interaction state stays local to the mounted
profile subtree.

## Fresh distinguishing Equipment proof

`r2/equipment-save-contract.mjs` executes exact recovered config-store `T` and exact
helper `P`, then mounts the current served `SquadsPage` and drives actual rendered
Rename, Save, Retry, Discard and React drop handlers. Controlled adapters provide
only acknowledgements/errors; no native provider is invoked.

The focused packet passes **38 assertions**, records **2 settled Equipment
captures**, and reports **0 browser issues**. It proves:

- pending Save on profile A → profile B → profile A → a later rendered move with no
  second Save: the first acknowledgement confirms the requested rename and leaves
  the later move dirty in both exact original and current behavior;
- a later explicit Save confirms that retained move;
- an explicit second Save while the first request is pending is queued and drains
  to the newer draft;
- write rejection retains the owning dirty draft/error and Retry confirms it;
- Discard restores the owning confirmed value;
- profile B stays isolated through success, queued-save, Retry and Discard flows.

The two focused captures are EN/light after the first acknowledgement with the move
still dirty, and JA/dark after the explicitly queued second Save confirms the moved
draft. The focused `--verify` path re-executes the contract and re-hashes the
captures without rewriting them.

`r2/equipment-regression.mjs` separately replays the accepted Equipment consumer
paths against corrected current source: ordinary save, same-profile AFK
hide/return retention, rejection, Retry, Discard and deferred busy state. It passes
**17 assertions** with **0 browser issues**.

## Fresh complete-App R2 verification

R2 does not rewrite or repin R1/A-E evidence. The fresh final runner binds the
frozen R1 host/profile/composition files by identity and requires the current
served App closure to differ from the frozen R1 host closure in exactly one file:
`src/LWBridge.UI-0.3.17/src/SquadsPage.jsx`.

The frozen R1 Squads hash is
`920892FCC3069D4F98B0662E4502910DDF5901EE707C8A663B751BBB3A2E4623`;
the corrected R2 Squads hash is
`5D0AE58CBE199441A046E326D699808CA31AEB96D6CC7DAA0E7EC0A8C3B231D7`.
Every other served source-closure file remains at the frozen R1 identity.

The R2 final browser pass executes the focused Equipment contract again inside the
same current-App verification and replays the accepted route, profile A/B/A/B,
AFK, Automation, Map, dialog/popover, timer/keyboard, locale/theme and corrected
race journeys. The rewrite-free validator reports:

- **67** served source files;
- **282** complete-App assertions;
- **16** decoded settled complete-App screenshots;
- **9** locale catalogs with **1,383** keys each;
- **12** detected semantic mutations;
- **0** console/page issues.

The twelfth mutation is the R2-specific inverse: replace Equipment
`flush(false)` with default `flush()`. It is detected because the later unrequested
edit no longer remains dirty after the first acknowledgement.

## Canonical gates

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`: PASS.
  Build fingerprint:
  `ff5b005c08e03051a7bc58aa2e88a44261592ffa96b5f2e9bfb85a62c09cc53d`.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`: PASS.
  Production-package fingerprint:
  `c0928be052ae6d376f939b8b8f7581df874fd8422bfa4a31f6c0c1b676eef737`.
- Legacy WIP archive guard:
  `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10`.
- Reference executable SHA-256:
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Focused Equipment save-contract rewrite-free verification: PASS, 38/2/0.
- Equipment regression rewrite-free verification: PASS, 17/0.
- R2 final rewrite-free validator: PASS, 67/282/16/9/12/0.
- `git diff --check`: PASS before final delivery staging.

## Evidence and integrity

Primary evidence is
`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/`.
Its final manifest hashes the current source closure, evidence scripts, support
tools, frozen acceptance inputs, result and complete-App captures. The focused
validator also hashes both Equipment screenshot sets.

Frozen `r1/`, `r1-lead-review/` and Milestone A-E records have no delivery diff.
The legacy WIP archive exact-file guard remains green. The production application
closure has exactly the intended `SquadsPage.jsx` change relative to the frozen R1
host closure.

## Remaining limits

The fresh browser evidence uses current source/local preview behavior and
controlled local acknowledgement/error adapters. The Equipment drop case invokes
the rendered React drag/drop handlers; it does not claim a physical HTML5 drag.

No gameplay/native/provider-positive action, updater/OS action,
original-service/auth access or protected post-auth original-runtime complete-App
pixel oracle was invoked. Loaded native game assets and provider-positive behavior
remain separate project dependencies. Independent project-lead R2 review is the
next acceptance step.
