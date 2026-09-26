# R8-033 — restore red-packet and treasure claim-delay configuration

**Date:** 2026-09-25
**Reference:** verified LWBridge 0.3.1
**Scope:** restore the host-local `red_packet_delay_configure` and `treasure_delay_configure` commands, including exact range validation, mirrored runtime-config persistence, native validation details, and success result shape.

## Authority

Primary authority:

- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `treasure_delay_configure` public handler VA `0x1401651C5`;
- `red_packet_delay_configure` public handler VA `0x1401821C6`;
- shared delay validation/config builder VA `0x1403B8109`;
- shared mirrored config write helper VA `0x1403CB2E0`;
- numeric payload parser VA `0x1401D2E32`;
- original installed per-profile `runtime/config.json`.

No retained frontend asset references these command names, so this is backend parity rather than visible UI recovery.
## Public payload and validation

Neither command takes a public `profileId`; the native command state resolves the current profile's runtime config.

Both expect numeric:

- `minSeconds`
- `maxSeconds`

The native numeric field parser yields NaN for missing or wrong-type values. The shared validator therefore returns `INVALID_REQUEST` whenever either value is missing, wrong type, non-finite, below zero, inverted, or above the command's maximum.

Exact limits:

- red packet: `0 <= minSeconds <= maxSeconds <= 60`
- treasure: `0 <= minSeconds <= maxSeconds <= 600`

Exact recovered validation details:

- `red packet delay must be a valid range from 0 to 60 seconds`
- `treasure delay must be a valid range from 0 to 600 seconds`
## Mirrored persistence

The original runtime config keeps each delay range in two locations.

Red packet:

```text
scheduler.redPacketClaimDelaySeconds
chat_automation.red_packet.claimDelaySeconds
```

Treasure:

```text
scheduler.treasureClaimDelaySeconds
chat_automation.treasure.claimDelaySeconds
```

The shared native writer updates both paths before saving `config.json`.

The installed reference profile confirms these keys exist in both locations and currently hold `[0, 0]`.

R8-033 uses the existing `ProfileRuntimeConfigStore`, preserving unrelated root fields, scheduler siblings, chat-automation siblings, task configs, and unknown future fields.
## Success result

The native command returns:

```json
{
  "ok": true,
  "range": [0, 0]
}
```

with the requested values in `range`.

R8-033 restores that result shape exactly for the normal success path.

## Implementation

R8-033 adds:

- `ClaimDelayConfigCommandService`;
- `ProfileRuntimeConfigStore.SaveClaimDelayRange`;
- production routing for both delay commands;
- global-command admission because the native public commands do not take `profileId`;
- deterministic persistence and validation coverage.

No provider/game action is invoked by these commands.
## Regression coverage

`ClaimDelayConfigChecks` proves:

- red-packet 1.5..60 succeeds;
- treasure 0..600 succeeds;
- exact `{ok:true, range:[min,max]}` result;
- scheduler and chat-automation mirrors are both updated;
- unrelated scheduler/chat/root/task fields are preserved;
- negative, inverted, over-limit, missing, and wrong-type values fail with `INVALID_REQUEST`;
- exact native human-readable validation details are preserved;
- backend routes both commands without requiring `profileId`.

## Validation

- Release desktop build: 0 warnings / 0 errors;
- Release checks build: 0 warnings / 0 errors;
- full deterministic suite: `ok=true`, `failures=[]`, exit code 0;
- no frontend file changed.

Final generator, JSON, diff and staged-diff checks are recorded in checkpoint evidence.
## Related commands intentionally fenced

During R8-033 target selection:

- `profile_enable_set` was found to depend on `license_capacity`, `INVALID_PROFILE_CAPACITY`, primary/selection repair and state availability, so it remains excluded/fenced with entitlement behavior;
- `profile_delete` was found to enforce runtime-running, primary, bound-profile, selected-profile-repair and profile-data cleanup semantics, so the destructive public command remains fenced until the cleanup flag/path behavior is completely closed;
- `trade_station_configure` remains provider-backed through `configureTradeStation` with a 5,000 ms timeout.

No substitute behavior was added for those commands.
