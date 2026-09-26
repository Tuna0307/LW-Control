# R8-020 — restore Settings visual-metrics persistence

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the original `visual_metrics_config_get/save` object, defaults, validation errors and per-profile runtime persistence. No unrelated Settings/account behavior is added.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `SettingsPanel-CjjIQp-r.js`;
- immutable API wrappers in `api-ClPPi2JT.js`;
- native 0.3.1 visual-metrics serializer/default-construction and validation code;
- original installed per-profile `runtime/config.json`, reading only the `visualMetrics` subsection.

The current Settings panel remains byte-identical to immutable 0.3.1. SHA-256:

`669c614914cd7b5a00c5d71d1cd9996cdb300b6a2609768fb931f554929bd69e`

No Settings frontend transform is added.

## Exact command/UI contract

The original API exposes:

- `visual_metrics_config_get()`;
- `visual_metrics_config_save(config)`.

The Settings panel loads the object once on mount. A toggle creates a complete copy of the current object with one changed field and saves that whole object.

The recovered object has exactly two booleans:

```json
{
  "showFps": false,
  "showPing": false
}
```

The native default-construction block builds both fields from the same zero/false value object before inserting `visualMetrics`. The same values were independently observed in an original profile's native `runtime/config.json`.

## Exact validation/error vocabulary

The native visual-metrics validator recovers:

- code: `INVALID_REQUEST`;
- message: `invalid visual metrics config`.

The native shared runtime-config unavailable pair is:

- code: `STATE_UNAVAILABLE`;
- message: `config state is unavailable`.

R8-020 uses those exact pairs.

## Shared runtime-config persistence

Visual metrics use the same recovered per-profile runtime-config family as Hotkeys.

Original concept:

`%APPDATA%\lwbridge\profiles\<profileId>\runtime\config.json`

Rebuild mapping:

`%LOCALAPPDATA%\LWBridgeRebuild\profiles\<profileId>\runtime\config.json`

The rebuild path remains an **EQUIVALENT_REIMPLEMENTATION** rather than a claim about the original absolute path.

R8-020 extends the R8-019 runtime-config store so only the `visualMetrics` member is replaced. Existing `hotkeys` and unknown sibling JSON remain unchanged. Conversely, later Hotkey saves preserve `visualMetrics`.

A missing rebuild runtime config returns the native `false/false` defaults. Malformed or unreadable runtime config fails closed with `STATE_UNAVAILABLE`.

## Production routing

Normal production now handles:

- `visual_metrics_config_get`;
- `visual_metrics_config_save`.

The existing backend command-scope layer continues to enforce the selected profile before the service runs. Isolated capture/probe modes do not create or mutate runtime config.

## Regression coverage

`VisualMetricsConfigChecks` proves:

- native defaults `showFps=false`, `showPing=false`;
- whole-object save and returned values;
- persistence after reopening;
- a visual-metrics save preserves the Hotkey object;
- a Hotkey save preserves visual metrics;
- incomplete/wrong-type saves return `INVALID_REQUEST`;
- malformed runtime config returns `STATE_UNAVAILABLE`.

The complete deterministic suite remains green.

## Still outside this checkpoint

R8-020 does not claim parity for:

- Feedback export internals;
- update-check/download internals;
- account-interaction settings;
- any account/login/authentication setting;
- native overlay/FPS/ping rendering implementation beyond the recovered persisted configuration.

## Validation

Completed before packaging:

- Settings panel hash matches immutable 0.3.1;
- Release build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0.

Final generator, JSON, diff and staged-diff checks are recorded in the checkpoint evidence.
