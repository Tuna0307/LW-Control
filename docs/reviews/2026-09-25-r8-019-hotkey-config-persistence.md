# R8-019 — restore Hotkey configuration persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the original Hotkey configuration object, defaults, validation errors, and production `hotkey_config_get/save` persistence. Native keyboard interception and game-action execution remain outside this checkpoint.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `HotkeyPanel-Do0ai_ZB.js`;
- immutable Hotkey API wrappers in `api-ClPPi2JT.js`;
- original profile runtime config observed at the native per-profile `runtime/config.json` location, reading only the `hotkeys` subsection.

The current Hotkey panel remains byte-identical to the recovered original. SHA-256:

`cbcb1293fda94a82ccd7b958d2f1ed0a751cc251e4c62d4a5abd28220b37ee31`

No Hotkey frontend transform is added.

## Exact frontend commands

The original frontend exposes:

- `hotkey_config_get()`;
- `hotkey_config_save(config)`.

The desktop bridge injects the selected profile identity into the command payload. The panel loads once on mount and, on every switch change, sends the whole current Hotkey object rather than a one-field patch.

## Exact Hotkey object

The native default-construction block creates the following ten booleans, in this serialized field set:

```json
{
  "attack": true,
  "attackMarchSpeedupItem": false,
  "attackMarchSpeedupDiamond": false,
  "recall": true,
  "shieldOverlay": true,
  "shieldUse": true,
  "equipment": true,
  "randomRelocate": false,
  "allianceRelocate": false,
  "frontlineReinforce": true
}
```

The same ten-field object and values were independently observed in an existing original LWBridge profile's native `runtime/config.json`.

The immutable panel maps these fields to the visible controls:

- attack — Q / W / E / R;
- recall — A / S / D / F;
- shield overlay — Space;
- shield use — F6 / F7 / F8;
- equipment — Alt + 1…4;
- random relocation — F9;
- alliance relocation — F10;
- frontline reinforce — G;
- attack march speed-up item — attack sub-option;
- attack march speed-up diamond — attack sub-option.

## Exact validation/error vocabulary

The original binary recovers the Hotkey validation pair:

- code: `INVALID_REQUEST`;
- message: `invalid hotkey config`.

The generic native profile-config unavailable pair is:

- code: `STATE_UNAVAILABLE`;
- message: `config state is unavailable`.

R8-019 uses those exact code/message pairs for the recovered boundary.

## Persistence mapping

Original LWBridge profile data uses a per-profile runtime `config.json`. An installed original profile on this machine confirms the native concept/path:

`%APPDATA%\lwbridge\profiles\<profileId>\runtime\config.json`

The rebuild maps that concept to its existing rebuild-owned profile root:

`%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\runtime\config.json`

The rebuild path is **EQUIVALENT_REIMPLEMENTATION**, not claimed as the original absolute path.

The store updates only the top-level `hotkeys` member and preserves unknown sibling JSON. This is deliberate so later recovery of `visualMetrics`, equipment, and other retained profile config does not lose data.

A missing rebuild runtime config returns the recovered Hotkey defaults. Malformed/unreadable runtime config fails closed with `STATE_UNAVAILABLE`.

## Production routing

Normal production now handles:

- `hotkey_config_get`;
- `hotkey_config_save`.

The existing backend command-scope layer remains responsible for requiring the selected profile and rejecting cross-profile payloads before the Hotkey service runs.

Isolated capture/probe modes do not create or mutate Hotkey runtime config.

## Regression coverage

`HotkeyConfigChecks` proves:

- all ten recovered default values;
- whole-object save and returned normalized object;
- persistence after reopening the same runtime config path;
- all ten booleans serialize under `hotkeys`;
- unrelated `visualMetrics` and unknown top-level config survive a Hotkey save;
- incomplete/wrong-type save payload returns `INVALID_REQUEST`;
- malformed runtime config returns `STATE_UNAVAILABLE`.

Release build and the complete deterministic suite pass with the service installed in normal production routing.

## Still intentionally missing

R8-019 does **not** implement or claim parity for:

- native global/local keyboard hook registration;
- key-down/up state machines;
- attack squad dispatch behavior;
- recall execution;
- shield overlay rendering/action behavior;
- shield-use item selection;
- equipment preset execution;
- random/alliance relocation game calls;
- frontline reinforcement execution;
- any game-bridge method used by those actions.

Those behaviors require separate native/runtime recovery. Restoring the persisted switches does not imply the associated game actions are implemented.

## Validation

Completed before packaging:

- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0.

Final generator, JSON, diff, and staged-diff checks are recorded in the checkpoint evidence.
