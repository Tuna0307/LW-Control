# R8-027 — restore generic Automation idle status

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the original generic `automation_status` normal missing-runtime-status projection and valid persisted-status passthrough. Live/configure Automation commands remain fenced.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `AutomationPanel-D06CxhPI.js`;
- immutable Automation API wrappers in `api-ClPPi2JT.js`;
- public `automation_status` handler at VA `0x14016b69a`;
- native automation-status loader/reconstructor at VA `0x1403b6b63`;
- public `automation_configure` handler at VA `0x140110c7d`;
- native task-config validator at VA `0x1403b9bce`;
- original installed per-profile `runtime/config.json`;
- original installed profile having no `runtime/automation-status.json`.

The Automation panel remains byte-identical to immutable 0.3.1. R8-027 changes no frontend asset.
## Exact native status task set

When `runtime/automation-status.json` is absent, native 0.3.1 reconstructs status for exactly these 18 tasks, in this order:

1. `construction`
2. `allianceTrainRide`
3. `officialPosition`
4. `stamina`
5. `staminaPotion`
6. `treatment`
7. `allianceDonate`
8. `allianceHelp`
9. `allianceGift`
10. `strongholdResource`
11. `allianceCenterResource`
12. `allianceGather`
13. `allianceGarrison`
14. `railway`
15. `dispatch`
16. `dispatchAssist`
17. `ghostRecon`
18. `monsterSweep`

This list comes directly from the static descriptor table assembled at approximately VA `0x1403b6dcc-0x1403b6f98`.
## Exact normal missing-status projection

For each of those tasks, the native fallback reads `tasks.<task>.enabled` from the shared runtime config.

Only an actual JSON boolean true produces `enabled=true`. Missing values, non-booleans, and false all project as false.

The synthesized task object is exactly:

```json
{
  "name": "<taskName>",
  "state": "idle",
  "step": "not_loaded",
  "running": false,
  "enabled": false
}
```

`enabled` is replaced by the persisted boolean value when it is true.

The top-level fallback is assembled in this field order:

```json
{
  "updatedAt": null,
  "lock": null,
  "tasks": {}
}
```

R8-027 restores that normal fallback without adding rebuild-only scheduler fields or derived waiting/disabled states.
## Existing runtime status file

The native loader first attempts to read:

`runtime/automation-status.json`

When the file exists and decodes successfully, the parsed status document is returned rather than replaced by the idle reconstruction.

R8-027 preserves that ownership boundary by passing a valid persisted JSON document through unchanged.

Malformed scheduler JSON is fenced with recovered code:

`INVALID_AUTOMATION_STATUS`

The exact Rust/serde human-readable parser detail is implementation-specific and is not claimed byte-for-byte by the rebuild. The error-code boundary is recovered; parser-message parity remains partial.

A missing status file is not an error. The installed original profile also has no `automation-status.json`, confirming that the reconstruction path is a normal reference condition.
## Configure/live boundary

R8-027 does **not** turn recovered validator knowledge into a synthetic live Configure implementation.

The original `automation_configure` handler:

- parses and validates task config locally;
- uses the native task-config validator around VA `0x1403b9bce`;
- then assembles protected method `configureAutomationTask`;
- uses exact timeout `0x1388 = 5000 ms`;
- awaits that provider/bridge operation.

Recovered validation already proves examples including:

- construction `maxBuilders`: integer 1..20;
- treatment `amountPerArmy`: integer 1..1,000,000;
- common interval tasks: integer 1..1440 minutes;
- task-specific validation for gather, garrison, train, stamina potion, dispatch assist, ghost recon, railway and dispatch.

Those facts are research evidence, not permission to skip the protected provider call.
Therefore R8-027 keeps these commands fenced until their complete provider/result contracts are recovered:

- `automation_configure`;
- `automation_inspect`;
- `automation_start`;
- `automation_stop`;
- other generic live Automation execution paths.

## Implementation

Production routing now installs `AutomationStatusCommandService` after the existing Resource Automation service.

The service handles only `automation_status`.

It reads the same profile-scoped runtime config and `automation-status.json` path as the recovered scheduler ownership model. The old backend fallback stub is bypassed in normal production composition.

No frontend asset, game file, proxy binary, authentication surface, or live game action is changed.

## Regression coverage

`AutomationStatusChecks` proves:

- exact 18-task set and order;
- exact fallback top-level field order;
- `updatedAt=null`;
- `lock=null`;
- exact per-task field order;
- `state="idle"`;
- `step="not_loaded"`;
- `running=false`;
- config-derived boolean `enabled`;
- missing/wrong-type `enabled` becomes false;
- valid persisted status passes through, including unknown future fields;
- malformed persisted JSON uses `INVALID_AUTOMATION_STATUS`;
- normal production backend routes `automation_status`;
- `automation_configure` remains `COMMAND_NOT_IMPLEMENTED` in this checkpoint.
## Validation

Completed before packaging:

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final frontend-generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.

## Still intentionally missing

R8-027 does not claim parity for:

- generic Automation configure provider/result semantics;
- Automation inspect/start/stop;
- live task execution;
- runtime scheduler writer/lock transitions;
- exact malformed JSON parser wording;
- non-file I/O error mapping for `automation-status.json`;
- any Account/Login/Authentication behavior.

Those remain separate retained/provider recovery work or explicit excluded scope.
