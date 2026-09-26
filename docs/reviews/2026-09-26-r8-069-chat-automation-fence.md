# R8-069 — fence Chat Automation Configure/Run Pending at authorization-state admission

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained native/public boundary of `chat_automation_configure` and `chat_automation_run_pending` without recreating owner-excluded authorization state.

## Native authority

- `chat_automation_run_pending` handler: `0x1401285CB-0x140129437`
- `chat_automation_configure` handler: `0x14012CAFF-0x14012E897`
- shared authorization-state future: `0x1400DC79E`
- selected-profile runtime resolver: `0x1402AE43C`
- game-route helper: `0x1403AD367`
- shared game-call future: `0x1400E4FAD`
- generic JSON result converter: `0x1402BB816`
- immutable API wrappers in `api-ClPPi2JT.js`

The frontend payloads are exactly `{kind,config,profileId?}` for Configure and `{kind,profileId?}` for Run Pending.

## Retained kinds and validation

Native handles the retained chat kinds `redPacket`, `fireworks`, and `treasure`. Unknown kinds use the recovered invalid-request boundary.

Configure requires/normalizes an object config. Recovered host-side vocabulary includes `claimDelaySeconds`, `replyPhrases`, `replyEnabled`, `enabled`, `dispatchRetrySeconds`, `dispatchEnabled`, and `dispatchSquadPriority`. Exact observed errors include `INVALID_CONFIG` / `object required`, `INVALID_REQUEST` / `at least one reply phrase is required`, and treasure dispatch retry constrained to 1..300 seconds.

## Provider boundary

Run Pending sends a provider request containing `kind` to `runChatAutomationPending`. Configure sends normalized `kind` + `config` to `configureChatAutomation`.

Both provider calls use an exact 5,000 ms deadline, no handler retry loop, and normal results pass through the shared generic JSON converter. Shared timeout behavior is `LUA_CALL_TIMEOUT` with the provider method name.

Both handlers first await the shared authorization-state future, then resolve the selected profile runtime and verify a game route. The already recovered shared failures therefore apply: `STATE_UNAVAILABLE` / `authorization state is unavailable` and `GAME_DISCONNECTED` / `game disconnected`.

## Fence

Unlike generic Automation R8-068, no `premium/admin` fields are observed in the chat provider request. The blocker is authorization-state **admission itself**: native command precedence requires that excluded state before the otherwise-retained game action.

Skipping that admission would produce behavior the original cannot produce when authorization state is unavailable. Recreating the state would cross the explicit owner exclusion. Therefore no runtime route is added.

R8-069 moves `chat_automation_configure` and `chat_automation_run_pending` from genuinely unclosed to audited/fenced. Remaining genuinely-unclosed retained frontend routing gaps: **13**.

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-069-chat-automation-fence.json`.
