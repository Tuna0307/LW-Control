# Milestone A — demonstrated corrections

Date: 2026-10-05

This milestone preserves the lead review's failing records and adds fresh executable inverse proof against the corrected canonical sources. The historical `LWB317-UI-VISUAL-REMAINING-002` validators remain unchanged and continue to describe the reviewed defective baseline.

## Closure

| Finding | Corrected canonical behavior | Fresh proof |
| --- | --- | --- |
| LR-A1 | Train reward rows own recovered source-dragging and target `drag-over` state; drop/end clear both and drop preserves reorder/save. | `automation-corrections.mjs` executes recovered `Ae` and live `AutomationFields`; producer and `--verify` pass. |
| LR-A2 | Missing normal/VIP fixed-carriage arrays select zero carriages; explicit empty/populated inputs remain distinct. | Four matched Train cases in `automation-corrections-results.json`. |
| LR-A3 | Generic Automation card errors use the recovered `DIV.automation-error[role=alert]` geometry before config. | Original/current browser geometry is exact: DIV, 0px margin, y=219, height=16, next y=247. |
| LR-S1 | Recovery envelopes from another profile are rejected; same-profile/raw events are accepted; replacement and cleanup retire old acknowledgements. | `shell-home-corrections.mjs` executes the live recovery effect plus recovered `Pe`. |
| LR-S2 | The recurring five-second status poll has its own in-flight fence and selected-profile/closed acknowledgement gate; explicit Refresh stays separately dispatchable. | Live callback/effect execution proves one request under overlap, zero stale/closed status/proxy/error writes, two explicit manual reads; recovered periodic callback also issues one request. |
| LR-S3 | An abandoned profile bootstrap cannot clear the currently selected profile's loading state; the current owner clears loading and cached return skips bootstrap. | B→C deferred RAF replay keeps C loading after B completes, then C completes and cached A returns with no RAF. |

Two additional source-proven local differences were found while closing the lead findings and were corrected within the same Automation boundary: Resource Gather's card-owned error now uses the recovered shared-card DIV composition, and missing Train mode fields derive fixed mode from populated fixed-carriage arrays exactly like recovered `Ae`.

## Commands

From repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/automation-corrections.mjs --verify
node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-a/shell-home-corrections.mjs --verify
npm.cmd run check --prefix src/LWBridge.UI-0.3.17
git diff --check
```

The Automation producer additionally executes the actual recovered Automation renderer and shared card with independent original/current CSS and Chrome geometry. The shell/Home producer AST-executes the actual current callbacks/effects and exact recovered callback functions using inert deferred responses. Neither proof invokes native/gameplay actions.

## Limits

This milestone closes the demonstrated local defects and their inverse cases. It does not by itself close whole-AFK composition, the finite conditional Automation matrix, complete-App reconciliation, native provider-positive behavior, loaded native assets, or protected original-runtime pixels; those are handled by later milestones or remain explicit external limits.
