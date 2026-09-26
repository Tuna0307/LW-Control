# R8-068 — fence generic Automation Configure/Start/Stop at authorization-derived request boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the recoverable native/public contracts of `automation_configure`, `automation_start`, and `automation_stop` without synthesizing owner-excluded authorization-derived request material.

## Native authority

Primary evidence:

- verified `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `api-ClPPi2JT.js` wrappers;
- immutable `AutomationPanel-D06CxhPI.js`;
- `automation_configure` handler `0x140110C7D-0x1401122A0`;
- `automation_stop` handler `0x140121C80-0x140122DA2`;
- `automation_start` handler `0x14014711B-0x14014809A`;
- native task-config validator `0x1403B9BCE-0x1403BB30B`;
- configure normalization helper `0x1403BB30B-0x1403BBE95`;
- shared authorization-state future `0x1400DC79E`;
- selected-profile runtime resolver `0x1402AE43C`;
- game-route helper `0x1403AD367`;
- shared game-call future `0x1400E4FAD`;
- generic JSON provider-result converter `0x1402BB816`.

## Exact frontend payloads

The immutable API wrapper sends:

- `automation_configure({ task, config, profileId? })`;
- `automation_start({ task, config, profileId? })`, with frontend default `config={}`;
- `automation_stop({ task, options, profileId? })`, with frontend default `options={}`.

The profile ID is injected by the common bridge wrapper when an active profile exists and the payload does not already contain one.

## Shared native admission chain

All three handlers await the same authorization-state future before constructing their provider request. They then resolve the selected profile runtime and verify a usable game route before sending the protected game call.

This is the same admission machinery recovered for `automation_inspect` in R8-051. Therefore the already recovered public failures apply at the same boundaries:

- authorization state unavailable: `STATE_UNAVAILABLE` / `authorization state is unavailable`;
- no usable game route: `GAME_DISCONNECTED` / `game disconnected`.

The request-building code contains the exact request-key cluster `task`, `options`, `premium`, `admin`; Configure and Start use `config` in the corresponding task request construction. The `premium` / `admin` values are derived from the shared authorization state. Their exact projection is owner-excluded and is not reconstructed.
## Provider calls and deadlines

The exact protected methods are:

- Configure → `configureAutomationTask`;
- Start → `startAutomationTask`;
- Stop → `stopAutomationTask`.

Each uses an exact `0x1388 = 5,000 ms` result deadline and the shared game-call future. No handler-level retry loop is present.

The shared timeout boundary therefore maps to the existing recovered `LUA_CALL_TIMEOUT` behavior with the method name in the message, for example `lua call result unknown after timeout: startAutomationTask`.

All three normal provider results pass through the shared generic JSON converter `0x1402BB816`. Unlike `automation_inspect("allianceGarrison")`, these handlers do not show a recovered task-specific host result rewrite after provider completion.

## Configure local validation

Configure performs substantial local work before the provider call.

It requires a task config object and uses the native validator at `0x1403B9BCE`, with task-specific normalization around `0x1403BB30B`. Recovered exact validation vocabulary includes, among other cases:

- construction builder limit integer 1..20;
- treatment amount-per-army integer 1..1,000,000;
- common interval integer 1..1440 minutes;
- stamina-potion threshold integer 0..9999 plus boolean preference/enabled checks;
- dispatch-assist delay 0..86400 seconds and interval 5..300 seconds;
- ghost alliance filter restricted to `sr`, `ur`, or `special`;
- alliance-gather squad-array/integer/duplicate requirements;
- alliance-garrison target, squad-priority, building-ID, duplicate-target, recall-on-disable and enabled validation;
- alliance-train carriage/selection-mode/auto-accept validation.

Malformed local task configuration uses native `INVALID_REQUEST` or `INVALID_CONFIG` boundaries depending on the parse/normalization stage.

This recovered local validator evidence is not sufficient to implement Configure exactly because the successful request still appends authorization-derived request material before `configureAutomationTask`.
## Why no production route is added

A route that sends only `task` + `config` / `options` would omit native authorization-derived `premium` / `admin` request fields. Hard-coding those fields would recreate owner-excluded account/authorization semantics and could change provider behavior.

R8-068 therefore makes no runtime implementation change. The three commands remain fail-closed through the existing unimplemented-command boundary until the retained product can reproduce their request without crossing the excluded authorization boundary.

This is a deliberate fence, not an unknown command surface.

## Classification

- public handler addresses: `EXACT_NATIVE`;
- frontend payload shapes/defaults: `EXACT_BYTES`;
- shared authorization-state dependency: `EXACT_NATIVE`;
- selected-runtime/game-route sequence: `EXACT_NATIVE`;
- authorization-derived `premium/admin` request values: `OWNER_EXCLUDED_UNKNOWN`;
- Configure validator function and recovered error vocabulary: `EXACT_NATIVE`;
- provider method names: `EXACT_NATIVE`;
- 5,000 ms deadlines: `EXACT_NATIVE`;
- provider-result conversion: `EXACT_NATIVE`;
- runtime implementation: `FENCED`.

R8-068 moves `automation_configure`, `automation_start`, and `automation_stop` from the R8-067 genuinely-unclosed routing queue into the audited/fenced set. The remaining genuinely unclosed retained frontend routing queue is 15 commands.

Machine-readable evidence: `evidence/lwbridge-implementation/2026-09-26-r8-068-generic-automation-live-fence.json`.
