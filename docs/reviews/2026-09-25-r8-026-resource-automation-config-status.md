# R8-026 — restore Resource Automation configure + idle status

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the two original Resource Automation task configs, native configure validation/persistence, the normal no-`automation-status.json` status projection, and the original status-change event. Live `resource_automation_run` execution remains fenced.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `AutomationPanel-D06CxhPI.js`;
- immutable Resource Automation API wrappers in `api-ClPPi2JT.js`;
- native two-entry task parser around VA `0x1403b9b64`;
- native configure validator around VA `0x1403b7aa8`;
- native resource-status builder around VA `0x1403b57f6`;
- native missing-runtime-status reconstruction around VA `0x1403b6d34`;
- native config/status projection around VA `0x1403c9e82`;
- original installed per-profile `runtime/config.json`;
- original installed profile having no `runtime/automation-status.json`, proving missing runtime status is a normal reference state.

The Automation panel remains byte-identical to immutable 0.3.1. SHA-256:

`6ceabc4c1e1e7fba512d1e6b6b9d0e1639aaf0adf5212ae27151b6bdb7d714ca`

R8-026 changes no frontend asset.

## Exact public task admission

The native task parser contains exactly two entries:

- `buildingResources` -> native action name `collectResources`;
- `armedTruckReward` -> native action name `collectArmedTruckIdleReward`.

Any other task uses:

- code: `INVALID_REQUEST`;
- message prefix: `unknown resource automation task: `.

R8-026 restores that exact admission boundary.

## Exact persisted config

Both original tasks persist under the shared profile runtime config:

`tasks.<taskName>`

with the object:

```json
{
  "enabled": false,
  "intervalMinutes": 60
}
```

The installed original profile contains those exact defaults for both tasks.

The immutable Automation panel edits the complete two-field object and locally accepts only integer intervals 1 through 1440 minutes.

### Native configure validation

`resource_automation_configure({task,config})` requires `config.intervalMinutes` to be an integer from 1 through 1440 inclusive.

Invalid/missing/noninteger/out-of-range values use:

- code: `INVALID_REQUEST`;
- message: `interval must be an integer from 1 to 1440 minutes`.

`enabled` is optional in native code:

- exact JSON `true` -> enabled;
- exact JSON `false` -> disabled;
- missing -> disabled;
- wrong type -> disabled rather than rejected.

R8-026 preserves this non-obvious wrong-type behavior.

Save replaces only `tasks.<resourceTask>` with the canonical native two-field object. Root siblings and unrelated tasks are preserved.

## Configure result and event

After persistence, native configure rebuilds Resource Automation status, emits:

`bridge://resource-automation-status`

and returns the updated task object extracted from `status.tasks.<task>`.

R8-026 restores the same production flow. The event uses the existing profile-scoped WebView event channel, so the selected profile identity is wrapped by the desktop event host exactly like other profile-scoped bridge events.

## Runtime status ownership

Native Resource Automation does **not** store live scheduler state in `config.json`.

It separately uses:

`runtime/automation-status.json`

and has native vocabulary including:

- `STATE_UNAVAILABLE / resource state is unavailable`;
- code `INVALID_AUTOMATION_STATUS` for invalid scheduler-state data;
- states `disabled`, `waiting`, `running`;
- default internal state `idle`;
- default internal step `not_loaded`;
- runtime `lastRunAt`;
- projected `nextRunAt`.

The installed original profile has **no** `automation-status.json`. Native code reconstructs a default scheduler document when that file is missing; therefore this is a first-class normal state, not an error.

## Exact normal idle projection

For a missing runtime status file, native reconstruction supplies task scheduler metadata equivalent to:

- `name = <taskName>`;
- `step = "not_loaded"`;
- `running = false`;
- config-derived `enabled`;
- public state:
  - `disabled` when config is disabled;
  - `waiting` when config is enabled but not running;
- config-derived `intervalMinutes`;
- missing `lastRunAt`;
- projected `nextRunAt`:
  - null when disabled;
  - current time when enabled and there has never been a run.

Top-level Resource Automation status exposes:

- `updatedAt` using the native wall-clock timestamp;
- `runningTask` (null in the no-status-file case);
- `tasks`.

R8-026 restores this exact normal idle projection.

The native scheduling projection also computes, when a last-run time exists:

`nextRunAt = max(lastRunAt + intervalMinutes*60000, now)`.

R8-026 preserves that arithmetic for a parsable future runtime-status task.

## Conservative existing-status parsing

The native binary proves the scheduler file and much of its state vocabulary, but R8-026 does not claim a complete live scheduler/executor reconstruction.

If `automation-status.json` exists, the rebuild reads only the recovered generic task metadata needed for Resource Automation projection. A malformed/non-object document fails closed with recovered code:

`INVALID_AUTOMATION_STATUS`

The exact native human-readable message attached to every malformed-state subcase has not been isolated; the rebuild therefore uses the code itself as the fallback message and does **not** claim message parity for that branch.

This branch is **PARTIAL/EQUIVALENT**. The no-file fallback used by the original installed profile is the exact parity claim in this checkpoint.

## Regression coverage

`ResourceAutomationConfigChecks` proves:

- exact two-task admission/order;
- original persisted defaults;
- native idle top-level status fields;
- native disabled/waiting states;
- `step="not_loaded"`;
- `running=false`;
- missing `lastRunAt` stays absent;
- disabled `nextRunAt=null`;
- enabled/no-history `nextRunAt=now`;
- configure result is the updated task-status object;
- configure emits a full Resource Automation status event;
- root siblings and unrelated tasks survive;
- only canonical `enabled/intervalMinutes` fields are persisted for a resource task;
- wrong-type/missing `enabled` becomes false;
- interval bounds/type validation uses exact native error;
- unknown/missing task uses exact native dynamic error;
- malformed scheduler JSON uses recovered `INVALID_AUTOMATION_STATUS` code;
- normal production backend routes `resource_automation_status`;
- `resource_automation_run` remains `COMMAND_NOT_IMPLEMENTED`.

Release build and the complete deterministic suite pass with the service installed in normal production routing.

## Still intentionally missing

R8-026 does **not** implement or claim parity for:

- `resource_automation_run`;
- `collectResources`;
- `collectArmedTruckIdleReward`;
- live game/provider calls;
- complete scheduler-lock semantics;
- live scheduler write/update ownership;
- complete `automation-status.json` validation/migration semantics;
- runtime `lastRunAt` production;
- task execution success/failure details.

Those remain live/provider/protocol dependent.

## Validation

Completed before packaging:

- Automation panel hash matches immutable 0.3.1;
- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
