# LWB317 UI visual final closeout R2 evidence

Date: 2026-10-06

This directory is the fresh correction packet for
`LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2`. Frozen R1, R1 lead-review and Milestone
A-E records remain historical inputs and are not rewritten or repinned.

R2 changes one production contract in `SquadsPage.jsx`: Equipment's shared explicit
save helper calls `equipmentConfig.store.flush(false)`. Exact recovered Equipment
helper `P` in `SquadPanel-HC3-DJei.js`, UTF-8 byte 180274, performs
`edit(e, false)` followed by `flush(false)`. The global draft-store `flush()`
default remains `true`, so no Automation/AFK/global save policy changes.

## Focused Equipment proof

`equipment-save-contract.mjs` executes exact recovered config-store `T` and exact
Equipment helper `P`, then mounts the current served `SquadsPage` with its actual
rendered Rename, Save, Retry, Discard and React drop callbacks. Controlled local
adapters exercise four ownership/save paths:

- first Save pending, profile A -> B -> A, later rendered move, no second Save:
  the first acknowledgement confirms only the requested rename and leaves the move
  dirty in both recovered/current behavior;
- a later explicit Save confirms that retained move;
- an explicit second Save while the first request is still pending is queued and
  drains to the newer draft;
- rejection -> Retry and rejection -> Discard remain profile isolated.

The focused result contains 38 assertions, zero browser issues and two settled
Equipment captures: EN/light for the dirty post-acknowledgement state and JA/dark
for the explicit queued-save confirmation state. `--verify` is rewrite-free and
also re-hashes those captures.

`equipment-regression.mjs` separately replays the accepted Equipment success,
same-profile Activity retention, rejection, Retry, Discard and deferred-busy
consumer paths against the corrected current source. It records 17 assertions and
zero browser issues.

## Fresh complete-App proof

`run-final-current.mjs` derives from the accepted R1 complete-App runner but writes
only R2 artifacts. It binds the frozen R1 host/profile/composition authority by
hash and requires the current App source closure to differ from the frozen R1 host
closure in exactly one file: `src/LWBridge.UI-0.3.17/src/SquadsPage.jsx`. The old
and corrected Squads hashes are pinned explicitly. The runner then executes the R2
focused Equipment contract again inside the same fresh current-App browser pass,
alongside the accepted eight-route, profile A/B/A/B, AFK, Automation, Map, shared
host, locale/theme, timer, dialog and controlled race journeys.

`validate-final-current.mjs --verify` rejects source, result, evidence, tool,
screenshot, pixel, locale, acceptance-chain or mutation drift without rewriting.
It verifies both focused Equipment screenshot sets in addition to the complete-App
captures. `mutation-check.mjs` retains the accepted A/B/C semantic mutations and
adds the R2-specific mutation that restores Equipment's default `flush(true)` mode;
that mutation must be detected by the dirty-after-first-acknowledgement contract.

## Recording and validation

Run against an explicitly owned local Vite listener from the repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-save-contract.mjs --record --port=<owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-save-contract.mjs --verify --port=<owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-regression.mjs --port=<owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-regression.mjs --verify --port=<owned-port>
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/run-final-current.mjs <owned-port> --record
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/validate-final-current.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/mutation-check.mjs
```

The repository closeout additionally runs the canonical frontend check, production
build, production-package verification, legacy-WIP archive guard, reference EXE
hash check and Git diff/integrity checks.

## Limits

All browser writes use source/local preview state or controlled local adapters. No
Last War/native provider, gameplay, updater, OS-hotkey, server-jump, scan/plunder or
other native action is invoked. Physical HTML5 drag is not claimed; the focused
case calls the rendered React drag/drop handlers. Provider-positive behavior,
loaded native assets and protected original-runtime complete-App pixels remain the
same separate project dependencies recorded before R2.
